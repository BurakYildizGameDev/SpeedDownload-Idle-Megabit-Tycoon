using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Mobil titresim (haptic feedback) yoneticisi.
    ///
    /// Bu sinif iki kez yanlis yonde ayarlandi ve ikisinde de titresim
    /// kullanilamaz hale geldi:
    ///
    ///   1. Ilk surum uc seviyede de <c>Handheld.Vibrate()</c> cagiriyordu —
    ///      Android'de ~250 ms, tum cihazi sarsan bir islem. Saniyede 5-10
    ///      tiklama olan bir oyunda bu dayanilmazdi.
    ///   2. Ikinci surum tepki olarak asiri kisiltti: 10 ms / 255'te 60 genlik.
    ///      Telefon motorunun donmeye baslamasi bile ~20 ms suruyor, dolayisiyla
    ///      bu degerler cogu cihazda HIC hissedilmiyordu. Ustelik titresim
    ///      yalnizca kritik vurus ve overheat'e baglanmisti; normal oynayista
    ///      hicbir sey titremiyordu. Disaridan bakinca "titresim calismiyor".
    ///
    /// Bu surum ortayi tutuyor:
    ///   - Android 10+ (API 29) uzerinde cihazin KENDI ayarlanmis efektleri
    ///     (EFFECT_TICK / EFFECT_CLICK / EFFECT_HEAVY_CLICK) kullaniliyor.
    ///     Bunlar uretici tarafindan o motora gore kalibre edilmis; ham
    ///     sure/genlik tahmininden her zaman daha iyi hissettiriyor.
    ///   - Kalibre efekt yoksa hissedilir sure/genlik degerlerine dusuyor.
    ///   - Tiklama titresimi geri geldi ama AYRI ve daha uzun bir bekleme
    ///     suresiyle: motor surekli calismiyor, pil yanmıyor.
    /// </summary>
    public static class HapticManager
    {
        const string HapticsPrefKey = "SpeedDownload_HapticsEnabled";

        // Kalibre efekt bulunamayan cihazlar icin yedek degerler.
        // Alt sinir bilincli: 20 ms'nin altinda cogu telefonda hicbir sey
        // hissedilmiyor, yalnizca pil harcaniyor.
        // TickMs 18'di — yukaridaki kendi kuralinin ALTINDA. Artik createOneShot
        // birincil yol oldugu icin bu deger dogrudan hissedilen sureyi belirliyor;
        // esigin altinda kalmasi "tiklayinca hicbir sey olmuyor" demekti.
        const long TickMs = 22;
        const long LightMs = 25;
        const long MediumMs = 40;
        const long HeavyMs = 65;

        const int TickAmplitude = 140;
        const int LightAmplitude = 150;
        const int MediumAmplitude = 200;
        const int HeavyAmplitude = 255;

        /// <summary>
        /// Genel titresimler arasindaki en kisa sure (sn).
        /// </summary>
        const float MinInterval = 0.05f;

        /// <summary>
        /// Tiklama titresimi icin ayri, daha uzun bekleme. Hizli tiklayan bir
        /// oyuncuda motorun kesintisiz calismasini engelliyor: saniyede en fazla
        /// ~8 darbe.
        /// </summary>
        const float MinClickInterval = 0.12f;

        static float _lastFireTime = -999f;
        static float _lastClickTime = -999f;

        /// <summary>
        /// Cihazda titresim kurulumunun sonucu — logcat'te gorunur.
        ///
        /// Bu sinif iki kez sessizce basarisiz oldu (once eksik VIBRATE izni,
        /// sonra GameActivity'de bulunamayan Context) ve her ikisinde de ne
        /// oyunda ne konsolda hicbir iz vardi. Artik kurulum sonucu tek satirda
        /// yaziliyor: sorun tekrarlarsa tahmin etmek yerine okunuyor.
        /// </summary>
        public static string Diagnostics { get; private set; } = "henuz cozumlenmedi";

        /// <summary>Android Context'in hangi yoldan bulundugu.</summary>
        public static string ContextSource { get; private set; } = "-";

        /// <summary>
        /// Platforma kac kez gercek darbe gonderildi.
        ///
        /// <see cref="Diagnostics"/> ile birlikte iki ayri soruyu ayirir:
        ///   sayac ARTMIYORSA  -> cagri yolu kopuk (tiklama haptik'e ulasmiyor)
        ///   sayac ARTIYOR ama hissedilmiyorsa -> cihaz/sistem tarafi
        /// Bu ayrim olmadan ucuncu kez tahmin yurutmek gerekiyordu.
        /// </summary>
        public static int FireCount { get; private set; }

        /// <summary>
        /// Titresim altyapisini onceden cozumler.
        ///
        /// Normalde ilk darbede tembel cozumleniyor; acilista cagrilinca
        /// <see cref="Diagnostics"/> daha oyuncu ekrana dokunmadan dolu olur,
        /// yani ayarlar ekranindaki teshis satiri hemen anlamli.
        /// </summary>
        public static void Warmup()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { Resolve(); }
            catch (System.Exception e) { Diagnostics = "warmup istisnasi: " + e.Message; }
#else
            Diagnostics = "bu platformda haptik yok (editor/PC)";
#endif
        }

        // PlayerPrefs okumasi ucuz degil; her tiklamada cagrildigi icin onbellek.
        static bool _cached;
        static bool _cacheValid;

        public static bool IsHapticsEnabled
        {
            get
            {
                if (!_cacheValid)
                {
                    // VARSAYILAN KAPALI.
                    //
                    // Titresim, cihazin sistem genelindeki "dokunsal geri
                    // bildirim" ayarina bagli: o kapaliyken oyun titresim
                    // istese de hicbir sey hissedilmiyor ve hata da olusmuyor.
                    // Acik gelseydi, o ayari kapali olan oyuncu "titresim ACIK
                    // yaziyor ama calismiyor" gorup oyunu bozuk sanardi —
                    // nitekim gelistirmede tam olarak bu yasandi.
                    //
                    // Kapali baslayip oyuncunun kendi actirmasi, acma aninda
                    // sistem ayarini hatirlatma firsati da veriyor
                    // (bkz. SettingsView.ToggleHaptics).
                    _cached = PlayerPrefs.GetInt(HapticsPrefKey, 0) == 1;
                    _cacheValid = true;
                }
                return _cached;
            }
            set
            {
                _cached = value;
                _cacheValid = true;
                PlayerPrefs.SetInt(HapticsPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Kadrana her dokunusta calan en hafif darbe. Kendi bekleme suresi var,
        /// bu yuzden diger geri bildirimleri (kritik, overheat) bastirmiyor.
        /// </summary>
        public static void ClickTick()
        {
            if (!IsHapticsEnabled) return;
            if (Time.unscaledTime - _lastClickTime < MinClickInterval) return;
            _lastClickTime = Time.unscaledTime;

            Fire(EffectTick, TickMs, TickAmplitude, true);
        }

        /// <summary>Dugme / arayuz dokunusu.</summary>
        public static void LightTap()
        {
            Fire(EffectTick, LightMs, LightAmplitude, false);
        }

        /// <summary>Satin alma, dosya tamamlama gibi olumlu anlar.</summary>
        public static void MediumImpact()
        {
            Fire(EffectClick, MediumMs, MediumAmplitude, false);
        }

        /// <summary>
        /// Agir darbe (kritik vurus, overheat, kademe atlama). Bekleme suresini
        /// atlar — bunlar seyrek ve oyuncunun hissetmesi GEREKEN anlar.
        /// </summary>
        public static void HeavyImpact()
        {
            Fire(EffectHeavyClick, HeavyMs, HeavyAmplitude, true);
        }

        // android.os.VibrationEffect sabitleri (API 29+).
        const int EffectTick = 2;        // EFFECT_TICK
        const int EffectClick = 0;       // EFFECT_CLICK
        const int EffectHeavyClick = 5;  // EFFECT_HEAVY_CLICK

        static void Fire(int predefinedEffect, long milliseconds, int amplitude, bool ignoreRateLimit)
        {
            if (!IsHapticsEnabled) return;

            // Bekleme suresini ATLAYAN cagrilar sayaci da ILERLETMEZ.
            //
            // Eskiden sayac kosulsuz guncelleniyordu: ClickTick ve HeavyImpact
            // (ikisi de bekleme suresini atlar) _lastFireTime'i surekli ileri
            // itiyor, bu da onlari izleyen 50 ms icindeki LightTap /
            // MediumImpact cagrilarini sessizce yutuyordu. Yani hizli tiklayan
            // bir oyuncuda satin alma titresimi cogu zaman hic hissedilmiyordu —
            // oysa bekleme suresinin amaci motoru kesintisiz calistirmamakti,
            // seyrek ve anlamli geri bildirimleri bastirmak degil.
            if (ignoreRateLimit)
            {
                FireNow(predefinedEffect, milliseconds, amplitude);
                return;
            }

            if (Time.unscaledTime - _lastFireTime < MinInterval) return;
            _lastFireTime = Time.unscaledTime;

            FireNow(predefinedEffect, milliseconds, amplitude);
        }

        /// <summary>Platforma gercek darbeyi gonderir (bekleme suresi kontrolu disinda).</summary>
        static void FireNow(int predefinedEffect, long milliseconds, int amplitude)
        {
            FireCount++;
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidVibrate(predefinedEffect, milliseconds, amplitude);
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS'ta Unity yerlesik bir haptic API'si sunmuyor; Handheld.Vibrate
            // tam titresim yapar. Yalnizca agir darbede kullaniliyor ki hafif
            // tiklamalar telefonu sarsmasin.
            if (amplitude >= HeavyAmplitude) Handheld.Vibrate();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static AndroidJavaClass _effectClass;
        static bool _resolved;
        static bool _supportsAmplitude;
        static bool _supportsPredefined;

        /// <summary>
        /// Sirasiyla: genlikli tek atim -> cihazin kalibre efekti -> duz sureli
        /// titresim. Hepsi basarisiz olursa sessizce vazgecer — titresim
        /// oynanisi etkilemeyen bir susleme, oyunu dusurmemeli.
        ///
        /// SIRA BILINCLI OLARAK DEGISTI. Once kalibre efekt (EFFECT_TICK vb.)
        /// deneniyordu; oyle hissettirmesi daha iyi ama GUVENILIR DEGIL:
        /// createPredefined ile uretilen efektler "dokunma geri bildirimi"
        /// sinifina giriyor ve bircok OEM'de sistemdeki "dokunsal geri bildirim"
        /// ayari kapaliysa SESSIZCE bastiriliyor. createOneShot ise acik
        /// sure+genlik verdigi icin izin varsa her zaman titriyor.
        ///
        /// Bu sinif iki kez "hicbir sey hissedilmiyor" diye geri dondu; bu
        /// noktada guvenilirlik, incelige tercih ediliyor. Kalibre efektlerin
        /// dokusunu geri istersen asagidaki iki blogun yerini degistirmen yeterli.
        /// </summary>
        static void AndroidVibrate(int predefinedEffect, long milliseconds, int amplitude)
        {
            try
            {
                Resolve();

                if (_vibrator == null)
                {
                    // SON CARE: Unity'nin yerlesik titresimi.
                    //
                    // Handheld.Vibrate, Unity'nin KENDI Java kodundan geciyor;
                    // benim Context bulma zincirime, VibratorManager'a, hicbirine
                    // bagli degil. Yani ozel JNI yolu neden basarisiz olursa
                    // olsun bu calisir — telefon titreyebiliyorsa titrer.
                    //
                    // Neden birincil degil: kaba bir ~250 ms tam cihaz sarsintisi,
                    // siddeti ayarlanamiyor. Her tiklamada bu olsa oyun kullanilamaz
                    // hale gelirdi; o yuzden yalnizca iyi yol YOKKEN devreye giriyor
                    // ve kendi uzun bekleme suresiyle sinirlandiriliyor.
                    FallbackVibrate();
                    return;
                }

                if (_supportsAmplitude && _effectClass != null)
                {
                    using (AndroidJavaObject effect =
                           _effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                    return;
                }

                if (_supportsPredefined && _effectClass != null)
                {
                    using (AndroidJavaObject effect =
                           _effectClass.CallStatic<AndroidJavaObject>("createPredefined", predefinedEffect))
                    {
                        _vibrator.Call("vibrate", effect);
                    }
                    return;
                }

                _vibrator.Call("vibrate", milliseconds);
            }
            catch (System.Exception e)
            {
                Diagnostics = "vibrate hatasi: " + e.Message;
            }
        }

        /// <summary>
        /// Titresim servisini alabilecegimiz bir Android Context bulur.
        ///
        /// BU, TITRESIMIN CALISMAMASININ IKINCI SEBEBIYDI.
        ///
        /// Eski kod dogrudan <c>com.unity3d.player.UnityPlayer.currentActivity</c>
        /// okuyordu. O statik alani KLASIK UnityPlayerActivity dolduruyor; bu
        /// proje ise GameActivity kullaniyor (ProjectSettings'te
        /// androidApplicationEntry = 2, manifest'teki launcher
        /// com.unity3d.player.UnityPlayerGameActivity). GameActivity altinda
        /// currentActivity dolmuyor: cagri ya null donuyor ya istisna atiyor,
        /// disaridaki catch onu yutuyor ve _vibrator null kaliyordu. Sonuc yine
        /// "titresim yok, hata da yok".
        ///
        /// Uc kaynak sirayla deneniyor; her biri kendi try'inda, biri patlayinca
        /// zincir devam ediyor:
        ///   1. UnityPlayer.currentActivity  — klasik Activity kurulumu
        ///   2. UnityPlayer.currentContext   — Unity 6'nin GameActivity yolu
        ///   3. ActivityThread.currentApplication() — Application context;
        ///      Unity'nin activity modelinden TAMAMEN bagimsiz calisir.
        /// getSystemService her Context uzerinde bulundugu icin ucu de yeterli.
        /// </summary>
        static AndroidJavaObject ResolveContext()
        {
            AndroidJavaObject ctx = TryUnityPlayerStatic("currentActivity");
            if (ctx != null) { ContextSource = "UnityPlayer.currentActivity"; return ctx; }

            ctx = TryUnityPlayerStatic("currentContext");
            if (ctx != null) { ContextSource = "UnityPlayer.currentContext"; return ctx; }

            try
            {
                using (var activityThread = new AndroidJavaClass("android.app.ActivityThread"))
                {
                    ctx = activityThread.CallStatic<AndroidJavaObject>("currentApplication");
                    if (ctx != null) { ContextSource = "ActivityThread.currentApplication"; return ctx; }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[HapticManager] ActivityThread yolu basarisiz: " + e.Message);
            }

            ContextSource = "yok";
            return null;
        }

        static AndroidJavaObject TryUnityPlayerStatic(string fieldName)
        {
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    return unityPlayer.GetStatic<AndroidJavaObject>(fieldName);
                }
            }
            catch (System.Exception)
            {
                // GameActivity'de currentActivity yok — beklenen durum, sessiz gec.
                return null;
            }
        }

        /// <summary>
        /// Yerlesik Unity titresimi. Kaba ve suresi sabit oldugu icin kendi
        /// (uzun) bekleme suresi var: hizli tiklamada telefon kesintisiz
        /// sarsilmasin.
        /// </summary>
        const float FallbackMinInterval = 0.35f;
        static float _lastFallbackTime = -999f;
        static int _fallbackCount;

        static void FallbackVibrate()
        {
            if (Time.unscaledTime - _lastFallbackTime < FallbackMinInterval) return;
            _lastFallbackTime = Time.unscaledTime;

            _fallbackCount++;
            Diagnostics = "YEDEK YOL (Handheld.Vibrate) x" + _fallbackCount
                        + " — ctx=" + ContextSource;

            Handheld.Vibrate();
        }

        static void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            try
            {
                int sdk;
                using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    sdk = versionClass.GetStatic<int>("SDK_INT");
                }

                using (AndroidJavaObject context = ResolveContext())
                {
                    if (context == null)
                    {
                        Diagnostics = "context yok";
                        Debug.LogWarning("[HapticManager] Android Context bulunamadi; titresim devre disi.");
                        return;
                    }

                    // API 31'den itibaren dogru yol VibratorManager. Eski
                    // getSystemService("vibrator") hala calisiyor ama bazi
                    // Android 12+ ROM'larinda bos bir sarmalayici donduruyor —
                    // titresimin "hic olmamasinin" sebeplerinden biri buydu.
                    if (sdk >= 31)
                    {
                        using (AndroidJavaObject manager =
                               context.Call<AndroidJavaObject>("getSystemService", "vibrator_manager"))
                        {
                            if (manager != null)
                                _vibrator = manager.Call<AndroidJavaObject>("getDefaultVibrator");
                        }
                    }

                    if (_vibrator == null)
                        _vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (_vibrator == null)
                {
                    Diagnostics = "sdk=" + sdk + " ctx=" + ContextSource + " vibrator=YOK";
                    Debug.LogWarning("[HapticManager] " + Diagnostics);
                    return;
                }

                // Titresim motoru olmayan cihazda hic ugrasma.
                if (!_vibrator.Call<bool>("hasVibrator"))
                {
                    _vibrator = null;
                    Diagnostics = "sdk=" + sdk + " ctx=" + ContextSource + " hasVibrator=false (donanim yok)";
                    Debug.LogWarning("[HapticManager] " + Diagnostics);
                    return;
                }

                if (sdk >= 26)
                {
                    _effectClass = new AndroidJavaClass("android.os.VibrationEffect");
                    _supportsAmplitude = _vibrator.Call<bool>("hasAmplitudeControl");
                    _supportsPredefined = sdk >= 29;
                }

                // Bu satir cihazda logcat'ten okunabiliyor. Titresim yine
                // calismazsa tahmin yerine buradaki degerlere bakilir:
                //   adb logcat -s Unity | findstr HapticManager
                Diagnostics = "sdk=" + sdk + " ctx=" + ContextSource
                            + " genlik=" + _supportsAmplitude
                            + " kalibreEfekt=" + _supportsPredefined;
                Debug.Log("[HapticManager] hazir — " + Diagnostics);
            }
            catch (System.Exception e)
            {
                _vibrator = null;
                _effectClass = null;
                _supportsAmplitude = false;
                _supportsPredefined = false;

                // Sessizce yutmak bu sinifi iki kez teshis edilemez hale
                // getirdi; artik sebep en azindan logcat'e dusuyor.
                Diagnostics = "istisna: " + e.Message;
                Debug.LogWarning("[HapticManager] kurulum basarisiz — " + Diagnostics);
            }
        }
#endif
    }
}
