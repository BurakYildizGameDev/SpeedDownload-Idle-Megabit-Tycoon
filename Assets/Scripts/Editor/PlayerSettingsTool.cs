using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Mobil yayin ayarlari (PLAN Bolum 10, Faz 10).
    ///
    /// Elle ayarlanan Player Settings zamanla kayar ve neyin neden oyle
    /// oldugu kaybolur; tek komutla uygulanabilir olmasi hem tekrarlanabilir
    /// hem de gerekcesi kodda yazili.
    /// </summary>
    public static class PlayerSettingsTool
    {
        const string CompanyName = "SpeedDownload";
        const string ProductName = "Megabit Tycoon";
        const string BundleId = "com.speeddownload.megabittycoon";
        const string IconPath = "Assets/Sprites/Branding/logo_icon_appstore.png";

        /// <summary>
        /// Hedef API seviyesi. Google Play yeni uygulamalar icin her yil yukselen
        /// bir minimum target API zorunlulugu uyguluyor — YUKLEMEDEN ONCE guncel
        /// degeri Play Console'dan dogrula ve gerekirse burayi yukselt.
        /// Sabit tutulmasi bilincli: "Automatic" build makinesine gore degisir.
        ///
        /// 2026-08-29: 35 -> 36. Google Play 31 Agustos 2026'dan itibaren yeni
        /// uygulamalarda ve guncellemelerde Android 16 (API 36) sart kosuyor;
        /// 35 ile yuklenen paket reddediliyor. Bir sonraki esik icin ayni yeri
        /// yukselt ve "4. Apply Mobile Player Settings" komutunu tekrar calistir.
        /// </summary>
        const AndroidSdkVersions TargetSdk = AndroidSdkVersions.AndroidApiLevel36;

        [MenuItem("Tools/SpeedDownload/4. Apply Mobile Player Settings", false, 40)]
        public static void Apply()
        {
            var log = new List<string>();

            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;
            log.Add("kimlik      : " + CompanyName + " / " + ProductName);

            // --- Yonelim: portre kilitli (GDD Bolum 12) ---
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            log.Add("yonelim     : Portrait (kilitli, ters portre kapali)");

            // --- Android ---
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, BundleId);
            // Unity 6'nin destekledigi en dusuk seviye 26 (Android 8.0).
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;

            // Target SDK ARTIK PINLI. "Automatic" makinede yuklu en yuksek SDK'yi
            // secer; build makinesi degisince hedef seviye sessizce degisir ve
            // Google Play'in target API sartini bilmeden asabilir/kacirabilirsin.
            PlayerSettings.Android.targetSdkVersion = TargetSdk;

            // Google Play 2021'den beri 64-bit sart; IL2CPP + ARM64 zorunlu.
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);

            // --- Mimari ---
            //
            // ARMv7 once kapatilmisti. Olculen APK'da native kutuphaneler soyle
            // dagiliyordu (acilmis boyut):
            //
            //     lib/arm64-v8a    56,4 MB
            //     lib/armeabi-v7a  43,1 MB   <- ikinci bir kopya
            //     assets/bin       16,7 MB
            //     classes.dex       6,4 MB
            //     TOPLAM          123,3 MB  -> cihazda "120 MB" olarak gorunuyordu
            //
            // O olcum DOGRUDAN DAGITILAN APK icindi: orada iki mimari de tek
            // pakette tasinir ve oyuncu ikisini birden indirir.
            //
            // 2026-09-17: dagitim AAB'ye gecti, ARMv7 geri acildi. AAB'de Play
            // cihaza YALNIZCA kendi mimarisini gonderir — yani ARMv7 eklemek
            // oyuncunun indirme boyutunu buyutmez, yalnizca build suresini ve
            // yuklenen .aab dosyasini buyutur. Karsiliginda 32-bit cihazlar
            // kapsama girer.
            //
            // Not: minSdk 26 (Android 8.0, 2017) zaten 32-bit-only cihazlarin
            // buyuk kismini disarida birakiyor, yani kazanc sinirli. Yine de
            // AAB'de maliyeti oyuncuya yansimadigi icin acik birakmak guvenli.
            PlayerSettings.Android.targetArchitectures =
                AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;

            // IL2CPP ciktisini hiz yerine BOYUT icin uret. libil2cpp.so tek
            // basina 33 MB'ti; bu ayar jenerik kod sisirmesini kirpiyor.
            // Bir clicker'da kaybedilen birkac yuzde islemci zamani hissedilmez.
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android,
                                                   Il2CppCodeGeneration.OptimizeSize);

            // Kullanilmayan motor kodunu ve yonetilen kodu at.
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,
                                                    ManagedStrippingLevel.High);

            // R8: classes.dex 6,4 MB'ti — kucultme kapaliydi.
            PlayerSettings.Android.minifyRelease = true;

            // Keystore'u arac YONETMEZ: parola gerektiriyor ve depoya sizmamali.
            // Yayin imzasi Player Settings > Publishing Settings'ten elle kurulur.

            // Google Play yeni uygulamalarda APK kabul etmiyor; ciktinin .aab olmasi sart.
            EditorUserBuildSettings.buildAppBundle = true;

            log.Add("android     : " + BundleId + "  minSdk 26  targetSdk " + (int)TargetSdk +
                    "  IL2CPP  ARMv7+ARM64  boyut-optimize  R8  AAB");

            // --- iOS ---
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);
            PlayerSettings.iOS.targetOSVersionString = "12.0";
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            log.Add("ios         : " + BundleId + "  min 12.0");

            // --- Cizim / performans ---
            // Idle oyun; 60 fps gereksiz pil yakar. 2D UI'da MSAA da gereksiz.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            PlayerSettings.MTRendering = true;
            log.Add("cizim       : vSync kapali, hedef 60 fps, multithreaded rendering");

            // --- Uygulama simgesi ---
            ApplyIcon(log);

            // --- Acilis ekrani ---
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = false;
            PlayerSettings.SplashScreen.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);

            // bg_loading ve logo_main uretilmisti ama hicbir yerde
            // kullanilmiyordu — oyunun yukleme ekrani yok, ana menusu de yok.
            // Dogru yerleri acilis ekrani.
            ApplySplashArt(log);

            log.Add("acilis      : arka plan #0D0D12 (Unity logosu Personal lisansta kapatilamayabilir)");

            AssetDatabase.SaveAssets();

            Debug.Log("[PlayerSettingsTool] Mobil ayarlar uygulandi.\n  " +
                      string.Join("\n  ", log.ToArray()));
        }

        const string SplashBackgroundPath = "Assets/Sprites/Backgrounds/bg_loading.png";
        const string SplashLogoPath = "Assets/Sprites/Branding/logo_main.png";

        static void ApplySplashArt(List<string> log)
        {
            var background = AssetDatabase.LoadAssetAtPath<Sprite>(SplashBackgroundPath);
            if (background != null)
            {
                PlayerSettings.SplashScreen.background = background;
                PlayerSettings.SplashScreen.backgroundPortrait = background;
                PlayerSettings.SplashScreen.drawMode = PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
                log.Add("  acilis/bg : " + SplashBackgroundPath);
            }
            else
            {
                log.Add("  acilis/bg : BULUNAMADI -> " + SplashBackgroundPath);
            }

            var logo = AssetDatabase.LoadAssetAtPath<Sprite>(SplashLogoPath);
            if (logo == null)
            {
                log.Add("  acilis/logo: BULUNAMADI -> " + SplashLogoPath);
                return;
            }

            PlayerSettings.SplashScreen.logos = new[]
            {
                PlayerSettings.SplashScreenLogo.Create(2f, logo)
            };
            log.Add("  acilis/logo: " + SplashLogoPath);
        }

        static void ApplyIcon(List<string> log)
        {
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null)
            {
                log.Add("SIMGE       : BULUNAMADI -> " + IconPath);
                return;
            }

            // Simge sprite degil, dogrudan texture olarak kullanilir; okunabilir
            // olmasi ve sikistirilmamis kalmasi gerekiyor.
            string path = AssetDatabase.GetAssetPath(icon);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                importer.SaveAndReimport();
                icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }

            // Her platform farkli sayida boyut bekliyor (iOS 9, Android birkac).
            // Tek gorseli beklenen sayida tekrarlamak dogru yol; Unity gerisini
            // olcekleyerek halleder.
            SetIconsFor(NamedBuildTarget.Android, icon, log);
            SetIconsFor(NamedBuildTarget.iOS, icon, log);
            SetIconsFor(NamedBuildTarget.Unknown, icon, log);

            log.Add("simge       : " + IconPath + " (" + icon.width + "x" + icon.height + ")");
        }

        static void SetIconsFor(NamedBuildTarget target, Texture2D icon, List<string> log)
        {
            int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Application);
            int count = sizes != null && sizes.Length > 0 ? sizes.Length : 1;

            var textures = new Texture2D[count];
            for (int i = 0; i < count; i++) textures[i] = icon;

            PlayerSettings.SetIcons(target, textures, IconKind.Application);
            log.Add("  simge/" + target.TargetName + " : " + count + " boyut dolduruldu");
        }

        [MenuItem("Tools/SpeedDownload/Verify Mobile Player Settings", false, 41)]
        public static void Verify()
        {
            var issues = new List<string>();

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.Portrait)
                issues.Add("yonelim Portrait degil");

            if (PlayerSettings.allowedAutorotateToLandscapeLeft ||
                PlayerSettings.allowedAutorotateToLandscapeRight)
                issues.Add("yatay donus hala acik");

            if (PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) != BundleId)
                issues.Add("Android paket adi yanlis");

            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP)
                issues.Add("Android IL2CPP degil (64-bit sarti karsilanmaz)");

            if ((PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) == 0)
                issues.Add("ARM64 kapali (Google Play reddeder)");

            // --- yayin sartlari ---

            if (!EditorUserBuildSettings.buildAppBundle)
                issues.Add("cikti APK (Google Play yeni uygulamalarda AAB istiyor)");

            if (PlayerSettings.Android.targetSdkVersion == AndroidSdkVersions.AndroidApiLevelAuto)
                issues.Add("targetSdkVersion Automatic — build makinesine gore degisir, pinlenmeli");

            // --- kod budama / kripto ---
            //
            // IL2CPP + "High" budama, yansima ile cozulen tipleri siliyor.
            // Kayit sifrelemesi kripto tiplerine dayaniyor: biri silinirse
            // oyun EDITORDE sorunsuz calisir ama APK'da kayit tutmaz —
            // gelistirme boyunca gorulmesi imkansiz bir hata.
            //
            // Asil koruma SaveCrypto'nun somut tipleri dogrudan kullanmasi;
            // link.xml ikinci emniyet kemeri. Dosyanin varligini burada
            // dogruluyoruz cunku silinmesi sessiz bir regresyon olurdu.
            ManagedStrippingLevel stripping =
                PlayerSettings.GetManagedStrippingLevel(NamedBuildTarget.Android);

            if (stripping >= ManagedStrippingLevel.High && !File.Exists("Assets/link.xml"))
            {
                issues.Add("budama seviyesi " + stripping + " ama Assets/link.xml YOK — " +
                           "kripto tipleri silinip kayit sistemi cihazda bozulabilir");
            }

            // Keystore YAYIN icin sart ama gelistirme sirasinda bos olmasi normal;
            // bu yuzden hata degil uyari olarak raporlaniyor.
            if (!PlayerSettings.Android.useCustomKeystore)
            {
                Debug.LogWarning("[PlayerSettingsTool] Imzalama anahtari kurulu degil. " +
                                 "Yayin build'i icin Player Settings > Publishing Settings " +
                                 "uzerinden keystore olustur ve YEDEKLE — kaybedilirse " +
                                 "uygulama bir daha guncellenemez.");
            }

            Texture2D[] icons = PlayerSettings.GetIcons(NamedBuildTarget.Android, IconKind.Application);
            if (icons == null || icons.Length == 0 || icons[0] == null)
                issues.Add("uygulama simgesi atanmamis");

            // --- AdMob kontrolu ---
            var settingsAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset");
            if (settingsAsset == null)
            {
                issues.Add("AdMob ayar dosyasi bulunamadi (Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset)");
            }
            else
            {
                var so = new SerializedObject(settingsAsset);
                var androidAppId = so.FindProperty("adMobAndroidAppId")?.stringValue;
                if (string.IsNullOrEmpty(androidAppId))
                    issues.Add("AdMob Android App ID bos! (Assets -> Google Mobile Ads -> Settings)");
            }

            if (issues.Count == 0)
            {
                Debug.Log("[PlayerSettingsTool] Mobil ve AdMob ayarlari eksiksiz dogrulandi.\n" +
                          "  yonelim   : " + PlayerSettings.defaultInterfaceOrientation + "\n" +
                          "  android   : " + PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android) + "\n" +
                          "  backend   : " + PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) + "\n" +
                          "  mimari    : " + PlayerSettings.Android.targetArchitectures + "\n" +
                          "  minSdk    : " + (int)PlayerSettings.Android.minSdkVersion + "\n" +
                          "  targetSdk : " + (int)PlayerSettings.Android.targetSdkVersion + "\n" +
                          "  cikti     : " + (EditorUserBuildSettings.buildAppBundle ? "AAB" : "APK") + "\n" +
                          "  keystore  : " + (PlayerSettings.Android.useCustomKeystore ? "kurulu" : "YOK (yayin icin sart)") + "\n" +
                          "  simge     : " + (icons[0] != null ? icons[0].name : "-"));
                return;
            }

            Debug.LogWarning("[PlayerSettingsTool] Eksikler (" + issues.Count + "):\n  - " +
                             string.Join("\n  - ", issues.ToArray()));
        }
    }
}
