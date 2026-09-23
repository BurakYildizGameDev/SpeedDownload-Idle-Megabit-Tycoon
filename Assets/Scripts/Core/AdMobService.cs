using System;
using UnityEngine;
using GoogleMobileAds.Api;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Google AdMob odullu reklam, gecis reklami ve banner saglayicisi.
    ///
    /// HEM EDITORDE HEM MOBILDE (Android/iOS) CALISIR.
    ///
    /// Varsayilan olarak Google'in resmi test reklam birim kimlikleri kullanilir.
    /// Canli yayina gecilecegi zaman:
    ///   1. Inspector'dan 'Use Test Ad Unit Ids' kutucugunun isaretini kaldirin.
    ///   2. Kendi Android / iOS AdMob Reklam Birim Kimliklerinizi (Ad Unit ID) girin.
    ///   3. 'Assets -> Google Mobile Ads -> Settings' menusunden AdMob App ID'lerinizi girin.
    /// </summary>
    public class AdMobService : MonoBehaviour, IAdService
    {
        // ==================================================================
        // Google Resmi Test Birimleri
        // ==================================================================
        const string TestAndroidRewardedId = "ca-app-pub-3940256099942544/5224354917";
        const string TestIosRewardedId = "ca-app-pub-3940256099942544/1712485313";

        const string TestAndroidBannerId = "ca-app-pub-3940256099942544/6300978111";
        const string TestIosBannerId = "ca-app-pub-3940256099942544/2934735716";

        const string TestAndroidInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        const string TestIosInterstitialId = "ca-app-pub-3940256099942544/4411468910";

        // ==================================================================
        // Inspector Ayarlari
        // ==================================================================
        [Header("AdMob Genel Ayarlar")]
        [Tooltip("Test reklam birimleri kullanilsin mi? (Gelistirme asamasinda guvenlidir, hesap banlanma riski yoktur).")]
        [SerializeField] bool useTestAdUnitIds = true;

        [Tooltip("Gercek AdMob reklami dolmazsa/yuklenemezse sahte cizim paneline devredilsin mi? Kapaliysa kullaniciya gereksiz sahte popup gosterilmez.")]
        [SerializeField] bool fallbackToStubAd = false;

        [Header("Odullu Reklam Kimlikleri (Rewarded Ads)")]
        [SerializeField] string customAndroidRewardedId = "ca-app-pub-3940256099942544/5224354917";
        [SerializeField] string customIosRewardedId = "ca-app-pub-3940256099942544/1712485313";

        [Header("Banner Reklam Ayarlari")]
        [SerializeField] bool enableBanner = false;
        [SerializeField] AdPosition bannerPosition = AdPosition.Bottom;
        [SerializeField] string customAndroidBannerId = "ca-app-pub-3940256099942544/6300978111";
        [SerializeField] string customIosBannerId = "ca-app-pub-3940256099942544/2934735716";

        [Header("Gecis Reklami Kimlikleri (Interstitial Ads)")]
        [SerializeField] string customAndroidInterstitialId = "ca-app-pub-3940256099942544/1033173712";
        [SerializeField] string customIosInterstitialId = "ca-app-pub-3940256099942544/4411468910";

        // ==================================================================
        // IAdService Arayuzu
        // ==================================================================
#if UNITY_WEBGL && !UNITY_EDITOR
        // Tarayicida Google Mobile Ads SDK yok: sahte panel (StubAdService) kazansin.
        public int Priority => -1;
#else
        public int Priority => 100;
#endif

        public static string Diagnostics { get; private set; } = "Baslatilmadi";

        public bool IsRewardedReady
        {
            get
            {
                if (RealRewardedUsable) return true;
                return fallbackToStubAd && _fallback != null && _fallback.IsRewardedReady;
            }
        }

        public bool IsShowing => _showingReal || (fallbackToStubAd && _fallback != null && _fallback.IsShowing);

        // ==================================================================
        // Dahili Durum
        // ==================================================================
        IAdService _fallback;
        RewardedAd _rewardedAd;
        InterstitialAd _interstitialAd;
        BannerView _bannerView;

        bool _initialized;
        bool _loadingRewarded;
        bool _loadingInterstitial;
        bool _showingReal;
        float _retryTimer;
        const float RetryInterval = 10f;

        bool RealRewardedUsable => _initialized && _rewardedAd != null && _rewardedAd.CanShowAd();
        bool RealInterstitialUsable => _initialized && _interstitialAd != null && _interstitialAd.CanShowAd();

        string RewardedAdUnitId
        {
            get
            {
                if (useTestAdUnitIds)
                    return Application.platform == RuntimePlatform.IPhonePlayer ? TestIosRewardedId : TestAndroidRewardedId;

                string id = Application.platform == RuntimePlatform.IPhonePlayer ? customIosRewardedId : customAndroidRewardedId;
                return string.IsNullOrEmpty(id) ? TestAndroidRewardedId : id;
            }
        }

        string BannerAdUnitId
        {
            get
            {
                if (useTestAdUnitIds)
                    return Application.platform == RuntimePlatform.IPhonePlayer ? TestIosBannerId : TestAndroidBannerId;

                string id = Application.platform == RuntimePlatform.IPhonePlayer ? customIosBannerId : customAndroidBannerId;
                return string.IsNullOrEmpty(id) ? TestAndroidBannerId : id;
            }
        }

        string InterstitialAdUnitId
        {
            get
            {
                if (useTestAdUnitIds)
                    return Application.platform == RuntimePlatform.IPhonePlayer ? TestIosInterstitialId : TestAndroidInterstitialId;

                string id = Application.platform == RuntimePlatform.IPhonePlayer ? customIosInterstitialId : customAndroidInterstitialId;
                return string.IsNullOrEmpty(id) ? TestAndroidInterstitialId : id;
            }
        }

        void Awake()
        {
            // Sahnedeki StubAdService bilesenini bul (eger varsa ve fallback istenirse kullanilacak)
            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var svc = behaviours[i] as IAdService;
                if (svc != null && !ReferenceEquals(svc, this))
                {
                    _fallback = svc;
                    break;
                }
            }
        }

        void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            Diagnostics = "WebGL: AdMob devre disi";
            return;
