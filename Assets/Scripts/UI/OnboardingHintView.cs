using SpeedDownload.Core;
using TMPro;
using UnityEngine;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Ilk oyunda "dokun" yonlendirmesi.
    ///
    /// Oyun ana menu olmadan dogrudan kadranla basliyor (PLAN Bolum 26). Bir
    /// mobil oyuncu ne yapacagini birkac saniyede anlamazsa uygulamayi kapatiyor;
    /// bu yuzden ilk dokunusa kadar ekranda nabiz gibi atan bir ipucu duruyor.
    ///
    /// Bir kez tamamlandiktan sonra PlayerPrefs'e yaziliyor ve bir daha cikmiyor.
    /// Kayit dosyasinda degil: prestij veya "kaydi sil" bunu sifirlamamali —
    /// oyuncu oynamayi zaten ogrendi.
    /// </summary>
    public class OnboardingHintView : MonoBehaviour
    {
        public const string SeenPrefKey = "sd_onboarding_seen";

        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI label;

        [Tooltip("Ipucunun kaybolmasi icin gereken tiklama sayisi.")]
        [SerializeField] int clicksToDismiss = 3;

        [SerializeField] float pulseSpeed = 2.2f;
        [SerializeField] float fadeOutDuration = 0.4f;

        int _clicks;
        bool _dismissing;
        float _alpha = 1f;
        SpeedController _speed;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { Deactivate(); return; }

            // Daha once gorulduyse hic gosterme.
            if (PlayerPrefs.GetInt(SeenPrefKey, 0) != 0) { Deactivate(); return; }

            _speed = gm.Speed;
            if (_speed == null) { Deactivate(); return; }

            _speed.Clicked += OnClicked;
            RefreshLabel();

            LocalizationManager.LanguageChanged += RefreshLabel;
        }

        void OnDestroy()
        {
            if (_speed != null) _speed.Clicked -= OnClicked;
            LocalizationManager.LanguageChanged -= RefreshLabel;
        }

        void RefreshLabel()
        {
            if (label != null)
                label.SetText(LocalizationManager.T("onboarding_tap", "TAP TO SPEED UP"));
        }

        void OnClicked()
        {
            if (_dismissing) return;

            _clicks++;
            if (_clicks < clicksToDismiss) return;

            _dismissing = true;
            PlayerPrefs.SetInt(SeenPrefKey, 1);
            PlayerPrefs.Save();
        }

        void Update()
        {
            if (group == null) return;

            if (_dismissing)
            {
                float step = fadeOutDuration <= 0f ? 1f : Time.deltaTime / fadeOutDuration;
                _alpha = Mathf.MoveTowards(_alpha, 0f, step);
                group.alpha = _alpha;

                if (_alpha <= 0f) Deactivate();
                return;
            }

            // Nabiz: dikkat cekmeli ama okumayi zorlastirmamali.
            group.alpha = Mathf.Lerp(0.45f, 1f, Mathf.PingPong(Time.time * pulseSpeed, 1f));
        }

        void Deactivate()
        {
            if (group != null) group.alpha = 0f;
            gameObject.SetActive(false);
        }

        /// <summary>SceneBuilder icin.</summary>
        public void Bind(CanvasGroup canvasGroup, TextMeshProUGUI text)
        {
            group = canvasGroup;
            label = text;
        }
    }
}
