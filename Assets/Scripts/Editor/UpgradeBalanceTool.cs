using System.Collections.Generic;
using System.Text;
using SpeedDownload.Data;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Faz 17 denge araci: yukseltme egrilerine KARAKTER verir.
    ///
    /// COZULEN SORUN
    /// -------------
    /// Tarama sonucu: seviyelenen 33 kartin costGrowth degeri BIREBIR AYNIYDI
    /// (1.15). Idle oyunlarda anlamli secimi ureten sey tam olarak bu katsayinin
    /// farklilasmasidir.
    ///
    /// Etki additif, maliyet ussel:
    ///     etki(n)    = n * effectPerLevel * kilometreCarpani
    ///     maliyet(n) = baseCost * costGrowth^n
    ///
    /// Yani bir kartin "para basina etki" verimi r^n ile soner. TUM kartlarda r
    /// ayni oldugunda kartlarin birbirine gore sirasi ASLA degismez: oyuncu
    /// effectPerLevel/baseCost oranina gore sabit bir sirayi takip eder. Bu bir
    /// strateji degil, bir bolme islemidir — 41 kart, davranis olarak 41 kez
    /// gosterilen 1 karta doner.
    ///
    /// r farklilastiginda sira ZAMAN ICINDE DEGISIR: yuksek r'li kart erken
    /// guclu olup sonra fiyattan duser, dusuk r'li kart basta zayifken sonra en
    /// iyi alim haline gelir. Aranan doku budur.
    ///
    /// ENFLASYON KORKUSU VE CEVABI
    /// ---------------------------
    /// r'yi topluca dusurmek ekonomiyi sisirir (ayni parayla cok daha fazla
    /// seviye). Bu yuzden kartlar 1.15'in ETRAFINA dagitiliyor, altina degil:
    /// dusuk tavanli yardimci kartlar yukari, uzun soluklu omurga kartlari
    /// asagi. Report toplam kaymayi olcup yaziyor; hedef +-%15 bandi.
    ///
    /// KILOMETRE TASI DUZELTMESI
    /// -------------------------
    /// Ayri bir kusur: 5 kartta esikler {10,25,50,100} sabitken tavan bunun
    /// altindaydi (HatBakimi tavan 20, HariciFan tavan 10...). Kartta ASLA
    /// ulasilamayacak hedefler yaziyordu; HariciFan'da tek ulasilabilir esik
    /// SON seviyeydi — yani odulun tadini cikaracak seviye hic kalmiyordu.
    ///
    /// Bunun sebebi 5. Migrate araci: esikleri sabit {10,25,50,100} verip
    /// tavani 10'un altinda olan karta sistemi tamamen KAPATIYOR. Dogru cozum
    /// sistemi kapatmak degil, esigi karta uydurmakti — burada yapilan bu.
    /// Carpan 2.0 yerine 1.6: iki cekim noktasi olusuyor ama guc butcesi
    /// patlamiyor (tavanda 1.6^2 = 2.56, eskiden tek esikle 2.0 idi).
    ///
    /// Arac idempotent: zaten dogru olan varliga dokunmaz.
    /// </summary>
    public static class UpgradeBalanceTool
    {
        // ------------------------------------------------------------------
        // Arketipler — sayilar kartin ROLUNE gore secildi, gorunusune gore degil
        //
        //   Sprint    1.30  ucuz baslar, 3-4 seviyede fiyattan duser.
        //                   "Simdi al, bitir" hissi veren yardimci kartlar.
        //   Standart  1.20  orta tavanli is gucu kartlari.
        //   Dayanikli 1.14  yuksek tavanli, donup donup alinan kartlar.
        //   Omurga    1.12  sinirsiz kartlar; uzun kuyrugu bunlar tasiyor.
        //   Sabit     1.15  tavani 1-2 olan kartlar; r'nin matematiksel olarak
        //                   hicbir anlami yok, bilerek dokunulmuyor.
        // ------------------------------------------------------------------
        //
        // KALIBRASYON GUNLUGU — sayilar tahmin degil, olcum sonucu
        // --------------------------------------------------------
        // 1. deneme (Omurga 1.10): Report, K=1e6 goreceli butcede toplam gucu
        //    +%148 gosterdi. Sebep bilesik: r'yi 1.15'ten 1.10'a cekmek ayni
        //    gelirle log(1.15)/log(1.10) = 1.47 kat daha fazla SEVIYE demek;
        //    daha fazla seviye daha fazla kilometre esigi asiyor; her esik
        //    ayrica x2. Yani iki carpan ust uste biniyordu.
        //
        // 2. deneme (Omurga 1.12, ModemeVurmak 1.14, PremiumSunucu 1.13):
        //    seviye carpani 1.24'e iniyor ve kuyruk makul banda giriyor. Kart
        //    hala "hic bitmeyen omurga" hissini veriyor ama gec oyunu
        //    duzlestirmiyor.
        //
        // 3. dogrulama (butce suprumu): K=1e6'da hala +%130 gorunuyordu.
        //    Daha cok nokta olculunce bunun ENFLASYON DEGIL FAZ FARKI oldugu
        //    cikti — sinirsiz kartlarin etkisi:
        //        K=1e5  +%20     K=1e6  +%130     K=3e6  +%133
        //        K=1e7  +%21     K=1e8  +%20      K=1e9  +%21
        //    Boşluk BUYUMUYOR, SALINIYOR: yeni egri seviye-100 kilometre
        //    ucurumunu daha erken geciyor, eski egri de gecince fark kapaniyor.
        //    Kalici kayma ~+%20; stratejik cesitliligin karsiligi olarak kabul
        //    edildi.
        //
        // Bir sonraki kim ayar yaparsa: Report'u calistirmadan sabit
        // degistirme. Kart basi MEDYAN kaymaya ve K sutunlarinin TAMAMINA
        // birlikte bak; tek bir K'ya bakmak faz farkini enflasyon sanmana yol
        // acar — yukarida tam olarak bu oldu.
        const float Sprint    = 1.30f;
        const float Standart  = 1.20f;
        const float Dayanikli = 1.14f;
        const float Omurga    = 1.12f;
        const float Sabit     = 1.15f;

        /// <summary>Sinirsiz kartlarda "tam maliyet" yok; raporda bu seviyeye kadar olculur.</summary>
        const int SinirsizOlcumSeviyesi = 30;

        struct Plan
        {
            public float growth;
            public int[] milestones;   // null = kilometre taslarina dokunma
            public float multiplier;   // <= 0 = dokunma
            public string gerekce;
        }

        static Plan P(float g, string why)
        {
            return new Plan { growth = g, milestones = null, multiplier = -1f, gerekce = why };
        }

        static Plan P(float g, int[] ms, float mul, string why)
        {
            return new Plan { growth = g, milestones = ms, multiplier = mul, gerekce = why };
        }

        static Dictionary<string, Plan> BuildPlan()
        {
            var p = new Dictionary<string, Plan>();

            // --- Sprint: dusuk tavan, erken fayda, hizla pahalanir ---
            p["Upg_IsiMacunu"]         = P(Sprint, "tavan 6, overheat cezasini kisaltir — erken al, bitir");
            p["Upg_DnsAyari"]          = P(Sprint, "tavan 5, kritik sansi");
            p["Upg_UPS"]               = P(Sprint, "tavan 3, overheat yumusak dusus");
            p["Upg_YonluAnten"]        = P(Sprint, "tavan 4, redline tepe carpani");
            p["Upg_ReklamEngelleyici"] = P(Sprint, "tavan 4, olay direnci");
            p["Upg_OverclockAraci"]    = P(Sprint, "tavan 5, redline esigi");

            // --- Standart: orta tavan is gucu kartlari ---
            p["Upg_Cat6Kablo"]     = P(Standart, "tavan 8, pasif hiz");
            p["Upg_Sikistirma"]    = P(Standart, "tavan 8, dosya boyutu");
            p["Upg_MekanikKlavye"] = P(Standart, "tavan 8, oto-tiklama");
            p["Upg_MakroScript"]   = P(Standart, "tavan 6, oto-tiklama (gec oyun)");
            // IXP ve Cat6Kablo AYNI tip (PassiveSpeed) ve AYNI tier'da (3).
            // Ikisine de Standart verilince o bandda duzluk geri geliyordu —
            // acgozlu alim simulasyonu bunu yakaladi. IXP'nin tavani daha dusuk
            // (6) ve etkisi daha buyuk (0.35): karakteri Sprint'e yakin.
            p["Upg_IXP"]           = P(1.26f, "tavan 6, pasif hiz — Cat6Kablo ile ayni tip/tier, ayrilmali");
            p["Upg_SiviSogutma"]   = P(Standart, "tavan 5, overheat toleransi");

            // --- Dayanikli: yuksek tavan + kilometre taslari tavanin ICINE olceklendi ---
            p["Upg_HatBakimi"] = P(Dayanikli, new[] { 7, 14 }, 1.6f,
                "tavan 20 ama esikler 10/25/50/100'du — ucu ULASILAMAZ hedefti");
            // ModemeVurmak ile AYNI tip (ClickPower). Ikisi de Dayanikli olunca
            // ClickPower bandi duzdu. Ayrim: SinyalYukselteci tavanli bir
            // PATLAMA karti, ModemeVurmak ise hic bitmeyen OMURGA. Dik olan
            // erken guclu, yayvan olan gec oyunda one geciyor.
            p["Upg_SinyalYukselteci"] = P(1.18f, new[] { 5, 10 }, 1.6f,
                "tavan 15, esiklerin ucu tavanin ustundeydi; ModemeVurmak'tan ayrildi");
            p["Upg_HariciFan"] = P(Dayanikli, new[] { 4, 8 }, 1.6f,
                "tavan 10: tek ulasilabilir esik SON seviyeydi, odulun tadi cikmiyordu");
            p["Upg_NvmeSSD"]          = P(Dayanikli, new[] { 4, 8 }, 1.6f, "tavan 10, ayni kusur");
            p["Upg_TorrentIstemcisi"] = P(Dayanikli, new[] { 4, 8 }, 1.6f, "tavan 10, ayni kusur");

            // --- Omurga: sinirsiz kartlar, uzun kuyrugu bunlar tasiyor ---
            // Kilometre taslari zaten {10,25,50,100} ve HEPSI ulasilabilir —
            // onlara dokunulmuyor; tek degisen egrinin yayvanlasmasi.
            p["Upg_ModemeVurmak"]   = P(1.14f, "sinirsiz, tier 0 tiklama omurgasi");
            p["Upg_PremiumSunucu"]  = P(1.13f, "sinirsiz, odul carpani omurgasi");
            p["Upg_YzOptimizer"]    = P(Omurga, "sinirsiz, gec oyun odul carpani");
            p["Upg_OmurgaPeering"]  = P(Omurga, "sinirsiz, gec oyun pasif hiz");
            p["Upg_KuantumIslemci"] = P(Omurga, "sinirsiz, endgame tiklama");

            // --- Prestij yetenekleri (Fiber Kredisi — ayri para birimi) ---
            // Burada da ayni duzluk vardi. Tek seferlik buyuk etkiler dik,
            // kademeli/bilesik olanlar yayvan.
            p["Skill_HizliBaslangic"] = P(1.25f, "tavan 3, buyuk tek etki");
            p["Skill_SogukBaslangic"] = P(1.25f, "tavan 3, buyuk tek etki");
            p["Skill_KritikUstasi"]   = P(1.20f, "tavan 4, kademeli");
            p["Skill_OfflineUstasi"]  = P(1.20f, "tavan 4, kademeli");
            p["Skill_KaliciGuc"]      = P(1.12f, "tavan 5, her prestijde donup alinan");
            p["Skill_KrediFaizi"]     = P(1.12f, "tavan 5, bilesik etki — yayvan olmali");

            // --- Sabit: tavani 1-2, r matematiksel olarak etkisiz ---
            p["Upg_IkinciHat"]       = P(Sabit, "tavan 2 — r etkisiz");
            p["Upg_RamYukseltmesi"]  = P(Sabit, "tavan 1 — r etkisiz");
            p["Upg_DownloadManager"] = P(Sabit, "tavan 2 — r etkisiz");
            p["Upg_OtomatikKuyruk"]  = P(Sabit, "tavan 2 — r etkisiz");
            p["Upg_VpnTuneli"]       = P(Sabit, "tavan 1 — r etkisiz");

            return p;
        }

        static double CostToLevel(double baseCost, double r, int levels)
        {
            if (levels <= 0) return 0.0;
            if (System.Math.Abs(r - 1.0) < 1e-9) return baseCost * levels;
            return baseCost * (System.Math.Pow(r, levels) - 1.0) / (r - 1.0);
        }

        /// <summary>
        /// Verilen butceyle bu kartta ulasilabilen seviyenin URETTIGI ETKI.
        ///
        /// Enflasyonu olcmenin dogru yolu bu: maliyetin kendisi degil, o
        /// maliyetin satin aldigi guc. Etki formulu EconomyManager.TotalEffect
        /// ile ayni — seviye * etkiPerSeviye * kilometreCarpani — yoksa rapor
        /// oyunun gercekten hissettigi seyi olcmemis olur.
        /// </summary>
        static double EtkiIcinButce(UpgradeSO u, double r, int[] milestones, float mul, double butce)
        {
            int tavan = u.maxLevel > 0 ? u.maxLevel : 500;

            int seviye = 0;
            while (seviye < tavan && CostToLevel(u.baseCost, r, seviye + 1) <= butce)
                seviye++;

            if (seviye <= 0) return 0.0;

            double carpan = 1.0;
            if (mul > 1f && milestones != null)
            {
                int asilan = 0;
                for (int i = 0; i < milestones.Length; i++)
                    if (seviye >= milestones[i]) asilan++;
                if (asilan > 0) carpan = System.Math.Pow(mul, asilan);
            }

            return seviye * (double)u.effectPerLevel * carpan;
        }

        static List<UpgradeSO> LoadAll()
        {
            var list = new List<UpgradeSO>();
            foreach (string guid in AssetDatabase.FindAssets("t:UpgradeSO"))
            {
                var u = AssetDatabase.LoadAssetAtPath<UpgradeSO>(AssetDatabase.GUIDToAssetPath(guid));
                if (u != null) list.Add(u);
            }
            list.Sort((a, b) => a.name.CompareTo(b.name));
            return list;
        }

        // ==================================================================
        // 1) RAPOR — hicbir sey yazmaz, yalnizca olcer
        // ==================================================================

        [MenuItem("Tools/SpeedDownload/5b. Report Upgrade Curves (dry run)", false, 52)]
        public static void Report()
        {
            var plan = BuildPlan();
            var log = new StringBuilder();
            // Rapor DISKTEKI degeri PLANLANAN degerle karsilastirir; tarihsel
            // bir "once/sonra" degil, bir SAPMA olcumudur. 5c uygulandiktan
            // sonra tum satirlarin +%0 olmasi BEKLENEN sonuctur — yakinsamanin
            // kanitidir, "degisiklik olmadi" degil. Sifirdan farkli bir satir
            // gorursen ya sabit degismistir ya da biri varligi elle kurcalamistir.
            log.AppendLine("[UpgradeBalance] SAPMA RAPORU — hicbir varlik degistirilmedi.");
            log.AppendLine("(diskteki deger vs planlanan deger; 5c sonrasi hepsinin +%0 olmasi normaldir)\n");
            log.AppendLine("kart".PadRight(26) + "tavan".PadLeft(6) + "eski r".PadLeft(8) +
                           "yeni r".PadLeft(8) + "eski maliyet".PadLeft(18) +
                           "yeni maliyet".PadLeft(18) + "   kayma");
            log.AppendLine(new string('-', 100));

            double eskiTop = 0, yeniTop = 0;
            int eslesen = 0;
            var eksik = new List<string>();
            var rSet = new HashSet<float>();
            var kaymalar = new List<double>();

            foreach (var u in LoadAll())
            {
                if (u.type == UpgradeType.Connection) continue;

                Plan pl;
                if (!plan.TryGetValue(u.name, out pl)) { eksik.Add(u.name); continue; }
                eslesen++;
                rSet.Add(pl.growth);

                int lv = u.maxLevel > 0 ? u.maxLevel : SinirsizOlcumSeviyesi;
                double eski = CostToLevel(u.baseCost, u.costGrowth, lv);
                double yeni = CostToLevel(u.baseCost, pl.growth, lv);
                eskiTop += eski;
                yeniTop += yeni;

                double kayma = eski > 0 ? (yeni / eski - 1.0) * 100.0 : 0.0;
                kaymalar.Add(kayma);
                log.AppendLine(u.name.PadRight(26) +
                               (u.maxLevel > 0 ? u.maxLevel.ToString() : "~" + lv).PadLeft(6) +
                               u.costGrowth.ToString("0.00").PadLeft(8) +
                               pl.growth.ToString("0.00").PadLeft(8) +
                               eski.ToString("N0").PadLeft(18) +
                               yeni.ToString("N0").PadLeft(18) +
                               "   " + (kayma >= 0 ? "+" : "") + kayma.ToString("0") + "%");
            }

            log.AppendLine(new string('-', 100));
            log.AppendLine("planda eslesen : " + eslesen);
            log.AppendLine("planda EKSIK   : " + eksik.Count + (eksik.Count > 0 ? "  -> " + string.Join(", ", eksik.ToArray()) : ""));
            log.AppendLine("benzersiz r    : " + rSet.Count + " (eskiden 1)");

            // ----------------------------------------------------------------
            // NEDEN MUTLAK TOPLAM KULLANILMIYOR
            //
            // Ilk surum kartlarin mutlak maliyetlerini topluyordu ve "-%62"
            // veriyordu. Bu olcum YANLISTI: baseCost degerleri 4 ile 30 milyar
            // arasinda, yani 10 buyukluk mertebesine yayiliyor. Toplam, en
            // pahali kartin (KuantumIslemci, 13 trilyon) golgesinden ibaret
            // kalıyor; geri kalan 32 kart gurultu seviyesinde.
            //
            // Dogru olcum olcekten BAGIMSIZ olmali. Iki tane kullaniyoruz:
            //   1. Kart basi kaymanin MEDYANI — tipik kart ne kadar etkilendi.
            //   2. Sabit GORECELI butceyle elde edilen GUC — oyuncu kartin
            //      giris fiyatinin K katini harcayinca kac seviye ve ne kadar
            //      etki aliyor. Enflasyonun gercek olcusu budur; maliyet degil,
            //      o maliyetin satin aldigi guc.
            // ----------------------------------------------------------------
            kaymalar.Sort();
            double medyan = kaymalar.Count == 0 ? 0.0
                : (kaymalar.Count % 2 == 1
                    ? kaymalar[kaymalar.Count / 2]
                    : (kaymalar[kaymalar.Count / 2 - 1] + kaymalar[kaymalar.Count / 2]) / 2.0);

            log.AppendLine("kart basi kayma MEDYANI : " + medyan.ToString("+0.0;-0.0") + "%   [hedef: +-15%]");
            log.AppendLine();
            log.AppendLine("GUC KARSILASTIRMASI — butce = kartin giris fiyati x K");
            log.AppendLine("K".PadLeft(8) + "eski toplam etki".PadLeft(20) +
                           "yeni toplam etki".PadLeft(20) + "   kayma");

            // Cok sayida nokta bilerek: tek bir K yaniltir. Sinirsiz kartlarda
            // kilometre esikleri (10/25/50/100) birer UCURUM yaratir; iki egri
            // ayni ucurumu FARKLI butcede gecer. Tek noktada olcersen bunu
            // "enflasyon" sanirsin, oysa faz farkidir — bir sonraki noktada
            // eski egri de ucurumu gecince fark kapanir. Salinim varsa faz,
            // buyuyorsa gercek enflasyon.
            int[] butceler = { 100, 1000, 10000, 100000, 1000000, 10000000, 100000000 };
            foreach (int K in butceler)
            {
                double eskiGuc = 0, yeniGuc = 0;
                foreach (var u in LoadAll())
                {
                    if (u.type == UpgradeType.Connection) continue;
                    Plan pl2;
                    if (!plan.TryGetValue(u.name, out pl2)) continue;

                    double butce = u.baseCost * K;
                    eskiGuc += EtkiIcinButce(u, u.costGrowth, u.milestoneLevels, u.milestoneMultiplier, butce);

                    int[] ms = pl2.milestones != null ? pl2.milestones : u.milestoneLevels;
                    float mm = pl2.multiplier > 0f ? pl2.multiplier : u.milestoneMultiplier;
                    yeniGuc += EtkiIcinButce(u, pl2.growth, ms, mm, butce);
                }
                log.AppendLine(K.ToString("N0").PadLeft(8) + eskiGuc.ToString("N1").PadLeft(20) +
                               yeniGuc.ToString("N1").PadLeft(20) + "   " +
                               ((yeniGuc / eskiGuc - 1.0) * 100.0).ToString("+0.0;-0.0") + "%");
            }

            Debug.Log(log.ToString());
        }

        // ==================================================================
        // 2) UYGULA
        // ==================================================================

        [MenuItem("Tools/SpeedDownload/5c. Apply Upgrade Curve Archetypes", false, 53)]
        public static void Apply()
        {
            var plan = BuildPlan();
            var log = new StringBuilder();
            int changed = 0, skipped = 0;
            var eksik = new List<string>();

            foreach (var u in LoadAll())
            {
                if (u.type == UpgradeType.Connection) continue;

                Plan pl;
                if (!plan.TryGetValue(u.name, out pl)) { eksik.Add(u.name); continue; }

                bool dirty = false;

                if (!Mathf.Approximately(u.costGrowth, pl.growth))
                {
                    log.AppendLine("  " + u.name.PadRight(24) + " r " +
                                   u.costGrowth.ToString("0.00") + " -> " + pl.growth.ToString("0.00") +
                                   "   (" + pl.gerekce + ")");
                    u.costGrowth = pl.growth;
                    dirty = true;
                }

                if (pl.milestones != null && pl.multiplier > 0f)
                {
                    if (!SameLevels(u.milestoneLevels, pl.milestones) ||
                        !Mathf.Approximately(u.milestoneMultiplier, pl.multiplier))
                    {
                        log.AppendLine("  " + u.name.PadRight(24) + " esik [" +
                                       Join(u.milestoneLevels) + "] x" + u.milestoneMultiplier.ToString("0.0") +
                                       " -> [" + Join(pl.milestones) + "] x" + pl.multiplier.ToString("0.0"));
                        u.milestoneLevels = (int[])pl.milestones.Clone();
                        u.milestoneMultiplier = pl.multiplier;
                        dirty = true;
                    }
                }

                if (dirty) { EditorUtility.SetDirty(u); changed++; }
                else skipped++;
            }

            AssetDatabase.SaveAssets();

            Debug.Log("[UpgradeBalance] Egri arketipleri uygulandi.\n" +
                      "  degisen: " + changed + ", zaten dogru: " + skipped +
                      (eksik.Count > 0 ? ", PLANDA EKSIK: " + string.Join(", ", eksik.ToArray()) : "") +
                      "\n" + log);
        }

        // ==================================================================
        // 3) DOGRULA
        // ==================================================================

        [MenuItem("Tools/SpeedDownload/Verify Upgrade Curves", false, 54)]
        public static void Verify()
        {
            var plan = BuildPlan();
            var issues = new List<string>();
            var rSet = new HashSet<float>();
            int leveled = 0;

            foreach (var u in LoadAll())
            {
                if (u.type == UpgradeType.Connection) continue;

                Plan pl;
                if (!plan.TryGetValue(u.name, out pl))
                {
                    issues.Add(u.name + ": planda YOK — yeni kart eklendiyse BuildPlan'a da ekle");
                    continue;
                }

                leveled++;
                if (u.maxLevel != 1 && u.maxLevel != 2) rSet.Add(u.costGrowth);

                if (!Mathf.Approximately(u.costGrowth, pl.growth))
                    issues.Add(u.name + ": r " + u.costGrowth.ToString("0.00") +
                               ", plan " + pl.growth.ToString("0.00") + " — 5c'yi calistir");

                // Ulasilamaz kilometre tasi: kartin gosterdigi ama asla
                // varilamayacak hedef. Ilk teshisin cikis noktasi buydu.
                if (u.maxLevel > 0 && u.milestoneMultiplier > 1f && u.milestoneLevels != null)
                {
                    foreach (int m in u.milestoneLevels)
                        if (m > u.maxLevel)
                            issues.Add(u.name + ": ULASILAMAZ kilometre tasi " + m +
                                       " (tavan " + u.maxLevel + ")");
                }
            }

            if (issues.Count == 0)
                Debug.Log("[UpgradeBalance] Egriler dogru.\n" +
                          "  seviyelenen kart : " + leveled + "\n" +
                          "  benzersiz r      : " + rSet.Count + " (duzlukten cikildi)\n" +
                          "  ulasilamaz esik  : yok");
            else
                Debug.LogWarning("[UpgradeBalance] " + issues.Count + " sorun:\n  " +
                                 string.Join("\n  ", issues.ToArray()));
        }

        // ------------------------------------------------------------------

        static bool SameLevels(int[] a, int[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        static string Join(int[] a)
        {
            if (a == null || a.Length == 0) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(a[i]);
            }
            return sb.ToString();
        }
    }
}
