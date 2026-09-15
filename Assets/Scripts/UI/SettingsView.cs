using SpeedDownload.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Ayarlar overlay'i: ses ac/kapa.
    ///
    /// Ses durumu PlayerPrefs'te; prestij veya kayit silme onu sifirlamamali,
    /// bu yuzden kayit dosyasinda degil.
    ///
    /// Ana menu yok — oyun dogrudan basliyor (bkz. PLAN Bolum 26), dolayisiyla
    /// buradan donulecek bir perde de yok.
    /// </summary>
    public class SettingsView : MonoBehaviour
    {
        public const string SoundPrefKey = "sd_sound_on";

        [SerializeField] CanvasGroup group;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button soundButton;
        [SerializeField] Image soundIcon;
        [SerializeField] Image hapticsIcon;

        [SerializeField] TMPro.TMP_Text titleText;
        [SerializeField] TMPro.TMP_Text soundLabelText;
        [SerializeField] Button languageButton;
        [SerializeField] Button hapticsButton;
        [SerializeField] TMPro.TMP_Text languageText;
        [SerializeField] TMPro.TMP_Text hapticsText;

        [SerializeField] Sprite soundOnSprite;
        [SerializeField] Sprite soundOffSprite;
        [SerializeField] Sprite hapticsOnSprite;
        [SerializeField] Sprite hapticsOffSprite;

        [SerializeField] float fadeDuration = 0.2f;

        float _alpha;
        bool _visible;

        public static bool SoundEnabled
        {
            get { return PlayerPrefs.GetInt(SoundPrefKey, 1) != 0; }
            set { PlayerPrefs.SetInt(SoundPrefKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (soundButton != null) soundButton.onClick.AddListener(ToggleSound);
            if (languageButton != null) languageButton.onClick.AddListener(ToggleLanguage);
            if (hapticsButton != null) hapticsButton.onClick.AddListener(ToggleHaptics);

            ApplyAlpha(0f);
            RefreshSoundIcon();
            RefreshHapticsIcon();
            RefreshStatusTexts();
        }

        void OnEnable()
        {
            // Panel kendi dil dugmesini tasiyor; kendi metinlerini de tazelemek
            // zorunda. Onceki surumde bu abonelik yoktu ve dil degistirildikten
            // sonra panel kapatilip acilana kadar eski dilde kaliyordu.
            LocalizationManager.LanguageChanged += RefreshStatusTexts;
            RefreshStatusTexts();
        }

        void OnDisable()
        {
            LocalizationManager.LanguageChanged -= RefreshStatusTexts;
        }

        void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
            if (soundButton != null) soundButton.onClick.RemoveListener(ToggleSound);
            if (languageButton != null) languageButton.onClick.RemoveListener(ToggleLanguage);
            if (hapticsButton != null) hapticsButton.onClick.RemoveListener(ToggleHaptics);
        }

        void ToggleLanguage()
        {
            if (LocalizationManager.Instance != null)
            {
                Language cur = LocalizationManager.Instance.CurrentLanguage;
                Language next = (Language)(((int)cur + 1) % LocalizationManager.LanguageCount);
                LocalizationManager.Instance.SetLanguage(next);
            }

            // SetLanguage zaten LanguageChanged yayinliyor ve tum ekran ona
            // abone; burada ayrica tazelemeye gerek yok.
            HapticManager.LightTap();
        }

        /// <summary>Hatirlatmanin ekranda kalacagi sure (sn).</summary>
        const float HintDuration = 6f;

        float _hintTimer;

        void ToggleHaptics()
        {
            HapticManager.IsHapticsEnabled = !HapticManager.IsHapticsEnabled;

            if (HapticManager.IsHapticsEnabled)
            {
                // Acildigi anda bir kez titret: ayarin gercekten calistigini
                // gostermenin tek yolu bu.
                HapticManager.MediumImpact();

                // Ve hatirlat: oyun titresim istese bile cihazin sistem
                // genelindeki dokunsal geri bildirimi kapaliysa hicbir sey
                // hissedilmez. Yukaridaki darbeyi hissetmeyen oyuncu, sorunun
                // oyunda olmadigini buradan ogreniyor.
                _hintTimer = HintDuration;
            }
            else
            {
                _hintTimer = 0f;
            }

            RefreshHapticsIcon();
            RefreshStatusTexts();
        }

        public void RefreshStatusTexts()
        {
            if (titleText != null)
                titleText.SetText(LocalizationManager.T("settings_title", "SETTINGS"));

            if (soundLabelText != null)
            {
                soundLabelText.SetText(LocalizationManager.T("settings_audio", "SOUND") +
                                       ": " + StateWord(SoundEnabled));
            }

            if (languageText != null)
            {
                // Dil adi her zaman KENDI dilinde yaziliyor: menuye bakan oyuncu
                // anlamadigi bir dilde bile kendi dilini tanir.
                Language lang = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.CurrentLanguage : Language.English;

                languageText.SetText(LocalizationManager.T("settings_language", "LANGUAGE") +
                                     ": " + LocalizationManager.NativeName(lang));
            }

            if (hapticsText != null)
            {
                string line = LocalizationManager.T("settings_haptics", "VIBRATION") +
                              ": " + StateWord(HapticManager.IsHapticsEnabled);

                // Hatirlatma ayni etiketin ikinci satirinda gosteriliyor: yeni
                // bir UI nesnesi eklemek sahneyi yeniden kurmayi gerektirirdi.
                if (_hintTimer > 0f)
                {
                    line += "\n<size=60%><color=#C8B48A>" +
                            LocalizationManager.T("haptics_hint",
                                "Feel nothing? Enable haptic feedback in your phone settings.") +
                            "</color></size>";
                }

                hapticsText.SetText(line);
            }
        }

        static string StateWord(bool on)
        {
            return on ? LocalizationManager.T("settings_on", "ON")
                      : LocalizationManager.T("settings_off", "OFF");
        }

        public void Open() { _visible = true; SetInteractable(true); }

        public void Close()
        {
            _visible = false;

            // Ayarlardan cikarken kaydet: oyuncu buradan uygulamayi kapatabilir.
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Save != null) gm.Save.Save();
        }

        void ToggleSound()
        {
            SoundEnabled = !SoundEnabled;
            RefreshSoundIcon();
            RefreshStatusTexts();

            AudioListener.volume = SoundEnabled ? 1f : 0f;
            HapticManager.LightTap();
        }

        void RefreshSoundIcon()
        {
            if (soundIcon == null) return;
            soundIcon.sprite = SoundEnabled ? soundOnSprite : soundOffSprite;
        }

        void RefreshHapticsIcon()
        {
            if (hapticsIcon == null) return;
            hapticsIcon.sprite = HapticManager.IsHapticsEnabled ? hapticsOnSprite : hapticsOffSprite;
        }

        void Start()
        {
            AudioListener.volume = SoundEnabled ? 1f : 0f;
        }

        void Update()
        {
            // Hatirlatmanin suresi dolunca etiketi bir kez temizle.
            if (_hintTimer > 0f)
            {
                _hintTimer -= Time.unscaledDeltaTime;
                if (_hintTimer <= 0f)
                {
                    _hintTimer = 0f;
                    RefreshStatusTexts();
                }
            }

            float target = _visible ? 1f : 0f;
            if (Mathf.Approximately(_alpha, target)) return;

            float step = fadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / fadeDuration;
            _alpha = Mathf.MoveTowards(_alpha, target, step);

            if (group != null) group.alpha = _alpha;
            SetInteractable(_alpha > 0.5f);
        }

        void ApplyAlpha(float a)
        {
            _alpha = a;
            _visible = a > 0.5f;
            if (group != null) group.alpha = a;
            SetInteractable(_visible);
        }

        void SetInteractable(bool on)
        {
            if (group == null) return;
            group.blocksRaycasts = on;
            group.interactable = on;
        }

        public void Bind(CanvasGroup canvasGroup, Button open, Button close, Button sound,
                         Image icon, Sprite onSprite, Sprite offSprite,
                         TMPro.TMP_Text titleTxt, TMPro.TMP_Text soundLbl,
                         Button langBtn, TMPro.TMP_Text langTxt,
                         Button hapticBtn, Image hapticIconImg, Sprite hapticOn, Sprite hapticOff, TMPro.TMP_Text hapticTxt)
        {
            group = canvasGroup;
            openButton = open;
            closeButton = close;
            soundButton = sound;
            soundIcon = icon;
            soundOnSprite = onSprite;
            soundOffSprite = offSprite;

            titleText = titleTxt;
            soundLabelText = soundLbl;

            languageButton = langBtn;
            languageText = langTxt;

            hapticsButton = hapticBtn;
            hapticsIcon = hapticIconImg;
            hapticsOnSprite = hapticOn;
            hapticsOffSprite = hapticOff;
            hapticsText = hapticTxt;

            RefreshSoundIcon();
            RefreshHapticsIcon();
            RefreshStatusTexts();
        }
    }
}
