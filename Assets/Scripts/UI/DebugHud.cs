using System.Text;
using SpeedDownload.Core;
using SpeedDownload.Data;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Faz 2 gelistirme HUD'u. Gercek HUD Faz 3'te gelecek; bu panel kadran
    /// fiziginin sayisal olarak dogrulanmasi icin var.
    ///
    /// 1-9 tuslari kademe degistirir — dokuz kadranin ve ozellikle koyu
    /// zeminlerdeki ibre renginin tek oturumda kontrol edilmesini saglar.
    /// </summary>
    public class DebugHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] SpeedController speedController;

        [Tooltip("Panelin gorunurlugu. SetActive yerine alfa kullaniliyor ki " +
                 "panel kapaliyken de 1-9 kisayollari calissin.")]
        [SerializeField] CanvasGroup panelGroup;

        [SerializeField] bool visibleOnStart = false;

        [Tooltip("Saniyede kac kez guncellensin. Her frame string uretmek GC baskisi yaratir.")]
        [SerializeField] float refreshRate = 10f;

        readonly StringBuilder _sb = new StringBuilder(512);
        float _timer;

        static readonly Key[] TierKeys =
        {
            Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5,
            Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9
        };

        bool _visible;

        void Start()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (speedController == null && GameManager.Instance != null)
                speedController = GameManager.Instance.Speed;

            SetVisible(visibleOnStart);
#else
            // Yayin surumunde hata ayiklama paneli hic calismasin. Panel alfa 0
            // oldugu icin GORUNMUYORDU ama Update'i calismaya devam ediyor,
            // saniyede 10 kez ~500 karakterlik bir metin uretip TMP'ye
            // yaziyordu. TMP her SetText'te mesh'i yeniden orer ve Canvas'i
            // kirletir — yani gorunmeyen bir panel tum arayuzu saniyede 10 kez
            // yeniden cizdiriyordu.
            SetVisible(false);
            enabled = false;
#endif
        }

        void SetVisible(bool on)
        {
            _visible = on;
            if (panelGroup == null) return;
            panelGroup.alpha = on ? 1f : 0f;
        }

        void Update()
        {
            HandleTierHotkeys();

            Keyboard toggleKb = Keyboard.current;
            if (toggleKb != null && toggleKb.f1Key.wasPressedThisFrame)
                SetVisible(!_visible);

            // Gizliyken metin uretme: gorunmeyen bir yaziyi tazelemek yalnizca
            // pil ve kare suresi harciyor.
            if (!_visible) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1f / Mathf.Max(1f, refreshRate);

            Refresh();
        }

        void HandleTierHotkeys()
        {
            Keyboard kb = Keyboard.current;
            GameManager gm = GameManager.Instance;
            if (kb == null || gm == null) return;

            for (int i = 0; i < TierKeys.Length; i++)
            {
                if (kb[TierKeys[i]].wasPressedThisFrame)
                {
                    gm.SetTier(i);
                    Refresh();
                    return;
                }
            }
        }

        void Refresh()
        {
            if (label == null || speedController == null) return;

            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            ConnectionTierSO tier = gm.CurrentTier;
            GameConfigSO cfg = gm.Config;
            if (tier == null || cfg == null) return;

            _sb.Length = 0;

            _sb.Append("KADEME ").Append(tier.tierIndex).Append("  ").Append(tier.tierName).Append('\n');
            _sb.Append("aralik  ").Append(NumberFormatter.FormatSpeed(tier.minSpeed))
               .Append("  ->  ").Append(NumberFormatter.FormatSpeed(tier.maxSpeed)).Append('\n');
            _sb.Append('\n');

            _sb.Append("HIZ     ").Append(NumberFormatter.FormatSpeed(speedController.CurrentSpeed)).Append('\n');
            _sb.Append("t       ").Append(speedController.NormalizedT.ToString("0.000")).Append('\n');
            _sb.Append("etkin   ").Append(NumberFormatter.FormatSpeed(speedController.EffectiveSpeed)).Append('\n');
            _sb.Append('\n');

            _sb.Append("redline ").Append(NumberFormatter.FormatMultiplier(speedController.RedlineMultiplier));
            if (speedController.InRedline) _sb.Append("   <<< REDLINE");
            _sb.Append('\n');
            _sb.Append("  esik  t >= ").Append(cfg.redlineThreshold.ToString("0.00"))
               .Append("  (").Append(cfg.redlineBonusMode).Append(")\n");
            _sb.Append('\n');

            if (speedController.IsOverheated)
            {
                _sb.Append("!!! MODEM ASIRI ISINDI !!!\n");
                _sb.Append("  baglanti kopuk ")
                   .Append(speedController.OverheatRemaining.ToString("0.0")).Append(" sn\n");
            }
            else
            {
                _sb.Append("overheat ")
                   .Append(NumberFormatter.FormatPercent(speedController.OverheatProgress)).Append('\n');
                _sb.Append("  limit ")
                   .Append((cfg.overheatLimit + speedController.ExtraOverheatTolerance).ToString("0.0"))
                   .Append(" sn  (t >= ").Append(cfg.overheatThreshold.ToString("0.000")).Append(")\n");
            }

            _sb.Append('\n');
            _sb.Append("tiklama gucu ").Append(NumberFormatter.FormatSpeed(speedController.ClickPower)).Append('\n');
            _sb.Append("taban hiz    ").Append(NumberFormatter.FormatSpeed(speedController.BaseSpeed)).Append('\n');
            _sb.Append('\n');
            _sb.Append("<size=80%>ekrana tikla = hizlan\n1-9 = kademe  ·  F1 = bu paneli gizle</size>");

            label.SetText(_sb);
        }

        public void Bind(TextMeshProUGUI text, SpeedController controller, CanvasGroup group)
        {
            label = text;
            speedController = controller;
            panelGroup = group;
        }
    }
}
