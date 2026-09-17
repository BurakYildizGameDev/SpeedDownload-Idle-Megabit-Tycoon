using System.Collections.Generic;
using System.Text;
using SpeedDownload.Data;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// GDD Bolum 3/5/6 tablolarindan tum ScriptableObject varliklarini uretir ve
    /// sprite referanslarini AssetDatabase uzerinden baglar.
    ///
    /// Varsayilan davranis: var olan asset'in UZERINE YAZMAZ — Inspector'dan
    /// yapilan denge ayarlari korunur. Sifirdan uretmek icin "Regenerate (OVERWRITE)".
    /// </summary>
    public static class DataAssetGenerator
    {
        const string RootFolder = "Assets/ScriptableObjects";
        const string TiersFolder = RootFolder + "/Tiers";
        const string FilesFolder = RootFolder + "/Files";
        const string UpgradesFolder = RootFolder + "/Upgrades";
        const string SpritesRoot = "Assets/Sprites/";

        /// <summary>
        /// Baglanti kademesi maliyeti = onceki kademenin ortalama dosya odulu x bu.
        ///
        /// 150 yazan eski deger uretilen asset'lerle uyusmuyordu: sahadaki
        /// asset'ler her baglantiyi onceki kademenin ~5 dakikalik gelirine
        /// ayarlamis (elle duzeltilmis), formul ise ~40 dakika veriyordu. Yani
        /// "Regenerate" calistirilinca oyun sessizce sekiz kat yavaslamis
        /// oluyordu. Katsayi olculen gercek degere cekildi.
        /// </summary>
        const double ConnectionCostFactor = 24.0;

        // ------------------------------------------------------------ tablolar

        struct TierDef
        {
            public int index;
            public string name;
            public string flavor;
            public double min, max;
            public float inertia, decay;
            public string dial, connIcon, room;
            public Color roomTint, needleColor;
        }

        static readonly Color DarkRed = new Color(1.00f, 0.22f, 0.22f);
        static readonly Color Red = new Color(1.00f, 0.30f, 0.15f);
        static readonly Color Orange = new Color(1.00f, 0.55f, 0.10f);
        static readonly Color NeonOrange = new Color(1.00f, 0.65f, 0.10f);
        static readonly Color NeonMagenta = new Color(1.00f, 0.25f, 0.85f);
        static readonly Color NeonYellow = new Color(1.00f, 0.95f, 0.15f);
        static readonly Color CoolBlue = new Color(0.80f, 0.88f, 1.00f);
        static readonly Color CoolCyan = new Color(0.85f, 1.00f, 1.00f);

        static readonly TierDef[] Tiers =
        {
            new TierDef { index = 0, name = "Sinyal Kulubesi", flavor = "Telsiz ve mors sinyali. Her bit degerli.",
                min = 1, max = 1e2, inertia = 0.05f, decay = 1.8f,
                dial = "Dial/dial_tier0_signal_bg", connIcon = null, room = "Backgrounds/bg_tier0_signal_room",
                roomTint = Color.white, needleColor = DarkRed },

            new TierDef { index = 1, name = "Telefon Hatti", flavor = "Ilk cevirmeli baglanti. Hat mesgul olmasin.",
                min = 1e2, max = 1e3, inertia = 0.07f, decay = 1.75f,
                dial = "Dial/dial_tier1_phoneline_bg", connIcon = "UpgradeIcons/icon_conn_phoneline",
                room = "Backgrounds/bg_tier1_2_retro_room", roomTint = Color.white, needleColor = DarkRed },

            new TierDef { index = 2, name = "Dial-up 56K", flavor = "O meshur bip-vzzzt sesi.",
                min = 1e3, max = 1e5, inertia = 0.09f, decay = 1.7f,
                dial = "Dial/dial_tier2_dialup_bg", connIcon = "UpgradeIcons/icon_conn_dialup",
                room = "Backgrounds/bg_tier1_2_retro_room", roomTint = Color.white, needleColor = Red },

            new TierDef { index = 3, name = "Gelismis Dial-up / ISDN", flavor = "Cift hat, cift hiz.",
                min = 1e5, max = 1e6, inertia = 0.11f, decay = 1.6f,
                dial = "Dial/dial_tier3_isdn_bg", connIcon = "UpgradeIcons/icon_conn_isdn",
                room = "Backgrounds/bg_tier1_2_retro_room", roomTint = CoolBlue, needleColor = Red },

            new TierDef { index = 4, name = "ADSL Cagi", flavor = "Ev interneti patlamasi.",
                min = 1e6, max = 1e8, inertia = 0.13f, decay = 1.55f,
                dial = "Dial/dial_tier4_adsl_bg", connIcon = "UpgradeIcons/icon_conn_adsl",
                room = "Backgrounds/bg_tier4_modern_room", roomTint = Color.white, needleColor = Orange },

            new TierDef { index = 5, name = "VDSL / Erken Fiber", flavor = "Sehir altyapisi yenileniyor.",
                min = 1e8, max = 1e9, inertia = 0.15f, decay = 1.45f,
                dial = "Dial/dial_tier5_vdsl_bg", connIcon = "UpgradeIcons/icon_conn_vdsl",
                room = "Backgrounds/bg_tier4_modern_room", roomTint = CoolCyan, needleColor = Orange },

            new TierDef { index = 6, name = "Tam Fiber", flavor = "Optik kablo cagi.",
                min = 1e9, max = 1e11, inertia = 0.17f, decay = 1.35f,
                dial = "Dial/dial_tier6_fiber_bg", connIcon = "UpgradeIcons/icon_conn_fullfiber",
                room = "Backgrounds/bg_tier6_hightech_room", roomTint = Color.white, needleColor = NeonOrange },

            new TierDef { index = 7, name = "Kuantum Hat", flavor = "Deneysel altyapi. Paketler ayni anda her yerde.",
                min = 1e11, max = 1e12, inertia = 0.19f, decay = 1.28f,
                dial = "Dial/dial_tier7_quantum_bg", connIcon = "UpgradeIcons/icon_conn_quantum",
                room = "Backgrounds/bg_tier7_quantum_lab", roomTint = Color.white, needleColor = NeonMagenta },

            new TierDef { index = 8, name = "Veri Merkezi Ag Gecidi", flavor = "Uctan uca kurumsal hat.",
                min = 1e12, max = 1e14, inertia = 0.22f, decay = 1.2f,
                dial = "Dial/dial_tier8_datacenter_bg", connIcon = "UpgradeIcons/icon_conn_datacenter",
                room = "Backgrounds/bg_tier8_server_room", roomTint = Color.white, needleColor = NeonYellow }
        };

        /// <summary>
        /// Kademe basina temel dosya odulu.
        ///
        /// Dosya boyutlari kademe referans hizinda ~12 saniyede bitecek sekilde
        /// turetildigi icin bir kademenin saniyelik geliri dogrudan bu sayiyla
        /// orantili. Yani ARDISIK IKI SAYININ ORANI, o kademeyi satin almanin
        /// oyuncuya ne kazandirdigidir.
        ///
        /// Sahadaki asset'lerde olculen eski egri son derece tutarsizdi:
        ///
        ///     T0->T1  x4.0     T4->T5  x42.9
        ///     T1->T2  x15.0    T5->T6  x50.0
        ///     T2->T3  x3.7 <-  T6->T7  x30.0
        ///     T3->T4  x6.4     T7->T8  x13.3
        ///
        /// Her baglantinin fiyati ise sabit: onceki kademenin ~5 dakikalik
        /// geliri. Yani oyuncu ISDN icin bes dakika biriktirip karsiliginda
        /// gelirinin yalnizca ucte bir fazlasini aliyor, ama ayni bes dakikayi
        /// VDSL icin verdiginde gelirini kirk kat artiriyordu. Ayni fiyat,
        /// bambaska sonuclar — "ekonomi yanlis hissettiriyor"un kaynagi buydu.
        ///
        /// Yeni tablo duzgun ve hafifce hizlanan bir egri:
        /// x9.2, x10.2, x11.5, x13.0, x14.3, x16.0, x18.1, x20.7.
        /// Ilk ve son degerler KORUNDU; her yukseltmenin "kac saniyelik gelire
        /// mal oldugu" da korunacak sekilde maliyetler birlikte olceklendi.
        /// </summary>
        static readonly double[] TierRewardBase =
        {
            0.5,      // 0  Sinyal Kulubesi
            4.6,      // 1  x9.2   (sahada 2)
            47.0,     // 2  x10.2  (sahada 30)
            540.0,    // 3  x11.5  (sahada 110 — "olu kademe")
            7.0e3,    // 4  x13.0  (sahada 700)
            1.0e5,    // 5  x14.3  (sahada 30.000)
            1.6e6,    // 6  x16.0  (sahada 1.5e6)
            2.9e7,    // 7  x18.1  (sahada 4.5e7)
            6.0e8     // 8  x20.7
        };

        struct FileDef
        {
            public string assetName;
            public string display;
            public string icon;
            public int tier;
            public double sizeMult;
            public float weight;
        }

        static readonly FileDef[] Files =
        {
            // KADEME 0 BOYUTLARI OZEL OLARAK KUCUK — SEBEBI OLCULDU
            //
            // SizeBitsFor dosyalari kademenin REFERANS hizinda (~%60 kadran)
            // 12 saniyede bitecek sekilde boyutluyor. Ama oyuncu oyuna kadranin
            // DIBINDE basliyor: kademe 0'da taban hiz 1 bps, referans hiz 15.85
            // bps — yani tiklamadan beklerken her sey 15.85 KAT yavas.
            //
            // Olcum: eski haliyle bos_belge.txt 190 bit = 1 bps'te 190 SANIYE.
            // Yani oyuncu uc dakika boyunca tek kurus gormuyordu; bakiye
            // kipirdamiyor, gelir gostergesi "$0/sn" yaziyordu. "Pasif para
            // gelmiyor" sikayetinin kaynagi tam olarak buydu.
            //
            // Yeni carpanlar ilk odemeyi bosta ~16 sn ve ~40 sn'ye cekiyor.
            // EKONOMI DEGISMIYOR: baseReward de ayni sizeMult ile olcekleniyor
            // (bkz. reward = TierRewardBase[tier] * sizeMult), dolayisiyla
            // $/bit orani birebir ayni kaliyor — yalnizca odeme SIKLIGI artiyor.
            // Kademe 1 ve sonrasinda bu sorun yok: oradaki taban/referans farki
            // 15.85 degil ~4 kat (bir dekatlik aralik), zaten 24 sn'de bitiyor.
            new FileDef { assetName = "File_PingTest",      display = "ping_test.txt",             icon = "txt",   tier = 0, sizeMult = 0.084, weight = 1f },
            new FileDef { assetName = "File_BosBelge",      display = "bos_belge.txt",             icon = "txt",   tier = 0, sizeMult = 0.21, weight = 1f },

            new FileDef { assetName = "File_HelloWorld",    display = "hello_world.txt",           icon = "txt",   tier = 1, sizeMult = 0.5,  weight = 1f },
            new FileDef { assetName = "File_OdevV2",        display = "odev_v2.docx",              icon = "docx",  tier = 1, sizeMult = 1.0,  weight = 1f },
            new FileDef { assetName = "File_SiirKoleksiyon",display = "siir_koleksiyonu.txt",      icon = "txt",   tier = 1, sizeMult = 2.0,  weight = 0.7f },

            new FileDef { assetName = "File_Avatar",        display = "avatar.jpeg",               icon = "jpeg",  tier = 2, sizeMult = 0.5,  weight = 1f },
            new FileDef { assetName = "File_WallpaperHD",   display = "wallpaper_hd.png",          icon = "png",   tier = 2, sizeMult = 1.2,  weight = 1f },
            new FileDef { assetName = "File_TatilFoto",     display = "tatil_fotograflari.jpeg",   icon = "jpeg",  tier = 2, sizeMult = 2.5,  weight = 0.7f },

            new FileDef { assetName = "File_EkranGoruntusu",display = "ekran_goruntusu.png",       icon = "png",   tier = 3, sizeMult = 0.7,  weight = 1f },
            new FileDef { assetName = "File_BannerTasarim", display = "banner_tasarim.jpeg",       icon = "jpeg",  tier = 3, sizeMult = 1.6,  weight = 1f },

            new FileDef { assetName = "File_FavoriteSong",  display = "favorite_song.mp3",         icon = "mp3",   tier = 4, sizeMult = 0.6,  weight = 1f },
            new FileDef { assetName = "File_PodcastEp1",    display = "podcast_ep1.wav",           icon = "wav",   tier = 4, sizeMult = 1.3,  weight = 1f },
            new FileDef { assetName = "File_AlbumFull",     display = "album_full.mp3",            icon = "mp3",   tier = 4, sizeMult = 2.6,  weight = 0.7f },

            new FileDef { assetName = "File_FunnyCat",      display = "funny_cat.mp4",             icon = "mp4",   tier = 5, sizeMult = 0.5,  weight = 1f },
            new FileDef { assetName = "File_GameSetup",     display = "game_setup.exe",            icon = "exe",   tier = 5, sizeMult = 1.4,  weight = 1f },
            new FileDef { assetName = "File_DriverPack",    display = "driver_pack.exe",           icon = "exe",   tier = 5, sizeMult = 2.8,  weight = 0.7f },

            new FileDef { assetName = "File_Movie4K",       display = "movie_4k.mkv",              icon = "mkv",   tier = 6, sizeMult = 0.6,  weight = 1f },
            new FileDef { assetName = "File_OsIso",         display = "os.iso",                    icon = "iso",   tier = 6, sizeMult = 1.5,  weight = 1f },
            new FileDef { assetName = "File_SeriesBoxset",  display = "series_boxset.mkv",         icon = "mkv",   tier = 6, sizeMult = 3.0,  weight = 0.7f },

            new FileDef { assetName = "File_DatacenterBak", display = "datacenter_backup.tar.gz",  icon = "targz", tier = 7, sizeMult = 0.8,  weight = 1f },
            new FileDef { assetName = "File_InternetArchive",display= "internet_archive.dat",      icon = "dat_pb",tier = 7, sizeMult = 2.0,  weight = 1f },

            new FileDef { assetName = "File_GenomeDb",      display = "genome_db.dat",             icon = "dat_pb",tier = 8, sizeMult = 0.9,  weight = 1f },
            new FileDef { assetName = "File_GalacticCensus",display = "galactic_census.tar.gz",    icon = "targz", tier = 8, sizeMult = 2.2,  weight = 1f }
        };

        struct UpgradeDef
        {
            public string assetName;
            public string display;
            public string description;
            public string icon;
            public UpgradeType type;
            public UpgradeTab tab;
            public double baseCost;      // Connection ise 0 -> otomatik hesaplanir
            public int maxLevel;
            public float effect;
            public int requiredTier;
            public int connTier;
        }

        static readonly UpgradeDef[] Upgrades =
        {
            // --- Altyapi (baglanti yukseltmeleri doguya ayrica uretiliyor) ---
            // GDD 6.3'teki pasif taban hiz formulunun tek kaynagi. Altyapi
            // sekmesinde duruyor cunku o sekme aksi halde yalnizca kilitli
            // baglanti kartlarindan olusuyor — arada alinacak bir sey kalmiyordu.
            new UpgradeDef { assetName = "Upg_HatBakimi", display = "Hat Bakimi",
                description = "Hattaki paraziti azaltir. Bosta (tiklamadan) gelen taban hizi artirir.",
                icon = "UpgradeIcons/icon_upgrade_linemaintenance", type = UpgradeType.PassiveSpeed,
                tab = UpgradeTab.Infrastructure, baseCost = 25, maxLevel = 20, effect = 0.10f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Upg_IXP", display = "Yerel Degisim Noktasi",
                description = "Trafik sehir disina cikmadan yonlenir. Taban hizi belirgin artirir.",
                icon = "UpgradeIcons/icon_upgrade_ixp", type = UpgradeType.PassiveSpeed,
                tab = UpgradeTab.Infrastructure, baseCost = 25000, maxLevel = 6, effect = 0.35f,
                requiredTier = 3, connTier = -1 },

            new UpgradeDef { assetName = "Upg_OmurgaPeering", display = "Omurga Peering",
                description = "Dogrudan omurgaya baglanirsin. Taban hizda buyuk sicrama.",
                icon = "UpgradeIcons/icon_upgrade_secondline", type = UpgradeType.PassiveSpeed,
                tab = UpgradeTab.Infrastructure, baseCost = 53000000, maxLevel = 0, effect = 0.80f,
                requiredTier = 6, connTier = -1 },

            // --- Donanim ---
            new UpgradeDef { assetName = "Upg_ModemeVurmak", display = "Modeme Vurmak",
                description = "Klasik cozum. Tiklama basina hiz artisini yukseltir.",
                icon = "UpgradeIcons/icon_upgrade_hammer", type = UpgradeType.ClickPower,
                tab = UpgradeTab.Hardware, baseCost = 4, maxLevel = 0, effect = 0.15f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Upg_HariciFan", display = "Harici Fan",
                description = "Modemi serin tutar. Kirmizi bolgede kalabilecegin sureyi uzatir.",
                icon = "UpgradeIcons/icon_upgrade_fan", type = UpgradeType.OverheatTolerance,
                tab = UpgradeTab.Hardware, baseCost = 140, maxLevel = 10, effect = 0.5f,
                requiredTier = 1, connTier = -1 },

            new UpgradeDef { assetName = "Upg_SinyalYukselteci", display = "Sinyal Yukselteci",
                description = "Zayif sinyali guclendirir. Tiklama basina hiz artisini buyutur.",
                icon = "UpgradeIcons/icon_upgrade_amplifier", type = UpgradeType.ClickPower,
                tab = UpgradeTab.Hardware, baseCost = 700, maxLevel = 15, effect = 0.25f,
                requiredTier = 2, connTier = -1 },

            new UpgradeDef { assetName = "Upg_IsiMacunu", display = "Isi Macunu",
                description = "Islemci isiyi daha iyi atar. Asiri isinma cezasini kisaltir.",
                icon = "UpgradeIcons/icon_upgrade_thermalpaste",
                type = UpgradeType.OverheatPenaltyReduction,
                tab = UpgradeTab.Hardware, baseCost = 1100, maxLevel = 6, effect = 0.12f,
                requiredTier = 2, connTier = -1 },

            new UpgradeDef { assetName = "Upg_Cat6Kablo", display = "Cat6 Kablo",
                description = "Bakir yerine korumali kablo. Taban hizi artirir.",
                icon = "UpgradeIcons/icon_upgrade_cat6", type = UpgradeType.PassiveSpeed,
                tab = UpgradeTab.Hardware, baseCost = 12000, maxLevel = 8, effect = 0.20f,
                requiredTier = 3, connTier = -1 },

            new UpgradeDef { assetName = "Upg_UPS", display = "Kesintisiz Guc (UPS)",
                description = "Asiri isinmada hat tamamen kopmaz, taban hizin bir kismi korunur.",
                icon = "UpgradeIcons/icon_upgrade_ups", type = UpgradeType.OverheatSoftFail,
                tab = UpgradeTab.Hardware, baseCost = 150000, maxLevel = 3, effect = 0.10f,
                requiredTier = 4, connTier = -1 },

            new UpgradeDef { assetName = "Upg_YonluAnten", display = "Yonlu Anten",
                description = "Sinyali tek noktaya odaklar. Kirmizi bolge tepe carpanini yukseltir.",
                icon = "UpgradeIcons/icon_upgrade_antenna", type = UpgradeType.RedlineBonus,
                tab = UpgradeTab.Hardware, baseCost = 250000, maxLevel = 4, effect = 0.25f,
                requiredTier = 4, connTier = -1 },

            new UpgradeDef { assetName = "Upg_NvmeSSD", display = "NVMe SSD",
                description = "Yazma darbogazi biter. Tamamlanan her dosyadan ek odul alirsin.",
                icon = "UpgradeIcons/icon_upgrade_ssd", type = UpgradeType.CompletionBonus,
                tab = UpgradeTab.Hardware, baseCost = 1300000, maxLevel = 10, effect = 0.08f,
                requiredTier = 5, connTier = -1 },

            new UpgradeDef { assetName = "Upg_SiviSogutma", display = "Sivi Sogutma",
                description = "Radyator devreye girer. Kirmizi bolgede cok daha uzun kalabilirsin.",
                icon = "UpgradeIcons/icon_upgrade_watercooling", type = UpgradeType.OverheatTolerance,
                tab = UpgradeTab.Hardware, baseCost = 2700000, maxLevel = 5, effect = 1.5f,
                requiredTier = 5, connTier = -1 },

            // Otomasyon kademe 5'te aciliyor (UPGRADE_TREE'de 6 yaziyordu).
            // "Artik elle yapmiyorum" ani bir idle oyunun en tatmin edici esigi;
            // kademe 6'ya birakmak oyuncuyu gereginden uzun sure tiklatiyordu.
            new UpgradeDef { assetName = "Upg_MekanikKlavye", display = "Mekanik Klavye",
                description = "Tuslar kendi kendine tiklar. Saniyede otomatik tiklama saglar.",
                icon = "UpgradeIcons/icon_upgrade_keyboard", type = UpgradeType.AutoClick,
                tab = UpgradeTab.Hardware, baseCost = 2000000, maxLevel = 8, effect = 0.5f,
                requiredTier = 5, connTier = -1 },

            new UpgradeDef { assetName = "Upg_KuantumIslemci", display = "Kuantum Islemci",
                description = "Paketleri es zamanli isler. Tiklama gucunu katlar.",
                icon = "UpgradeIcons/icon_upgrade_cpu", type = UpgradeType.ClickPower,
                tab = UpgradeTab.Hardware, baseCost = 30000000000, maxLevel = 0, effect = 1.00f,
                requiredTier = 8, connTier = -1 },

            // --- Yazilim ---
            new UpgradeDef { assetName = "Upg_DnsAyari", display = "DNS Ayari",
                description = "Her tiklamada Kritik Hiz Patlamasi (5x) tetikleme ihtimali.",
                icon = "UpgradeIcons/icon_upgrade_dns", type = UpgradeType.CritChance,
                tab = UpgradeTab.Software, baseCost = 390, maxLevel = 5, effect = 0.05f,
                requiredTier = 2, connTier = -1 },

            new UpgradeDef { assetName = "Upg_PremiumSunucu", display = "Premium Sunucu Uyeligi",
                description = "Dosya basi temel odulu kalici olarak artirir.",
                icon = "UpgradeIcons/icon_upgrade_premiumserver", type = UpgradeType.RewardMultiplier,
                tab = UpgradeTab.Software, baseCost = 1900, maxLevel = 0, effect = 0.10f,
                requiredTier = 2, connTier = -1 },

            new UpgradeDef { assetName = "Upg_DownloadManager", display = "Download Manager",
                description = "Oyun kapaliyken de indirmeye devam eder. 2. seviye: Pro (%100 / 24 saat).",
                icon = "UpgradeIcons/icon_upgrade_downloadmanager", type = UpgradeType.OfflineCapacity,
                tab = UpgradeTab.Software, baseCost = 9800, maxLevel = 2, effect = 1f,
                requiredTier = 3, connTier = -1 },

            new UpgradeDef { assetName = "Upg_Sikistirma", display = "Sikistirma Algoritmasi",
                description = "Dosyalar sikistirilmis gelir. Indirilecek boyut kuculur.",
                icon = "UpgradeIcons/icon_upgrade_compression", type = UpgradeType.FileSizeReduction,
                tab = UpgradeTab.Software, baseCost = 15000, maxLevel = 8, effect = 0.06f,
                requiredTier = 3, connTier = -1 },

            new UpgradeDef { assetName = "Upg_ReklamEngelleyici", display = "Reklam Engelleyici",
                description = "Hatti mesgul eden istekleri keser. Olumsuz olaylarin cikma sansi duser.",
                icon = "UpgradeIcons/icon_upgrade_adblock", type = UpgradeType.EventResistance,
                tab = UpgradeTab.Software, baseCost = 200000, maxLevel = 4, effect = 0.15f,
                requiredTier = 4, connTier = -1 },

            new UpgradeDef { assetName = "Upg_OverclockAraci", display = "Overclock Araci",
                description = "Modemi sinirlarinin otesine zorlar. Kirmizi bolge daha erken baslar.",
                icon = "UpgradeIcons/icon_upgrade_overclock", type = UpgradeType.RedlineThreshold,
                tab = UpgradeTab.Software, baseCost = 32000000, maxLevel = 5, effect = 0.04f,
                requiredTier = 6, connTier = -1 },

            new UpgradeDef { assetName = "Upg_OtomatikKuyruk", display = "Otomatik Kuyruk",
                description = "Dosyalari kendisi secer: en karli olani indirir. 2. seviye: her zaman en iyisi.",
                icon = "UpgradeIcons/icon_upgrade_autoqueue", type = UpgradeType.QueueAutomation,
                tab = UpgradeTab.Software, baseCost = 13000000, maxLevel = 2, effect = 1f,
                requiredTier = 6, connTier = -1 },

            new UpgradeDef { assetName = "Upg_MakroScript", display = "Makro Script",
                description = "Tiklamalari betikler. Otomatik tiklama hizini belirgin artirir.",
                icon = "UpgradeIcons/icon_upgrade_macro", type = UpgradeType.AutoClick,
                tab = UpgradeTab.Software, baseCost = 260000000, maxLevel = 6, effect = 0.8f,
                requiredTier = 7, connTier = -1 },

            // Not: cip ikonunu Kuantum Islemci ile paylasiyor. Ikisi de "islemci /
            // zeka" temasinda, farkli sekmelerde ve farkli kademelerde oldugu icin
            // yan yana gorunmuyorlar. Yeni sprite gerekirse tek eksik bu.
            new UpgradeDef { assetName = "Upg_RamYukseltmesi", display = "RAM Yukseltmesi",
                description = "Ayni anda ikinci bir dosya inmeye baslar (ek slot tam hizin %60'inda calisir).",
                icon = "UpgradeIcons/icon_upgrade_ram", type = UpgradeType.ParallelSlots,
                tab = UpgradeTab.Hardware, baseCost = 2300000, maxLevel = 1, effect = 1f,
                requiredTier = 5, connTier = -1 },

            new UpgradeDef { assetName = "Upg_IkinciHat", display = "Ikinci Hat",
                description = "Bir indirme slotu daha acar. Hat sayisi arttikca es zamanli indirirsin.",
                icon = "UpgradeIcons/icon_upgrade_secondline", type = UpgradeType.ParallelSlots,
                tab = UpgradeTab.Infrastructure, baseCost = 43000000, maxLevel = 2, effect = 1f,
                requiredTier = 6, connTier = -1 },

            new UpgradeDef { assetName = "Upg_TorrentIstemcisi", display = "Torrent Istemcisi",
                description = "Bitmis dosyalari paylasirsin. Bosta gelirinin bir kismi kadar ek kazanc.",
                icon = "UpgradeIcons/icon_upgrade_torrent", type = UpgradeType.SeedIncome,
                tab = UpgradeTab.Software, baseCost = 1000000, maxLevel = 10, effect = 0.04f,
                requiredTier = 5, connTier = -1 },

            new UpgradeDef { assetName = "Upg_VpnTuneli", display = "VPN Tuneli",
                description = "Kota asimi artik hizini kesemez. Bedeli durur ama hat yavaslamaz.",
                icon = "UpgradeIcons/icon_upgrade_vpn", type = UpgradeType.QuotaBypass,
                tab = UpgradeTab.Software, baseCost = 1300000000, maxLevel = 1, effect = 1f,
                requiredTier = 7, connTier = -1 },

            // --- Prestij yetenekleri (Fiber Kredisi ile alinir) ---
            // baseCost burada FK ADEDIDIR, para degil. Maliyet yine 1,15^n ile
            // buyudugu icin ust seviyeler birkac Hat Degisimi biriktirmeyi
            // gerektiriyor — FK kit bir kaynak olmali.
            new UpgradeDef { assetName = "Skill_HizliBaslangic", display = "Hizli Baslangic",
                description = "Hat Degisimi sonrasi sifirdan degil, daha ust bir kademeden baslarsin.",
                icon = "UpgradeIcons/icon_prestige_quickstart", type = UpgradeType.StartingTier,
                tab = UpgradeTab.Prestige, baseCost = 3, maxLevel = 3, effect = 1f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Skill_KaliciGuc", display = "Kalici Guc",
                description = "Tiklama gucu kalici olarak artar. Sifirlanmaz.",
                icon = "UpgradeIcons/icon_prestige_archive", type = UpgradeType.ClickPower,
                tab = UpgradeTab.Prestige, baseCost = 4, maxLevel = 5, effect = 0.20f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Skill_OfflineUstasi", display = "Offline Ustasi",
                description = "Cevrimdisi kazanc verimini kalici olarak yukseltir.",
                icon = "UpgradeIcons/icon_prestige_offline", type = UpgradeType.OfflineEfficiency,
                tab = UpgradeTab.Prestige, baseCost = 4, maxLevel = 4, effect = 0.10f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Skill_KrediFaizi", display = "Kredi Faizi",
                description = "Her Hat Degisiminde kazandigin Fiber Kredisi artar.",
                icon = "UpgradeIcons/icon_prestige_interest", type = UpgradeType.PrestigeCreditBonus,
                tab = UpgradeTab.Prestige, baseCost = 6, maxLevel = 5, effect = 0.20f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Skill_SogukBaslangic", display = "Soguk Baslangic",
                description = "Modem kalici olarak daha gec isinir.",
                icon = "UpgradeIcons/icon_prestige_coldstart", type = UpgradeType.OverheatTolerance,
                tab = UpgradeTab.Prestige, baseCost = 5, maxLevel = 3, effect = 1.0f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Skill_KritikUstasi", display = "Kritik Ustasi",
                description = "Kritik hiz patlamasi ihtimali kalici olarak artar.",
                icon = "UpgradeIcons/icon_prestige_themes", type = UpgradeType.CritChance,
                tab = UpgradeTab.Prestige, baseCost = 5, maxLevel = 4, effect = 0.03f,
                requiredTier = 0, connTier = -1 },

            new UpgradeDef { assetName = "Upg_YzOptimizer", display = "Yapay Zeka Optimizer",
                description = "Rotalari kendi ogrenir. Dosya basi odulu buyuk oranda artirir.",
                icon = "UpgradeIcons/icon_upgrade_cpu", type = UpgradeType.RewardMultiplier,
                tab = UpgradeTab.Software, baseCost = 580000000, maxLevel = 0, effect = 0.75f,
                requiredTier = 7, connTier = -1 }
        };

        // ------------------------------------------------------------ menu

        [MenuItem("Tools/SpeedDownload/2. Generate Data Assets", false, 110)]
        public static void Generate()
        {
            Run(false);
        }

        [MenuItem("Tools/SpeedDownload/2b. Regenerate Data Assets (OVERWRITE)", false, 111)]
        public static void Regenerate()
        {
            bool ok = EditorUtility.DisplayDialog(
                "Veri varliklarini sifirla",
                "Mevcut ScriptableObject'lerin TUM alanlari GDD tablolarindaki degerlerle " +
                "yeniden yazilacak. Inspector'dan yaptigin denge ayarlari kaybolur.\n\nDevam?",
                "Evet, uzerine yaz", "Vazgec");

            if (ok) Run(true);
        }

        // ------------------------------------------------------------ uretim

        static void Run(bool overwrite)
        {
            EnsureFolders();

            var log = new StringBuilder();
            int created = 0, kept = 0;
            var missingSprites = new List<string>();

            // --- Config ---
            GameConfigSO config = GetOrCreate<GameConfigSO>(RootFolder + "/GameConfig.asset", overwrite,
                                                            ref created, ref kept, log);

            // --- Kademeler ---
            var tierAssets = new ConnectionTierSO[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
            {
                TierDef def = Tiers[i];
                string path = TiersFolder + "/Tier" + def.index + "_" + Sanitize(def.name) + ".asset";

                bool isNew;
                ConnectionTierSO tier = GetOrCreate<ConnectionTierSO>(path, overwrite, ref created, ref kept, log, out isNew);
                tierAssets[i] = tier;

                if (isNew || overwrite)
                {
                    tier.tierIndex = def.index;
                    tier.tierName = def.name;
                    tier.flavorText = def.flavor;
                    tier.minSpeed = def.min;
                    tier.maxSpeed = def.max;
                    tier.baseSpeed = def.min;
                    tier.useLogarithmicScale = true;
                    tier.needleInertia = def.inertia;
                    tier.speedDecayRate = def.decay;
                    tier.roomTint = def.roomTint;
                    tier.needleColor = def.needleColor;
                    tier.dialHueShift = 0f;
                    tier.isProceduralTemplate = false;

                    tier.dialBackground = LoadSprite(def.dial, missingSprites);
                    tier.roomBackground = LoadSprite(def.room, missingSprites);
                    tier.connectionIcon = def.connIcon == null ? null : LoadSprite(def.connIcon, missingSprites);

                    EditorUtility.SetDirty(tier);
                }
            }

            // --- Sonsuz kademe sablonu ---
            bool infNew;
            ConnectionTierSO infinite = GetOrCreate<ConnectionTierSO>(
                TiersFolder + "/Tier9_SonsuzKademe_Template.asset", overwrite, ref created, ref kept, log, out infNew);

            if (infNew || overwrite)
            {
                TierDef last = Tiers[Tiers.Length - 1];
                infinite.tierIndex = 9;
                infinite.tierName = "Sonsuz Kademe";
                infinite.flavorText = "Prosedurel uretilen kademe. Kadran hue kaymasiyla ayrisir.";
                infinite.minSpeed = last.max;
                infinite.maxSpeed = last.max * 100.0;
                infinite.baseSpeed = last.max;
                infinite.useLogarithmicScale = true;
                infinite.needleInertia = 0.24f;
                infinite.speedDecayRate = 1.15f;
                infinite.roomTint = Color.white;
                infinite.needleColor = NeonYellow;
                infinite.isProceduralTemplate = true;
                infinite.dialBackground = LoadSprite("Dial/dial_tier8_datacenter_bg", missingSprites);
                infinite.roomBackground = LoadSprite("Backgrounds/bg_tier9_abstract_space", missingSprites);
                infinite.connectionIcon = LoadSprite("UpgradeIcons/icon_conn_datacenter", missingSprites);
                EditorUtility.SetDirty(infinite);
            }

            // --- Dosyalar ---
            var fileAssets = new List<FileDataSO>();
            var rewardSumByTier = new double[Tiers.Length];
            var rewardCountByTier = new int[Tiers.Length];

            foreach (FileDef def in Files)
            {
                string path = FilesFolder + "/" + def.assetName + ".asset";

                bool isNew;
                FileDataSO file = GetOrCreate<FileDataSO>(path, overwrite, ref created, ref kept, log, out isNew);
                fileAssets.Add(file);

                double reward = TierRewardBase[def.tier] * def.sizeMult;
                rewardSumByTier[def.tier] += reward;
                rewardCountByTier[def.tier]++;

                if (isNew || overwrite)
                {
                    file.displayName = def.display;
                    file.icon = LoadSprite("FileIcons/icon_file_" + def.icon, missingSprites);
                    file.sizeBits = SizeBitsFor(Tiers[def.tier], def.sizeMult, config);
                    file.baseReward = reward;
                    file.minConnectionTier = def.tier;
                    file.maxConnectionTier = def.tier;
                    file.weight = def.weight;
                    file.isProceduralTemplate = false;
                    EditorUtility.SetDirty(file);
                }
            }

            // Son kademedeki dosyalar ust kademelerde de gecerli olsun
            // (yoksa kademe 8'den sonra havuz boslar).
            for (int i = 0; i < fileAssets.Count; i++)
            {
                if (fileAssets[i].minConnectionTier == Tiers.Length - 1 &&
                    (fileAssets[i].maxConnectionTier == Tiers.Length - 1))
                {
                    if (overwrite || fileAssets[i].maxConnectionTier != -1)
                    {
                        fileAssets[i].maxConnectionTier = -1;
                        EditorUtility.SetDirty(fileAssets[i]);
                    }
                }
            }

            // --- Prosedurel dosya sablonu ---
            bool procNew;
            FileDataSO procFile = GetOrCreate<FileDataSO>(
                FilesFolder + "/File_MegaProcedural_Template.asset", overwrite, ref created, ref kept, log, out procNew);

            if (procNew || overwrite)
            {
                procFile.displayName = "blackhole_dump.mega";
                procFile.icon = LoadSprite("FileIcons/icon_file_mega_procedural", missingSprites);
                procFile.sizeBits = SizeBitsFor(Tiers[Tiers.Length - 1], 4.0, config);
                procFile.baseReward = TierRewardBase[Tiers.Length - 1] * 4.0;
                procFile.minConnectionTier = 9;
                procFile.maxConnectionTier = -1;
                procFile.weight = 1f;
                procFile.isProceduralTemplate = true;
                EditorUtility.SetDirty(procFile);
            }

            // --- Yukseltmeler: baglanti (maliyet oduller uzerinden turetilir) ---
            var upgradeAssets = new List<UpgradeSO>();

            for (int t = 1; t < Tiers.Length; t++)
            {
                TierDef def = Tiers[t];
                string path = UpgradesFolder + "/Upg_Conn" + t + "_" + Sanitize(def.name) + ".asset";

                bool isNew;
                UpgradeSO up = GetOrCreate<UpgradeSO>(path, overwrite, ref created, ref kept, log, out isNew);
                upgradeAssets.Add(up);

                if (isNew || overwrite)
                {
                    double prevAvg = rewardCountByTier[t - 1] > 0
                        ? rewardSumByTier[t - 1] / rewardCountByTier[t - 1]
                        : TierRewardBase[t - 1];

                    up.displayName = def.name;
                    up.description = def.flavor + "\nHizi " + t + ". kademeye yukseltir.";
                    up.icon = LoadSprite(def.connIcon, missingSprites);
                    up.type = UpgradeType.Connection;
                    up.tab = UpgradeTab.Infrastructure;
                    up.baseCost = RoundNice(prevAvg * ConnectionCostFactor);
                    up.costGrowth = 1f;      // tek seferlik satin alma
                    up.maxLevel = 1;
                    up.effectPerLevel = 0f;
                    up.requiredTierIndex = t - 1;
                    up.connectionTierIndex = t;
                    EditorUtility.SetDirty(up);
                }
            }

            // --- Yukseltmeler: donanim + yazilim ---
            foreach (UpgradeDef def in Upgrades)
            {
                string path = UpgradesFolder + "/" + def.assetName + ".asset";

                bool isNew;
                UpgradeSO up = GetOrCreate<UpgradeSO>(path, overwrite, ref created, ref kept, log, out isNew);
                upgradeAssets.Add(up);

                if (isNew || overwrite)
                {
                    up.displayName = def.display;
                    up.description = def.description;
                    up.icon = LoadSprite(def.icon, missingSprites);
                    up.type = def.type;
                    up.tab = def.tab;
                    up.baseCost = def.baseCost;
                    up.costGrowth = config != null ? config.upgradeCostGrowth : 1.15f;
                    up.maxLevel = def.maxLevel;
                    up.effectPerLevel = def.effect;
                    up.requiredTierIndex = def.requiredTier;
                    up.connectionTierIndex = def.connTier;
                    EditorUtility.SetDirty(up);
                }
            }

            // --- Veritabani (her zaman yeniden baglanir) ---
            bool dbNew;
            GameDatabaseSO db = GetOrCreate<GameDatabaseSO>(
                RootFolder + "/GameDatabase.asset", overwrite, ref created, ref kept, log, out dbNew);

            db.config = config;
            db.tiers = tierAssets;
            db.files = fileAssets.ToArray();
            db.upgrades = upgradeAssets.ToArray();
            db.infiniteTierTemplate = infinite;
            db.proceduralFileTemplate = procFile;
            EditorUtility.SetDirty(db);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // --- Rapor ---
            var report = new StringBuilder();
            report.AppendLine("[SpeedDownload] Veri varliklari uretildi.");
            report.AppendLine("  Kademe      : " + tierAssets.Length + " (+1 sonsuz sablon)");
            report.AppendLine("  Dosya       : " + fileAssets.Count + " (+1 prosedurel sablon)");
            report.AppendLine("  Yukseltme   : " + upgradeAssets.Count);
            report.AppendLine("  Olusturulan : " + created + ",  korunan : " + kept);

            if (missingSprites.Count > 0)
            {
                report.AppendLine("  ! BULUNAMAYAN SPRITE: " + missingSprites.Count);
                foreach (string m in missingSprites) report.AppendLine("      " + m);
                Debug.LogError(report.ToString());
            }
            else
            {
                report.AppendLine("  Tum sprite referanslari baglandi.");
                Debug.Log(report.ToString());
            }

            // EGRI POLITIKASININ SAHIBI BU ARAC DEGIL.
            //
            // Yukarida costGrowth herkese config.upgradeCostGrowth (1.15) olarak
            // yaziliyor. Bu, kart arketiplerini (Sprint/Standart/Dayanikli/
            // Omurga) TEK TEK SILER — yani "2b. Regenerate (OVERWRITE)" her
            // calistiginda yukseltme agaci sessizce yeniden duzlesirdi ve bunu
            // fark etmek icin oyunu oynamak gerekirdi.
            //
            // Uretim bittikten sonra politikanin sahibini cagirip egrileri geri
            // koyuyoruz. Boylece 2b idempotent kaliyor.
            UpgradeBalanceTool.Apply();

            LogBalanceTable(tierAssets, fileAssets, upgradeAssets, config);
        }

        // ------------------------------------------------------------ yardimcilar

        /// <summary>
        /// Dosya boyutunu kademe hizindan turetir. GDD v2.2'nin sabit boyut tablosu
        /// dusuk kademelerde saatlerce suren indirmelere yol aciyordu; burada her
        /// dosya kademe referans hizinda ~fileTargetSeconds saniyede biter.
        /// </summary>
        static double SizeBitsFor(TierDef tier, double sizeMult, GameConfigSO config)
        {
            double targetSeconds = config != null ? config.fileTargetSeconds : 12.0;
            float t = config != null ? config.fileReferenceSpeedT : 0.6f;

            double logMin = System.Math.Log10(tier.min);
            double logMax = System.Math.Log10(tier.max);
            double refSpeed = System.Math.Pow(10.0, logMin + t * (logMax - logMin));

            return refSpeed * targetSeconds * sizeMult;
        }

        /// <summary>Maliyetleri okunabilir sayilara yuvarlar (2 anlamli hane).</summary>
        static double RoundNice(double v)
        {
            if (v <= 0) return 1;
            double mag = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(v)) - 1);
            return System.Math.Round(v / mag) * mag;
        }

        static Sprite LoadSprite(string relativePath, List<string> missing)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            string full = SpritesRoot + relativePath + ".png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(full);
            if (sprite == null) missing.Add(full);
            return sprite;
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(TiersFolder))
                AssetDatabase.CreateFolder(RootFolder, "Tiers");
            if (!AssetDatabase.IsValidFolder(FilesFolder))
                AssetDatabase.CreateFolder(RootFolder, "Files");
            if (!AssetDatabase.IsValidFolder(UpgradesFolder))
                AssetDatabase.CreateFolder(RootFolder, "Upgrades");
        }

        static T GetOrCreate<T>(string path, bool overwrite, ref int created, ref int kept,
                                StringBuilder log) where T : ScriptableObject
        {
            bool isNew;
            return GetOrCreate<T>(path, overwrite, ref created, ref kept, log, out isNew);
        }

        static T GetOrCreate<T>(string path, bool overwrite, ref int created, ref int kept,
                                StringBuilder log, out bool isNew) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                isNew = false;
                if (!overwrite) kept++;
                return existing;
            }

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            created++;
            isNew = true;
            log.AppendLine("  + " + path);
            return asset;
        }

        static string Sanitize(string s)
        {
            return s.Replace(" ", "").Replace("/", "").Replace("-", "");
        }

        /// <summary>
        /// Uretilen degerleri insan okunur halde konsola dokerek denge kontrolunu
        /// mumkun kilar — Inspector'da 24 asset acmaya gerek kalmaz.
        /// </summary>
        static void LogBalanceTable(ConnectionTierSO[] tiers, List<FileDataSO> files,
                                    List<UpgradeSO> upgrades, GameConfigSO config)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[SpeedDownload] Denge tablosu (uretilen degerler)");
            sb.AppendLine();
            sb.AppendLine("KADEMELER");
            sb.AppendLine("  #  Ad                          Hiz araligi                  Referans hiz");

            foreach (ConnectionTierSO t in tiers)
            {
                if (t == null) continue;
                double logMin = System.Math.Log10(t.minSpeed);
                double logMax = System.Math.Log10(t.maxSpeed);
                double refSpeed = System.Math.Pow(10.0, logMin + 0.6 * (logMax - logMin));

                sb.AppendLine(string.Format("  {0}  {1,-26}  {2,-12} -> {3,-12}  {4}",
                    t.tierIndex, t.tierName,
                    Util.NumberFormatter.FormatSpeed(t.minSpeed),
                    Util.NumberFormatter.FormatSpeed(t.maxSpeed),
                    Util.NumberFormatter.FormatSpeed(refSpeed)));
            }

            sb.AppendLine();
            sb.AppendLine("DOSYALAR  (sure = referans hizda)");
            sb.AppendLine("  Kademe  Ad                          Boyut         Odul          Sure");

            foreach (FileDataSO f in files)
            {
                if (f == null) continue;
                ConnectionTierSO t = tiers[Mathf.Clamp(f.minConnectionTier, 0, tiers.Length - 1)];
                double logMin = System.Math.Log10(t.minSpeed);
                double logMax = System.Math.Log10(t.maxSpeed);
                double refSpeed = System.Math.Pow(10.0, logMin + 0.6 * (logMax - logMin));

                sb.AppendLine(string.Format("  {0,-6}  {1,-26}  {2,-12}  {3,-12}  {4}",
                    f.minConnectionTier, f.displayName,
                    Util.NumberFormatter.FormatSize(f.sizeBits),
                    Util.NumberFormatter.FormatMoney(f.baseReward),
                    Util.NumberFormatter.FormatDuration(f.sizeBits / refSpeed)));
            }

            sb.AppendLine();
            sb.AppendLine("YUKSELTMELER");
            sb.AppendLine("  Sekme           Ad                          Baslangic maliyeti  Max sv.");

            foreach (UpgradeSO u in upgrades)
            {
                if (u == null) continue;
                sb.AppendLine(string.Format("  {0,-14}  {1,-26}  {2,-18}  {3}",
                    u.tab, u.displayName,
                    Util.NumberFormatter.FormatMoney(u.baseCost),
                    u.maxLevel == 0 ? "sinirsiz" : u.maxLevel.ToString()));
            }

            Debug.Log(sb.ToString());
        }
    }
}
