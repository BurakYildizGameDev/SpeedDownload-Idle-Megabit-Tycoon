using System.Text;
using SpeedDownload.Core;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// "Yokken de indirdik" raporu (GDD Bolum 6.3).
    ///
    /// Yalnizca gercekten para kazanildiysa acilir. Download Manager
    /// alinmamissa hic gosterilmez — her acilista "0 kazandin" demek
    /// bilgi degil, gurultu olurdu.
    /// </summary>
    public class OfflineReportView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI bodyText;
        [SerializeField] TextMeshProUGUI amountText;
        [SerializeField] Button closeButton;

        [Header("Odullu reklam — kazanci ikiye katla")]
        [SerializeField] Button doubleButton;
        [SerializeField] TextMeshProUGUI doubleLabel;

        [SerializeField] float fadeDuration = 0.25f;

        /// <summary>Bu panelde gosterilen kazanc — reklam odulu bunun kadarini EKLER.</summary>
        double _earnings;

        /// <summary>Bu acilista reklam odulu zaten alindi mi?</summary>
        bool _doubled;

        readonly StringBuilder _sb = new StringBuilder(160);
        float _fade;
        bool _visible;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (doubleButton != null) doubleButton.onClick.AddListener(OnDoubleClicked);
            ApplyAlpha(0f);
        }

        void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
            if (doubleButton != null) doubleButton.onClick.RemoveListener(OnDoubleClicked);

            SaveManager save = GameManager.Instance != null ? GameManager.Instance.Save : null;
            if (save != null) save.Loaded -= OnLoaded;
        }

        void Start()
        {
            GameManager gm = GameManager.Instance;
            SaveManager save = gm != null ? gm.Save : null;

            if (save == null) { ApplyAlpha(0f); return; }

            // SaveManager'in calisma sirasi geride (100), bizimki varsayilan (0):
            // Loaded olayi biz abone olmadan once tetiklenmis olamaz.
            save.Loaded += OnLoaded;
        }

        void OnLoaded(OfflineResult result)
        {
            if (!result.HasEarnings) { ApplyAlpha(0f); return; }
            Show(result);
        }

        public void Show(OfflineResult result)
        {
            _earnings = result.earnings;
            _doubled = false;
            RefreshDoubleButton();

            if (titleText != null)
                titleText.SetText(LocalizationManager.T("offline_report_title", "WELCOME BACK"));

            _sb.Length = 0;
            _sb.Append(LocalizationManager.TF("offline_away", "You were away for {0}.",
                                              NumberFormatter.FormatDuration(result.elapsedSeconds)))
               .Append('\n');

            if (result.WasCapped)
            {
                _sb.Append(LocalizationManager.TF(
                    "offline_capped", "Your line downloaded for {0} (cap reached).",
                    NumberFormatter.FormatDuration(result.creditedSeconds)));
            }
            else
            {
                _sb.Append(LocalizationManager.T(
                    "offline_running",
                    "Your line kept downloading in the background."));
            }

            _sb.Append("\n<size=85%>").Append(BuildStatusLine(result)).Append("</size>");

            if (bodyText != null) bodyText.SetText(_sb);
            if (amountText != null) amountText.SetText("+" + NumberFormatter.FormatMoney(result.earnings));

            _visible = true;
            SetInteractable(true);
        }

        /// <summary>
        /// "Su an neredesin, bir sonraki adim ne?" satiri.
        ///
        /// Offline kazanc artik uc kademeli (taban / Download Manager / Pro).
        /// Oyuncunun hangi kademede oldugunu ve yukseltirse ne kazanacagini
        /// GORMESI, yukseltmeyi anlamli kilan tek sey — eski surumde kazanc
        /// yoksa pencere hic acilmadigi icin sistemin varligindan bile
        /// haberdar olunmuyordu.
        /// </summary>
        string BuildStatusLine(OfflineResult result)
        {
            int eff = Mathf.RoundToInt((float)(result.efficiency * 100.0));
            int cap = Mathf.RoundToInt((float)result.capHours);

            GameManager gm = GameManager.Instance;
            Data.GameConfigSO cfg = gm != null ? gm.Config : null;

            if (result.managerLevel >= 2)
            {
                return LocalizationManager.TF("offline_pro_active",
                    "Pro: {0}% efficiency, {1}h cap.", eff, cap);
            }

            if (result.managerLevel == 1)
            {
                int proEff = cfg != null ? Mathf.RoundToInt(cfg.offlineEfficiencyPro * 100f) : 100;
                int proCap = cfg != null ? Mathf.RoundToInt(cfg.offlineCapHoursPro) : 24;

                return LocalizationManager.TF("offline_dm_hint",
                           "Download Manager: {0}% efficiency, {1}h cap.", eff, cap)
                     + "\n"
                     + LocalizationManager.TF("offline_pro_hint",
                           "Upgrade to Pro: {0}% efficiency, {1}h cap.", proEff, proCap);
            }

            int dmEff = cfg != null ? Mathf.RoundToInt(cfg.offlineEfficiency * 100f) : 50;
            int dmCap = cfg != null ? Mathf.RoundToInt(cfg.offlineCapHours) : 8;

            return LocalizationManager.TF("offline_basic_hint",
                       "Basic line: {0}% efficiency, {1}h cap.", eff, cap)
                 + "\n"
                 + LocalizationManager.TF("offline_dm_hint",
                       "Download Manager: {0}% efficiency, {1}h cap.", dmEff, dmCap);
        }

        // ------------------------------------------------------------------
        // Odullu reklam: kazanci ikiye katla
        //
        // Bu, oyunu bozmayan yerlestirmenin ders kitabi ornegi: oyuncu zaten
        // paneli okumak icin durmus, hicbir sey kesilmiyor ve basmamak da
        // tamamen gecerli bir secim. Odul de anlasilir — ekrandaki sayi iki
        // katina cikiyor.
        // ------------------------------------------------------------------

        void OnDoubleClicked()
        {
            if (_doubled || _earnings <= 0.0) return;

            GameManager gm = GameManager.Instance;
            if (gm == null || gm.Ads == null) return;
            if (!gm.Ads.IsRewardedReady || gm.Ads.IsShowing) return;

            gm.Ads.ShowRewarded(AdPlacement.OfflineDouble, GrantDouble, null);
        }

        void GrantDouble()
        {
            // Cift tetiklemeye karsi: odul yalnizca bir kez.
            if (_doubled || _earnings <= 0.0) return;
            _doubled = true;

            GameManager gm = GameManager.Instance;
            if (gm != null && gm.Money != null) gm.Money.Add(_earnings);
            if (gm != null && gm.Stats != null) gm.Stats.Add(StatType.AdsWatched);

            // Panel artik TOPLAM kazanci gostersin — odulun geldigi gorulmeli.
            _earnings *= 2.0;
            if (amountText != null)
                amountText.SetText("+" + NumberFormatter.FormatMoney(_earnings));

            if (gm != null && gm.Audio != null) gm.Audio.PlayReward();
            HapticManager.MediumImpact();

            RefreshDoubleButton();
        }

        /// <summary>
        /// Dugme yalnizca gercekten ise yarayacaksa gorunur: kazanc varsa,
        /// reklam hazirsa ve bu acilista henuz alinmadiysa.
        /// </summary>
        void RefreshDoubleButton()
        {
            if (doubleButton == null) return;

            GameManager gm = GameManager.Instance;
            bool adsReady = gm != null && gm.Ads != null && gm.Ads.IsRewardedReady;
            bool show = adsReady && !_doubled && _earnings > 0.0;

            doubleButton.gameObject.SetActive(show);

            if (show && doubleLabel != null)
                doubleLabel.SetText(LocalizationManager.T("offline_double", "WATCH AD  ·  2x"));
        }

        public void Hide()
        {
            _visible = false;
            SetInteractable(false);
        }

        void Update()
        {
            if (group == null) return;

            float target = _visible ? 1f : 0f;
            if (Mathf.Approximately(_fade, target)) return;

            float step = fadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / fadeDuration;
            _fade = Mathf.MoveTowards(_fade, target, step);
            group.alpha = _fade;
        }

        void ApplyAlpha(float a)
        {
            _fade = a;
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

        public void Bind(CanvasGroup canvasGroup, TextMeshProUGUI title,
                         TextMeshProUGUI body, TextMeshProUGUI amount, Button close,
                         Button doubleBtn = null, TextMeshProUGUI doubleBtnLabel = null)
        {
            group = canvasGroup;
            titleText = title;
            bodyText = body;
            amountText = amount;
            closeButton = close;
            doubleButton = doubleBtn;
            doubleLabel = doubleBtnLabel;
        }
    }
}
