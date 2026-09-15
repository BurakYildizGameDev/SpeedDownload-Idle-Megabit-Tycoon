using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Basit ac/kapa overlay davranisi: bir dugme acar, bir dugme kapatir,
    /// gecis alfa ile yumusatilir.
    ///
    /// SettingsView kendi ic mantigina sahip oldugu icin bu ayri bir sinif;
    /// Indirilenler Arsivi gibi "sadece gorunur/gizli" overlay'ler bunu kullanir.
    /// </summary>
    public class OverlayToggleView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] float fadeDuration = 0.18f;

        bool _visible;
        float _alpha;

        void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            Apply(0f);
        }

        void OnDestroy()
        {
            if (openButton != null) openButton.onClick.RemoveListener(Open);
            if (closeButton != null) closeButton.onClick.RemoveListener(Close);
        }

        public void Open() { _visible = true; SetInteractable(true); }

        public void Close() { _visible = false; }

        void Update()
        {
            float target = _visible ? 1f : 0f;
            if (Mathf.Approximately(_alpha, target)) return;

            float step = fadeDuration <= 0f ? 1f : Time.unscaledDeltaTime / fadeDuration;
            _alpha = Mathf.MoveTowards(_alpha, target, step);

            if (group != null) group.alpha = _alpha;
            SetInteractable(_alpha > 0.5f);
        }

        void Apply(float a)
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

        public void Bind(CanvasGroup canvasGroup, Button open, Button close)
        {
            group = canvasGroup;
            openButton = open;
            closeButton = close;
        }
    }
}
