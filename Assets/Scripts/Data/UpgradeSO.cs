using UnityEngine;

namespace SpeedDownload.Data
{
    /// <summary>Yukseltmenin oyuna etkisi (GDD Bolum 6).</summary>
    public enum UpgradeType
    {
        /// <summary>Tiklama basina hiz artisi. (Modeme Vurmak)</summary>
        ClickPower = 0,
        /// <summary>Overheat'e dayanma suresi. (Harici Fan)</summary>
        OverheatTolerance = 1,
        /// <summary>Kritik hiz patlamasi ihtimali. (DNS Ayari)</summary>
        CritChance = 2,
        /// <summary>Offline kazanc verimi ve tavani. (Download Manager)</summary>
        OfflineCapacity = 3,
        /// <summary>Dosya basi odul carpani. (Premium Sunucu)</summary>
        RewardMultiplier = 4,
        /// <summary>Bir sonraki baglanti kademesini acar. Tek seferlik.</summary>
        Connection = 5,
        /// <summary>
        /// Taban (bosta) hizi artirir — GDD Bolum 6.3'teki
        /// "TabanHiz = kademeBazHizi * (1 + 0.10 * pasifSeviye)" formulunun karsiligi.
        /// (Hat Bakimi)
        /// </summary>
        PassiveSpeed = 6,

        // --- Faz 13 ve sonrasi (UPGRADE_TREE.md Bolum 7) ---
        // Numaralar ASLA degistirilmemeli: kayit dosyasi ve .asset'ler bu
        // sayilarla serilesiyor.

        /// <summary>Overheat cezasinin suresini kisaltir. (Isi Macunu)</summary>
        OverheatPenaltyReduction = 7,

        /// <summary>Overheat'te hiz tam sifirlanmaz, bir kismi korunur. (UPS)</summary>
        OverheatSoftFail = 8,

        /// <summary>Redline tepe carpanini yukseltir. (Yonlu Anten)</summary>
        RedlineBonus = 9,

        /// <summary>Redline esigini asagi ceker, bolgeyi genisletir. (Overclock Araci)</summary>
        RedlineThreshold = 10,

        /// <summary>Ayni anda inen dosya sayisi. (RAM / Ikinci Hat) — Faz 16</summary>
        ParallelSlots = 11,

        /// <summary>Dosya tamamlaninca ek odul yuzdesi. (NVMe SSD)</summary>
        CompletionBonus = 12,

        /// <summary>Saniyede otomatik tiklama. (Mekanik Klavye / Makro) — Faz 14</summary>
        AutoClick = 13,

        /// <summary>Dosya boyutunu kucultur. (Sikistirma Algoritmasi)</summary>
        FileSizeReduction = 14,

        /// <summary>Tamamlanan dosyalardan pasif gelir. (Torrent) — Faz 16</summary>
        SeedIncome = 15,

        /// <summary>Olumsuz olay cikma sansini dusurur. (Reklam Engelleyici)</summary>
        EventResistance = 16,

        /// <summary>Virus olayina karsi koruma. (Antivirus) — Faz 16</summary>
        VirusImmunity = 17,

        /// <summary>Kota olayinin hiz tavanini etkisizlestirir. (VPN) — Faz 16</summary>
        QuotaBypass = 18,

        /// <summary>Dosya kuyrugunu otomatik secer. (Otomatik Kuyruk) — Faz 14</summary>
        QueueAutomation = 19,

        // --- Faz 15: prestij yetenekleri (Fiber Kredisi ile alinir) ---

        /// <summary>Offline kazanc verimini artirir. (Offline Ustasi)</summary>
        OfflineEfficiency = 20,

        /// <summary>Prestijde kazanilan Fiber Kredisi'ni artirir. (Kredi Faizi)</summary>
        PrestigeCreditBonus = 21,

        /// <summary>Prestij sonrasi baslanacak kademe. (Hizli Baslangic)</summary>
        StartingTier = 22
    }

    /// <summary>Alt paneldeki sekme (GDD Bolum 9).</summary>
    public enum UpgradeTab
    {
        Infrastructure = 0,
        Hardware = 1,
        Software = 2,

        /// <summary>Fiber Kredisi ile alinan kalici yetenekler — Faz 15.</summary>
        Prestige = 3
    }

