using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>Bir gunluk gorevin tanimi.</summary>
    public struct QuestDef
    {
        /// <summary>Ceviri anahtari (metin LocalizationManager'da).</summary>
        public string key;

        public StatType stat;

        /// <summary>Gun basindaki degere EK olarak gereken miktar.</summary>
        public long amount;

        /// <summary>Odul: bosta gelirin kac saniyelik karsiligi.</summary>
        public double rewardSeconds;

        public QuestDef(string key, StatType stat, long amount, double rewardSeconds)
        {
            this.key = key;
            this.stat = stat;
            this.amount = amount;
            this.rewardSeconds = rewardSeconds;
        }
    }

    /// <summary>
    /// Gunluk 3 mini gorev.
    ///
    /// TASARIM KARARLARI
    /// -----------------
    /// 1. GOREVLER KAYDEDILMEZ, GUNDEN TURETILIR. Kayda yalnizca "hangi
    ///    gundeyiz" yaziliyor; hangi uc gorevin secildigi gun numarasindan
    ///    beslenen sabit bir karistiriciyla yeniden uretiliyor. Boylece kayit
    ///    kucuk kaliyor ve gorev listesi degistiginde eski kayitlar bozulmuyor.
    ///
    /// 2. OLCUM GUN BASINDAKI DEGERE GORE. "5 dosya indir" gorevi TOPLAM
    ///    sayaca bakarsa, 900 dosya indirmis bir oyuncuda gorev daha acilir
    ///    acilmaz tamamlanmis gorunurdu. Bu yuzden gun basinda sayaclarin
    ///    anlik goruntusu aliniyor ve ilerleme fark uzerinden olculuyor.
    ///
    /// 3. ODUL BOSTA GELIRE ORANLI, sabit para degil. Sabit bir odul kademe
    ///    2'de comert, kademe 8'de gorunmez olurdu.
    ///
    /// 4. ODUL ELLE ALINIYOR (Claim). Kendiliginden verilen odul, oyuncunun
    ///    gorevi tamamladigini fark etmemesi demek; panelde "AL" dugmesine
    ///    basmak kazanci gorunur kiliyor.
    ///
    /// 5. YEREL GUN kullaniliyor (UTC degil): oyuncunun "bugun"u kendi
    ///    takvimindeki gundur. Saat degistirip gun atlatma somurusune karsi
    ///    koruma <see cref="OnDayChanged"/> icinde.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class DailyQuestManager : MonoBehaviour
    {
        /// <summary>Her gun kac gorev verilir.</summary>
        public const int QuestsPerDay = 3;

        /// <summary>
        /// Gorev havuzu. Gunluk uclu bunlardan seciliyor.
        ///
        /// Hepsi NORMAL OYNAYISLA tamamlanabilir olmali: oyuncuyu alisilmadik
        /// bir sey yapmaya zorlayan gunluk gorev, oyunu kendi ritminden
        /// koparir. "Reklam izle" bilincli olarak havuzda YOK — gunluk gorevi
        /// reklam izlemeye baglamak, istege bagli olmasi gereken bir seyi
        /// zorunlulastirirdi (GDD Bolum 11).
        /// </summary>
        public static readonly QuestDef[] Pool =
        {
            new QuestDef("quest_files_15",    StatType.FilesDownloaded,  15,  90.0),
            new QuestDef("quest_files_40",    StatType.FilesDownloaded,  40,  180.0),
            new QuestDef("quest_clicks_150",  StatType.Clicks,           150, 120.0),
            new QuestDef("quest_clicks_400",  StatType.Clicks,           400, 200.0),
            new QuestDef("quest_upgrades_3",  StatType.UpgradesBought,   3,   120.0),
            new QuestDef("quest_upgrades_8",  StatType.UpgradesBought,   8,   220.0),
            new QuestDef("quest_overheat_2",  StatType.Overheats,        2,   150.0),
            new QuestDef("quest_vent_3",      StatType.Vents,            3,   150.0),
            new QuestDef("quest_signal_1",    StatType.SignalsCaught,    1,   140.0),
            new QuestDef("quest_event_2",     StatType.EventsResolved,   2,   160.0)
        };

        /// <summary>Gun numarasinin sayildigi baslangic (sabit bir capa).</summary>
        static readonly DateTime Epoch = new DateTime(2020, 1, 1);

        int _dayNumber;
        readonly QuestDef[] _today = new QuestDef[QuestsPerDay];
        readonly long[] _baseline = new long[QuestsPerDay];
        readonly bool[] _claimed = new bool[QuestsPerDay];

        /// <summary>Gorevler yenilendi veya ilerleme degisti — panel tazelensin.</summary>
        public event Action Changed;

        /// <summary>Bir gorev odulu alindi (gorev sirasi, verilen para).</summary>
        public event Action<int, double> Claimed;

        GameManager _gm;
        PlayerStats _stats;
        bool _ready;

        public int DayNumber { get { return _dayNumber; } }

        public QuestDef GetQuest(int index)
        {
            return index >= 0 && index < QuestsPerDay ? _today[index] : default(QuestDef);
        }

        public bool IsClaimed(int index)
        {
            return index >= 0 && index < QuestsPerDay && _claimed[index];
        }

        /// <summary>Bu gorevde bugun kaydedilen ilerleme (hedefle kirpilmis).</summary>
        public long ProgressOf(int index)
        {
            if (index < 0 || index >= QuestsPerDay || _stats == null) return 0L;

            long done = _stats.Get(_today[index].stat) - _baseline[index];
            if (done < 0L) done = 0L;
            return Math.Min(done, _today[index].amount);
        }

        public bool IsComplete(int index)
        {
            return index >= 0 && index < QuestsPerDay && ProgressOf(index) >= _today[index].amount;
        }

        public bool CanClaim(int index)
        {
            return IsComplete(index) && !IsClaimed(index);
        }

        /// <summary>Bugun alinmayi bekleyen odul var mi? (HUD rozetı icin)</summary>
        public bool HasClaimable
        {
            get
            {
                for (int i = 0; i < QuestsPerDay; i++) if (CanClaim(i)) return true;
                return false;
            }
        }

        void Start()
        {
            Initialize();
        }

        bool _initialized;

        /// <summary>
        /// Bagimliliklari cozer. Start'tan ayri: edit modunda Start hic
        /// calismadigi icin testler bileseni elle ayaga kaldirmak zorunda
        /// (ayni desen EconomyManager.Initialize'da da var). Idempotent.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            _gm = GameManager.Instance;
            if (_gm == null) { enabled = false; return; }

            _initialized = true;

            _stats = _gm.Stats;
            if (_stats != null) _stats.Changed += OnStatChanged;
        }

        void OnDestroy()
        {
            if (_stats != null) _stats.Changed -= OnStatChanged;
        }

        void OnStatChanged(StatType type, long value)
        {
            if (!_ready) return;
            if (Changed != null) Changed();
        }

        void Update()
        {
            if (!_ready) return;

            // Uygulama gece yarisini gecerek acik kalabilir; gun degisimini
            // yalnizca acilista kontrol etmek, oyunu kapatmayan bir oyuncuya
            // gorevleri hic yenilemezdi.
            int today = TodayNumber();
            if (today != _dayNumber) OnDayChanged(today);
        }

        static int TodayNumber()
        {
            return (int)(DateTime.Now.Date - Epoch).TotalDays;
        }

        void OnDayChanged(int newDay)
        {
            // Saat GERI alindiysa gun numarasi kucuulur. Gorevleri yenilemek,
            // saati ileri-geri oynatarak sinirsiz odul almak demek olurdu —
            // bu yuzden yalnizca ILERI giden gun degisimi yenileme sayiliyor.
            if (newDay < _dayNumber)
            {
                _dayNumber = newDay;   // takip et ama gorevleri yenileme
                return;
            }

            _dayNumber = newDay;
            RollToday();

            if (Changed != null) Changed();
        }

        /// <summary>
        /// Gunun uc gorevini secer ve gun basi sayaclarini kaydeder.
        /// </summary>
        void RollToday()
        {
            PickForDay(_dayNumber, _today);

            for (int i = 0; i < QuestsPerDay; i++)
            {
                _baseline[i] = _stats != null ? _stats.Get(_today[i].stat) : 0L;
                _claimed[i] = false;
            }
        }

        /// <summary>
        /// Gun numarasindan uc FARKLI gorev secer.
        ///
        /// <see cref="UnityEngine.Random"/> KULLANILMIYOR: oyunun geri kalani
        /// da ondan cekiyor ve global durumu tohumlamak (InitState) baska her
        /// yerdeki rastgeleligi sessizce belirlenimli hale getirirdi. Buradaki
        /// karistirici kendi icinde kapali.
        /// </summary>
        public static void PickForDay(int day, QuestDef[] into)
        {
            int count = Mathf.Min(QuestsPerDay, Pool.Length);

            // Havuzun indekslerini gune bagli olarak karistir (Fisher-Yates).
            var order = new int[Pool.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;

            uint state = Hash((uint)day);
            for (int i = order.Length - 1; i > 0; i--)
            {
                state = Hash(state);
                int j = (int)(state % (uint)(i + 1));

                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            for (int i = 0; i < count; i++) into[i] = Pool[order[i]];
        }

        /// <summary>Basit bir tamsayi karistirici (xorshift benzeri).</summary>
        static uint Hash(uint x)
        {
            x += 0x9E3779B9u;
            x ^= x >> 16;
            x *= 0x85EBCA6Bu;
            x ^= x >> 13;
            x *= 0xC2B2AE35u;
            x ^= x >> 16;
            return x;
        }

        // ------------------------------------------------------------------
        // Odul
        // ------------------------------------------------------------------

        /// <summary>Gorev odulunu verir. Alinamiyorsa false doner.</summary>
        public bool TryClaim(int index)
        {
            if (!CanClaim(index)) return false;

            _claimed[index] = true;

            double reward = RewardOf(index);
            if (reward > 0.0 && _gm != null && _gm.Money != null) _gm.Money.Add(reward);

            if (_gm != null && _gm.Stats != null) _gm.Stats.Add(StatType.QuestsCompleted);
            if (_gm != null && _gm.Audio != null) _gm.Audio.PlayReward();
            HapticManager.MediumImpact();

            if (Claimed != null) Claimed(index, reward);
            if (Changed != null) Changed();
            return true;
        }

        /// <summary>Bu gorevin su anki para odulu (bosta gelire oranli).</summary>
        public double RewardOf(int index)
        {
            if (index < 0 || index >= QuestsPerDay) return 0.0;
            if (_gm == null || _gm.Download == null) return 0.0;

            double perSecond = _gm.Download.EstimateIdleIncomePerSecond();
            double reward = perSecond * _today[index].rewardSeconds;

            // Kademe 0'da bosta gelir neredeyse sifir; odul de sifir gorunurdu.
            // Kucuk bir taban, gorevin ilk gunden anlamli olmasini sagliyor.
            return Math.Max(10.0, reward);
        }

        // ------------------------------------------------------------------
        // Kayit
        // ------------------------------------------------------------------

        public void Capture(SaveData data)
        {
            if (data == null) return;

            data.questDayNumber = _dayNumber;

            data.questBaseline.Clear();
            for (int i = 0; i < QuestsPerDay; i++)
            {
                data.questBaseline.Add(new SaveData.StatEntry
                {
                    name = _today[i].stat.ToString(),
                    value = _baseline[i]
                });
            }

            data.questsClaimed.Clear();
            for (int i = 0; i < QuestsPerDay; i++)
                if (_claimed[i]) data.questsClaimed.Add(i);
        }

        /// <summary>
        /// Kayittan yukleme. Sayaclar bu cagriya kadar geri konmus olmali.
        /// </summary>
        public void Restore(int savedDay, List<SaveData.StatEntry> baseline, List<int> claimed)
        {
            Initialize();

            int today = TodayNumber();
            _dayNumber = today;

            // Kayittaki gun BUGUN degilse (ya da hic yoksa) taze gorevler.
            if (savedDay != today || baseline == null || baseline.Count < QuestsPerDay)
            {
                RollToday();
                _ready = true;
                if (Changed != null) Changed();
                return;
            }

            PickForDay(_dayNumber, _today);

            for (int i = 0; i < QuestsPerDay; i++)
            {
                // Baslangic degerini ADIYLA eslestir: gorev havuzu degisirse
                // sirali okuma yanlis sayaca baglanirdi.
                _baseline[i] = FindBaseline(baseline, _today[i].stat,
                                            _stats != null ? _stats.Get(_today[i].stat) : 0L);
                _claimed[i] = claimed != null && claimed.Contains(i);
            }

            _ready = true;
            if (Changed != null) Changed();
        }

        static long FindBaseline(List<SaveData.StatEntry> list, StatType stat, long fallback)
        {
            string name = stat.ToString();

            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i].name, name, StringComparison.Ordinal))
                    return list[i].value;

            // Eslesme yoksa "bugun sifirdan basla": mevcut degeri taban al.
            return fallback;
        }

        /// <summary>Kayit yoksa (ilk oyun) gorevleri yine de kur.</summary>
        public void StartFresh()
        {
            Initialize();

            _dayNumber = TodayNumber();
            RollToday();
            _ready = true;
            if (Changed != null) Changed();
        }
    }
}
