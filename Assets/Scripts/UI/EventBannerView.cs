using System.Text;
using SpeedDownload.Core;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Aktif olayin banneri (GDD Bolum 8). Ustten kayarak girer.
    ///
    /// Uc olay tek banner'i paylasiyor: yalnizca Gece Tarifesi'nin kendi arka
    /// plan gorseli var (event_happyhour_banner), digerleri panel zeminini
    /// kullaniyor. Aksiyon butonu yalnizca cozulebilir olaylarda gorunur.
    /// </summary>
    public class EventBannerView : MonoBehaviour
    {
        [SerializeField] RectTransform slider;
        [SerializeField] CanvasGroup group;
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI bodyText;
        [SerializeField] Button actionButton;
        [SerializeField] TextMeshProUGUI actionLabel;

        [Tooltip("Yalnizca Kota olayinda gorunur: odullu reklamla kotayi acar.")]
        [SerializeField] Button adButton;

        [Header("Gorseller")]
        [SerializeField] Sprite happyHourBackground;
        [SerializeField] Sprite panelBackground;
        [SerializeField] Sprite wifiIcon;
        [SerializeField] Sprite passwordIcon;
        [SerializeField] Sprite quotaIcon;

        [Header("Kayma")]
        [Tooltip("Gizliyken kac piksel yukarida dursun. Acik konum SceneBuilder'in " +
                 "yerlestirdigi yerden okunur — iki tarafta ayri sabit tutulmaz.")]
        [SerializeField] float slideOffset = 70f;
        [SerializeField] float slideDuration = 0.28f;

        float _shownY;
        float _hiddenY;

        readonly StringBuilder _sb = new StringBuilder(140);
        GameEventManager _events;
        GameManager _gm;
        float _t;
        bool _visible;

        void Awake()
        {
            if (actionButton != null) actionButton.onClick.AddListener(OnAction);
            if (adButton != null) adButton.onClick.AddListener(OnWatchAd);

            _shownY = slider != null ? slider.anchoredPosition.y : 0f;
            _hiddenY = _shownY + slideOffset;

            ApplyOffset(0f);
        }

        void OnWatchAd()
        {
            if (_events == null || _events.Active != GameEventType.QuotaExceeded) return;

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.Ads == null || !gm.Ads.IsRewardedReady) return;

            gm.Ads.ShowRewarded(AdPlacement.QuotaClear, OnAdRewarded, null);
        }

        void OnAdRewarded()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Stats != null) gm.Stats.Add(StatType.AdsWatched);

            if (_events != null) _events.ClearQuotaByAd();
        }

        void OnDestroy()
        {
            if (adButton != null) adButton.onClick.RemoveListener(OnWatchAd);
            if (actionButton != null) actionButton.onClick.RemoveListener(OnAction);

            if (_events != null)
            {
                _events.EventStarted -= OnEventStarted;
                _events.EventEnded -= OnEventEnded;
                _events.EventProgress -= Refresh;
            }

            LocalizationManager.LanguageChanged -= Refresh;
        }

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) { enabled = false; return; }

            _events = _gm.Events;
            if (_events == null) { enabled = false; return; }

            _events.EventStarted += OnEventStarted;
            _events.EventEnded += OnEventEnded;
            _events.EventProgress += Refresh;

            // Banner ekrandayken dil degistirilirse eski dilde kalmasin.
            LocalizationManager.LanguageChanged += Refresh;

            // Kayittan gelen bir olay Start'tan once baslamis olabilir.
            if (_events.HasActiveEvent) OnEventStarted(_events.Active);
        }

        void OnEventStarted(GameEventType type)
        {
            _visible = true;
            _shownSeconds = -1;
            Refresh();
        }

        void OnEventEnded(GameEventType type)
        {
            _visible = false;
        }

        void OnAction()
        {
            if (_events == null) return;

            switch (_events.Active)
            {
                case GameEventType.NeighborWifi: _events.TapPassword(); break;
                case GameEventType.QuotaExceeded: _events.PayQuotaFee(); break;
            }

            Refresh();
        }

        int _shownSeconds = -1;

        void Update()
        {
            // Gece Tarifesi'nin geri sayimi. Ekranda YALNIZCA tam saniye
            // gorunuyor, ama eski kod her karede Refresh cagiriyordu: saniyede
            // 60 kez ayni metin yeniden yaziliyor ve TMP her seferinde mesh'i
            // yeniden oruyordu. Artik yalnizca gosterilen saniye degisince.
            if (_visible && _events != null && _events.Active == GameEventType.HappyHour)
            {
                int secs = Mathf.CeilToInt(Mathf.Max(0f, _events.Remaining));
                if (secs != _shownSeconds)
                {
                    _shownSeconds = secs;
                    Refresh();
                }
            }

            float target = _visible ? 1f : 0f;
            if (!Mathf.Approximately(_t, target))
            {
                float step = slideDuration <= 0f ? 1f : Time.deltaTime / slideDuration;
                _t = Mathf.MoveTowards(_t, target, step);
                ApplyOffset(_t);
            }
        }

        void ApplyOffset(float t)
        {
            _t = t;

            if (slider != null)
            {
                Vector2 pos = slider.anchoredPosition;
                pos.y = Mathf.Lerp(_hiddenY, _shownY, Mathf.SmoothStep(0f, 1f, t));
                slider.anchoredPosition = pos;
            }

            if (group != null)
            {
                group.alpha = t;
                group.blocksRaycasts = t > 0.9f;
                group.interactable = t > 0.9f;
            }
        }

        public void Refresh()
        {
            if (_events == null || !_events.HasActiveEvent) return;

            bool showAction = true;

            // Reklam yolu yalnizca Kota'da anlamli.
            if (adButton != null)
                adButton.gameObject.SetActive(_events.Active == GameEventType.QuotaExceeded);

            switch (_events.Active)
            {
                case GameEventType.NeighborWifi:
                {
                    SetVisuals(panelBackground, wifiIcon);

                    string drop = NumberFormatter.FormatPercent(
                        1.0 - (_gm.Config != null ? _gm.Config.wifiStealFactor : 0.6f));

                    SetText(LocalizationManager.T("event_wifi_title", "NEIGHBOR STOLE WI-FI"),
                            LocalizationManager.TF("event_wifi_body",
                                                   "Your speed dropped by {0}.", drop));

                    SetAction(LocalizationManager.TF("event_wifi_action",
                                                     "CHANGE PASSWORD ({0})",
                                                     _events.RemainingTaps),
                              true, passwordIcon);
                    break;
                }

                case GameEventType.QuotaExceeded:
                {
                    SetVisuals(panelBackground, quotaIcon);

                    SetText(LocalizationManager.T("event_quota_title", "DATA CAP EXCEEDED"),
                            LocalizationManager.T("event_quota_body",
                                                  "Speed throttled to base level."));

                    bool affordable = _gm.Money != null && _gm.Money.CanAfford(_events.QuotaFee);
                    SetAction(LocalizationManager.TF("event_quota_action", "LIFT CAP  {0}",
                                                     NumberFormatter.FormatMoney(_events.QuotaFee)),
                              affordable, null);
                    break;
                }

                case GameEventType.HappyHour:
                {
                    SetVisuals(happyHourBackground, null);

                    string mult = NumberFormatter.FormatMultiplier(
                        _gm.Config != null ? _gm.Config.happyHourMultiplier : 3f);
                    int secs = Mathf.CeilToInt(Mathf.Max(0f, _events.Remaining));

                    SetText(LocalizationManager.T("event_happy_title", "NIGHT RATE"),
                            LocalizationManager.TF("event_happy_body",
                                                   "All earnings {0}  ·  {1}s", mult, secs));
                    showAction = false;
                    break;
                }
            }

            if (!showAction && actionButton != null) actionButton.gameObject.SetActive(false);
        }

        void SetVisuals(Sprite bg, Sprite iconSprite)
        {
            if (background != null && bg != null) background.sprite = bg;

            if (icon != null)
            {
                icon.sprite = iconSprite;
                icon.enabled = iconSprite != null;
            }
        }

        void SetText(string title, string body)
        {
            if (titleText != null) titleText.SetText(title);
            if (bodyText != null) bodyText.SetText(body);
        }

        void SetAction(string label, bool interactable, Sprite buttonIcon)
        {
            if (actionButton == null) return;

            actionButton.gameObject.SetActive(true);
            actionButton.interactable = interactable;

            if (actionLabel != null) actionLabel.SetText(label);
        }

        public void Bind(RectTransform sliderRect, CanvasGroup canvasGroup, Image bg, Image iconImage,
                         TextMeshProUGUI title, TextMeshProUGUI body,
                         Button button, TextMeshProUGUI label, Button watchAdButton,
                         Sprite happyBg, Sprite panelBg, Sprite wifi, Sprite password, Sprite quota)
        {
            adButton = watchAdButton;
            slider = sliderRect;
            group = canvasGroup;
            background = bg;
            icon = iconImage;
            titleText = title;
            bodyText = body;
            actionButton = button;
            actionLabel = label;
            happyHourBackground = happyBg;
            panelBackground = panelBg;
            wifiIcon = wifi;
            passwordIcon = password;
            quotaIcon = quota;
        }
    }
}