#endif
            Diagnostics = "AdMob SDK baslatiliyor...";
            MobileAds.Initialize(status =>
            {
                _initialized = true;
                Diagnostics = "AdMob SDK Hazir";
                Debug.Log("[AdMobService] Google Mobile Ads SDK basariyla baslatildi.");

                LoadRewarded();
                LoadInterstitial();

                if (enableBanner)
                {
                    LoadBanner();
                }
            });
        }

        void Update()
        {
            // Eger SDK baslatildiysa ve odullu reklam yuklu degilse periyodik olarak tekrar dene
            if (_initialized && !_loadingRewarded && (_rewardedAd == null || !_rewardedAd.CanShowAd()))
            {
                _retryTimer += Time.unscaledDeltaTime;
                if (_retryTimer >= RetryInterval)
                {
                    _retryTimer = 0f;
                    LoadRewarded();
                }
            }
        }

        void OnDestroy()
        {
            DestroyRewarded();
            DestroyInterstitial();
            DestroyBanner();
        }

        // ==================================================================
        // Odullu Reklam (Rewarded Ad)
        // ==================================================================
        public void LoadRewarded()
        {
            if (!_initialized || _loadingRewarded || (_rewardedAd != null && _rewardedAd.CanShowAd()))
                return;

            _loadingRewarded = true;
            DestroyRewarded();

            string unitId = RewardedAdUnitId;
            Debug.Log($"[AdMobService] Odullu reklam yukleniyor: {unitId}");

            RewardedAd.Load(unitId, new AdRequest(), (ad, error) =>
            {
                _loadingRewarded = false;

                if (error != null || ad == null)
                {
                    _rewardedAd = null;
                    Diagnostics = "Odullu reklam yukleme hatasi: " + (error != null ? error.GetMessage() : "bos reklam");
                    Debug.LogWarning($"[AdMobService] Odullu reklam yuklenemedi: {error?.GetMessage()}");
                    return;
                }

                _rewardedAd = ad;
                Diagnostics = "Odullu reklam hazir";
                Debug.Log("[AdMobService] Odullu reklam basariyla yuklendi.");
            });
        }

        public void ShowRewarded(AdPlacement placement, Action onRewarded, Action onSkipped)
        {
            if (RealRewardedUsable)
            {
                ShowRealRewarded(placement, onRewarded, onSkipped);
                return;
            }

            if (fallbackToStubAd && _fallback != null)
            {
                _fallback.ShowRewarded(placement, onRewarded, onSkipped);
                return;
            }

            Debug.LogWarning("[AdMobService] Odullu reklam hazir degil, reklam atlandi.");
            onSkipped?.Invoke();
            LoadRewarded();
        }

        void ShowRealRewarded(AdPlacement placement, Action onRewarded, Action onSkipped)
        {
            RewardedAd ad = _rewardedAd;
            _rewardedAd = null;
            _showingReal = true;

            bool earned = false;
            bool finished = false;

            Action<bool> finish = rewarded =>
            {
                if (finished) return;
                finished = true;

                _showingReal = false;
                ad.Destroy();

                if (rewarded)
                {
                    onRewarded?.Invoke();
                }
                else
                {
                    onSkipped?.Invoke();
                }

                LoadRewarded();
            };

            ad.OnAdFullScreenContentClosed += () =>
            {
                finish(earned);
            };

            ad.OnAdFullScreenContentFailed += adError =>
            {
                Diagnostics = "Odullu gosterim hatasi: " + adError.GetMessage();
                Debug.LogWarning($"[AdMobService] Odullu gosterim hatasi: {adError.GetMessage()}");
                finish(false);
            };

            try
            {
                ad.Show(reward =>
                {
                    earned = true;
                    Debug.Log($"[AdMobService] Odul kazanildi! Type: {reward.Type}, Amount: {reward.Amount}");
                });
            }
            catch (Exception e)
            {
                Diagnostics = "Odullu gosterim istisnasi: " + e.Message;
                Debug.LogError($"[AdMobService] Odullu gosterim istisnasi: {e.Message}");
                finish(false);
            }
        }

        void DestroyRewarded()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;
            }
        }

        // ==================================================================
        // Gecis Reklami (Interstitial Ad)
        // ==================================================================
        public void LoadInterstitial()
        {
            if (!_initialized || _loadingInterstitial || (_interstitialAd != null && _interstitialAd.CanShowAd()))
                return;

            _loadingInterstitial = true;
            DestroyInterstitial();

            string unitId = InterstitialAdUnitId;
            InterstitialAd.Load(unitId, new AdRequest(), (ad, error) =>
            {
                _loadingInterstitial = false;

                if (error != null || ad == null)
                {
                    _interstitialAd = null;
                    Debug.LogWarning($"[AdMobService] Gecis reklami yuklenemedi: {error?.GetMessage()}");
                    return;
                }

                _interstitialAd = ad;
                Debug.Log("[AdMobService] Gecis reklami basariyla yuklendi.");
            });
        }

        public void ShowInterstitial(Action onClosed = null)
        {
            if (!RealInterstitialUsable)
            {
                Debug.LogWarning("[AdMobService] Gecis reklami hazir degil.");
                onClosed?.Invoke();
                LoadInterstitial();
                return;
            }

            InterstitialAd ad = _interstitialAd;
            _interstitialAd = null;

            ad.OnAdFullScreenContentClosed += () =>
            {
                ad.Destroy();
                onClosed?.Invoke();
                LoadInterstitial();
            };

            ad.OnAdFullScreenContentFailed += error =>
            {
                ad.Destroy();
                onClosed?.Invoke();
                LoadInterstitial();
            };

            try
            {
                ad.Show();
            }
            catch (Exception e)
            {
                Debug.LogError($"[AdMobService] Gecis reklami gosterim hatasi: {e.Message}");
                ad.Destroy();
                onClosed?.Invoke();
                LoadInterstitial();
            }
        }

        void DestroyInterstitial()
        {
            if (_interstitialAd != null)
            {
                _interstitialAd.Destroy();
                _interstitialAd = null;
            }
        }

        // ==================================================================
        // Banner Reklam (Banner Ad)
        // ==================================================================
        public void LoadBanner()
        {
            DestroyBanner();

            string unitId = BannerAdUnitId;
            Debug.Log($"[AdMobService] Banner yukleniyor: {unitId}");

            _bannerView = new BannerView(unitId, AdSize.Banner, bannerPosition);
            _bannerView.OnBannerAdLoaded += () =>
            {
                Debug.Log("[AdMobService] Banner basariyla yuklendi.");
            };
            _bannerView.OnBannerAdLoadFailed += error =>
            {
                Debug.LogWarning($"[AdMobService] Banner yukleme hatasi: {error.GetMessage()}");
            };

            _bannerView.LoadAd(new AdRequest());
        }

        public void ShowBanner()
        {
            if (_bannerView != null)
            {
                _bannerView.Show();
            }
            else
            {
                LoadBanner();
            }
        }

        public void HideBanner()
        {
            if (_bannerView != null)
            {
                _bannerView.Hide();
            }
        }

        public void DestroyBanner()
        {
            if (_bannerView != null)
            {
                _bannerView.Destroy();
                _bannerView = null;
            }
        }
    }
}
