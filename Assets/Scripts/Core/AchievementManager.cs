using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Bir basarimin tanimi.
    ///
    /// Tanimlar ScriptableObject degil KOD icinde: basarim listesi denge
    /// ayari degil, oyunun sabit bir parcasi. Asset'e tasimak her yeni
    /// basarim icin bir dosya uretmeyi ve onu kaybetme riskini getirirdi;
    /// ustelik kimlikler kayda yazildigi icin asset adiyla kimlik arasinda
    /// ikinci bir baglanti daha olusurdu.
    /// </summary>
    public struct AchievementDef
    {
        /// <summary>Kayda yazilan kalici kimlik. ASLA DEGISTIRME.</summary>
        public string id;

        /// <summary>Hangi sayaca bakiyor.</summary>
        public StatType stat;

        /// <summary>Acilmasi icin gereken deger.</summary>
        public long target;

        /// <summary>Odul: kac Fiber Kredisi verilecek.</summary>
        public int creditReward;

        public AchievementDef(string id, StatType stat, long target, int creditReward)
        {
            this.id = id;
            this.stat = stat;
            this.target = target;
            this.creditReward = creditReward;
        }
    }

    /// <summary>
    /// Basarimlar (kupa paneli).
    ///
    /// Tasarim kurallari:
    ///   - Basarimlar PRESTIJDE SIFIRLANMAZ. "Bu hesabin tarihi" olduklari icin
    ///     dayandiklari sayaclar da (<see cref="PlayerStats"/>) sifirlanmiyor.
    ///   - Odul Fiber Kredisi: para vermek ust kademelerde anlamsiz kalirdi
    ///     (kademe 8'de bir basarimin verecegi para bir saniyelik gelirin
    ///     altinda olur). Kredi her zaman degerli.
    ///   - Ilerleme KAYDEDILMEZ, sayaclardan yeniden hesaplanir. Ikisini birden
    ///     yazmak zamanla ayrisabilen iki gercek yaratirdi.
    ///   - Acilma bir kez olur ve geri alinmaz.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class AchievementManager : MonoBehaviour
    {
        /// <summary>
        /// Basarim listesi.
        ///
        /// Hedefler oyunun kendi olceklerine gore secildi: ilk basamak birkac
        /// dakikada, son basamak birkac prestij turunda geliyor. Her olcude
        /// birden fazla basamak var ki ilerleme hissi surekli olsun.
        /// </summary>
        public static readonly AchievementDef[] Definitions =
        {
            // --- indirme ---
            new AchievementDef("files_10",      StatType.FilesDownloaded,  10,     1),
            new AchievementDef("files_250",     StatType.FilesDownloaded,  250,    2),
            new AchievementDef("files_5000",    StatType.FilesDownloaded,  5000,   5),
            new AchievementDef("files_100000",  StatType.FilesDownloaded,  100000, 12),

            // --- tiklama ve kombo ---
            new AchievementDef("clicks_100",    StatType.Clicks,           100,    1),
            new AchievementDef("clicks_2500",   StatType.Clicks,           2500,   3),
            new AchievementDef("clicks_50000",  StatType.Clicks,           50000,  8),
            new AchievementDef("combo_25",      StatType.MaxCombo,         25,     4),

            // --- risk / isi ---
            new AchievementDef("overheat_1",    StatType.Overheats,        1,      1),
            new AchievementDef("overheat_50",   StatType.Overheats,        50,     4),
            new AchievementDef("vent_10",       StatType.Vents,            10,     3),
            new AchievementDef("vent_25",       StatType.Vents,            25,     4),

            // --- ekonomi ---
            new AchievementDef("upgrades_25",   StatType.UpgradesBought,   25,     2),
            new AchievementDef("upgrades_400",  StatType.UpgradesBought,   400,    6),

            // --- prestij ---
            new AchievementDef("prestige_1",    StatType.Prestiges,        1,      2),
            new AchievementDef("prestige_10",   StatType.Prestiges,        10,     10),

            // --- kesif ---
            new AchievementDef("signal_5",      StatType.SignalsCaught,    5,      2),
            new AchievementDef("holo_1",        StatType.HoloDrops,        1,      2),
            new AchievementDef("holo_10",       StatType.HoloDrops,        10,     4),
            new AchievementDef("holo_25",       StatType.HoloDrops,        25,     6),
            new AchievementDef("events_20",     StatType.EventsResolved,   20,     3),

            // --- gunluk gorevler ---
            new AchievementDef("quests_10",     StatType.QuestsCompleted,  10,     3),
            new AchievementDef("quests_100",    StatType.QuestsCompleted,  100,    10)
        };

        readonly HashSet<string> _unlocked = new HashSet<string>();

        /// <summary>Yeni acilan basarim (kimlik, verilen kredi).</summary>
        public event Action<AchievementDef> Unlocked;

        /// <summary>Herhangi bir degisiklik — panel tazelensin.</summary>
        public event Action Changed;

        public int UnlockedCount { get { return _unlocked.Count; } }
        public int TotalCount { get { return Definitions.Length; } }

        public bool IsUnlocked(string id)
        {
            return !string.IsNullOrEmpty(id) && _unlocked.Contains(id);
        }

        GameManager _gm;
        PlayerStats _stats;

        /// <summary>
        /// Yukleme bitene kadar odul VERILMEZ.
        ///
        /// Kayit yuklenirken sayaclar tek seferde geri konuyor ve o anda daha
        /// once acilmis her basarim yeniden "acildi" sayilirdi: oyuncu her
        /// acilista ayni kutlamayi ve ayni krediyi tekrar alirdi. Yukleme
        /// bittikten sonra bir kez sessiz senkron yapiliyor.
        /// </summary>
        bool _ready;

        void Start()
        {
            Initialize();
        }

        bool _initialized;

        /// <summary>
        /// Bagimliliklari cozer ve sayac degisimlerini dinlemeye baslar.
        ///
        /// Start'tan AYRI bir metot olmasinin sebebi test edilebilirlik: Unity
        /// edit modunda Start hic calismiyor, dolayisiyla birim testleri
        /// bileseni elle ayaga kaldirmak zorunda. Ayni desen
        /// <see cref="EconomyManager.Initialize"/>'da da var.
        ///
        /// Idempotent: iki kez cagrilmasi zararsiz.
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
            Evaluate(type, true);
        }

        /// <summary>
        /// Bu olcuye bagli basarimlari kontrol eder.
        /// <paramref name="grantRewards"/> false ise yalnizca isaretler —
        /// kayittan yukleme yolunda kullanilir.
        /// </summary>
        void Evaluate(StatType type, bool grantRewards)
        {
            if (_stats == null) return;

            long value = _stats.Get(type);
            bool any = false;

            for (int i = 0; i < Definitions.Length; i++)
            {
                AchievementDef def = Definitions[i];
                if (def.stat != type || value < def.target) continue;
                if (!_unlocked.Add(def.id)) continue;

                any = true;
                if (grantRewards) Reward(def);
            }

            if (any && Changed != null) Changed();
        }

        void Reward(AchievementDef def)
        {
            if (def.creditReward > 0 && _gm != null && _gm.Prestige != null)
                _gm.Prestige.RefundCredits(def.creditReward);

            if (_gm != null && _gm.Audio != null) _gm.Audio.PlayReward();
            HapticManager.MediumImpact();

            if (Unlocked != null) Unlocked(def);
        }

        // ------------------------------------------------------------------
        // Kayit
        // ------------------------------------------------------------------

        public List<string> ToList()
        {
            return new List<string>(_unlocked);
        }

        /// <summary>
        /// Kayittan yukleme. Sayaclar bu cagriya kadar geri konmus olmali.
        ///
        /// Yukledikten sonra TUM olculer bir kez sessizce degerlendiriliyor:
        /// oyun guncellenip yeni bir basarim eklendiginde, sartini coktan
        /// saglayan oyuncunun onu almasi gerekiyor. Odul verilmiyor cunku
        /// gecmise donuk kredi dagitmak ekonomiyi bozardi; basarim yine de
        /// panelde acik gorunuyor.
        /// </summary>
        public void Restore(List<string> unlockedIds)
        {
            // Cagiran taraf Start'tan once gelebilir (SaveManager) veya Start
            // hic calismamis olabilir (edit modu testleri). Bagimliliklarin
            // cozulmus oldugunu VARSAYMAK, sayaclarin okunamamasi ve hicbir
            // basarimin acilmamasi demekti.
            Initialize();

            _unlocked.Clear();

            if (unlockedIds != null)
                for (int i = 0; i < unlockedIds.Count; i++)
                    if (!string.IsNullOrEmpty(unlockedIds[i])) _unlocked.Add(unlockedIds[i]);

            for (int i = 0; i < PlayerStats.Count; i++) Evaluate((StatType)i, false);

            _ready = true;
            if (Changed != null) Changed();
        }

        /// <summary>
        /// Kayit yoksa (ilk oyun) yine de dinlemeye baslamali.
        /// SaveManager kayit bulamadiginda bunu cagiriyor.
        /// </summary>
        public void MarkReady()
        {
            Initialize();
            _ready = true;
        }

        // ------------------------------------------------------------------
        // Panel icin
        // ------------------------------------------------------------------

        /// <summary>Bu basarimin 0-1 arasi ilerlemesi.</summary>
        public float Progress01(AchievementDef def)
        {
            if (def.target <= 0L) return 1f;
            if (_stats == null) return 0f;
            return Mathf.Clamp01((float)((double)_stats.Get(def.stat) / def.target));
        }

        /// <summary>Bu basarimin sayacinin su anki degeri (panelde "37 / 250").</summary>
        public long CurrentValue(AchievementDef def)
        {
            return _stats != null ? Math.Min(_stats.Get(def.stat), def.target) : 0L;
        }
    }
}
