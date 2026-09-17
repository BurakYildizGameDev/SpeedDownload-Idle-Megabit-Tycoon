using System.Text;
using SpeedDownload.Data;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Faz 12 tasima araci: kilometre tasi alanlarini mevcut yukseltme
    /// varliklarina yazar.
    ///
    /// Neden gerekli: <c>milestoneLevels</c> ve <c>milestoneMultiplier</c> alanlari
    /// UpgradeSO'ya sonradan eklendi. Daha once uretilmis 14 .asset dosyasinda bu
    /// alanlar hic yok; Unity onlari C# alan baslatidicisindan dolduruyor ama
    /// diske yazili degiller. Bu, dosyaya bakan birinin degerleri gorememesi ve
    /// Inspector'dan tek tek duzeltmeye calismasi demek.
    ///
    /// Arac idempotent: zaten dogru olan varliga dokunmaz.
    /// </summary>
    public static class UpgradeMigrationTool
    {
        static readonly int[] DefaultMilestones = { 10, 25, 50, 100 };
        const float DefaultMultiplier = 2f;

        [MenuItem("Tools/SpeedDownload/5. Migrate Upgrade Assets (Milestones)", false, 50)]
        public static void Migrate()
        {
            string[] guids = AssetDatabase.FindAssets("t:UpgradeSO");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[UpgradeMigration] Hic UpgradeSO bulunamadi.");
                return;
            }

            int changed = 0;
            int skipped = 0;
            var log = new StringBuilder();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var upgrade = AssetDatabase.LoadAssetAtPath<UpgradeSO>(path);
                if (upgrade == null) continue;

                bool needsLevels = upgrade.milestoneLevels == null ||
                                   upgrade.milestoneLevels.Length == 0;
                bool needsMultiplier = upgrade.milestoneMultiplier <= 1f;

                // Baglanti yukseltmeleri tek seferlik (maxLevel 1) ve tavani ilk
                // esigin altinda kalan yukseltmeler (DNS max 5, Download Manager
                // max 2) hicbir kilometre tasina ULASAMAZ. Onlarda sistemi acik
                // birakmak, kartta asla gelmeyecek bir hedef gostermek olurdu.
                bool unreachable = upgrade.maxLevel > 0 &&
                                   upgrade.maxLevel < DefaultMilestones[0];

                if (upgrade.type == UpgradeType.Connection || unreachable)
                {
                    if (upgrade.milestoneMultiplier != 1f || needsLevels)
                    {
                        upgrade.milestoneLevels = new int[0];
                        upgrade.milestoneMultiplier = 1f;
                        EditorUtility.SetDirty(upgrade);
                        changed++;
                        log.AppendLine("  + " + upgrade.name + "  (kilometre tasi kapatildi: " +
                                       (upgrade.type == UpgradeType.Connection
                                            ? "baglanti"
                                            : "tavan " + upgrade.maxLevel + " < ilk esik") + ")");
                    }
                    else skipped++;
                    continue;
                }

                if (!needsLevels && !needsMultiplier)
                {
                    skipped++;
                    continue;
                }

                // Esikler TAVANIN ICINE kirpilir.
                //
                // Onceki hali sabit {10,25,50,100} yaziyordu. Tavani 20 olan
                // HatBakimi'nda bu, kartta ASLA varilamayacak uc hedef gostermek
                // demekti; tavani 10 olan HariciFan'da ise tek ulasilabilir esik
                // SON seviyeye denk geliyordu — yani odulun tadini cikaracak
                // seviye hic kalmiyordu. Bu kusur uretilmis 5 kartta bulundu.
                //
                // Guncel egri/esik politikasinin sahibi UpgradeBalanceTool'dur;
                // burasi yalnizca Faz 12 tasimasinin geri kalanini tamamliyor ve
                // bir daha ULASILAMAZ esik uretmemekle yukumlu.
                if (needsLevels)
                    upgrade.milestoneLevels = ClampToCap(DefaultMilestones, upgrade.maxLevel);

                if (needsMultiplier) upgrade.milestoneMultiplier = DefaultMultiplier;

                EditorUtility.SetDirty(upgrade);
                changed++;
                log.AppendLine("  + " + upgrade.name);
            }

            if (MigrateConfig(log)) changed++;

            if (changed > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log("[UpgradeMigration] Tamamlandi.\n" +
                      "  Toplam     : " + guids.Length + "\n" +
                      "  Guncellenen: " + changed + "\n" +
                      "  Zaten dogru: " + skipped + "\n" +
                      (changed > 0 ? log.ToString() : ""));
        }

        /// <summary>
        /// Esikleri tavanin icine kirpar. Sinirsiz kartta (tavan 0) hepsi gecerli.
        /// Hicbiri sigmiyorsa bos dizi doner — kart hedefsiz kalir ama YALAN
        /// hedef gostermez.
        /// </summary>
        static int[] ClampToCap(int[] levels, int maxLevel)
        {
            if (maxLevel <= 0) return (int[])levels.Clone();

            var kept = new System.Collections.Generic.List<int>();
            for (int i = 0; i < levels.Length; i++)
                if (levels[i] <= maxLevel) kept.Add(levels[i]);

            return kept.ToArray();
        }

        /// <summary>
        /// Faz 15: prestij esigi 8'den 6'ya cekildi. GameConfig varligi daha once
        /// uretildigi icin varsayilan degisikligi ona kendiliginden yansimaz.
        /// </summary>
        static bool MigrateConfig(StringBuilder log)
        {
            const int TargetUnlockTier = 6;

            string[] guids = AssetDatabase.FindAssets("t:GameConfigSO");
            bool changed = false;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var config = AssetDatabase.LoadAssetAtPath<GameConfigSO>(path);
                if (config == null || config.prestigeUnlockTier <= TargetUnlockTier) continue;

                log.AppendLine("  + " + config.name + "  prestij esigi " +
                               config.prestigeUnlockTier + " -> " + TargetUnlockTier);

                config.prestigeUnlockTier = TargetUnlockTier;
                EditorUtility.SetDirty(config);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Kilometre tasi carpanlarinin gercekten uygulandigini gosterir —
        /// asset'e bakmadan, calisan koddan.
        /// </summary>
        [MenuItem("Tools/SpeedDownload/Verify Upgrade Milestones", false, 51)]
        public static void Verify()
        {
            string[] guids = AssetDatabase.FindAssets("t:UpgradeSO");
            var report = new StringBuilder();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var u = AssetDatabase.LoadAssetAtPath<UpgradeSO>(path);
                if (u == null || u.type == UpgradeType.Connection) continue;

                report.AppendLine(string.Format(
                    "  {0,-24} esikler[{1}]  sv9={2:0.##}x  sv10={3:0.##}x  sv25={4:0.##}x  sv50={5:0.##}x",
                    u.name,
                    u.milestoneLevels != null ? u.milestoneLevels.Length : 0,
                    u.MilestoneMultiplierAt(9),
                    u.MilestoneMultiplierAt(10),
                    u.MilestoneMultiplierAt(25),
                    u.MilestoneMultiplierAt(50)));
            }

            Debug.Log("[UpgradeMigration] Kilometre tasi durumu:\n" + report);
        }
    }
}
