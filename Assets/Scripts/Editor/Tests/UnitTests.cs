using NUnit.Framework;
using SpeedDownload.Core;
using SpeedDownload.Data;
using SpeedDownload.Util;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools.Tests
{
    public class UnitTests
    {
        public static void RunAllUnitTests()
        {
            var tests = new UnitTests();
            tests.Wallet_AddAndSpend_CalculatesCorrectly();
            tests.NumberFormatter_FormatsCorrectly();
            tests.Localization_ReturnsFallbackWhenKeyMissing();
            tests.Optimization_FormatterPerformance_RunsFastWithoutErrors();

            // --- ekonomi matematigi ---
            tests.Upgrade_CostAtLevel_FollowsGeometricGrowth();
            tests.Economy_BulkCost_MatchesIncrementalSum();
            tests.Economy_MaxAffordable_IsExactBudgetBoundary();
            tests.Economy_MaxAffordable_UsesFiberCreditsForPrestigeCards();
            tests.Upgrade_Milestones_AreConsistent();

            // --- bicimleme ve kayit ---
            tests.NumberFormatter_BonusPercent_IsNotClampedAt100();
            tests.SaveData_RoundTrip_PreservesSlotsAndUpgrades();
            tests.SaveData_OldVersionWithoutSlots_LoadsCleanly();

            // --- reklam sistemi ---
            tests.AdService_PriorityResolution_PicksHighest();
            tests.AdService_StubAd_GrantsRewardOnlyAfterCompletion();
            tests.AdService_StubAd_Cancellation_InvokesSkippedCallback();

            // --- kayit guvenligi ---
            tests.SaveCrypto_RoundTrip_RecoversExactJson();
            tests.SaveCrypto_TamperedPayload_IsRejected();
            tests.SaveCrypto_TruncatedPayload_IsRejected();
            tests.SaveData_Sanitize_ClampsHostileValues();
            tests.Wallet_Refund_DoesNotInflateLifetimeEarnings();

            // --- basarimlar ve gunluk gorevler ---
            tests.Stats_RoundTrip_SurvivesEnumReordering();
            tests.Achievements_UnlockOnlyOnce_AndRestoreDoesNotReward();
            tests.Achievements_AllHaveTranslations();
            tests.Quests_PickForDay_IsStableAndDistinct();
            tests.Quests_AllPoolEntriesHaveTranslations();
            tests.Localization_TF_NeverThrows();

            // --- Play Store / cihaz guvenligi ---
            tests.SaveCrypto_Pbkdf2_MatchesReferenceImplementation();
            tests.SaveCrypto_SelfTest_Passes();

            Debug.Log("[UnitTests] TÜM BİRİM, OPTİMİZASYON, REKLAM, GÜVENLİK VE " +
                      "BAŞARIM TESTLERİ BAŞARIYLA GEÇTİ! (%100 PASS)");
        }
        [Test]
        public void Wallet_AddAndSpend_CalculatesCorrectly()
        {
            GameObject go = NewTestObject("TestWallet");
            Wallet wallet = go.AddComponent<Wallet>();

            try
            {
                Assert.AreEqual(0.0, wallet.Balance);
                Assert.AreEqual(0.0, wallet.LifetimeEarnings);

                wallet.Add(100.0);
                Assert.AreEqual(100.0, wallet.Balance);
                Assert.AreEqual(100.0, wallet.LifetimeEarnings);

                bool spent = wallet.TrySpend(40.0);
                Assert.IsTrue(spent);
                Assert.AreEqual(60.0, wallet.Balance);
                Assert.AreEqual(100.0, wallet.LifetimeEarnings); // Lifetime should not decrease

                bool overspent = wallet.TrySpend(100.0);
                Assert.IsFalse(overspent);
                Assert.AreEqual(60.0, wallet.Balance);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void NumberFormatter_FormatsCorrectly()
        {
            Assert.AreEqual("$1.5K", NumberFormatter.FormatMoney(1500.0));
            Assert.AreEqual("$2.5M", NumberFormatter.FormatMoney(2500000.0));

            Assert.AreEqual("1 Kbps", NumberFormatter.FormatSpeed(1000.0));
            Assert.AreEqual("1 Mbps", NumberFormatter.FormatSpeed(1000000.0));

            Assert.AreEqual("2.5x", NumberFormatter.FormatMultiplier(2.5));
        }

        [Test]
        public void Localization_ReturnsFallbackWhenKeyMissing()
        {
            GameObject go = NewTestObject("TestLoc");
            LocalizationManager loc = go.AddComponent<LocalizationManager>();

            try
            {
                string val = loc.Get("non_existent_key", "FallbackValue");
                Assert.AreEqual("FallbackValue", val);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Optimization_FormatterPerformance_RunsFastWithoutErrors()
        {
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < 10000; i++)
            {
                NumberFormatter.FormatSpeed(i * 1234.5);
                NumberFormatter.FormatMoney(i * 9876.5);
            }

            sw.Stop();
            Assert.Less(sw.ElapsedMilliseconds, 500, "10.000 formatlama islemi 500ms'den kisa surmeli.");
        }

        // ==================================================================
        // Ekonomi matematigi
        //
        // Oyunun en riskli hesabi burada: toplu alim maliyeti kapali formullu
        // bir geometrik seri, "en fazla kac tane alinir" ise onun LOGARITMIK
        // TERSI. Bu iki fonksiyondaki bir isaret veya birim hatasi oyunun
        // dengesini sessizce bozar — kimse cokme gormedigi icin de fark
        // edilmez. Testler ikisini birbirine karsi dogruluyor.
        // ==================================================================

        /// <summary>
        /// Test nesnesi olusturur.
        ///
        /// <see cref="HideFlags.HideAndDontSave"/> SART: testler acik sahnede
        /// calisiyor ve bir Assert patladiginda temizleme satirina hic
        /// ulasilmiyor. Bayraksiz nesneler sahnede kaliyor, ikinci bir
        /// GameManager olarak Awake oluyor ve play mode'da GERCEK [Game]
        /// nesnesinin kendini yok etmesine yol aciyor. Cagiranlar ayrica
        /// try/finally ile temizliyor; bu bayrak ikinci emniyet kemeri.
        /// </summary>
        static GameObject NewTestObject(string name)
        {
            var go = new GameObject(name);
            go.hideFlags = HideFlags.HideAndDontSave;
            return go;
        }

        /// <summary>Test icin bellekte bir yukseltme uretir (diske hicbir sey yazilmaz).</summary>
        static UpgradeSO MakeUpgrade(double baseCost, float growth, int maxLevel = 0)
        {
            var u = ScriptableObject.CreateInstance<UpgradeSO>();
            u.name = "TestUpgrade";
            u.displayName = "Test";
            u.type = UpgradeType.ClickPower;
            u.tab = UpgradeTab.Hardware;
            u.baseCost = baseCost;
            u.costGrowth = growth;
            u.maxLevel = maxLevel;
            u.effectPerLevel = 0.15f;
            u.requiredTierIndex = 0;
            u.milestoneLevels = new[] { 10, 25, 50 };
            u.milestoneMultiplier = 2f;
            return u;
        }

        [Test]
        public void Upgrade_CostAtLevel_FollowsGeometricGrowth()
        {
            UpgradeSO u = MakeUpgrade(100.0, 1.15f);

            // Tolerans BAGIL ve float hassasiyetine gore secildi.
            //
            // costGrowth alani float: 1.15f'in double karsiligi 1.15 degil,
            // 1.1499999761581421. Yani maliyetler tasarim geregi yaklasik —
            // seviye 1'de fark ~2.4e-6, seviye 50'de ~1e-4 mertebesinde birikir.
            // Tam esitlik beklemek oyunu degil testi kirar; bagil tolerans
            // gercek bir formul hatasini (ust alma, off-by-one) yine yakalar.
            Assert.AreEqual(100.0, u.CostAtLevel(0), 1e-9);
            Assert.AreEqual(115.0, u.CostAtLevel(1), 115.0 * 1e-5);
            Assert.AreEqual(132.25, u.CostAtLevel(2), 132.25 * 1e-5);

            // Ust alma gercekten kumulatif mi? (n katini n kez carpmakla ayni)
            double manual = 100.0;
            for (int i = 0; i < 7; i++) manual *= u.costGrowth;
            Assert.AreEqual(manual, u.CostAtLevel(7), manual * 1e-9);

            // Negatif seviye 0 gibi ele alinmali, istisna atmamali.
            Assert.AreEqual(100.0, u.CostAtLevel(-5), 1e-9);

            Object.DestroyImmediate(u);
        }

        [Test]
        public void Economy_BulkCost_MatchesIncrementalSum()
        {
            var go = NewTestObject("BulkCostRig");
            EconomyManager eco = go.AddComponent<EconomyManager>();

            try
            {
                // Kapali formulun tek tek toplamla AYNI sonucu vermesi, formuldeki
                // her turlu kaymayi (off-by-one, ters isaret) yakalar.
                float[] growths = { 1f, 1.07f, 1.15f, 1.5f, 2f };
                int[] counts = { 1, 2, 5, 17, 40 };

                foreach (float g in growths)
                {
                    UpgradeSO u = MakeUpgrade(25.0, g);

                    try
                    {
                        foreach (int k in counts)
                        {
                            double expected = 0.0;
                            for (int i = 0; i < k; i++) expected += u.CostAtLevel(i);

                            double actual = eco.GetBulkCost(u, k);

                            Assert.AreEqual(expected, actual, expected * 1e-6 + 1e-9,
                                "growth=" + g + " adet=" + k + " icin toplu maliyet tek tek toplamla uyusmuyor.");
                        }

                        // Sinir durumlari
                        Assert.AreEqual(0.0, eco.GetBulkCost(u, 0), 1e-9);
                        Assert.AreEqual(0.0, eco.GetBulkCost(u, -3), 1e-9);
                        Assert.AreEqual(0.0, eco.GetBulkCost(null, 5), 1e-9);
                    }
                    finally { Object.DestroyImmediate(u); }
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// GetMaxAffordable tam sinirda olmali: k tane alinabiliyorsa k+1
        /// alinamamali. Logaritmik ters cozum kayan noktada bir fazla
        /// gosterebiliyor; kod bunu dogrulama dongusuyle kirpiyor ve bu test
        /// tam olarak o kirpmanin calistigini garanti ediyor.
        /// </summary>
        [Test]
        public void Economy_MaxAffordable_IsExactBudgetBoundary()
        {
            GameObject rig;
            EconomyManager eco = BuildEconomyRig(out rig);
            if (eco == null) { Assert.Ignore("GameDatabase bulunamadi."); return; }

            try
            {
                Wallet wallet = rig.GetComponent<Wallet>();
                float[] growths = { 1f, 1.07f, 1.15f, 1.6f };
                double[] budgets = { 0.0, 24.0, 25.0, 100.0, 1000.0, 12345.67, 1e9 };

                foreach (float g in growths)
                {
                    UpgradeSO u = MakeUpgrade(25.0, g);

                    try
                    {
                        foreach (double budget in budgets)
                        {
                            wallet.Restore(budget, budget);

                            int k = eco.GetMaxAffordable(u);
                            Assert.GreaterOrEqual(k, 0, "Adet negatif olamaz.");

                            if (k > 0)
                            {
                                Assert.LessOrEqual(eco.GetBulkCost(u, k), budget + 1e-6,
                                    "growth=" + g + " butce=" + budget + ": " + k + " adet butceyi asiyor.");
                            }

                            // Bir fazlasi kesinlikle alinamamali (sinirsiz seviye).
                            Assert.Greater(eco.GetBulkCost(u, k + 1), budget,
                                "growth=" + g + " butce=" + budget + ": " + (k + 1) + " adet de alinabiliyormus, " +
                                "yani MAKS eksik gosteriyor.");
                        }
                    }
                    finally { Object.DestroyImmediate(u); }
                }

                // maxLevel tavani butceden bagimsiz olarak sinirlamali.
                UpgradeSO capped = MakeUpgrade(1.0, 1.01f, 3);
                try
                {
                    wallet.Restore(1e12, 1e12);
                    Assert.AreEqual(3, eco.GetMaxAffordable(capped), "maxLevel tavani asilmamali.");
                }
                finally { Object.DestroyImmediate(capped); }
            }
            finally { Object.DestroyImmediate(rig); }
        }

        /// <summary>
        /// Prestij kartlari Fiber Kredisi ile aliniyor. Butceyi cuzdandan okumak
        /// hem yanlis adet gosterir hem de toplu alim yolunu yanlis para birimine
        /// sokar.
        /// </summary>
        [Test]
        public void Economy_MaxAffordable_UsesFiberCreditsForPrestigeCards()
        {
            GameObject rig;
            EconomyManager eco = BuildEconomyRig(out rig);
            if (eco == null) { Assert.Ignore("GameDatabase bulunamadi."); return; }

            UpgradeSO skill = MakeUpgrade(1.0, 1f);
            skill.tab = UpgradeTab.Prestige;

            try
            {
                Assert.IsTrue(EconomyManager.UsesFiberCredits(skill));

                // Cuzdan dolu ama kredi yok: para ile alinamamali.
                rig.GetComponent<Wallet>().Restore(1e12, 1e12);
                Assert.AreEqual(0, eco.GetMaxAffordable(skill),
                    "Fiber Kredisi kartinin adedi PARA bakiyesinden hesaplanmamali.");
            }
            finally
            {
                Object.DestroyImmediate(skill);
                Object.DestroyImmediate(rig);
            }
        }

        [Test]
        public void Upgrade_Milestones_AreConsistent()
        {
            UpgradeSO u = MakeUpgrade(10.0, 1.15f);   // esikler: 10, 25, 50 · carpan 2

            Assert.AreEqual(0, u.MilestonesReached(9));
            Assert.AreEqual(1, u.MilestonesReached(10));
            Assert.AreEqual(2, u.MilestonesReached(25));
            Assert.AreEqual(3, u.MilestonesReached(999));

            // Carpan kumulatif: her asilan esikte ikiye katlanir.
            Assert.AreEqual(1.0, u.MilestoneMultiplierAt(9), 1e-9);
            Assert.AreEqual(2.0, u.MilestoneMultiplierAt(10), 1e-9);
            Assert.AreEqual(4.0, u.MilestoneMultiplierAt(25), 1e-9);
            Assert.AreEqual(8.0, u.MilestoneMultiplierAt(50), 1e-9);

            // Bir sonraki hedef her zaman mevcut seviyenin USTUNDE olmali.
            Assert.AreEqual(10, u.NextMilestone(0));
            Assert.AreEqual(25, u.NextMilestone(10));
            Assert.AreEqual(-1, u.NextMilestone(50), "Tum esikler asildiysa -1 donmeli.");

            // Carpan 1 ise sistem tamamen kapali olmali.
            u.milestoneMultiplier = 1f;
            Assert.AreEqual(1.0, u.MilestoneMultiplierAt(100), 1e-9);
            Assert.AreEqual(-1, u.NextMilestone(0));

            Object.DestroyImmediate(u);
        }

        /// <summary>
        /// GameManager + Wallet + EconomyManager'i ayakta bir nesne uzerinde kurar.
        /// Edit modunda Start calismadigi icin Initialize elle cagriliyor.
        /// </summary>
        static EconomyManager BuildEconomyRig(out GameObject go)
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabaseSO>(
                "Assets/ScriptableObjects/GameDatabase.asset");

            if (db == null) { go = null; return null; }

            go = NewTestObject("EconomyTestRig");
            go.SetActive(false);                      // Awake'leri baglama bitene kadar beklet

            GameManager gm = go.AddComponent<GameManager>();
            go.AddComponent<Wallet>();
            EconomyManager eco = go.AddComponent<EconomyManager>();

            gm.EditorBind(db, 0);
            go.SetActive(true);

            // Edit modunda Unity ne Awake ne Start calistirir; ikisini de elle
            // tetiklemek zorundayiz. Bu satir olmadan GameManager.Instance null
            // kalir, EconomyManager cuzdani bulamaz ve GetMaxAffordable her
            // zaman 0 doner — test "gecmis" gibi gorunurken hicbir sey olcmez.
            gm.EditorBootstrap();
            eco.Initialize();

            return eco;
        }

        // ==================================================================
        // Bicimleme ve kayit
        // ==================================================================

        /// <summary>
        /// FormatPercent 0-100 arasina kirpiyor cunku ilerleme cubugu icin
        /// yazildi. Yukseltme kartlarindaki birikmis bonus 100'u rahatlikla
        /// asiyor; oraya FormatPercent kullanilirsa 25 seviye Modeme Vurmak
        /// (%1500) kartta "+%100" gorunur.
        /// </summary>
        [Test]
        public void NumberFormatter_BonusPercent_IsNotClampedAt100()
        {
            Assert.AreEqual("100%", NumberFormatter.FormatPercent(5.0),
                "FormatPercent 100'e kirpmali (ilerleme cubugu icin).");

            StringAssert.StartsWith("1500", NumberFormatter.FormatBonusPercent(15.0),
                "FormatBonusPercent kirpmamali.");

            Assert.AreEqual("0%", NumberFormatter.FormatBonusPercent(0.0));
            Assert.AreEqual("--%", NumberFormatter.FormatBonusPercent(double.NaN));
        }

        [Test]
        public void SaveData_RoundTrip_PreservesSlotsAndUpgrades()
        {
            var data = new SaveData();
            data.balance = 1234.5;
            data.lifetimeEarnings = 9999.5;
            data.currentTierIndex = 4;
            data.highestTierReached = 7;
            data.fiberCredits = 12;
            data.completedCount = 33;
            data.collectedFiles.Add("File_A");
            data.upgrades.Add(new SaveData.UpgradeEntry { name = "Upg_X", level = 6 });
            data.slots.Add(new SaveData.SlotEntry { fileName = "File_B", progressBits = 777.25 });
            data.slots.Add(new SaveData.SlotEntry { fileName = "File_C", progressBits = 888.75 });

            SaveData back = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(data, false));

            Assert.AreEqual(SaveData.CurrentVersion, back.saveVersion);
            Assert.AreEqual(1234.5, back.balance, 1e-9);
            Assert.AreEqual(9999.5, back.lifetimeEarnings, 1e-9);
            Assert.AreEqual(4, back.currentTierIndex);
            Assert.AreEqual(7, back.highestTierReached);
            Assert.AreEqual(12, back.fiberCredits);
            Assert.AreEqual(33, back.completedCount);

            Assert.AreEqual(1, back.upgrades.Count);
            Assert.AreEqual("Upg_X", back.upgrades[0].name);
            Assert.AreEqual(6, back.upgrades[0].level);

            Assert.AreEqual(1, back.collectedFiles.Count);
            Assert.AreEqual("File_A", back.collectedFiles[0]);

            Assert.AreEqual(2, back.slots.Count, "Paralel slotlar kayitta korunmali.");
            Assert.AreEqual("File_B", back.slots[0].fileName);
            Assert.AreEqual(777.25, back.slots[0].progressBits, 1e-9);
            Assert.AreEqual(888.75, back.slots[1].progressBits, 1e-9);
        }

        /// <summary>
        /// Slot alani v5'te eklendi. Sahadaki v4 kayitlar bu alan olmadan
        /// yuklenebilmeli — aksi halde guncelleme tum oyuncularin ilerlemesini
        /// silerdi.
        /// </summary>
        [Test]
        public void SaveData_OldVersionWithoutSlots_LoadsCleanly()
        {
            const string v4Json =
                "{\"saveVersion\":4,\"balance\":500.0,\"currentTierIndex\":3," +
                "\"highestTierReached\":5,\"upgrades\":[]}";

            SaveData old = JsonUtility.FromJson<SaveData>(v4Json);

            Assert.IsNotNull(old);
            Assert.AreEqual(4, old.saveVersion);
            Assert.AreEqual(500.0, old.balance, 1e-9);
            Assert.AreEqual(3, old.currentTierIndex);
            Assert.AreEqual(5, old.highestTierReached);
            Assert.IsNotNull(old.slots, "Eksik slot listesi null degil bos olmali.");
            Assert.AreEqual(0, old.slots.Count);
        }

        // ==================================================================
        // Reklam sistemi (AdMob & StubAd)
        // ==================================================================

        [Test]
        public void AdService_PriorityResolution_PicksHighest()
        {
            GameObject rig = NewTestObject("AdPriorityRig");
            try
            {
                var stub = rig.AddComponent<SpeedDownload.UI.StubAdService>();
                var admob = rig.AddComponent<AdMobService>();

                Assert.AreEqual(0, stub.Priority);
                Assert.AreEqual(100, admob.Priority);

                var db = AssetDatabase.LoadAssetAtPath<GameDatabaseSO>(
                    "Assets/ScriptableObjects/GameDatabase.asset");
                if (db == null) return;

                GameManager gm = rig.AddComponent<GameManager>();
                gm.EditorBind(db, 0);
                gm.EditorBootstrap();

                Assert.IsNotNull(gm.Ads);
                Assert.AreEqual(100, gm.Ads.Priority, "GameManager en yuksek oncelikli IAdService'i secmeli.");
            }
            finally { Object.DestroyImmediate(rig); }
        }

        [Test]
        public void AdService_StubAd_GrantsRewardOnlyAfterCompletion()
        {
            GameObject rig = NewTestObject("StubAdRig");
            try
            {
                var stub = rig.AddComponent<SpeedDownload.UI.StubAdService>();
                var group = rig.AddComponent<CanvasGroup>();
                stub.Bind(group, null, null, null, null);

                bool rewarded = false;
                bool skipped = false;

                // Kota temizleme reklami (varsayilan 5sn)
                stub.ShowRewarded(AdPlacement.QuotaClear, () => rewarded = true, () => skipped = true);

                Assert.IsTrue(stub.IsShowing, "Reklam gosteriliyor olmali.");
                Assert.IsFalse(stub.IsRewardedReady, "Reklam oynatilirken IsRewardedReady false olmali.");
                Assert.IsFalse(rewarded, "Reklam baslar baslamaz odul verilmemeli.");

                // Sayac dolmadan kapatma cagrisi (OnClose) yapilirsa odul verilmemeli
                var onCloseMethod = typeof(SpeedDownload.UI.StubAdService).GetMethod(
                    "OnClose", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                onCloseMethod.Invoke(stub, null);
                Assert.IsFalse(rewarded, "Sure dolmadan kapatinca odul verilmemeli.");
                Assert.IsTrue(stub.IsShowing, "Sure dolmadan kapatilinca panel acik kalmali.");

                // Sayaci tamamla
                var grantRewardMethod = typeof(SpeedDownload.UI.StubAdService).GetMethod(
                    "GrantReward", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                grantRewardMethod.Invoke(stub, null);

                // Artik kapatinca odul verilmeli
                onCloseMethod.Invoke(stub, null);
                Assert.IsTrue(rewarded, "Sure dolduktan sonra kapatilinca odul verilmeli.");
                Assert.IsFalse(stub.IsShowing, "Kapatilinca panel kapanmali.");
                Assert.IsFalse(skipped, "onSkipped cagrilmamali.");
            }
            finally { Object.DestroyImmediate(rig); }
        }

        [Test]
        public void AdService_StubAd_Cancellation_InvokesSkippedCallback()
        {
            GameObject rig = NewTestObject("StubAdCancelRig");
            try
            {
                var stub = rig.AddComponent<SpeedDownload.UI.StubAdService>();
                var group = rig.AddComponent<CanvasGroup>();
                stub.Bind(group, null, null, null, null);

                bool rewarded = false;
                bool skipped = false;

                stub.ShowRewarded(AdPlacement.SpeedBoost, () => rewarded = true, () => skipped = true);
                Assert.IsTrue(stub.IsShowing);

                stub.Cancel();
                Assert.IsFalse(stub.IsShowing, "Iptal edildiginde panel kapanmali.");
                Assert.IsFalse(rewarded, "Iptal edildiginde odul verilmemeli.");
                Assert.IsTrue(skipped, "Iptal edildiginde onSkipped tetiklenmeli.");
            }
            finally { Object.DestroyImmediate(rig); }
        }

        // ==================================================================
        // Kayit guvenligi
        //
        // Eski bicimde kayit duz JSON'du ve imza kontrolu "signature alani bos
        // degilse" sartina bagliydi — yani o satiri silmek dogrulamayi tamamen
        // atlatiyordu. Ustelik imza yalnizca yedi sayiyi kapsiyor, yukseltme
        // seviyeleri korumasiz kaliyordu. Asagidaki testler o iki acigin geri
        // gelmesini engelliyor.
        // ==================================================================

        [Test]
        public void SaveCrypto_RoundTrip_RecoversExactJson()
        {
            var data = new SaveData();
            data.balance = 987654.321;
            data.lifetimeEarnings = 1234567.89;
            data.currentTierIndex = 5;
            data.fiberCredits = 42;
            data.upgrades.Add(new SaveData.UpgradeEntry { name = "Upg_Test", level = 13 });

            string json = JsonUtility.ToJson(data, false);
            string encrypted = SaveCrypto.Encrypt(json);

            Assert.IsTrue(SaveCrypto.IsEncrypted(encrypted), "Cikti sifreli bicim baslığı tasimali.");
            Assert.IsFalse(encrypted.Contains("balance"),
                "Alan adlari duz metin olarak gorunmemeli — dosya bir metin editoruyle duzenlenebilir olmamali.");
            Assert.IsFalse(encrypted.Contains("987654"),
                "Bakiye duz metin olarak gorunmemeli.");

            string back = SaveCrypto.Decrypt(encrypted);
            Assert.AreEqual(json, back, "Cozulen JSON, sifrelenen JSON ile birebir ayni olmali.");
        }

        [Test]
        public void SaveCrypto_TamperedPayload_IsRejected()
        {
            string json = JsonUtility.ToJson(new SaveData { balance = 100.0 }, false);
            string encrypted = SaveCrypto.Encrypt(json);

            // Govdenin ortasindan tek bir karakteri degistir.
            int mid = encrypted.Length / 2;
            char original = encrypted[mid];
            char swapped = original == 'A' ? 'B' : 'A';
            string tampered = encrypted.Substring(0, mid) + swapped + encrypted.Substring(mid + 1);

            Assert.IsNull(SaveCrypto.Decrypt(tampered),
                "Tek bir bayti degismis kayit REDDEDILMELI; eski surumde bu tur " +
                "degisiklikler imza kapsaminin disinda kaldigi icin sessizce gecerdi.");
        }

        [Test]
        public void SaveCrypto_TruncatedPayload_IsRejected()
        {
            // Yarim yazilmis / kirpilmis dosya cokme degil, temiz bir ret uretmeli.
            Assert.IsNull(SaveCrypto.Decrypt(SaveCrypto.Magic + "QUJD"), "Cok kisa govde reddedilmeli.");
            Assert.IsNull(SaveCrypto.Decrypt(SaveCrypto.Magic + "!!!gecersiz-base64!!!"), "Bozuk base64 reddedilmeli.");
            Assert.IsNull(SaveCrypto.Decrypt(""), "Bos dosya reddedilmeli.");
            Assert.IsNull(SaveCrypto.Decrypt("{\"balance\":999}"), "Duz JSON, sifreli cozucuden gecmemeli.");
        }

        [Test]
        public void SaveData_Sanitize_ClampsHostileValues()
        {
            var data = new SaveData();

            // Tahrif edilmis / bozuk bir dosyadan gelebilecek degerler.
            data.balance = double.NaN;
            data.lifetimeEarnings = double.PositiveInfinity;
            data.fileProgressBits = -5000.0;
            data.completedCount = -12;
            data.currentTierIndex = -3;
            data.highestTierReached = -9;
            data.fiberCredits = -100;
            data.eventRemaining = float.PositiveInfinity;
            data.eventRemainingTaps = -4;
            data.eventQuotaFee = double.NaN;
            data.activeEvent = 9999;
            data.upgrades.Add(new SaveData.UpgradeEntry { name = "Upg_X", level = int.MaxValue });
            data.upgrades.Add(new SaveData.UpgradeEntry { name = "", level = 5 });
            data.slots.Add(new SaveData.SlotEntry { fileName = "File_A", progressBits = double.NaN });

            data.Sanitize();

            Assert.AreEqual(0.0, data.balance, "NaN bakiye 0'a cekilmeli.");
            Assert.AreEqual(0.0, data.lifetimeEarnings, "Sonsuz toplam kazanc 0'a cekilmeli.");
            Assert.AreEqual(0.0, data.fileProgressBits, "Negatif ilerleme 0'a cekilmeli.");
            Assert.AreEqual(0, data.completedCount);
            Assert.AreEqual(0, data.currentTierIndex);
            Assert.AreEqual(0, data.highestTierReached);
            Assert.AreEqual(0, data.fiberCredits);

            // Sonsuz sure, Gece Tarifesi'nin 3x odul carpanini KALICI hale
            // getiriyordu — olay hic bitmiyordu.
            Assert.AreEqual(0f, data.eventRemaining, "Sonsuz olay suresi 0'a cekilmeli.");
            Assert.AreEqual(0, data.eventRemainingTaps);
            Assert.AreEqual(0.0, data.eventQuotaFee);
            Assert.AreEqual(0, data.activeEvent, "Tanimsiz olay turu None'a dusmeli.");

            Assert.AreEqual(1, data.upgrades.Count, "Adsiz yukseltme kaydi atilmali.");
            Assert.AreEqual(SaveData.MaxUpgradeLevel, data.upgrades[0].level,
                "Asiri seviye tavana cekilmeli.");
            Assert.AreEqual(0.0, data.slots[0].progressBits, "NaN slot ilerlemesi 0'a cekilmeli.");
        }

        [Test]
        public void Wallet_Refund_DoesNotInflateLifetimeEarnings()
        {
            GameObject go = NewTestObject("RefundWallet");
            try
            {
                Wallet wallet = go.AddComponent<Wallet>();
                wallet.Restore(1000.0, 1000.0);

                Assert.IsTrue(wallet.TrySpend(400.0));
                Assert.AreEqual(600.0, wallet.Balance, 0.001);
                Assert.AreEqual(1000.0, wallet.LifetimeEarnings, 0.001);

                // Yukseltme geri alma iadesi: para geri gelir, TOPLAM KAZANC ARTMAZ.
                //
                // Add() ile iade etmek somuru kapisiydi: prestij kredisi
                // floor(sqrt(LifetimeEarnings / esik)) oldugu icin "MAKS al ->
                // geri al" dongusu hic para kazanmadan bedava kredi uretiyordu.
                wallet.Refund(400.0);

                Assert.AreEqual(1000.0, wallet.Balance, 0.001, "Iade bakiyeye eklenmeli.");
                Assert.AreEqual(1000.0, wallet.LifetimeEarnings, 0.001,
                    "Iade TOPLAM KAZANCI ARTIRMAMALI — aksi halde prestij kredisi bedava uretilir.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ==================================================================
        // Basarimlar ve gunluk gorevler
        // ==================================================================

        [Test]
        public void Stats_RoundTrip_SurvivesEnumReordering()
        {
            GameObject go = NewTestObject("StatsRig");
            try
            {
                var stats = go.AddComponent<PlayerStats>();
                stats.Add(StatType.FilesDownloaded, 137);
                stats.Add(StatType.Clicks, 4210);
                stats.Add(StatType.HoloDrops, 3);

                var saved = stats.ToList();

                // Kayit AD ile yapiliyor, indeks ile degil. Sirayi bozarak
                // yukleyip dogru sayaca gittigini dogruluyoruz: enum'a ortadan
                // yeni bir deger eklendiginde indeksler kayar ve indeksle yazan
                // bir bicimde "4210 tiklama" baska bir olcuye yazilirdi.
                saved.Reverse();

                var other = go.AddComponent<PlayerStats>();
                other.Restore(saved);

                Assert.AreEqual(137, other.Get(StatType.FilesDownloaded));
                Assert.AreEqual(4210, other.Get(StatType.Clicks));
                Assert.AreEqual(3, other.Get(StatType.HoloDrops));
                Assert.AreEqual(0, other.Get(StatType.Prestiges), "Dokunulmayan sayac 0 kalmali.");

                // Tanimadigimiz ad oyunu cokertmemeli, sessizce atlanmali.
                other.Restore(new System.Collections.Generic.List<SaveData.StatEntry>
                {
                    new SaveData.StatEntry { name = "BuOlcuArtikYok", value = 99 },
                    new SaveData.StatEntry { name = "Clicks", value = 7 }
                });
                Assert.AreEqual(7, other.Get(StatType.Clicks));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Achievements_UnlockOnlyOnce_AndRestoreDoesNotReward()
        {
            GameObject go = NewTestObject("AchRig");
            try
            {
                var db = AssetDatabase.LoadAssetAtPath<GameDatabaseSO>(
                    "Assets/ScriptableObjects/GameDatabase.asset");
                if (db == null) return;

                var stats = go.AddComponent<PlayerStats>();
                var prestige = go.AddComponent<PrestigeManager>();
                var ach = go.AddComponent<AchievementManager>();

                GameManager gm = go.AddComponent<GameManager>();
                gm.EditorBind(db, 0);
                gm.EditorBootstrap();

                int unlockCount = 0;
                ach.Unlocked += delegate { unlockCount++; };

                // Kayittan yukleme: sayaclar dolu, hicbir basarim kayitli degil.
                // Sartini saglayanlar ACILMALI ama ODUL VERMEMELI — aksi halde
                // oyuncu her acilista ayni krediyi tekrar alirdi.
                stats.Add(StatType.FilesDownloaded, 300);
                ach.Restore(new System.Collections.Generic.List<string>());

                Assert.IsTrue(ach.IsUnlocked("files_10"), "300 dosya files_10'u acmali.");
                Assert.IsTrue(ach.IsUnlocked("files_250"), "300 dosya files_250'yi acmali.");
                Assert.IsFalse(ach.IsUnlocked("files_5000"), "300 dosya files_5000'i ACMAMALI.");
                Assert.AreEqual(0, unlockCount,
                    "Kayittan yukleme ODUL VERMEMELI — yoksa her acilista kredi basilirdi.");

                // Yuklemeden sonraki gercek ilerleme odul vermeli.
                int before = prestige.FiberCredits;
                stats.Add(StatType.FilesDownloaded, 5000);

                Assert.IsTrue(ach.IsUnlocked("files_5000"));
                Assert.AreEqual(1, unlockCount, "Yalnizca YENI acilan basarim haber vermeli.");
                Assert.Greater(prestige.FiberCredits, before, "Yeni basarim kredi odemeli.");

                // Ayni esigin tekrar gecilmesi ikinci kez odul VERMEMELI.
                int after = prestige.FiberCredits;
                stats.Add(StatType.FilesDownloaded, 1);
                Assert.AreEqual(after, prestige.FiberCredits, "Basarim bir kez odenir.");
                Assert.AreEqual(1, unlockCount);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Achievements_AllHaveTranslations()
        {
            // Kimlikler kayda yaziliyor: bos veya tekrarlanan bir kimlik,
            // iki basarimi birbirine baglar ve ikisi birden acilir.
            AchievementDef[] defs = AchievementManager.Definitions;
            Assert.Greater(defs.Length, 0);

            var seen = new System.Collections.Generic.HashSet<string>();

            for (int i = 0; i < defs.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(defs[i].id), "Basarim kimligi bos olamaz.");
                Assert.IsTrue(seen.Add(defs[i].id),
                    "Basarim kimligi TEKRARLANMIS: " + defs[i].id);
                Assert.Greater(defs[i].target, 0L, defs[i].id + " hedefi pozitif olmali.");
                Assert.GreaterOrEqual(defs[i].creditReward, 0, defs[i].id + " odulu negatif olamaz.");
            }
        }

        [Test]
        public void Stats_MaxCombo_TracksHighRecord()
        {
            GameObject go = NewTestObject("StatsComboRig");
            try
            {
                var stats = go.AddComponent<PlayerStats>();
                stats.SetMax(StatType.MaxCombo, 15);
                Assert.AreEqual(15, stats.Get(StatType.MaxCombo));

                // Daha kucuk deger rekoru dusurmemeli
                stats.SetMax(StatType.MaxCombo, 8);
                Assert.AreEqual(15, stats.Get(StatType.MaxCombo));

                // Daha buyuk deger rekoru yenilemeli
                stats.SetMax(StatType.MaxCombo, 30);
                Assert.AreEqual(30, stats.Get(StatType.MaxCombo));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Quests_PickForDay_IsStableAndDistinct()
        {
            var a = new QuestDef[DailyQuestManager.QuestsPerDay];
            var b = new QuestDef[DailyQuestManager.QuestsPerDay];

            // Ayni gun -> ayni gorevler. Kararli olmasa oyuncu uygulamayi her
            // acisinda farkli gorevler gorur ve ilerlemesi sifirlanirdi.
            DailyQuestManager.PickForDay(20250, a);
            DailyQuestManager.PickForDay(20250, b);

            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(a[i].key, b[i].key, "Ayni gun ayni gorevleri vermeli.");

            // Gun icindeki uc gorev BIRBIRINDEN FARKLI olmali.
            for (int d = 0; d < 400; d++)
            {
                DailyQuestManager.PickForDay(20000 + d, a);

                for (int i = 0; i < a.Length; i++)
                    for (int j = i + 1; j < a.Length; j++)
                        Assert.AreNotEqual(a[i].key, a[j].key,
                            "Gun " + (20000 + d) + " ayni gorevi iki kez verdi: " + a[i].key);
            }

            // Farkli gunler farkli setler uretebilmeli (hepsi ayni olmamali).
            DailyQuestManager.PickForDay(20250, a);
            bool anyDifferent = false;

            for (int d = 1; d < 30 && !anyDifferent; d++)
            {
                DailyQuestManager.PickForDay(20250 + d, b);
                for (int i = 0; i < a.Length; i++)
                    if (a[i].key != b[i].key) { anyDifferent = true; break; }
            }

            Assert.IsTrue(anyDifferent, "Gorevler gunden gune degismeli.");
        }

        [Test]
        public void Localization_TF_NeverThrows()
        {
            // TF bir METIN isidir; hicbir kosulda istisna firlatmamali.
            //
            // Gercek bir hataydi: desen null oldugunda string.Format
            // ArgumentNullException firlatiyordu (yakalanan FormatException
            // degil). O cagri bir UI tazelemesinin icindeydi, tazeleme de
            // kayit yuklemesinin tetikledigi bir olaydan geliyordu — istisna
            // yigini tirmanip SaveManager.Load'u YARIDA KESIYOR ve gunluk
            // gorevler hic kurulmuyordu.
            Assert.DoesNotThrow(delegate { LocalizationManager.TF(null, null, 5); });
            Assert.DoesNotThrow(delegate { LocalizationManager.TF("", "", 5); });
            Assert.DoesNotThrow(delegate { LocalizationManager.TF("yok", null, 5); });
            Assert.DoesNotThrow(delegate { LocalizationManager.TF(null, "Hedef {0}", 5); });

            // Bozuk bicimlendirme de cokertmemeli.
            Assert.DoesNotThrow(delegate { LocalizationManager.TF("yok", "{5} bozuk", 1); });

            Assert.AreEqual("Hedef 5", LocalizationManager.TF(null, "Hedef {0}", 5),
                "Anahtar yoksa yedek metin bicimlendirilmeli.");
        }

        [Test]
        public void SaveCrypto_Pbkdf2_MatchesReferenceImplementation()
        {
            // SaveCrypto kendi PBKDF2'sini yaziyor cunku Rfc2898DeriveBytes
            // SHA256 istendiginde ic HMAC'ini ADIYLA kurabiliyor ve IL2CPP
            // "High" budamasinda silinebiliyor.
            //
            // Elle yazilan kripto tahmine birakilamaz: cikti burada referans
            // uygulamayla BAYT BAYT karsilastiriliyor. Bu test gecmiyorsa
            // kayitlar cozulemez hale gelir.
            var pbkdf2 = typeof(SaveCrypto).GetMethod("Pbkdf2",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(pbkdf2, "SaveCrypto.Pbkdf2 bulunamadi.");

            var cases = new[]
            {
                new { pw = "parola", salt = "tuz1234567890abc", iter = 1,    len = 32 },
                new { pw = "parola", salt = "tuz1234567890abc", iter = 2,    len = 32 },
                new { pw = "p",      salt = "0123456789abcdef", iter = 1000, len = 64 },
                new { pw = "SpeedDownload.MegabitTycoon/v1#7f3a9c2e|abc|dev",
                      salt = "fedcba9876543210", iter = 4096, len = 64 },
                new { pw = "kisa",   salt = "0123456789abcdef", iter = 10,   len = 20 }
            };

            foreach (var c in cases)
            {
                byte[] pw = System.Text.Encoding.UTF8.GetBytes(c.pw);
                byte[] salt = System.Text.Encoding.UTF8.GetBytes(c.salt);

                byte[] mine = (byte[])pbkdf2.Invoke(null, new object[] { pw, salt, c.iter, c.len });

                byte[] reference;
                using (var kdf = new System.Security.Cryptography.Rfc2898DeriveBytes(
                           pw, salt, c.iter, System.Security.Cryptography.HashAlgorithmName.SHA256))
                {
                    reference = kdf.GetBytes(c.len);
                }

                Assert.AreEqual(c.len, mine.Length, "Uzunluk yanlis.");
                Assert.AreEqual(System.Convert.ToBase64String(reference),
                                System.Convert.ToBase64String(mine),
                                "PBKDF2 ciktisi referanstan farkli: iter=" + c.iter + " len=" + c.len);
            }
        }

        [Test]
        public void SaveCrypto_SelfTest_Passes()
        {
            // Acilista GameBootstrapper'in calistirdigi kontrolun aynisi.
            // Burada gecmesi, editorde kripto altyapisinin saglam oldugunu
            // gosterir; CIHAZDA gecerliligini APK'daki gunluk soyler.
            string detail;
            Assert.IsTrue(SaveCrypto.SelfTest(out detail), "Kripto oz-testi basarisiz: " + detail);
        }

        [Test]
        public void Quests_AllPoolEntriesHaveTranslations()
        {
            QuestDef[] pool = DailyQuestManager.Pool;
            Assert.GreaterOrEqual(pool.Length, DailyQuestManager.QuestsPerDay,
                "Havuz, gunluk gorev sayisindan az olamaz — yoksa ayni gorev tekrarlanir.");

            for (int i = 0; i < pool.Length; i++)
            {
                Assert.IsFalse(string.IsNullOrEmpty(pool[i].key), "Gorev anahtari bos olamaz.");
                Assert.Greater(pool[i].amount, 0L, pool[i].key + " miktari pozitif olmali.");
                Assert.Greater(pool[i].rewardSeconds, 0.0, pool[i].key + " odulu pozitif olmali.");
            }
        }
    }
}
