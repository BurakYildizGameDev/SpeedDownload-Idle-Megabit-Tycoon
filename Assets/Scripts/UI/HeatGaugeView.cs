using SpeedDownload.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Isi gostergesi — kadranin ustunu saran yarim halka (PLAN Bolum 28.2).
    ///
    /// Neden gerekliydi: <see cref="SpeedController.OverheatProgress"/> bastan beri
    /// hesaplaniyordu ama yalnizca DebugHud'da gorunuyordu. Yani oyuncu overheat'e
    /// ne kadar yakin oldugunu PATLAYANA kadar goremiyordu; risk/odul mekaniginin
    /// tum geri bildirimi eksikti ve overheat "adaletsiz surpriz" gibi hissettiriyordu.
    ///
    /// Dolgu <c>Image.Filled / Radial360</c> ile ciziliyor. Sprite yalnizca ust
    /// yarim halka oldugu icin tam doluluk 360 derecenin yarisina denk gelir —
    /// bkz. <see cref="arcSpan"/>.
    /// </summary>
    public class HeatGaugeView : MonoBehaviour
    {
        [Header("Katmanlar")]
        [Tooltip("Bos kanal (ui_heat_gauge).")]
        [SerializeField] Image track;

        [Tooltip("Dolgu (ui_heat_fill). Image Type = Filled, Radial 360.")]
        [SerializeField] Image fill;

        [Header("Bagimliliklar")]
        [SerializeField] SpeedController speedController;

        [Header("Yay — sprite'in alfa kanalindan olculdu")]
        // ui_heat_fill'in opak yayi -129,0 .. +129,3 derece (saat 12 = 0, saat
        // yonu pozitif). Yani 258,3 derecelik bir at nali; TAM DAIRE DEGIL ve
        // yarim halka da degil. Radial360 dolgusu 360 dereceye gore hesapladigi
        // icin dolgu dogrudan 0..1 arasinda surulemez:
        //
        //   origin = Bottom (saat 6 = 180 derece), saat yonunde
        //   180 -> -129 arasi 51 derece BOS   -> 51/360   = 0,142
        //   -129 -> +129 arasi 258,3 derece   -> ...      = 0,859'da biter
        //
        // Bu yuzden doluluk [0,142 .. 0,859] araligina eslenir. Sprite yeniden
        // uretilirse bu iki sayi da yeniden olculmeli.
        [Tooltip("Dolgunun bosken aldigi fillAmount (sprite'in yay baslangici).")]
        [Range(0f, 1f)] [SerializeField] float fillMin = 0.142f;

        [Tooltip("Dolgunun tam doluyken aldigi fillAmount (sprite'in yay sonu).")]
        [Range(0f, 1f)] [SerializeField] float fillMax = 0.859f;

        [Header("Renkler")]
        [SerializeField] Color coolColor = new Color(0.29f, 0.87f, 0.42f, 1f);
        [SerializeField] Color warmColor = new Color(1.00f, 0.84f, 0.22f, 1f);
        [SerializeField] Color hotColor = new Color(1.00f, 0.30f, 0.25f, 1f);

        [Tooltip("Bu oranin altinda gosterge tamamen saydam — bosta ekrani kirletmesin.")]
        [Range(0f, 0.5f)] [SerializeField] float fadeInBelow = 0.04f;

        [Header("Overheat yanip sonmesi")]
        [SerializeField] float overheatBlinkSpeed = 8f;

        [Tooltip("Dolgunun ani ziplamasini onleyen yumusatma (sn). 0 = aninda.")]
        [SerializeField] float smoothing = 0.06f;

        float _displayed;
        float _velocity;

        [Header("Acil Isı Tahliyesi & Uyarı")]
        [SerializeField] Image warningIcon;
        [SerializeField] Button emergencyVentButton;

        Sprite _ventSprite;
        Sprite _warningSprite;
        GameObject _ventRoot;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogError("[HeatGaugeView] GameManager bulunamadi.");
                enabled = false;
                return;
            }

            if (speedController == null) speedController = gm.Speed;

            // Kaynaklardan dinamik yukle
            _ventSprite = Resources.Load<Sprite>("ui_icon_vent_emergency");
            _warningSprite = Resources.Load<Sprite>("ui_icon_warning_heat");

            EnsureVentButton();
            EnsureWarningIcon();

            if (fill != null)
            {
                // Kod tarafinda garanti altina al: prefab/sahne ayari kaysa bile
                // dolgu dogru modda kalsin.
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Radial360;
                // Bottom = saat 6. Yayin alt acikligi tam orada oldugu icin
                // dolgu gorunmez bir noktadan baslayip iki uca dogru buyuyor.
                fill.fillOrigin = (int)Image.Origin360.Bottom;
                fill.fillClockwise = true;
                fill.fillAmount = fillMin;
                fill.raycastTarget = false;
            }

            if (track != null)
            {
                track.raycastTarget = false;

                // Editorde gorunur duruyor (konum dogrulamasi icin); oyunda
                // bostayken ekrani kirletmesin diye hemen saydamlastiriliyor.
                Color c = track.color;
                c.a = 0f;
                track.color = c;
            }
        }

        void EnsureVentButton()
        {
            if (emergencyVentButton != null)
            {
                // Sahneden gelen bir dugme: kendi kokunu de bilmemiz gerekiyor,
                // yoksa Update onu hic gosterip gizleyemez ve dugme kalici
                // olarak ekranda kalirdi.
                _ventRoot = emergencyVentButton.gameObject;
                emergencyVentButton.onClick.AddListener(OnEmergencyVentClicked);
                EnsureRaycaster();
                _ventRoot.SetActive(false);
                return;
            }

            // Yoksa hiyerarsi icinde dinamik uret
            _ventRoot = new GameObject("EmergencyVentButton", typeof(RectTransform));
            _ventRoot.transform.SetParent(transform, false);

            var rt = (RectTransform)_ventRoot.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 150f);
            rt.sizeDelta = new Vector2(120f, 120f);

            var img = _ventRoot.AddComponent<Image>();
            img.sprite = _ventSprite != null ? _ventSprite : _warningSprite;
            img.preserveAspect = true;
            img.raycastTarget = true;

            emergencyVentButton = _ventRoot.AddComponent<Button>();
            emergencyVentButton.targetGraphic = img;
            emergencyVentButton.transition = Selectable.Transition.None;
            emergencyVentButton.onClick.AddListener(OnEmergencyVentClicked);

            EnsureRaycaster();
            _ventRoot.SetActive(false);
        }

        /// <summary>
        /// Isi tehlikeli seviyeye ciktiginda gostergenin ustunde yanip sonen
        /// uyari ucgeni.
        ///
        /// NEDEN GEREKLI: gostergenin tek dili RENK (yesil -> sari -> kirmizi).
        /// Kirmizi-yesil renk korlugu erkeklerin yaklasik %8'inde var; onlar icin
        /// gosterge yalnizca "dolan bir yay" ve tehlike esigi hic okunmuyor.
        /// Simge, renkten BAGIMSIZ ikinci bir kanal.
        ///
        /// Alan (warningIcon) ve sprite bastan beri yukleniyordu ama HICBIR KOD
        /// onlari kullanmiyordu — ozellik yarim birakilmis, gorsel bosa
        /// uretilmisti.
        /// </summary>
        void EnsureWarningIcon()
        {
            if (warningIcon == null)
            {
                if (_warningSprite == null) return;

                var go = new GameObject("HeatWarningIcon", typeof(RectTransform));
                go.transform.SetParent(transform, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, 260f);
                rt.sizeDelta = new Vector2(72f, 72f);

                warningIcon = go.AddComponent<Image>();
                warningIcon.sprite = _warningSprite;
                warningIcon.preserveAspect = true;
            }

            // Dokunuslar arkadaki ClickCatcher'a gecmeli: bu bir gosterge,
            // dugme degil.
            warningIcon.raycastTarget = false;
            warningIcon.enabled = false;
        }

        [Tooltip("Uyari simgesinin belirdigi isi orani.")]
        [Range(0.5f, 1f)] [SerializeField] float warningThreshold = 0.75f;

        /// <summary>
        /// Bu dugmenin bulundugu Canvas'a bir <see cref="GraphicRaycaster"/>
        /// oldugundan emin olur.
        ///
        /// BU OLMADAN DUGME TAMAMEN OLUYDU. Isi gostergesi kadranin icinde
        /// (DialArea) duruyor ve SceneBuilder o alt Canvas'i BILEREK raycaster'siz
        /// kuruyor: kadran katmanlarinin hepsi raycastTarget=false, dokunuslar
        /// arkadaki ClickCatcher'a gecsin diye. Sonradan buraya eklenen acil
        /// tahliye dugmesi de o kuralin kurbani oldu — ekranda gorunuyor, basiliyor,
        /// ama raycaster olmadigi icin tiklama HIC ulasmiyordu.
        ///
        /// Kadran katmanlarinin raycastTarget'i kapali oldugu icin raycaster
        /// eklemek onlari etkilemiyor: yalnizca bu dugme tiklanabilir hale geliyor.
        /// </summary>
        void EnsureRaycaster()
        {
            if (_ventRoot == null) return;

            Canvas canvas = _ventRoot.GetComponentInParent<Canvas>();
            if (canvas == null) return;

            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        void OnEmergencyVentClicked()
        {
            if (speedController == null) return;
            if (speedController.TriggerEmergencyVent())
            {
                HapticManager.HeavyImpact();
                if (_ventRoot != null) _ventRoot.SetActive(false);
            }
        }

        void Update()
        {
            if (speedController == null || fill == null) return;

            float heat = Mathf.Clamp01(speedController.OverheatProgress);

            _displayed = smoothing <= 0f
                ? heat
                : Mathf.SmoothDamp(_displayed, heat, ref _velocity, smoothing);

            if (Mathf.Abs(heat - _displayed) < 0.0005f)
            {
                _displayed = heat;
                _velocity = 0f;
            }

            fill.fillAmount = Mathf.Lerp(fillMin, fillMax, _displayed);
            fill.color = ResolveColor(_displayed);

            if (track != null)
            {
                Color t = track.color;
                float target = _displayed <= fadeInBelow ? 0f : 1f;
                t.a = Mathf.MoveTowards(t.a, target, Time.deltaTime * 4f);
                track.color = t;
            }

            // Acil tahliye butonu / uyari ikonu gorunurlugu
            bool canVent = speedController.CanEmergencyVent;
            if (_ventRoot != null && _ventRoot.activeSelf != canVent)
            {
                _ventRoot.SetActive(canVent);
            }

            UpdateWarningIcon(heat);
        }

        /// <summary>
        /// Uyari ucgeni: esigi asinca gorunur ve isi arttikca hizlanan bir
        /// ritimle yanip soner. Overheat sirasinda kalici olarak parlak kalir.
        /// </summary>
        void UpdateWarningIcon(float heat)
        {
            if (warningIcon == null) return;

            bool overheated = speedController.IsOverheated;
            bool show = overheated || heat >= warningThreshold;

            if (warningIcon.enabled != show) warningIcon.enabled = show;
            if (!show) return;

            // Esikte yavas, tepede hizli: aciliyet renkten bagimsiz okunabilsin.
            float urgency = overheated ? 1f : Mathf.InverseLerp(warningThreshold, 1f, heat);
            float rate = Mathf.Lerp(3f, 10f, urgency);
            float blink = Mathf.PingPong(Time.time * rate, 1f);

            Color c = warningIcon.color;
            c.a = Mathf.Lerp(0.45f, 1f, blink);
            warningIcon.color = c;
        }

        void OnDestroy()
        {
            // Dinleyici Start'ta ekleniyor; bileseni yok edip yeniden kuran her
            // akista (kademe gecisi, sahne yeniden yuklemesi) birikmesin.
            if (emergencyVentButton != null)
                emergencyVentButton.onClick.RemoveListener(OnEmergencyVentClicked);
        }

        Color ResolveColor(float heat)
        {
            Color c;

            if (heat < 0.5f)
            {
                // Yesil -> sari
                c = Color.Lerp(coolColor, warmColor, Mathf.InverseLerp(0f, 0.5f, heat));
            }
            else
            {
                // Sari -> kirmizi
                c = Color.Lerp(warmColor, hotColor, Mathf.InverseLerp(0.5f, 1f, heat));
            }

            if (speedController != null && speedController.IsOverheated)
            {
                // Ceza suresince tam kirmizi yanip sonsun — durumun bittigi
                // an da gorsel olarak okunabilir olmali.
                c = hotColor;
                float blink = Mathf.PingPong(Time.time * overheatBlinkSpeed, 1f);
                c.a = Mathf.Lerp(0.35f, 1f, blink);
                return c;
            }

            c.a = _displayed <= fadeInBelow ? 0f : 1f;
            return c;
        }

        /// <summary>SceneBuilder'in referanslari baglamasi icin.</summary>
        public void Bind(Image trackImage, Image fillImage, SpeedController controller)
        {
            track = trackImage;
            fill = fillImage;
            speedController = controller;
        }

        /// <summary>
        /// Prefab sahne referansi saklayamaz; SpeedController prefab kaydedildikten
        /// sonra sahne ornegi uzerinde baglanir (DialView ile ayni gerekce).
        /// </summary>
        public void BindSpeed(SpeedController controller)
        {
            speedController = controller;
        }
    }
}
