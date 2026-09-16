using System;
using System.Text;
using SpeedDownload.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Sahte odullu reklam (PLAN Bolum 2.7).
    ///
    /// Var olan sprite'larla kurulmus tam islevsel bir panel. Internet
    /// gerektirmez; gercek SDK doldurama­diginda da bu devreye giriyor.
    ///
    /// ONCEKI SURUMUN UC KUSURU
    /// ------------------------
    ///   1. KAPATMA DUGMESI OLU GORUNUYORDU. <c>OnClose</c> sayac dolmadan
    ///      sartsiz geri donuyordu: 30 saniye boyunca dugmeye basmak HICBIR SEY
    ///      yapmiyor, hicbir geri bildirim de vermiyordu. Ustelik simge %35
    ///      saydamdi — yani "devre disi" degil "bozuk" gibi duruyordu. Oyuncu
    ///      icin bunun adi "kapatma butonu calismiyor".
    ///   2. ATLAMA YOLU YOKTU. <see cref="IAdService"/> sozlesmesinde
    ///      <c>onSkipped</c> var ama panelden ulasilamiyordu; yani odulu
    ///      reddetmek isteyen oyuncunun bekleme disinda secenegi yoktu. Gercek
    ///      odullu reklamlar boyle calismaz: erken kapatabilirsin, odulu
    ///      alamazsin.
    ///   3. "VIDEO" HISSI YOKTU. Sabit bir simge ve bir sayidan ibaretti;
    ///      ekranda bir sey OYNAMADIGI icin oyuncu "reklam cikmadi" diye
    ///      okuyordu.
    ///
    /// SIMDIKI SOZLESME
    /// ----------------
    ///   - Ilk <see cref="skipUnlockSeconds"/> saniye: kapatma kilitli, dugmede
    ///     geri sayim yaziyor (neden basilamadigi GORUNUYOR).
    ///   - Sonrasi: kapatma ATLAMA'ya doner — panel kapanir, <c>onSkipped</c>
    ///     calisir, odul verilmez.
    ///   - Sayac dolunca: kapatma ODULU AL'a doner, <c>onRewarded</c> calisir.
    ///   - Ilerleme cubugu ve nabiz atan simge, reklamin oynadigini gosterir.
    /// </summary>
    public class StubAdService : MonoBehaviour, IAdService
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI countdownText;
        [SerializeField] TextMeshProUGUI captionText;
        [SerializeField] Button closeButton;
        [SerializeField] Image closeIcon;

        [Tooltip("Izleme ilerlemesini gosteren dolgu. SceneBuilder baglar.")]
        [SerializeField] Image progressFill;

        [Tooltip("Reklam simgesi — izleme suresince hafifce nabiz atar.")]
        [SerializeField] Image adIcon;

        [Header("Izleme sureleri (sn)")]
        [Tooltip("Kota temizleme — ucuz bir cozum yolu, kisa reklam.")]
        [SerializeField] float quotaWatchSeconds = 5f;

        [Tooltip("Offline kazanci ikiye katlama — buyuk odul, uzun reklam.")]
        [SerializeField] float offlineWatchSeconds = 15f;

        [Tooltip("Hiz boost'u — buyuk odul, uzun reklam.")]
        [SerializeField] float boostWatchSeconds = 15f;

        [Tooltip("Kac saniye sonra ATLAMA'ya izin verilsin. Gercek odullu " +
                 "reklamlarin 'skip' penceresiyle ayni fikir: oyuncu her zaman " +
                 "cikabilmeli, ama odulden vazgecerek.")]
        [SerializeField] float skipUnlockSeconds = 3f;

        /// <summary>
        /// Yerlestirmeye gore izleme suresi.
        ///
        /// Gercek reklamlarda sureyi biz secemeyiz (yayinci belirler); bu ayrim
        /// yalnizca sahte panelde gecerli ve odulun buyuklugu ile izleme
        /// maliyetini dengelemek icin var.
        /// </summary>
        float WatchSecondsFor(AdPlacement placement)
        {
            switch (placement)
            {
                case AdPlacement.QuotaClear: return quotaWatchSeconds;
                case AdPlacement.OfflineDouble: return offlineWatchSeconds;
                case AdPlacement.SpeedBoost: return boostWatchSeconds;
                default: return offlineWatchSeconds;
            }
        }

        readonly StringBuilder _sb = new StringBuilder(48);

        Action _onRewarded;
        Action _onSkipped;
        float _remaining;
        float _total;
        float _elapsed;
        bool _showing;
        bool _rewardGranted;

        /// <summary>Yedek saglayici: gercek SDK varsa o kazanir.</summary>
        public int Priority { get { return 0; } }

        public bool IsRewardedReady { get { return !_showing; } }
        public bool IsShowing { get { return _showing; } }

        /// <summary>Kapatma dugmesi su an bir sey yapiyor mu? (test ve teshis)</summary>
        public bool CanCloseNow
        {
            get { return _showing && (_rewardGranted || _elapsed >= skipUnlockSeconds); }
        }

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(OnClose);
            SetVisible(false);
        }

        void OnDestroy()
        {
            if (closeButton != null) closeButton.onClick.RemoveListener(OnClose);

            // Panel yok edilirken bekleyen cagriyi bosa dusurme.
            //
            // Eskiden geri cagrilar sessizce kayboluyordu: sahne kapanirken
            // reklam acıksa BoostAdButtonView ne odul ne de "atlandi" haberi
            // aliyor, dugmesini bir daha asla etkinlestirmiyordu.
            Action skipped = _onSkipped;
            _onRewarded = null;
            _onSkipped = null;
            _showing = false;

            if (skipped != null) skipped();
        }

        public void ShowRewarded(AdPlacement placement, Action onRewarded, Action onSkipped)
        {
            if (_showing) { if (onSkipped != null) onSkipped(); return; }

            _onRewarded = onRewarded;
            _onSkipped = onSkipped;

            _total = Mathf.Max(1f, WatchSecondsFor(placement));
            _remaining = _total;
            _elapsed = 0f;
            _rewardGranted = false;
            _showing = true;

            SetVisible(true);
            RefreshVisuals();
        }

        void Update()
        {
            if (!_showing) return;

            if (_remaining > 0f)
            {
                // Oyun duraklatilsa bile reklam saymali.
                float dt = Time.unscaledDeltaTime;
                _remaining -= dt;
                _elapsed += dt;

                if (_remaining <= 0f)
                {
                    _remaining = 0f;
                    GrantReward();
                }
                else
                {
                    RefreshVisuals();
                }
            }

            // Simge nabzi: ekranda gercekten bir sey OYNADIGINI gosteren tek
            // isaret. Sabit bir resim "reklam cikmadi" gibi okunuyordu.
            if (adIcon != null)
            {
                float pulse = 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.035f;
                adIcon.transform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        void GrantReward()
        {
            if (_rewardGranted) return;
            _rewardGranted = true;

            RefreshVisuals();
        }

        // ------------------------------------------------------------------
        // Gorunum
        // ------------------------------------------------------------------

        int _shownSeconds = -1;

        void RefreshVisuals()
        {
            if (progressFill != null)
                progressFill.fillAmount = _total <= 0f ? 1f : Mathf.Clamp01(_elapsed / _total);

            bool canSkip = !_rewardGranted && _elapsed >= skipUnlockSeconds;

            // Kapatma simgesi UC durumu ayirt edilebilir sekilde gostermeli:
            // kilitli (soluk), atlanabilir (yari parlak), odul hazir (parlak).
            if (closeIcon != null)
            {
                float alpha = _rewardGranted ? 1f : (canSkip ? 0.75f : 0.30f);
                closeIcon.color = new Color(1f, 1f, 1f, alpha);
            }

            int secs = Mathf.CeilToInt(Mathf.Max(0f, _remaining));
            if (secs == _shownSeconds && !_rewardGranted) return;
            _shownSeconds = secs;

            if (countdownText != null)
            {
                if (_rewardGranted)
                {
                    countdownText.SetText(LocalizationManager.T("ad_ready", "REWARD READY"));
                }
                else
                {
                    _sb.Length = 0;
                    _sb.Append(secs).Append(' ').Append(LocalizationManager.T("unit_seconds", "s"));
                    countdownText.SetText(_sb);
                }
            }

            if (captionText == null) return;

            if (_rewardGranted)
            {
                captionText.SetText(LocalizationManager.T(
                    "ad_close_hint", "Close to claim your reward."));
            }
            else if (canSkip)
            {
                // Dugmenin ARTIK ne yaptigini soyluyor. Bu satir olmadan oyuncu
                // basiyor, panel kapaniyor, odul gelmiyor ve sebebini bilmiyor.
                captionText.SetText(LocalizationManager.T(
                    "ad_skip_hint", "AD  ·  close now and you forfeit the reward"));
            }
            else
            {
                captionText.SetText(LocalizationManager.T(
                    "ad_caption", "AD  ·  watch to the end for your reward"));
            }
        }

        // ------------------------------------------------------------------
        // Kapatma
        // ------------------------------------------------------------------

        void OnClose()
        {
            if (!_showing) return;

            // 1) Sayac doldu: odulu ver.
            if (_rewardGranted) { Finish(true); return; }

            // 2) Atlama penceresi acildi: kapat ama odul verme.
            if (_elapsed >= skipUnlockSeconds) { Finish(false); return; }

            // 3) Henuz cok erken. SESSIZCE GERI DONME — dugmenin bozuk oldugu
            //    izlenimini yaratan tam olarak buydu. Ne kadar beklenecegini
            //    soyle ve dokunsal bir ret ver.
            HapticManager.LightTap();

            if (captionText != null)
            {
                int wait = Mathf.Max(1, Mathf.CeilToInt(skipUnlockSeconds - _elapsed));
                captionText.SetText(LocalizationManager.TF(
                    "ad_wait_hint", "Please wait {0}s before closing.", wait));
            }

            if (closeIcon != null) closeIcon.color = new Color(1f, 0.45f, 0.40f, 0.9f);
        }

        /// <summary>Paneli kapatir ve TEK bir geri cagri yapar.</summary>
        void Finish(bool rewarded)
        {
            _showing = false;
            SetVisible(false);

            if (adIcon != null) adIcon.transform.localScale = Vector3.one;

            // Geri cagrilar once kopyalanip alanlar temizleniyor: cagrilan kod
            // icinde yeni bir reklam acilirsa (odul -> yeni panel) buradaki
            // eski durum ona karismasin.
            Action reward = _onRewarded;
            Action skipped = _onSkipped;
            _onRewarded = null;
            _onSkipped = null;

            if (rewarded) { if (reward != null) reward(); }
            else { if (skipped != null) skipped(); }
        }

        /// <summary>Disaridan iptal (or. sahne kapanisi). Odul verilmez.</summary>
        public void Cancel()
        {
            if (!_showing) return;
            Finish(false);
        }

        void SetVisible(bool on)
        {
            if (group == null) return;

            group.alpha = on ? 1f : 0f;
            group.blocksRaycasts = on;
            group.interactable = on;
        }

        public void Bind(CanvasGroup canvasGroup, TextMeshProUGUI countdown,
                         TextMeshProUGUI caption, Button close, Image icon)
        {
            group = canvasGroup;
            countdownText = countdown;
            captionText = caption;
            closeButton = close;
            closeIcon = icon;
        }

        /// <summary>Ilerleme cubugu ve nabiz atan simge (SceneBuilder icin).</summary>
        public void BindPlayback(Image fill, Image icon)
        {
            progressFill = fill;
            adIcon = icon;
        }
    }
}
