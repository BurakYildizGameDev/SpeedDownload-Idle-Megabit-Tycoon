using System.Collections.Generic;
using UnityEngine;

namespace SpeedDownload.Data
{
    /// <summary>
    /// Tum veri varliklarinin tek giris noktasi. GameManager yalnizca buna referans
    /// tutar; boylece sahnede 40+ ayri referans alani olusmaz.
    /// </summary>
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "SpeedDownload/Game Database")]
    public class GameDatabaseSO : ScriptableObject
    {
        public GameConfigSO config;

        [Tooltip("tierIndex sirasina gore dizilmis olmali (0..8).")]
        public ConnectionTierSO[] tiers;

        public FileDataSO[] files;
        public UpgradeSO[] upgrades;

        [Tooltip("Kademe 9+ prosedurel uretim icin sablon.")]
        public ConnectionTierSO infiniteTierTemplate;

        [Tooltip("Kademe 9+ mega arsiv sablonu.")]
        public FileDataSO proceduralFileTemplate;

        // Sik cagrilan aramalar icin onbellek — her frame LINQ calistirmamak icin.
        Dictionary<int, List<FileDataSO>> _filesByTier;

        // Kademe 9+ calisma aninda uretilir; uretilenler burada tutulur.
        Dictionary<int, ConnectionTierSO> _infiniteTiers;
        Dictionary<int, FileDataSO> _infiniteFiles;
        Dictionary<int, UpgradeSO> _infiniteUpgrades;

        void OnEnable()
        {
            _filesByTier = null;
            _infiniteTiers = null;
            _infiniteFiles = null;
            _infiniteUpgrades = null;
        }

        public ConnectionTierSO GetTier(int tierIndex)
        {
            if (tiers == null || tiers.Length == 0) return null;
            if (tierIndex < 0) tierIndex = 0;

            if (tierIndex > HighestFixedTierIndex)
            {
                ConnectionTierSO infinite = GetInfiniteTier(tierIndex);
                if (infinite != null) return infinite;
                return tiers[tiers.Length - 1];
            }

            return tiers[tierIndex];
        }

        // ------------------------------------------------------------------
        // Sonsuz kademeler (9+) — PLAN Bolum 2.8
        //
        // Her sonsuz kademe bir oncekinin infiniteTierSpeedFactor kati.
        // Boyut ve odul ayni katsayiyla buyudugu icin bir dosyanin tamamlanma
        // suresi sabit kalir; yalnizca sayilar buyur.
        // ------------------------------------------------------------------

        /// <summary>Sonsuz kademeler kullanilabilir mi? (sablon uretilmis mi)</summary>
        public bool HasInfiniteTiers { get { return infiniteTierTemplate != null; } }

        static readonly string[] InfiniteNames =
        {
            "Karadelik Yonlendirici",
            "Solucan Deligi Hatti",
            "Kuasar Omurgasi",
            "Nebula Anahtari",
            "Pulsar Rolesi",
            "Galaktik Veriyolu",
            "Entropi Kanali",
            "Kuantum Kopugu Hatti"
        };

        /// <summary>Kademe 9+ icin uretilmis kademe. Ayni indeks hep ayni nesneyi doner.</summary>
        public ConnectionTierSO GetInfiniteTier(int tierIndex)
        {
            if (!HasInfiniteTiers || tierIndex <= HighestFixedTierIndex) return null;

            if (_infiniteTiers == null) _infiniteTiers = new Dictionary<int, ConnectionTierSO>();

            ConnectionTierSO cached;
            if (_infiniteTiers.TryGetValue(tierIndex, out cached) && cached != null) return cached;

            int step = tierIndex - infiniteTierTemplate.tierIndex; // 9 -> 0
            double factor = System.Math.Pow(InfiniteSpeedFactor, step);

            var tier = Instantiate(infiniteTierTemplate);
            tier.name = InfiniteTierPrefix + tierIndex + "_Infinite";
            tier.tierIndex = tierIndex;
            tier.tierName = InfiniteName(step);
            tier.isProceduralTemplate = false;

            tier.minSpeed = infiniteTierTemplate.minSpeed * factor;
            tier.maxSpeed = infiniteTierTemplate.maxSpeed * factor;
            tier.baseSpeed = infiniteTierTemplate.baseSpeed * factor;

            // Altin aci adimi: ardisik kademeler renkte olabildigince uzak duser.
            tier.dialHueShift = Mathf.Repeat((step + 1) * InfiniteHueStep, 1f);

            _infiniteTiers[tierIndex] = tier;
            return tier;
        }

        static string InfiniteName(int step)
        {
            string baseName = InfiniteNames[step % InfiniteNames.Length];
            int cycle = step / InfiniteNames.Length;
            return cycle == 0 ? baseName : baseName + " " + (cycle + 1);
        }

        /// <summary>Kademe 9+ icin uretilmis mega dosya.</summary>
        public FileDataSO GetInfiniteFile(int tierIndex)
        {
            if (proceduralFileTemplate == null || tierIndex <= HighestFixedTierIndex) return null;

            if (_infiniteFiles == null) _infiniteFiles = new Dictionary<int, FileDataSO>();

            FileDataSO cached;
            if (_infiniteFiles.TryGetValue(tierIndex, out cached) && cached != null) return cached;

            int step = tierIndex - proceduralFileTemplate.minConnectionTier; // 9 -> 0
            double factor = System.Math.Pow(InfiniteSpeedFactor, step);

            var file = Instantiate(proceduralFileTemplate);
            file.name = InfiniteFilePrefix + tierIndex;
            file.isProceduralTemplate = false;
            file.minConnectionTier = tierIndex;
            file.maxConnectionTier = tierIndex;
            file.sizeBits = proceduralFileTemplate.sizeBits * factor;
            file.baseReward = proceduralFileTemplate.baseReward * factor;

            _infiniteFiles[tierIndex] = file;
            return file;
        }

        /// <summary>Kademe 9+ acan uretilmis baglanti yukseltmesi.</summary>
        public UpgradeSO GetInfiniteConnectionUpgrade(int tierIndex)
        {
            if (!HasInfiniteTiers || tierIndex <= HighestFixedTierIndex) return null;

            if (_infiniteUpgrades == null) _infiniteUpgrades = new Dictionary<int, UpgradeSO>();

            UpgradeSO cached;
            if (_infiniteUpgrades.TryGetValue(tierIndex, out cached) && cached != null) return cached;

            ConnectionTierSO tier = GetInfiniteTier(tierIndex);
            if (tier == null) return null;

            // Maliyet, bir onceki kademenin dosya odulunden turetiliyor —
            // sabit kademelerdeki (odul x 25) kuralinin aynisi.
            FileDataSO previousFile = tierIndex - 1 > HighestFixedTierIndex
                ? GetInfiniteFile(tierIndex - 1)
                : null;

            double previousReward = previousFile != null
                ? previousFile.baseReward
                : AverageRewardOfTier(HighestFixedTierIndex);

            var upgrade = CreateInstance<UpgradeSO>();
            upgrade.name = InfiniteUpgradePrefix + tierIndex;
            upgrade.displayName = tier.tierName;
            upgrade.description = "Sonsuz kademe. Hat " + InfiniteSpeedFactor + " kat hizlanir.";
            upgrade.icon = tier.connectionIcon;
            upgrade.type = UpgradeType.Connection;
            upgrade.tab = UpgradeTab.Infrastructure;
            upgrade.baseCost = previousReward * InfiniteConnectionCostFactor;
            upgrade.costGrowth = 1f;
            upgrade.maxLevel = 1;
            upgrade.effectPerLevel = 0f;
            upgrade.requiredTierIndex = tierIndex - 1;
            upgrade.connectionTierIndex = tierIndex;

            _infiniteUpgrades[tierIndex] = upgrade;
            return upgrade;
        }

        double AverageRewardOfTier(int tierIndex)
        {
            List<FileDataSO> pool = GetFilesForTier(tierIndex);
            if (pool == null || pool.Count == 0) return 1.0;

            double sum = 0.0;
            for (int i = 0; i < pool.Count; i++) sum += pool[i].baseReward;
            return sum / pool.Count;
        }

        double InfiniteSpeedFactor
        {
            get { return config != null ? config.infiniteTierSpeedFactor : 100.0; }
        }

        float InfiniteHueStep
        {
            get { return config != null ? config.infiniteTierHueStep : 0.381966f; }
        }

        const double InfiniteConnectionCostFactor = 25.0;

        public int HighestFixedTierIndex
        {
            get { return tiers == null ? 0 : tiers.Length - 1; }
        }

        public List<FileDataSO> GetAllFiles()
        {
            var result = new List<FileDataSO>();
            if (files != null) result.AddRange(files);
            return result;
        }

        /// <summary>Verilen kademede cikabilecek dosyalar (onbellekli).</summary>
        public List<FileDataSO> GetFilesForTier(int tierIndex)
        {
            if (_filesByTier == null) _filesByTier = new Dictionary<int, List<FileDataSO>>();

            List<FileDataSO> cached;
            if (_filesByTier.TryGetValue(tierIndex, out cached)) return cached;

            // Sonsuz kademelerde sabit dosya listesi yok; uretilen tek mega arsiv var.
            if (tierIndex > HighestFixedTierIndex)
            {
                var infiniteList = new List<FileDataSO>();
                FileDataSO infiniteFile = GetInfiniteFile(tierIndex);
                if (infiniteFile != null) infiniteList.Add(infiniteFile);

                _filesByTier[tierIndex] = infiniteList;
                return infiniteList;
            }

            var result = new List<FileDataSO>();
            if (files != null)
            {
                for (int i = 0; i < files.Length; i++)
                {
                    FileDataSO f = files[i];
                    if (f != null && !f.isProceduralTemplate && f.IsAvailableAt(tierIndex))
                        result.Add(f);
                }
            }

            // Hicbiri uymuyorsa en yakin alt kademenin dosyalarina dus — oyun asla
            // "indirilecek dosya yok" durumuna girmemeli.
            //
            // Sonuc ONBELLEGE DE yazilmali. Eskiden bu dal dogrudan return
            // ediyordu: o kademe icin onbellek hicbir zaman dolmuyor ve her
            // cagri tum dosya dizisini yeniden tarayip bir de rekursiyona
            // giriyordu. GetFilesForTier saniyede onlarca kez cagriliyor
            // (AverageRewardPerBit -> CurrentIncomePerSecond -> HUD).
            if (result.Count == 0 && tierIndex > 0)
            {
                List<FileDataSO> fallback = GetFilesForTier(tierIndex - 1);
                _filesByTier[tierIndex] = fallback;
                return fallback;
            }

            _filesByTier[tierIndex] = result;
            return result;
        }

        /// <summary>Agirliga gore rastgele dosya secer; ayni dosyayi art arda vermez.</summary>
        public FileDataSO PickRandomFile(int tierIndex, FileDataSO exclude)
        {
            List<FileDataSO> pool = GetFilesForTier(tierIndex);
            if (pool.Count == 0) return null;
            if (pool.Count == 1) return pool[0];

            float total = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == exclude) continue;
                total += Mathf.Max(0.0001f, pool[i].weight);
            }

            float roll = Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == exclude) continue;
                roll -= Mathf.Max(0.0001f, pool[i].weight);
                if (roll <= 0f) return pool[i];
            }

            return pool[pool.Count - 1];
        }

        /// <summary>
        /// Kademedeki en karli dosya — birim veri basina en cok odul veren.
        ///
        /// Otomatik Kuyruk yukseltmesi icin. Kriter odul/boyut orani: indirme
        /// suresi boyutla dogru orantili oldugu icin bu oran dogrudan "saniyede
        /// kazanilan para" demek.
        /// </summary>
        public FileDataSO PickBestFile(int tierIndex, FileDataSO exclude)
        {
            List<FileDataSO> pool = GetFilesForTier(tierIndex);
            if (pool == null || pool.Count == 0) return null;
            if (pool.Count == 1) return pool[0];

            FileDataSO best = null;
            double bestRate = -1.0;

            for (int i = 0; i < pool.Count; i++)
            {
                FileDataSO f = pool[i];
                if (f == null || f.sizeBits <= 0.0) continue;
                if (f == exclude) continue;

                double rate = f.baseReward / f.sizeBits;
                if (rate > bestRate)
                {
                    bestRate = rate;
                    best = f;
                }
            }

            // Havuzda exclude disinda uygun dosya yoksa kurala takilip
            // bos donmektense exclude'u kabul et.
            return best != null ? best : pool[0];
        }

        public List<UpgradeSO> GetUpgradesForTab(UpgradeTab tab)
        {
            var result = new List<UpgradeSO>();
            if (upgrades == null) return result;

            for (int i = 0; i < upgrades.Length; i++)
            {
                if (upgrades[i] != null && upgrades[i].tab == tab)
                    result.Add(upgrades[i]);
            }
            return result;
        }

        /// <summary>Belirli bir kademeyi acan baglanti yukseltmesi.</summary>
        public UpgradeSO GetConnectionUpgrade(int tierIndex)
        {
            if (tierIndex > HighestFixedTierIndex) return GetInfiniteConnectionUpgrade(tierIndex);
            if (upgrades == null) return null;

            for (int i = 0; i < upgrades.Length; i++)
            {
                UpgradeSO u = upgrades[i];
                if (u != null && u.type == UpgradeType.Connection && u.connectionTierIndex == tierIndex)
                    return u;
            }
            return null;
        }

        // Sonsuz kademe varliklari calisma aninda uretildigi icin diskteki adla
        // yeniden bulunabilmeleri gerekiyor. Uretim ve cozumleme ayni sabitleri
        // kullanir ki isimlendirme ikiye ayrilamasin.
        const string InfiniteTierPrefix = "Tier";
        const string InfiniteFilePrefix = "File_Infinite";
        const string InfiniteUpgradePrefix = "Upg_InfiniteConn";

        /// <summary>Asset adiyla yukseltme bulur (kayit yukleme icin).</summary>
        public UpgradeSO FindUpgrade(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            if (upgrades != null)
            {
                for (int i = 0; i < upgrades.Length; i++)
                    if (upgrades[i] != null && upgrades[i].name == assetName) return upgrades[i];
            }

            int tierIndex;
            if (TryParseSuffix(assetName, InfiniteUpgradePrefix, out tierIndex))
                return GetInfiniteConnectionUpgrade(tierIndex);

            return null;
        }

        /// <summary>Asset adiyla dosya bulur (kayit yukleme icin).</summary>
        public FileDataSO FindFile(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) return null;

            if (files != null)
            {
                for (int i = 0; i < files.Length; i++)
                    if (files[i] != null && files[i].name == assetName) return files[i];
            }

            int tierIndex;
            if (TryParseSuffix(assetName, InfiniteFilePrefix, out tierIndex))
                return GetInfiniteFile(tierIndex);

            return null;
        }

        static bool TryParseSuffix(string assetName, string prefix, out int value)
        {
            value = 0;
            if (!assetName.StartsWith(prefix, System.StringComparison.Ordinal)) return false;

            return int.TryParse(assetName.Substring(prefix.Length), out value);
        }

        public UpgradeSO GetFirstUpgradeOfType(UpgradeType type)
        {
            if (upgrades == null) return null;

            for (int i = 0; i < upgrades.Length; i++)
            {
                if (upgrades[i] != null && upgrades[i].type == type)
                    return upgrades[i];
            }
            return null;
        }
    }
}
