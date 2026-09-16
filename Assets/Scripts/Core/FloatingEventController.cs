using System;
using SpeedDownload.Util;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Rastgele araliklarla ekrandan suzulen "Sinyal Yakalandi" balonu (GDD 5.6).
    /// Tiklanirsa ya sureli bir hiz boost'u ya da anlik nakit odulu verir.
    ///
    /// Bu ozellik sahnede duruyordu ama TAMAMEN OLUYDU; uc ayri kopukluk vardi:
    ///
    ///   1. <c>canvasRect</c> hicbir zaman atanmiyordu. Bilesen [Game] nesnesinde
    ///      duruyor, o da Canvas'in ALTINDA degil — dolayisiyla
    ///      GetComponentInParent&lt;Canvas&gt;() null donuyor ve SpawnEvent her
    ///      seferinde ilk satirda geri donuyordu. Balon hic cikmadi.
    ///   2. Simge <c>Resources.Load</c> ile araniyordu ama projede o yolda bir
    ///      Resources klasoru yok. Balon cikabilseydi bile beyaz bir kare olurdu.
    ///   3. Odul olarak verilen <c>ActiveMultiplier</c>'i HICBIR sistem okumuyordu.
    ///      Yani "5x hiz" odulu hicbir sey yapmiyordu.
    ///
    /// Uc kopukluk da kapatildi: katman ve simge SceneBuilder'dan baglaniyor,
    /// boost da DownloadController.BoostMultiplier uzerinden gercekten isliyor.
    /// </summary>
    public class FloatingEventController : MonoBehaviour
    {
        [Tooltip("Balonun icinde hareket edecegi katman. SceneBuilder baglar.")]
        [SerializeField] RectTransform layer;

        [SerializeField] Sprite iconSprite;

        [SerializeField] float minInterval = 40f;
        [SerializeField] float maxInterval = 70f;

        [Tooltip("Balonun ekrani gecme suresi (sn). Bu sure icinde yakalanmali.")]
        [SerializeField] float travelSeconds = 4.5f;

        [SerializeField] float boostDuration = 15f;
        [SerializeField] float boostMultiplier = 5f;

        [Tooltip("Nakit odulu, kac saniyelik bosta gelire denk olsun.")]
        [SerializeField] double cashRewardSeconds = 45.0;

        [Tooltip("Balonun boyutu (px). Parmakla yakalanabilecek kadar buyuk olmali.")]
        [SerializeField] float size = 120f;

        /// <summary>Aktif hiz boost carpani (1 = boost yok).</summary>
        public float ActiveMultiplier { get; private set; }

        /// <summary>Boost basladiginda / bittiginde.</summary>
        public event Action BoostChanged;

        float _spawnTimer;
        float _boostTimer;

        GameObject _balloonGo;
        RectTransform _balloonRect;
        Button _balloonButton;
        Image _balloonImage;

        float _travel;
        Vector2 _startPos;
        Vector2 _endPos;
        float _sineOffset;

        GameManager _gm;

        void Awake()
        {
            // Statik degildi ama yine de acik baslat: sahne yeniden yuklendiginde
            // yarim kalmis bir boost tasinmasin.
            ActiveMultiplier = 1f;
        }

        void Start()
        {
            _gm = GameManager.Instance;
            _spawnTimer = UnityEngine.Random.Range(minInterval, maxInterval);
        }

        void OnDestroy()
        {
            // Oyun kapanirken carpani birak; kayitta tutulmuyor.
            ApplyBoost(1f);
        }

        void Update()
        {
            TickBoost();

            if (_balloonGo != null && _balloonGo.activeSelf) { TickBalloon(); return; }

            if (layer == null) return;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer > 0f) return;

            _spawnTimer = UnityEngine.Random.Range(minInterval, maxInterval);
            Spawn();
        }

        void TickBoost()
        {
            if (_boostTimer <= 0f) return;

            _boostTimer -= Time.deltaTime;
            if (_boostTimer > 0f) return;

            _boostTimer = 0f;
            ApplyBoost(1f);
        }

        void ApplyBoost(float multiplier)
        {
            if (Mathf.Approximately(ActiveMultiplier, multiplier)) return;

            ActiveMultiplier = multiplier;

            if (_gm == null) _gm = GameManager.Instance;
            if (_gm != null && _gm.Download != null) _gm.Download.BoostMultiplier = multiplier;

            if (BoostChanged != null) BoostChanged();
        }

        void TickBalloon()
        {
            _travel += Time.deltaTime / Mathf.Max(0.5f, travelSeconds);

            if (_travel >= 1f) { Despawn(); return; }

            Vector2 p = Vector2.Lerp(_startPos, _endPos, _travel);
            p.y += Mathf.Sin(_travel * Mathf.PI * 3f + _sineOffset) * 40f;
            _balloonRect.anchoredPosition = p;
        }

        void Spawn()
        {
            if (_balloonGo == null) Build();
            if (_balloonGo == null) return;

            // Gorunmez bir balonu ekrana salma: oyuncunun goremedigi bir odul
            // dugmesi, yanlislikla basildiginda "sebepsiz para" gibi hissettirir
            // ve kadrana yapilan tiklamayi da yutar.
            if (_balloonImage == null || !_balloonImage.enabled) return;

            _travel = 0f;
            _sineOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);

            Rect r = layer.rect;
            float y = UnityEngine.Random.Range(-r.height * 0.18f, r.height * 0.18f);

            bool fromLeft = UnityEngine.Random.value > 0.5f;
            float from = fromLeft ? -r.width * 0.6f : r.width * 0.6f;
            float to = -from;

            _startPos = new Vector2(from, y);
            _endPos = new Vector2(to, y);

            _balloonRect.anchoredPosition = _startPos;
            _balloonGo.SetActive(true);
        }

        void Despawn()
        {
            if (_balloonGo != null) _balloonGo.SetActive(false);
        }

        void Build()
        {
            if (layer == null) return;

            _balloonGo = new GameObject("FloatingEvent", typeof(RectTransform));
            _balloonGo.transform.SetParent(layer, false);

            _balloonRect = (RectTransform)_balloonGo.transform;
            _balloonRect.anchorMin = new Vector2(0.5f, 0.5f);
            _balloonRect.anchorMax = new Vector2(0.5f, 0.5f);
            _balloonRect.pivot = new Vector2(0.5f, 0.5f);
            _balloonRect.sizeDelta = new Vector2(size, size);

            // Simgeyi ONCE cozumle.
            //
            // Eski sira tersti: sprite atanip img.enabled = (iconSprite != null)
            // yazildiktan SONRA Resources.Load deneniyordu. Build() yalnizca bir
            // kez calistigi icin sonuc kalici oluyordu: SceneBuilder simgeyi
            // baglamamissa balon sonsuza dek GORUNMEZ ama TIKLANABILIR kaliyordu
            // — ekranda dolasan gorunmez bir dugme, yani ozellik oyuncu icin hic
            // yoktu ama yanlislikla basildiginda odul veriyordu.
            if (iconSprite == null)
                iconSprite = Resources.Load<Sprite>("event_overdrive_flame");

            Image img = _balloonGo.AddComponent<Image>();
            img.sprite = iconSprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            // Simge hala yoksa gorunmez olsun: sprite'siz bir Image beyaz bir
            // kare cizer ve ekranda gezen anlamsiz bir kutu birakirdi.
            img.enabled = iconSprite != null;

            _balloonImage = img;

            _balloonButton = _balloonGo.AddComponent<Button>();
            _balloonButton.targetGraphic = img;
            _balloonButton.transition = Selectable.Transition.None;
            _balloonButton.onClick.AddListener(OnCaught);

            _balloonGo.SetActive(false);
        }

        void OnCaught()
        {
            Despawn();

            if (_gm == null) _gm = GameManager.Instance;
            if (_gm == null) return;

            if (_gm.Audio != null) _gm.Audio.PlayReward();
            if (_gm.Stats != null) _gm.Stats.Add(StatType.SignalsCaught);
            HapticManager.HeavyImpact();

            // %60 Overdrive Boost, %40 Nakit Odulu.
            // (Yorum eskiden "%50" diyordu ama esik 0.4 — kod dogruydu, yorum
            // yanlisti. Boost'un daha sik cikmasi bilincli: nakit odulu sessiz,
            // boost ise ibrede GORULUYOR.)
            if (UnityEngine.Random.value > 0.4f)
            {
                _boostTimer = boostDuration;
                ApplyBoost(boostMultiplier);

                // Aşırı ısınmayı anında soğut (Overdrive rahatlığı)
                if (_gm.Speed != null) _gm.Speed.TriggerEmergencyVent();
                return;
            }

            // Nakit odulu: dosyanin ham odulune degil, oyuncunun GERCEK gelir
            // hizina baglaniyor.
            if (_gm.Money == null || _gm.Download == null) return;

            double reward = _gm.Download.EstimateIdleIncomePerSecond() * cashRewardSeconds;
            if (reward > 0.0) _gm.Money.Add(reward);
        }

        /// <summary>SceneBuilder icin.</summary>
        public void Bind(RectTransform balloonLayer, Sprite icon)
        {
            layer = balloonLayer;
            iconSprite = icon;
        }
    }
}
