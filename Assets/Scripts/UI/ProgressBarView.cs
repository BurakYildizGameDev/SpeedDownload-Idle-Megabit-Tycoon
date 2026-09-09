using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Cerceve + dolgu ikilisi (Asset_Integration_Guide Bolum 2).
    ///
    /// ui_progressbar_frame gercekten ici bos (%21,5 opak); dolgu cercevenin
    /// ICINE, olculen 21/22/11/16 px bosluklarla oturur. Cerceve 9-slice oldugu
    /// icin bu bosluklar her boyutta piksel olarak sabit kalir — kenar bolgeleri
    /// (75,20,75,20) esnemez, dolayisiyla oyuk her zaman ayni yerdedir.
    /// </summary>
    public class ProgressBarView : MonoBehaviour
    {
        [SerializeField] Image frame;
        [SerializeField] Image fill;

        [Tooltip("Dolgunun ani zipmasi yerine yumusak ilerlemesi (sn). 0 = anlik.")]
        [SerializeField] float smoothing = 0.08f;

        float _displayed;
        float _target;
        float _velocity;

        public float Value { get { return _target; } }

        void Awake()
        {
            if (fill != null) fill.fillAmount = 0f;
        }

        /// <summary>0-1 arasi ilerleme.</summary>
        public void SetProgress(float value01)
        {
            _target = Mathf.Clamp01(value01);

            // Dosya bittiginde deger 1'den 0'a duser; yumusatma bunu geri sarma
            // gibi gosterirdi, o yuzden geriye gidiste aninda uygula.
            if (_target < _displayed) ApplyImmediate(_target);
        }

        public void ApplyImmediate(float value01)
        {
            _target = Mathf.Clamp01(value01);
            _displayed = _target;
            _velocity = 0f;
            if (fill != null) fill.fillAmount = _displayed;
        }

        void Update()
        {
            if (fill == null) return;

            if (smoothing <= 0f)
            {
                _displayed = _target;
            }
            else
            {
                _displayed = Mathf.SmoothDamp(_displayed, _target, ref _velocity, smoothing);
                if (Mathf.Abs(_target - _displayed) < 0.0005f) _displayed = _target;
            }

            fill.fillAmount = _displayed;
        }

        public void Bind(Image frameImage, Image fillImage)
        {
            frame = frameImage;
            fill = fillImage;
        }
    }
}