    [CreateAssetMenu(fileName = "Upgrade", menuName = "SpeedDownload/Upgrade")]
    public class UpgradeSO : ScriptableObject
    {
        [Header("Kimlik")]
        public string displayName = "Yeni Yukseltme";
        [TextArea(2, 4)] public string description;
        public Sprite icon;

        [Header("Siniflandirma")]
        public UpgradeType type = UpgradeType.ClickPower;
        public UpgradeTab tab = UpgradeTab.Hardware;

        [Header("Maliyet")]
        public double baseCost = 10.0;

        [Tooltip("Maliyet(n) = baseCost * costGrowth^n")]
        public float costGrowth = 1.15f;

        [Tooltip("0 = sinirsiz seviye.")]
        public int maxLevel = 0;

        [Header("Etki")]
        [Tooltip("Seviye basina etki. Anlami type'a gore degisir:\n" +
                 "ClickPower: +oran (0.15 = %15)\n" +
                 "OverheatTolerance: +saniye\n" +
                 "CritChance: +ihtimal (0.05 = %5)\n" +
                 "RewardMultiplier: +oran\n" +
                 "PassiveSpeed: +oran (0.10 = %10 taban hiz)\n" +
                 "OfflineCapacity: seviye kademesi")]
        public float effectPerLevel = 0.15f;

        [Header("Kilometre taslari")]
        [Tooltip("Bu seviyelere ulasilinca yukseltmenin etkisi milestoneMultiplier " +
                 "ile carpilir (kumulatif). Bos birakilirsa sistem kapalidir.")]
        public int[] milestoneLevels = { 10, 25, 50, 100 };

        [Tooltip("Her asilan kilometre tasinda etkinin carpani. 1 = etkisiz.")]
        public float milestoneMultiplier = 2f;

        [Header("Kilit")]
        [Tooltip("Bu kademeye ulasmadan gorunmez / satin alinamaz.")]
        public int requiredTierIndex = 0;

        [Tooltip("type == Connection ise: actigi kademe indeksi.")]
        public int connectionTierIndex = -1;

        /// <summary>Maliyet(n) = baseCost * costGrowth^n</summary>
        public double CostAtLevel(int level)
        {
            if (level < 0) level = 0;
            return baseCost * System.Math.Pow(costGrowth, level);
        }

        public bool IsMaxed(int level)
        {
            return maxLevel > 0 && level >= maxLevel;
        }

        // ------------------------------------------------------------------
        // Kilometre taslari
        //
        // Sinirsiz yukseltmelerin sorunu, 40. seviyenin 39. seviyeden farksiz
        // hissettirmesiydi. Belirli seviyelerde etkiyi ikiye katlamak, oyuncunun
        // gozunu hep bir sonraki esige dikili tutuyor — ve bunu yeni icerik
        // uretmeden yapiyor.
        // ------------------------------------------------------------------

        /// <summary>Verilen seviyede asilan kilometre tasi sayisi.</summary>
        public int MilestonesReached(int level)
        {
            if (milestoneLevels == null) return 0;

            int count = 0;
            for (int i = 0; i < milestoneLevels.Length; i++)
                if (level >= milestoneLevels[i]) count++;

            return count;
        }

        /// <summary>
        /// Etkiye uygulanacak kilometre tasi carpani. Asilan her esik icin
        /// kumulatif olarak carpilir (10 -> x2, 25 -> x4, 50 -> x8 ...).
        /// </summary>
        public double MilestoneMultiplierAt(int level)
        {
            if (milestoneMultiplier <= 1f) return 1.0;

            int reached = MilestonesReached(level);
            if (reached <= 0) return 1.0;

            return System.Math.Pow(milestoneMultiplier, reached);
        }

        /// <summary>
        /// Bir sonraki kilometre tasi seviyesi. Hepsi asildiysa veya sistem
        /// kapaliysa -1 doner.
        /// </summary>
        public int NextMilestone(int level)
        {
            if (milestoneLevels == null || milestoneMultiplier <= 1f) return -1;

            for (int i = 0; i < milestoneLevels.Length; i++)
                if (level < milestoneLevels[i]) return milestoneLevels[i];

            return -1;
        }
    }
}
