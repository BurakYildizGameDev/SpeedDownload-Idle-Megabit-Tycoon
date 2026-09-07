using System;
using System.Collections.Generic;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Yukseltme seviyeleri ve bunlarin oyuna yansimasi (GDD Bolum 6).
    ///
    ///     Maliyet(n)   = baseCost * costGrowth^n
    ///     TiklamaGucu  = baseClickPower * (1 + 0.15*sv) * prestijCarpani
    ///     KritikSans   = baseCritChance + sv * effectPerLevel
    ///     FanToleransi = sv * effectPerLevel  (sn)
    ///     OdulCarpani  = (1 + sv * effectPerLevel) * prestijCarpani
    ///
    /// Baglanti yukseltmeleri (type == Connection) tek seferliktir ve satin
    /// alininca kademeyi yukseltir.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        /// <summary>
        /// Faz 7 prestij carpani. 1.0 = prestij yok.
        ///
        /// Varsayilan Awake'te degil BURADA veriliyor: Unity edit modunda Awake
        /// calismadigi icin testlerde carpan 0 kaliyor ve tum etkiler sifirlaniyordu.
        /// Alan baslaticisi her zaman calisir, dolayisiyla nesne dogdugu anda gecerli.
        /// </summary>
        public double PrestigeMultiplier { get; set; } = 1.0;

        /// <summary>Satin alinan yukseltme.</summary>
        public event Action<UpgradeSO> Purchased;

        /// <summary>Seviye veya kademe degisince — UI kartlarini tazelemek icin.</summary>
        public event Action StatsChanged;

        readonly Dictionary<UpgradeSO, int> _levels = new Dictionary<UpgradeSO, int>();

        GameManager _gm;
        GameConfigSO _config;
        GameDatabaseSO _database;
        Wallet _wallet;
        SpeedController _speed;
        DownloadController _download;

        void Awake()
        {
            PrestigeMultiplier = 1.0;
        }

        void Start()
        {
            Initialize();
        }

        bool _initialized;

        /// <summary>
        /// Bagimliliklari GameManager uzerinden cozer ve ilk istatistikleri uygular.
        ///
        /// Start'tan ayri bir metot olmasinin sebebi test edilebilirlik: Unity
        /// edit modunda Start hic calismadigi icin birim testleri bileseni elle
        /// ayaga kaldirmak zorunda. Toplu alim matematigi (geometrik seri ve onun
        /// logaritmik tersi) oyunun en riskli hesabi ve test edilemez olmasi
        /// kabul edilebilir degildi.
        ///
        /// Idempotent: iki kez cagrilmasi zararsiz.
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            _gm = GameManager.Instance;
            if (_gm == null)
            {
                Debug.LogError("[EconomyManager] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            _initialized = true;

            _config = _gm.Config;
            _database = _gm.Database;
            _wallet = _gm.Money;
            _speed = _gm.Speed;
            _download = _gm.Download;

            _gm.TierChanged += OnTierChanged;
            ApplyStats();
        }

        void OnDestroy()
        {
            if (_gm != null) _gm.TierChanged -= OnTierChanged;
        }

        void OnTierChanged(ConnectionTierSO tier)
        {
            // Kademe degisti: yeni yukseltmelerin kilidi acilmis olabilir.
            ApplyStats();
        }

        // ------------------------------------------------------------------
        // Sorgular
        // ------------------------------------------------------------------

        public int GetLevel(UpgradeSO upgrade)
        {
            if (upgrade == null) return 0;
            int level;
            return _levels.TryGetValue(upgrade, out level) ? level : 0;
        }

        public double GetCost(UpgradeSO upgrade)
        {
            return upgrade == null ? 0.0 : upgrade.CostAtLevel(GetLevel(upgrade));
        }

        public bool IsMaxed(UpgradeSO upgrade)
        {
            return upgrade != null && upgrade.IsMaxed(GetLevel(upgrade));
        }

        /// <summary>Kademe sarti saglandi mi?</summary>
        public bool IsUnlocked(UpgradeSO upgrade)
        {
            if (upgrade == null || _gm == null) return false;
            return _gm.CurrentTierIndex >= upgrade.requiredTierIndex;
        }

        /// <summary>
        /// Prestij sekmesindeki kartlar para ile degil Fiber Kredisi ile alinir.
        /// Ayri bir yonetici kurmak yerine ayni kart/seviye altyapisi kullaniliyor;
        /// yalnizca odeme yolu farkli.
        /// </summary>
        public static bool UsesFiberCredits(UpgradeSO upgrade)
        {
            return upgrade != null && upgrade.tab == UpgradeTab.Prestige;
        }

        public bool CanAfford(UpgradeSO upgrade)
        {
            if (upgrade == null) return false;

            if (UsesFiberCredits(upgrade))
            {
                PrestigeManager prestige = _gm != null ? _gm.Prestige : null;
                return prestige != null && prestige.FiberCredits >= (int)GetCost(upgrade);
            }

            return _wallet != null && _wallet.CanAfford(GetCost(upgrade));
        }

        public bool CanBuy(UpgradeSO upgrade)
        {
            return IsUnlocked(upgrade) && !IsMaxed(upgrade) && CanAfford(upgrade);
        }

        // ------------------------------------------------------------------
        // Satin alma
        // ------------------------------------------------------------------

        // ------------------------------------------------------------------
        // Toplu satin alma
        //
        // Maliyet geometrik bir dizi oldugu icin k adedin toplami kapali
        // formulle cikiyor; tek tek dongu kurmaya gerek yok:
        //
        //     toplam = baseCost * r^n * (r^k - 1) / (r - 1)      (r != 1)
        //     toplam = baseCost * k                              (r == 1)
        //
        // n = mevcut seviye, r = costGrowth, k = alinacak adet.
        // ------------------------------------------------------------------

        /// <summary>Mevcut seviyeden itibaren <paramref name="count"/> adedin toplam maliyeti.</summary>
        public double GetBulkCost(UpgradeSO upgrade, int count)
        {
            if (upgrade == null || count <= 0) return 0.0;

            int level = GetLevel(upgrade);
            double r = upgrade.costGrowth;
            double first = upgrade.CostAtLevel(level);

            if (System.Math.Abs(r - 1.0) < 0.000001) return first * count;

            return first * (System.Math.Pow(r, count) - 1.0) / (r - 1.0);
        }

        /// <summary>Tavan ve butceyi asmadan alinabilecek en fazla adet.</summary>
        public int GetMaxAffordable(UpgradeSO upgrade)
        {
            if (upgrade == null) return 0;
            if (!IsUnlocked(upgrade) || IsMaxed(upgrade)) return 0;

            int level = GetLevel(upgrade);
            int roomLeft = upgrade.maxLevel > 0 ? upgrade.maxLevel - level : int.MaxValue;
            if (roomLeft <= 0) return 0;

            // Prestij yetenekleri Fiber Kredisi ile aliniyor; butceyi cuzdandan
            // okumak hem yanlis adet gosterir hem de asagidaki toplu alim yolunu
            // yanlis para birimine sokar.
            //
            // Cuzdan kontrolu de bu yuzden basta degil burada: metodun ilk satiri
            // "_wallet == null ise 0 don" diyordu ve bu, cuzdanla hicbir ilgisi
            // olmayan Fiber Kredisi kartlarinda da MAKS adedini 0 gosteriyordu.
            double budget;

            if (UsesFiberCredits(upgrade))
            {
                budget = _gm != null && _gm.Prestige != null ? _gm.Prestige.FiberCredits : 0.0;
            }
            else
            {
                if (_wallet == null) return 0;
                budget = _wallet.Balance;
            }
            double r = upgrade.costGrowth;
            double first = upgrade.CostAtLevel(level);

            if (first <= 0.0) return roomLeft;
            if (budget < first) return 0;

            int affordable;
            if (System.Math.Abs(r - 1.0) < 0.000001)
            {
                affordable = (int)System.Math.Floor(budget / first);
            }
            else
            {
                // k = log_r(1 + butce*(r-1)/ilk)
                double inner = 1.0 + budget * (r - 1.0) / first;
                affordable = (int)System.Math.Floor(System.Math.Log(inner) / System.Math.Log(r));
            }

            if (affordable < 0) affordable = 0;

            // Kayan nokta hatasi bir fazla gosterebilir; dogrula ve kirp.
            while (affordable > 0 && GetBulkCost(upgrade, affordable) > budget) affordable--;

            return System.Math.Min(affordable, roomLeft);
        }

        /// <summary>
        /// Istenen adedi almaya calisir, gercekte alinan adedi doner.
        /// Butce veya tavan yetmezse mumkun olan kadarini alir.
        /// </summary>
        public int TryBuyBulk(UpgradeSO upgrade, int requested)
        {
            if (upgrade == null || requested <= 0) return 0;
            if (!IsUnlocked(upgrade) || IsMaxed(upgrade)) return 0;

            // Baglanti tek seferlik; toplu alim anlamsiz.
            if (upgrade.type == UpgradeType.Connection)
                return TryBuy(upgrade) ? 1 : 0;

            // Fiber Kredisi ile alinan yetenekler bu yoldan GECMEMELI.
            //
            // Asagisi dogrudan _wallet.TrySpend cagiriyor. Toplu alim modu
            // (x10 / MAKS) statik ve sekmeler arasi paylasildigi icin oyuncu
            // yukseltmeler sekmesinde MAKS secip prestij sekmesine gecince
            // yetenekleri PARAYLA satin alabiliyordu — kredi hic harcanmadan.
            // Tek tek satin alma yolu para birimini dogru sectigi icin
            // istenen adet kadar onu tekrarliyoruz.
            if (UsesFiberCredits(upgrade))
            {
                int bought = 0;
                double creditsSpent = 0.0;

                while (bought < requested)
                {
                    // Maliyet satin almadan ONCE okunmali: TryBuy seviyeyi
                    // artirir ve bir sonraki maliyet degisir.
                    double step = GetCost(upgrade);
                    if (!TryBuy(upgrade)) break;

                    creditsSpent += step;
                    bought++;
                }

                // Geri alma penceresi burada da acilmali. Onceki surumde
                // _lastTransaction yalnizca para yolunda yaziliyordu; yani
                // TryUndo'nun isFiberCredit dali HIC calismiyordu — yanlislikla
                // 10 kredi harcayan oyuncunun geri donusu yoktu.
                if (bought > 0)
                {
                    _lastTransaction = new LastTransaction
                    {
                        upgrade = upgrade,
                        count = bought,
                        costSpent = creditsSpent,
                        timestamp = Time.unscaledTime,
                        isFiberCredit = true
                    };

                    if (UndoStateChanged != null) UndoStateChanged();
                }

                return bought;
            }

            int count = System.Math.Min(requested, GetMaxAffordable(upgrade));
            if (count <= 0) return 0;

            double cost = GetBulkCost(upgrade, count);
            if (!_wallet.TrySpend(cost)) return 0;

            _levels[upgrade] = GetLevel(upgrade) + count;

            // Kaza Korumasi: Buyuk/MAX alimlarda 4.5 saniyelik Geri Al (Undo) penceresi olustur.
            _lastTransaction = new LastTransaction
            {
                upgrade = upgrade,
                count = count,
                costSpent = cost,
                timestamp = Time.unscaledTime,
                isFiberCredit = false
            };

            ApplyStats();
            CountStat(StatType.UpgradesBought, count);

            if (Purchased != null) Purchased(upgrade);
            if (UndoStateChanged != null) UndoStateChanged();
            return count;
        }

        public struct LastTransaction
        {
            public UpgradeSO upgrade;
            public int count;
            public double costSpent;
            public float timestamp;
            public bool isFiberCredit;
        }

        LastTransaction _lastTransaction;

        /// <summary>Geri alma suresi (sn) — MAX veya toplu alimlarda kaza korumasi.</summary>
        public const float UndoWindowSeconds = 4.5f;

        public bool CanUndo
        {
            get
            {
                return _lastTransaction.upgrade != null
                    && _lastTransaction.count > 0
                    && (Time.unscaledTime - _lastTransaction.timestamp) <= UndoWindowSeconds;
            }
        }

        public LastTransaction CurrentUndoTransaction { get { return _lastTransaction; } }

        public event Action UndoStateChanged;

        public bool TryUndo()
        {
            if (!CanUndo) return false;

            UpgradeSO u = _lastTransaction.upgrade;
            int count = _lastTransaction.count;
            double cost = _lastTransaction.costSpent;
            bool isFiber = _lastTransaction.isFiberCredit;

            int curLevel = GetLevel(u);
            if (curLevel < count) return false;

            _levels[u] = curLevel - count;

            if (isFiber)
            {
                if (_gm != null && _gm.Prestige != null)
                    _gm.Prestige.RefundCredits((int)cost);
            }
            else
            {
                // Add DEGIL Refund: iade toplam kazanci artirmamali, yoksa
                // "MAKS al -> geri al" dongusu bedava prestij kredisi uretir.
                if (_wallet != null)
                    _wallet.Refund(cost);
            }

            _lastTransaction = default(LastTransaction);
            ApplyStats();

            if (UndoStateChanged != null) UndoStateChanged();
            return true;
        }

        public bool TryBuy(UpgradeSO upgrade)
        {
            if (!CanBuy(upgrade)) return false;

            double cost = GetCost(upgrade);

            if (UsesFiberCredits(upgrade))
            {
                if (_gm.Prestige == null || !_gm.Prestige.TrySpendCredits((int)cost)) return false;
            }
            else if (!_wallet.TrySpend(cost))
            {
                return false;
            }

            _levels[upgrade] = GetLevel(upgrade) + 1;

            // Baglanti yukseltmesi kademeyi acar. SetTier zaten TierChanged
            // yayinlar, o da ApplyStats'i cagirir.
            if (upgrade.type == UpgradeType.Connection && upgrade.connectionTierIndex >= 0)
                _gm.SetTier(upgrade.connectionTierIndex);
            else
                ApplyStats();

            CountStat(StatType.UpgradesBought, 1);

            if (Purchased != null) Purchased(upgrade);
            return true;
        }

        /// <summary>
        /// Basarim/gorev sayacini artirir.
        ///
        /// Geri alma (TryUndo) sayaci DUSURMUYOR bilincli olarak: sayac
        /// "kac kez satin aldin" degil "kac satin alma islemi yaptin"
        /// olcusu. Dusurmek, al-geri al dongusuyle gunluk gorev ilerlemesini
        /// silmeye calisan tuhaf bir kenar durumu acardi.
        /// </summary>
        static void CountStat(StatType type, int amount)
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Stats != null) gm.Stats.Add(type, amount);
        }

        // ------------------------------------------------------------------
        // Etkilerin uygulanmasi
        // ------------------------------------------------------------------

        /// <summary>
        /// Verilen tipteki tum yukseltmelerin toplam etkisi.
        ///
        ///     etki = seviye * etkiPerSeviye * kilometreTasiCarpani
        ///
        /// Kilometre tasi carpani TUM seviyelere uygulanir, yalnizca esigi asan
        /// seviyeye degil — esige ulasmak boylece hissedilir bir sicrama olur.
        /// </summary>
        public double TotalEffect(UpgradeType type)
        {
            if (_database == null || _database.upgrades == null) return 0.0;

            double sum = 0.0;
            for (int i = 0; i < _database.upgrades.Length; i++)
            {
                UpgradeSO u = _database.upgrades[i];
                if (u == null || u.type != type) continue;

                int level = GetLevel(u);
                if (level <= 0) continue;

                sum += level * (double)u.effectPerLevel * u.MilestoneMultiplierAt(level);
            }
            return sum;
        }

        public int TotalLevel(UpgradeType type)
        {
            if (_database == null || _database.upgrades == null) return 0;

            int sum = 0;
            for (int i = 0; i < _database.upgrades.Length; i++)
            {
                UpgradeSO u = _database.upgrades[i];
                if (u != null && u.type == type) sum += GetLevel(u);
            }
            return sum;
        }

        public void ApplyStats()
        {
            if (_config == null) return;

            if (_speed != null)
            {
                // Tiklama gucu ORAN olarak veriliyor: taban hizin kati.
                //
                // Eski kod logaritmik bir kademe olcegi kullaniyordu
                // (log10(minSpeed) -> 1, 2, 3 ... 12) ve sonucu MUTLAK bit/sn
                // olarak yaziyordu. Kademe hizlari ustel buyudugu icin bu iki
                // buyume hizi arasindaki fark her kademede katlaniyordu:
                // kademe 8'de kadran 1e12..1e14 arasindayken tiklama basina
                // ~96 bit ekleniyordu. Ibre kimildamiyordu, yani oyunun ana
                // etkilesimi olu bir dugmeye donusmustu.
                _speed.ClickPowerRatio = _config.clickPowerRatio
                                       * (1.0 + TotalEffect(UpgradeType.ClickPower))
                                       * PrestigeMultiplier;

                _speed.CritChance = Mathf.Clamp01(
                    _config.baseCritChance + (float)TotalEffect(UpgradeType.CritChance));

                _speed.ExtraOverheatTolerance = (float)TotalEffect(UpgradeType.OverheatTolerance);

                // GDD 6.3: TabanHiz = kademeBazHizi * (1 + 0.10 * pasifSeviye)
                // Faz 16: Indirilenler Arsivi bonusu da ayni havuza giriyor.
                double collectionBonus = (_gm != null && _gm.Collection != null)
                    ? _gm.Collection.SpeedBonus : 0.0;

                _speed.PassiveSpeedMultiplier =
                    1.0 + TotalEffect(UpgradeType.PassiveSpeed) + collectionBonus;

                // --- Faz 13 ---
                _speed.ExtraRedlineBonus = (float)TotalEffect(UpgradeType.RedlineBonus);
                _speed.RedlineThresholdReduction =
                    (float)TotalEffect(UpgradeType.RedlineThreshold);

                // Oranlar 1'i asamaz: ceza sifirlanirsa overheat mekanigi kaybolur,
                // yumusak dusus 1'i asarsa overheat hizi artirir hale gelirdi.
                _speed.OverheatPenaltyReduction = Mathf.Clamp01(
                    (float)TotalEffect(UpgradeType.OverheatPenaltyReduction));
                _speed.OverheatSoftFailRatio = Mathf.Clamp01(
                    (float)TotalEffect(UpgradeType.OverheatSoftFail));

                // --- Faz 14 ---
                // Mekanik Klavye ve Makro Script'in ikisi de saniyedeki tiklama
                // sayisina EKLIYOR. (UPGRADE_TREE'de Makro "hiz +%40" diye
                // gecmisti; ayni havuzda toplanan iki farkli birim tutarsiz
                // olurdu, ikisi de tik/sn olarak sadelestirildi.)
                _speed.AutoClicksPerSecond = (float)TotalEffect(UpgradeType.AutoClick);
            }

            if (_download != null)
            {
                // NVMe SSD (CompletionBonus) da odul havuzuna giriyor: ayri bir
                // kart ama mekanik olarak dosya basi odul carpani.
                _download.RewardMultiplier =
                    (1.0 + TotalEffect(UpgradeType.RewardMultiplier)
                         + TotalEffect(UpgradeType.CompletionBonus)) * PrestigeMultiplier;

                // Sikistirma: boyut carpani. %90'dan fazla kucultmeye izin yok —
                // dosyalar aninda biterse indirme hissi tamamen kayboluyor.
                double shrink = TotalEffect(UpgradeType.FileSizeReduction);
                _download.FileSizeMultiplier = System.Math.Max(0.10, 1.0 - shrink);

                // Otomatik Kuyruk bir SEVIYE kademesi (etki toplami degil):
                // sv1 en karliyi secer, sv2 tekrar sinirini da kaldirir.
                _download.QueueAutomationLevel = TotalLevel(UpgradeType.QueueAutomation);

                // --- Faz 16 ---
                _download.ExtraSlots = TotalLevel(UpgradeType.ParallelSlots);
                _download.SeedIncomeRate = TotalEffect(UpgradeType.SeedIncome);
            }

            if (_gm != null && _gm.Events != null)
            {
                _gm.Events.NegativeEventResistance = Mathf.Clamp01(
                    (float)TotalEffect(UpgradeType.EventResistance));

                _gm.Events.QuotaBypassed = TotalLevel(UpgradeType.QuotaBypass) > 0;

                // Kota aktifken taban hiz degistiyse tavani tazele — yoksa
                // oyuncu baglanti alir ama hiz eski tavanda takili kalirdi.
                _gm.Events.RefreshQuotaCeiling();
            }

            // --- Faz 15: prestij yetenekleri ---
            if (_gm != null && _gm.Prestige != null)
            {
                _gm.Prestige.CreditBonus = TotalEffect(UpgradeType.PrestigeCreditBonus);

                // Baslangic kademesi bir SEVIYE kademesi: her seviye bir kademe.
                _gm.Prestige.StartingTier = TotalLevel(UpgradeType.StartingTier);
            }

            if (StatsChanged != null) StatsChanged();
        }

        /// <summary>Kayittan yukleme icin (Faz 6).</summary>
        public void Restore(Dictionary<UpgradeSO, int> levels)
        {
            _levels.Clear();
            if (levels != null)
            {
                foreach (KeyValuePair<UpgradeSO, int> kv in levels)
                    if (kv.Key != null) _levels[kv.Key] = kv.Value;
            }
            ApplyStats();
        }

        /// <summary>
        /// Prestij sifirlamasi (Faz 7). Baglantilar dahil her sey sifirlanir —
        /// ama prestij yetenekleri KORUNUR: onlar Fiber Kredisi ile alindi,
        /// her turda yeniden satin alinmalari sistemin amacini bozardi.
        /// </summary>
        public void ResetUpgrades()
        {
            var keep = new List<KeyValuePair<UpgradeSO, int>>();

            foreach (KeyValuePair<UpgradeSO, int> kv in _levels)
                if (UsesFiberCredits(kv.Key)) keep.Add(kv);

            _levels.Clear();

            for (int i = 0; i < keep.Count; i++)
                _levels[keep[i].Key] = keep[i].Value;

            ApplyStats();
        }

        /// <summary>Kayit yazimi icin (Faz 6).</summary>
        public IEnumerable<KeyValuePair<UpgradeSO, int>> AllLevels
        {
            get { return _levels; }
        }
    }
}
