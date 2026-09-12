using System.Text;
using SpeedDownload.Core;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// "Hat Degisimi" ekrani (GDD Bolum 7). Alt panelin 4. sekmesi.
    ///
    /// Yukseltme listesiyle ayni alani paylasir; sekme secildiginde biri acilir
    /// digeri kapanir.
    /// </summary>
    public class PrestigePanelView : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI creditsText;
        [SerializeField] TextMeshProUGUI multiplierText;
        [SerializeField] TextMeshProUGUI pendingText;
        [SerializeField] TextMeshProUGUI explainText;
        [SerializeField] Button prestigeButton;
        [SerializeField] TextMeshProUGUI buttonLabel;

        [Tooltip("Saniyede kac kez tazelensin — bekleyen kredi toplam kazancla degisiyor.")]
        [SerializeField] float refreshRate = 4f;

        readonly StringBuilder _sb = new StringBuilder(220);
        PrestigeManager _prestige;
        GameManager _gm;
        float _timer;

        void Awake()
        {
            if (prestigeButton != null) prestigeButton.onClick.AddListener(OnPrestigeClicked);
        }

        void OnDestroy()
        {
            if (prestigeButton != null) prestigeButton.onClick.RemoveListener(OnPrestigeClicked);
            if (_prestige != null) _prestige.Changed -= Refresh;
        }

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) { enabled = false; return; }

            _prestige = _gm.Prestige;
            if (_prestige != null) _prestige.Changed += Refresh;

            Refresh();
        }

        void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1f / Mathf.Max(1f, refreshRate);

            Refresh();
        }

        void OnPrestigeClicked()
        {
            if (_prestige == null) return;
            _prestige.DoPrestige();
            Refresh();
        }

        public void Refresh()
        {
            if (_prestige == null || _gm == null) return;

            int pending = _prestige.PendingCredits;
            bool can = _prestige.CanPrestige;

            // Para birimi de cevriliyor: Turkcede "Fiber Kredisi" -> FK.
            string unit = LocalizationManager.T("prestige_unit", "FP");

            if (creditsText != null)
                creditsText.SetText(_prestige.FiberCredits + " " + unit);

            if (multiplierText != null)
            {
                multiplierText.SetText(
                    NumberFormatter.FormatMultiplier(_prestige.Multiplier) + " " +
                    LocalizationManager.T("prestige_boost_label", "permanent boost"));
            }

            if (pendingText != null)
            {
                _sb.Length = 0;
                _sb.Append(LocalizationManager.T("prestige_pending_prefix", "Format now: +"))
                   .Append(pending)
                   .Append(' ')
                   .Append(unit);
                pendingText.SetText(_sb);
            }

            if (explainText != null)
            {
                _sb.Length = 0;

                if (!_prestige.IsUnlocked)
                {
                    int required = _gm.Config != null ? _gm.Config.prestigeUnlockTier : 8;
                    _sb.Append(LocalizationManager.TF("prestige_locked_info",
                        "Format unlocks at Tier {0}.\nCurrent Tier: {1}.",
                        required, _gm.CurrentTierIndex));
                }
                else if (pending <= 0)
                {
                    double threshold = _gm.Config != null ? _gm.Config.prestigeThreshold : 1e9;
                    _sb.Append(LocalizationManager.TF("prestige_threshold_info",
                        "Requires at least {0} lifetime earnings.\nCurrent: {1}",
                        NumberFormatter.FormatMoney(threshold),
                        NumberFormatter.FormatMoney(_gm.Money != null ? _gm.Money.LifetimeEarnings : 0.0)));
                }
                else
                {
                    _sb.Append(LocalizationManager.TF("prestige_ready_info",
                        "<b>Resets:</b> balance, upgrades, tier.\n<b>Retains:</b> {1} and its permanent bonus.\n\nNew multiplier: {0}",
                        NumberFormatter.FormatMultiplier(MultiplierAfter(pending)), unit));
                }

                explainText.SetText(_sb);
            }

            if (buttonLabel != null)
            {
                buttonLabel.SetText(can
                    ? LocalizationManager.T("prestige_button_ready", "FORMAT SYSTEM")
                    : LocalizationManager.T("prestige_button_not_ready", "NOT YET"));
            }

            if (prestigeButton != null) prestigeButton.interactable = can;
        }

        double MultiplierAfter(int extraCredits)
        {
            float perCredit = _gm.Config != null ? _gm.Config.prestigeCreditMultiplier : 0.02f;
            return 1.0 + perCredit * (_prestige.FiberCredits + extraCredits);
        }

        public void Bind(TextMeshProUGUI credits, TextMeshProUGUI multiplier, TextMeshProUGUI pending,
                         TextMeshProUGUI explain, Button button, TextMeshProUGUI label)
        {
            creditsText = credits;
            multiplierText = multiplier;
            pendingText = pending;
            explainText = explain;
            prestigeButton = button;
            buttonLabel = label;
        }
    }
}
