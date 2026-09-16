using SpeedDownload.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// "Reklam izle, gecici hiz boost'u kazan" dugmesi (GDD Bolum 11).
    ///
    /// Tasarim kurallari — GDD "zorunlu banner/gecis reklami yok, oyun akisini
    /// bolmeyecek sekilde" diyor, bu dugme de ona gore:
    ///
    ///   - TAMAMEN istege bagli. Basmayan oyuncu hicbir sey kaybetmiyor,
    ///     hicbir sey de onu kesmiyorr.
    ///   - Boost SURERKEN dugme gizleniyor: ust uste basip carpani
    ///     katlamak mumkun degil.
    ///   - Boost bitince BEKLEME SURESI basliyor. Bu olmadan oyuncu surekli
    ///     reklam izleyen bir moda giriyor ve oyunun kendi ekonomisi
    ///     anlamsizlasiyor — idle oyunlari en cok bu sekilde bozuluyor.
    ///   - Reklam saglayicisi hazir degilse dugme hic gorunmuyor; basilinca
    ///     hicbir sey olmayan bir dugme, olmayan dugmeden kotudur.
    ///
    /// Odul YALNIZCA reklam sonuna kadar izlenirse veriliyor (onRewarded).
    /// </summary>
    public class BoostAdButtonView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI label;

        [Header("Odul")]
        [Tooltip("Reklam izlenince uygulanan hiz carpani.")]
        [SerializeField] double boostMultiplier = 2.0;

        [Tooltip("Boost'un suresi (sn).")]
        [SerializeField] float boostSeconds = 300f;

        [Tooltip("Boost bittikten sonra dugmenin tekrar cikmasi icin gereken sure (sn).")]
        [SerializeField] float cooldownSeconds = 300f;

        GameManager _gm;
        float _boostRemaining;
        float _cooldownRemaining;

        /// <summary>Su an reklam boost'u aktif mi? (HUD gostergesi icin)</summary>
        public bool BoostActive { get { return _boostRemaining > 0f; } }

        /// <summary>Aktif boost'un kalan suresi (sn).</summary>
        public float BoostRemaining { get { return _boostRemaining; } }

        void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);

            // Oyun kapanirken carpani birak: kayitta tutulmuyor, yarim kalmis
            // bir boost sonraki oturuma tasinmamali.
            ClearBoost();
        }

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) { enabled = false; return; }

            ApplyVisibility();
        }

        void OnClick()
        {
            if (_gm == null || _gm.Ads == null) return;
            if (!_gm.Ads.IsRewardedReady || _gm.Ads.IsShowing) return;
            if (BoostActive || _cooldownRemaining > 0f) return;

            // Atlama dali artik gercekten calisiyor (StubAdService'te kapatma
            // dugmesi bir "skip" yolu actı). Bos gecmek, reklam yarida
            // kapatildiginda dugmenin gorunumunu tazeleyecek kimseyi
            // birakmiyordu: dugme gizli kalirdi.
            _gm.Ads.ShowRewarded(AdPlacement.SpeedBoost, GrantBoost, OnAdSkipped);
            ApplyVisibility();
        }

        /// <summary>Reklam yarida kapatildi: bekleme yok, dugme geri gelsin.</summary>
        void OnAdSkipped()
        {
            ApplyVisibility();
        }

        void GrantBoost()
        {
            if (_gm == null || _gm.Download == null) return;

            _boostRemaining = Mathf.Max(1f, boostSeconds);
            _gm.Download.AdBoostMultiplier = boostMultiplier <= 0.0 ? 1.0 : boostMultiplier;

            // Sayac YALNIZCA odul verildiginde artiyor: atlanan reklam
            // "izlenmis" sayilsaydi, kapatma dugmesine basip basip basarim
            // ilerletmek mumkun olurdu.
            if (_gm.Stats != null) _gm.Stats.Add(StatType.AdsWatched);

            HapticManager.MediumImpact();
            ApplyVisibility();
        }

        void ClearBoost()
        {
            _boostRemaining = 0f;

            GameManager gm = _gm != null ? _gm : GameManager.Instance;
            if (gm != null && gm.Download != null) gm.Download.AdBoostMultiplier = 1.0;
        }

        void Update()
        {
            bool changed = false;

            if (_boostRemaining > 0f)
            {
                _boostRemaining -= Time.deltaTime;
                if (_boostRemaining <= 0f)
                {
                    ClearBoost();
                    _cooldownRemaining = Mathf.Max(0f, cooldownSeconds);
                    changed = true;
                }
            }
            else if (_cooldownRemaining > 0f)
            {
                _cooldownRemaining -= Time.deltaTime;
                if (_cooldownRemaining <= 0f)
                {
                    _cooldownRemaining = 0f;
                    changed = true;
                }
            }

            RefreshLabel();

            // Reklam acilip kapandiginda da tazele — o gecisler bu bilesenin
            // kendi sayaclarindan gelmiyor, dolayisiyla "changed" onlari gormez.
            if (changed || CurrentlyAvailable != _lastAvailable) ApplyVisibility();
        }

        int _shownSeconds = -1;

        /// <summary>
        /// Geri sayim yalnizca gosterilen SANIYE degisince yaziliyor: her karede
        /// metin uretmek TMP mesh'ini bos yere yeniden ordurur.
        /// </summary>
        void RefreshLabel()
        {
            if (label == null) return;

            float remaining = BoostActive ? _boostRemaining : _cooldownRemaining;
            int secs = Mathf.CeilToInt(Mathf.Max(0f, remaining));

            if (secs == _shownSeconds) return;
            _shownSeconds = secs;

            if (remaining <= 0f) { label.SetText(""); return; }

            label.SetText(Util.NumberFormatter.FormatDuration(secs));
        }

        /// <summary>
        /// Dugme yalnizca gercekten basilabilir oldugunda gorunur.
        /// Boost surerken, bekleme suresinde ve reklam EKRANDAYKEN gizli.
        /// </summary>
        void ApplyVisibility()
        {
            bool adsReady = _gm != null && _gm.Ads != null && _gm.Ads.IsRewardedReady;
            bool showing = _gm != null && _gm.Ads != null && _gm.Ads.IsShowing;
            bool available = adsReady && !showing && !BoostActive && _cooldownRemaining <= 0f;

            if (icon != null) icon.enabled = available;
            if (button != null) button.interactable = available;
            if (label != null) label.enabled = !available && (BoostActive || _cooldownRemaining > 0f);

            _lastAvailable = available;
        }

        /// <summary>
        /// Son uygulanan gorunurluk. Reklamin kapanmasi gibi DISARIDAN gelen
        /// durum degisimlerini yakalamanin tek yolu bunu her karede
        /// karsilastirmak: reklam saglayicisi bir olay yayinlamiyor.
        /// </summary>
        bool _lastAvailable;

        bool CurrentlyAvailable
        {
            get
            {
                if (_gm == null || _gm.Ads == null) return false;
                return _gm.Ads.IsRewardedReady && !_gm.Ads.IsShowing
                       && !BoostActive && _cooldownRemaining <= 0f;
            }
        }

        /// <summary>SceneBuilder icin.</summary>
        public void Bind(Button btn, Image iconImage, TextMeshProUGUI countdownLabel)
        {
            button = btn;
            icon = iconImage;
            label = countdownLabel;
        }
    }
}
