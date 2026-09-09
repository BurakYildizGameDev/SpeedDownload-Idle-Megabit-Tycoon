using SpeedDownload.Core;
using SpeedDownload.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Kadranin gorsel tarafi (Asset_Integration_Guide Bolum 3).
    ///
    /// Aci haritasi olcume dayanir: dial_redline_overlay yayi 0..+89 derece
    /// (saat 12 -> saat 3) arasini kapliyor, bu yuzden supurme -90..+90.
    ///
    /// Unity UI'da Z rotasyonu saat yonunun TERSI olduğu icin donusum:
    ///     saatYonuTepedenAci = Lerp(minAngle, maxAngle, t)
    ///     zRotasyon          = 90 - saatYonuTepedenAci
    /// Kontrol: t=0 -> z=180 (saat 9) · t=0.5 -> z=90 (saat 12) · t=1 -> z=0 (saat 3)
    /// </summary>
    public class DialView : MonoBehaviour
    {
        [Header("Katmanlar")]
        [SerializeField] Image backplate;
        [SerializeField] Image background;
        [SerializeField] Image tickMarks;
        [SerializeField] Image redlineOverlay;
        [SerializeField] RectTransform needlePivot;
        [SerializeField] Image needle;

        [Header("Bagimliliklar")]
        [SerializeField] SpeedController speedController;

        [Tooltip("SpriteSolidTint materyali. Calisma aninda kopyalanir ki " +
                 "asset dosyasi degismesin.")]
        [SerializeField] Material needleMaterialSource;

        [Tooltip("HueShift materyali. Sonsuz kademelerde (9+) kadran zemini " +
                 "tier8 gorselinin hue'su kaydirilarak uretilir.")]
        [SerializeField] Material hueShiftMaterialSource;

        [Header("Redline parlamasi")]
        [SerializeField] float redlineIdleAlpha = 0.85f;
        [SerializeField] float redlinePulseSpeed = 3f;

        [Header("Hedef bolge isareti")]
        [Tooltip("Kadranda 'burayi tuttur' isareti. Redline carpani tepeye yakin " +
                 "en yuksek degerine ulasiyor ama t=0,995'te overheat basliyor — " +
                 "yani en verimli oyun ikisinin arasinda. Bu bilgi mekanikte vardi " +
                 "ama tamamen gorunmezdi; oyuncu deneme yanilmayla bulmak zorundaydi.")]
        [SerializeField] RectTransform sweetSpotMarker;

        [Range(0f, 1f)] [SerializeField] float sweetSpotT = 0.90f;

        [Tooltip("Ibre isarete bu kadar yaklasinca isaret parlar.")]
        [Range(0.01f, 0.3f)] [SerializeField] float sweetSpotTolerance = 0.06f;

        [SerializeField] Graphic sweetSpotGraphic;
        [SerializeField] Color sweetSpotIdleColor = new Color(1f, 1f, 1f, 0.28f);
        [SerializeField] Color sweetSpotHitColor = new Color(0.45f, 1f, 0.55f, 0.95f);

        [Header("Kademe gecisi")]
        [Tooltip("Her kademenin minSpeed'i bir oncekinin maxSpeed'ine esit. Bu yuzden " +
                 "kademe atlayinca t 1.0'dan 0.0'a duser; normal ataletle ibre 180 dereceyi " +
                 "0,25 sn'de savurur. Gecis suresince atalet yukseltilerek suzulme saglanir.")]
        [SerializeField] float tierChangeInertia = 0.45f;
        [SerializeField] float tierChangeDuration = 1.2f;

        Material _needleMaterial;
        Material _hueMaterial;
        Material _backgroundDefaultMaterial;
        ConnectionTierSO _tier;
        GameConfigSO _config;

        float _currentAngle;
        float _angleVelocity;
        float _inertia = 0.05f;
        float _transitionTimer;

        // Supurmenin aci sinirlari (config'ten turetilir; -90..+90 icin 0..180).
        float _minSweepAngle;
        float _maxSweepAngle = 180f;

        /// <summary>Kademe gecis animasyonu suruyor mu?</summary>
        public bool IsTransitioning { get { return _transitionTimer > 0f; } }

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int HueShiftId = Shader.PropertyToID("_HueShift");

        void Awake()
        {
            if (needle != null && needleMaterialSource != null)
            {
                _needleMaterial = new Material(needleMaterialSource);
                _needleMaterial.name = needleMaterialSource.name + " (runtime)";
                needle.material = _needleMaterial;
            }

            if (background != null) _backgroundDefaultMaterial = background.material;

            if (hueShiftMaterialSource != null)
            {
                _hueMaterial = new Material(hueShiftMaterialSource);
                _hueMaterial.name = hueShiftMaterialSource.name + " (runtime)";
            }
        }

        void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.TierChanged -= ApplyTier;

            if (speedController != null) speedController.Clicked -= OnClicked;

            if (_needleMaterial != null) Destroy(_needleMaterial);
            if (_hueMaterial != null) Destroy(_hueMaterial);
        }

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[DialView] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            if (speedController == null) speedController = gm.Speed;

            _config = gm.Config;

            float endA = AngleForT(0f);
            float endB = AngleForT(1f);
            _minSweepAngle = Mathf.Min(endA, endB);
            _maxSweepAngle = Mathf.Max(endA, endB);

            gm.TierChanged += ApplyTier;
            ApplyTier(gm.CurrentTier);

            if (speedController != null) speedController.Clicked += OnClicked;

            // Ibre baslangicta taban konumda dursun, ilk frame'de zipmasin.
            _currentAngle = AngleForT(0f);
            if (needlePivot != null)
                needlePivot.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
        }

        float _punchScale = 1f;
        bool _punchDirty;

        void OnClicked()
        {
            // Ses AudioManager'in isi; o zaten SpeedController.Clicked'a abone.
            // Burada ikinci bir klip calmak her tiklamada cift ses uretiyordu.
            _punchScale = 0.94f;
            _punchDirty = true;
        }

        /// <summary>
        /// Tiklama "ezilmesinin" 1.0'a geri donmesi.
        ///
        /// Onceki surum her karede kosulsuz <c>transform.localScale</c> yaziyordu.
        /// Bu kadranin KOKU ve altinda onlarca Image/TMP var; bir UI transformunu
        /// olceklemek o Canvas'in tum geometrisini kirletiyor. Yani oyun hicbir
        /// sey olmasa bile saniyede 60 kez tum arayuzu yeniden insa ediyordu —
        /// telefonun isinmasinin en buyuk tek sebebi buydu.
        ///
        /// Ustelik Lerp hedefe hicbir zaman TAM ulasmadigi icin bu durum asla
        /// sona ermiyordu. Simdi olcek yalnizca bir tiklamadan sonra, hedefe
        /// oturana kadar yaziliyor.
        /// </summary>
        void TickPunch()
        {
            if (!_punchDirty) return;

            _punchScale = Mathf.Lerp(_punchScale, 1f, Time.deltaTime * 18f);

            if (_punchScale > 0.999f)
            {
                _punchScale = 1f;
                _punchDirty = false;
            }

            transform.localScale = new Vector3(_punchScale, _punchScale, 1f);
        }

        public void ApplyTier(ConnectionTierSO tier)
        {
            bool changed = _tier != null && _tier != tier;

            _tier = tier;
            if (_tier == null) return;

            // Ilk atamada gecis yok — ibre zaten baslangic konumunda.
            if (changed) _transitionTimer = tierChangeDuration;

            if (background != null && _tier.dialBackground != null)
                background.sprite = _tier.dialBackground;

            ApplyDialHue();

            if (_needleMaterial != null)
                _needleMaterial.SetColor(ColorId, _tier.needleColor);

            _inertia = Mathf.Max(0.01f, _tier.needleInertia);
        }

        void Update()
        {
            if (speedController == null || _config == null || needlePivot == null) return;

            TickPunch();

            float target = AngleForT(speedController.NormalizedT);

            float inertia = _inertia;
            if (_transitionTimer > 0f)
            {
                _transitionTimer -= Time.deltaTime;

                // Gecisin sonuna dogru normal atalete geri harmanla ki ibre
                // aniden "canlanmis" gibi hissettirmesin.
                float k = Mathf.Clamp01(_transitionTimer / Mathf.Max(0.01f, tierChangeDuration));
                inertia = Mathf.Lerp(_inertia, tierChangeInertia, k);
            }

            _currentAngle = Mathf.SmoothDamp(_currentAngle, target, ref _angleVelocity, inertia);

            // SmoothDamp hedefe asimptotik yaklasir, TAM ulasmaz. Kelepcelenmemis
            // haliyle ibre bosta dururken bile her karede binde bir derece
            // oynuyor ve her oynama Canvas'i kirletiyordu. Gozle gorulemeyecek
            // farki burada kapatmak, oyun bostayken cizim isini tamamen durduruyor.
            if (Mathf.Abs(target - _currentAngle) < 0.02f && Mathf.Abs(_angleVelocity) < 0.5f)
            {
                _currentAngle = target;
                _angleVelocity = 0f;
            }

            // Ibre kadran yuzunu asla terk etmemeli. SmoothDamp buyuk bir dt'de
            // (kare takilmasi, GC duraklamasi) hedefi asabiliyor; _currentAngle
            // sinirsiz bir float oldugu icin 180'i gecip donuyor ve
            // localEulerAngles sarmasi yuzunden makul ama YANLIS bir aciya
            // dusuyordu. Kelepce bunu kokten kesiyor.
            _currentAngle = Mathf.Clamp(_currentAngle, _minSweepAngle, _maxSweepAngle);

            // Aci degismediyse transformu hic yazma: ayni degeri atamak bile
            // RectTransform'u ve dolayisiyla Canvas'i kirletir.
            if (!Mathf.Approximately(_appliedAngle, _currentAngle))
            {
                _appliedAngle = _currentAngle;
                needlePivot.localRotation = Quaternion.Euler(0f, 0f, _currentAngle);
            }

            UpdateRedlineGlow();
            UpdateSweetSpot();
        }

        float _appliedAngle = float.NaN;

        /// <summary>
        /// Hedef isaretini konumlandirir ve ibre uzerine geldiginde parlatir.
        /// Isaret sabit bir acida durur; yalnizca rengi degisir.
        /// </summary>
        void UpdateSweetSpot()
        {
            if (sweetSpotMarker == null) return;

            // Isaretin acisi SABIT — sweetSpotT ve config degismedigi surece ayni
            // deger. Her karede yeniden yazmak Canvas'i bos yere kirletiyordu.
            if (!_sweetSpotPlaced)
            {
                _sweetSpotPlaced = true;
                sweetSpotMarker.localRotation = Quaternion.Euler(0f, 0f, AngleForT(sweetSpotT));
            }

            if (sweetSpotGraphic == null) return;

            bool onTarget = !speedController.IsOverheated &&
                            Mathf.Abs(speedController.NormalizedT - sweetSpotT) <= sweetSpotTolerance;

            // Graphic.color kendi icinde esitlik kontrolu yapiyor, ama durumu
            // burada da tutmak her karede bir Color karsilastirmasini eliyor.
            // -1 = henuz hic uygulanmadi, ilk karede kesin yazilsin diye.
            int state = onTarget ? 1 : 0;
            if (_sweetSpotState != state)
            {
                _sweetSpotState = state;
                sweetSpotGraphic.color = onTarget ? sweetSpotHitColor : sweetSpotIdleColor;
            }
        }

        bool _sweetSpotPlaced;
        int _sweetSpotState = -1;

        /// <summary>
        /// Sonsuz kademelerde (9+) kadran zemini yeni bir gorsel degil, tier8
        /// gorselinin hue kaydirilmis hali (PLAN Bolum 2.8). dialHueShift 0 ise
        /// materyal varsayilana doner.
        /// </summary>
        void ApplyDialHue()
        {
            if (background == null) return;

            bool shifted = _tier != null && _tier.dialHueShift > 0.0001f;

            if (shifted && _hueMaterial != null)
            {
                _hueMaterial.SetFloat(HueShiftId, _tier.dialHueShift);
                background.material = _hueMaterial;
            }
            else
            {
                background.material = _backgroundDefaultMaterial;
            }
        }

        float AngleForT(float t)
        {
            float clockwiseFromTop = Mathf.Lerp(_config.minAngle, _config.maxAngle, Mathf.Clamp01(t));
            return 90f - clockwiseFromTop;
        }

        void UpdateRedlineGlow()
        {
            if (redlineOverlay == null) return;

            float alpha = redlineIdleAlpha;

            if (speedController != null && speedController.InRedline && !speedController.IsOverheated)
            {
                float pulse = Mathf.PingPong(Time.time * redlinePulseSpeed, 1f);
                alpha = Mathf.Lerp(redlineIdleAlpha, 1f, pulse);

            }

            Color c = redlineOverlay.color;
            if (!Mathf.Approximately(c.a, alpha))
            {
                c.a = alpha;
                redlineOverlay.color = c;
            }
        }

        /// <summary>Hedef isareti SceneBuilder tarafindan baglanir.</summary>
        public void BindSweetSpot(RectTransform marker, Graphic graphic)
        {
            sweetSpotMarker = marker;
            sweetSpotGraphic = graphic;
        }

        /// <summary>SceneBuilder'in referanslari baglamasi icin.</summary>
        public void Bind(Image backplateImg, Image backgroundImg, Image tickImg, Image redlineImg,
                         RectTransform pivot, Image needleImg, SpeedController controller,
                         Material needleMat, Material hueMat)
        {
            hueShiftMaterialSource = hueMat;
            backplate = backplateImg;
            background = backgroundImg;
            tickMarks = tickImg;
            redlineOverlay = redlineImg;
            needlePivot = pivot;
            needle = needleImg;
            speedController = controller;
            needleMaterialSource = needleMat;
        }

        /// <summary>
        /// Prefab sahne referansi saklayamaz; SpeedController prefab kaydedildikten
        /// sonra sahne ornegi uzerinde baglanir (prefab override olur).
        /// </summary>
        public void BindSpeed(SpeedController controller)
        {
            speedController = controller;
        }
    }
}
