using UnityEngine;
using UnityEngine.InputSystem;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Oyun baslatici: kare hizi, ekran uyanikligi ve pil/isi ile ilgili
    /// platform ayarlari.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class GameBootstrapper : MonoBehaviour
    {
        [Tooltip("Oyuncu ekranla ilgilenirken hedeflenen kare hizi.")]
        [SerializeField] int targetFrameRate = 60;

        [Tooltip("Uzun suredir HIC dokunulmadiginda dusulen kare hizi. Oyun " +
                 "cebe/masaya birakildiginda 60 kare cizmeye devam etmesin. " +
                 "Bu degeri targetFrameRate ile ayni yaparsan ozellik kapanir.")]
        [SerializeField] int idleFrameRate = 30;

        [Tooltip("Bu kadar saniye HIC dokunulmazsa bosta moduna gecilir. " +
                 "Aktif oynayista asla tetiklenmez: her dokunus sayaci sifirlar.")]
        [SerializeField] float idleAfterSeconds = 60f;

        [Tooltip("Son dokunustan sonra ekranin acik tutuldugu sure (sn). " +
                 "0 = sistem varsayilanina birak.")]
        [SerializeField] float keepScreenOnSeconds = 120f;

        [SerializeField] bool disableVSync = true;

        float _lastInputTime;
        bool _idleMode;
        bool _screenHeld;
        bool _screenStateKnown;

        void Awake()
        {
            if (disableVSync) QualitySettings.vSyncCount = 0;

            Application.targetFrameRate = targetFrameRate;

            // Arka planda calismak mobilde yalnizca pil yakar; kayit zaten
            // OnApplicationPause'da aliniyor ve offline kazanc gecen sureyi
            // kendisi hesapliyor.
            Application.runInBackground = false;

            _lastInputTime = Time.unscaledTime;
            ApplyScreenAwake(true);

            // Titresim altyapisini acilista cozumle. Tembel cozumlemede teshis
            // bilgisi ancak ilk dokunustan sonra doluyordu; ayarlar ekranindaki
            // durum satirinin ilk andan itibaren anlamli olmasi gerekiyor.
            HapticManager.Warmup();

            VerifyCrypto();
        }

        /// <summary>
        /// Kayit sifrelemesinin bu CIHAZDA calistigini acilista dogrular.
        ///
        /// NEDEN: Android derlemesi IL2CPP + "High" kod budama kullaniyor ve bu
        /// birlesim, yansima ile cozulen kripto tiplerini silebiliyor. Editorde
        /// budama olmadigi icin boyle bir hata gelistirme boyunca HIC gorunmez;
        /// yalnizca APK'da, oyuncunun kaydedilemeyen ilerlemesi olarak ortaya
        /// cikar — ve <see cref="SaveManager.Save"/> istisnayi yakalayip
        /// gunluge bir satir yazdigi icin oyuncu hicbir sey fark etmez.
        ///
        /// Bir gidis-donus denemesi bunu ilk saniyede ve acikca soyluyor.
        /// Maliyeti tek seferlik birkac on milisaniye.
        /// </summary>
        static void VerifyCrypto()
        {
            string detail;
            if (SaveCrypto.SelfTest(out detail)) return;

            Debug.LogError("[GameBootstrapper] KAYIT SIFRELEMESI CALISMIYOR: " + detail +
                           "\n  Muhtemel sebep: IL2CPP kod budamasi kripto tiplerini sildi." +
                           "\n  Bakilacak yer: Assets/link.xml ve SaveCrypto icindeki somut tip kullanimi.");
        }

        void Update()
        {
            if (HasInputThisFrame()) _lastInputTime = Time.unscaledTime;

            float since = Time.unscaledTime - _lastInputTime;

            // --- kare hizi ---
            bool shouldIdle = idleFrameRate > 0 && idleFrameRate < targetFrameRate &&
                              since >= idleAfterSeconds;
            if (shouldIdle != _idleMode)
            {
                _idleMode = shouldIdle;
                Application.targetFrameRate = shouldIdle ? idleFrameRate : targetFrameRate;
            }

            // --- ekran uyanikligi ---
            // Onceki surum ekrani KOSULSUZ acik tutuyordu (SleepTimeout.NeverSleep).
            // Telefonu isitan seylerin en buyugu ekranin kendisi; oyuncu oyunu
            // acik unuttugunda cihaz saatlerce yaniyordu. Artik ekran yalnizca
            // yakin zamanda dokunulduysa acik tutuluyor.
            bool hold = keepScreenOnSeconds <= 0f || since < keepScreenOnSeconds;
            ApplyScreenAwake(hold);
        }

        void ApplyScreenAwake(bool on)
        {
            if (_screenStateKnown && _screenHeld == on) return;

            _screenStateKnown = true;
            _screenHeld = on;
            Screen.sleepTimeout = on ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }

        /// <summary>
        /// Bu karede dokunma / tiklama var mi? Yalnizca "bir sey oluyor mu"
        /// sorusuna cevap ariyor, girdiyi islemiyor — onu ClickCatcher yapiyor.
        /// </summary>
        static bool HasInputThisFrame()
        {
            Touchscreen touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed) return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed) return true;

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.anyKey.isPressed) return true;

            return false;
        }

        void OnApplicationPause(bool paused)
        {
            // Geri donuste oyuncu kesin etkilesimde: tam hizla basla.
            if (!paused)
            {
                _lastInputTime = Time.unscaledTime;
                _idleMode = false;
                Application.targetFrameRate = targetFrameRate;
            }
        }
    }
}
