using SpeedDownload.Core;
using SpeedDownload.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Oda arka planini kademeye gore degistirir.
    ///
    /// Arka planlar 16:9 (1024x576) ama ekran 9:16 portre. "Cover" davranisi
    /// icin goruntu ekran yuksekligine gore olceklenip yanlardan kirpilir —
    /// preserveAspect kullanilsaydi letterbox olurdu.
    ///
    /// Kademe 3 ve 5'in kendi odasi olmadigi icin komsu oda roomTint ile ayrisir.
    /// </summary>
    public class RoomBackgroundView : MonoBehaviour
    {
        [SerializeField] Image image;
        [SerializeField] float sourceAspect = 1024f / 576f;
        [SerializeField] float referenceHeight = 1920f;
        [SerializeField] float fadeDuration = 0.35f;

        ConnectionTierSO _tier;
        Color _targetTint = Color.white;
        Color _fadeFrom = Color.white;
        float _fadeTimer = -1f;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { enabled = false; return; }

            ResizeToCover();
            gm.TierChanged += ApplyTier;
            ApplyTier(gm.CurrentTier);
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TierChanged -= ApplyTier;
        }

        void ResizeToCover()
        {
            if (image == null) return;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(referenceHeight * sourceAspect, referenceHeight);
        }

        public void ApplyTier(ConnectionTierSO tier)
        {
            if (tier == null || image == null) return;

            bool spriteChanged = _tier == null || _tier.roomBackground != tier.roomBackground;
            _tier = tier;

            if (tier.roomBackground != null) image.sprite = tier.roomBackground;

            _targetTint = tier.roomTint;
            _fadeFrom = image.color;
            _fadeTimer = 0f;

            // Ayni oda tekrar kullaniliyorsa (kademe 2->3, 4->5) tint gecisi
            // zaten yeterli; sprite degistiyse de kisa bir fade yumusatir.
            if (!spriteChanged && _fadeFrom == _targetTint) _fadeTimer = -1f;
        }

        void Update()
        {
            if (_fadeTimer < 0f || image == null) return;

            _fadeTimer += Time.deltaTime;
            float k = fadeDuration <= 0f ? 1f : Mathf.Clamp01(_fadeTimer / fadeDuration);
            image.color = Color.Lerp(_fadeFrom, _targetTint, k);

            if (k >= 1f) _fadeTimer = -1f;
        }

        public void Bind(Image target)
        {
            image = target;
        }
    }
}
