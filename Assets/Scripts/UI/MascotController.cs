using SpeedDownload.Core;
using SpeedDownload.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Maskotun ruh hali (GDD Bolum 9).
    ///
    /// Oncelik: asiri isinma > sevinc > bosta. Overheat suresince mutlu yuz
    /// gostermek yanlis geri bildirim olurdu, o yuzden sira onemli.
    /// </summary>
    public class MascotController : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
    {
        [SerializeField] Image image;
        [SerializeField] Sprite idleSprite;
        [SerializeField] Sprite happySprite;
        [SerializeField] Sprite overheatedSprite;

        [SerializeField] Sprite laserSprite;
        [SerializeField] Sprite sleepingSprite;
        [SerializeField] Sprite panicSprite;

        [Header("Konuşma Balonu")]
        [SerializeField] Sprite speechBubbleSprite;

        [Tooltip("Dosya bitince kac saniye sevinsin.")]
        [SerializeField] float happyDuration = 1.2f;

        [Tooltip("Oyuncu bu kadar saniye HIC dokunmazsa maskot uyur.")]
        [SerializeField] float sleepAfterSeconds = 60f;

        [Header("Bosta salinim")]
        [SerializeField] float bobAmplitude = 8f;
        [SerializeField] float bobSpeed = 1.6f;

        SpeedController _speed;
        DownloadController _download;
        RectTransform _rect;

        float _happyTimer;
        float _baseY;
        float _scaleBounce = 1f;
        float _idleSeconds;
        Sprite _current;

        GameObject _bubbleGo;
        TMPro.TextMeshProUGUI _bubbleText;
        float _bubbleTimer;

        // Replikler LocalizationManager'da, bes dilde.
        //
        // Eskiden burada SABIT TURKCE diziler duruyordu: Almanca veya Rusca
        // oynayan biri, oyunun geri kalani kendi dilindeyken maskotun Turkce
        // konustugunu goruyordu. Metin artik anahtarla cekiliyor; yeni bir
        // replik eklemek icin hem sozluge satir eklemek hem de asagidaki
        // sayaci artirmak gerekiyor (sayac LocalizationManager'da tanimli,
        // yani iki yer degil tek yer).
        static string RandomQuote(string prefix, int count)
        {
            int i = Random.Range(0, count);
            return LocalizationManager.T(prefix + i, "");
        }

        void Awake()
        {
            _rect = image != null ? image.rectTransform : (RectTransform)transform;
            if (_rect != null) _baseY = _rect.anchoredPosition.y;

            LoadResourcesSprites();
        }

        void LoadResourcesSprites()
        {
            if (laserSprite == null) laserSprite = Resources.Load<Sprite>("mascot_rage_laser");
            if (sleepingSprite == null) sleepingSprite = Resources.Load<Sprite>("mascot_sleeping");
            if (panicSprite == null) panicSprite = Resources.Load<Sprite>("mascot_sweating_panic");
            if (speechBubbleSprite == null) speechBubbleSprite = Resources.Load<Sprite>("ui_speech_bubble");
        }

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { enabled = false; return; }

            _speed = gm.Speed;
            _download = gm.Download;

            if (_download != null) _download.FileCompleted += OnFileCompleted;
            if (_speed != null) _speed.Clicked += OnPlayerClicked;

            EnsureSpeechBubble();
            Apply(idleSprite);
        }

        void EnsureSpeechBubble()
        {
            if (_bubbleGo != null) return;

            _bubbleGo = new GameObject("MascotBubble", typeof(RectTransform));
            _bubbleGo.transform.SetParent(transform, false);

            var rt = (RectTransform)_bubbleGo.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(20f, 10f);

            // Balon KAYNAK ORANINDA ciziliyor (1035x512, kabaca 2:1).
            //
            // Eskiden 220x75 (3:1) idi ve Image.Type.Sliced kullaniliyordu — ama
            // sprite'in 9-slice border'i 0 oldugu icin Sliced pratikte Simple
            // gibi davraniyor, yani gorsel yatayda eziliyordu: yuvarlak koseler
            // ovalleşiyor, sol alttaki konusma kuyrugu carpik cikiyordu.
            // Sprite'i kendi oraninda cizmek, border olcup 9-slice kurmaktan
            // hem daha basit hem de bu boyutta (kenarlar genislige yakin)
            // gorsel olarak daha dogru.
            rt.sizeDelta = new Vector2(260f, 129f);

            var img = _bubbleGo.AddComponent<Image>();
            img.sprite = speechBubbleSprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = true;
            img.color = new Color(1f, 1f, 1f, 0.95f);
            img.raycastTarget = false;

            // Sprite yoksa beyaz bir kutu cizme.
            img.enabled = speechBubbleSprite != null;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_bubbleGo.transform, false);

            var textRt = (RectTransform)textGo.transform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            // Metin, balonun GOVDESINDE kalmali: alt kenarda konusma kuyrugu var
            // ve oraya yazi tasarsa kuyrugun uzerine binerdi.
            textRt.offsetMin = new Vector2(26f, 34f);
            textRt.offsetMax = new Vector2(-26f, -16f);

            _bubbleText = textGo.AddComponent<TMPro.TextMeshProUGUI>();
            _bubbleText.fontSize = 15f;
            _bubbleText.alignment = TMPro.TextAlignmentOptions.Center;
            _bubbleText.color = new Color(0.25f, 0.95f, 0.45f, 1f);
            _bubbleText.raycastTarget = false;

            _bubbleGo.SetActive(false);
        }

        public void Say(string message, float duration = 2.2f)
        {
            if (_bubbleGo == null) EnsureSpeechBubble();
            if (_bubbleGo == null || _bubbleText == null) return;

            _bubbleText.SetText(message);
            _bubbleTimer = duration;
            _bubbleGo.SetActive(true);
        }

        void OnDestroy()
        {
            if (_download != null) _download.FileCompleted -= OnFileCompleted;
            if (_speed != null) _speed.Clicked -= OnPlayerClicked;
        }

        public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
        {
            _happyTimer = happyDuration;
            _scaleBounce = 1.25f;
            _idleSeconds = 0f;
            HapticManager.LightTap();

            Say(RandomQuote("mascot_tap_", LocalizationManager.MascotTapQuoteCount));
        }

        void OnFileCompleted(FileDataSO file, double reward)
        {
            _happyTimer = happyDuration;
            _scaleBounce = 1.18f;

            // _idleSeconds BURADA SIFIRLANMAZ.
            //
            // "Bosta" olcusu OYUNCUNUN dokunmamasi; dosya tamamlanmasi degil.
            // Dosyalar oyun acik durdukca surekli bitiyor, dolayisiyla eski kod
            // sayaci saniyede birkac kez sifirliyordu: 45 saniyelik esik ASLA
            // asilamiyor ve uyuyan maskot sprite'i hic gorunmuyordu — ozellik
            // ve onun icin uretilen gorsel tamamen oluydu.
        }

        /// <summary>
        /// Oyuncu ekrana dokundu: bosta sayaci sifirlanir.
        ///
        /// Kadrana yapilan tiklamalar maskota ULASMIYOR (onlari ClickCatcher
        /// yakaliyor), bu yuzden dogrudan hiz denetleyicisinin tiklama olayina
        /// baglaniyoruz — yoksa maskot, oyuncu deli gibi tiklarken bile uyurdu.
        /// </summary>
        void OnPlayerClicked()
        {
            _idleSeconds = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _idleSeconds += dt;

            if (_happyTimer > 0f) _happyTimer -= dt;

            if (_bubbleTimer > 0f)
            {
                _bubbleTimer -= dt;
                if (_bubbleTimer <= 0f && _bubbleGo != null) _bubbleGo.SetActive(false);
            }

            Sprite target = idleSprite;

            if (_speed != null && _speed.IsOverheated)
            {
                target = overheatedSprite;
                if (_bubbleTimer <= 0f && Random.value < 0.05f)
                    Say(RandomQuote("mascot_overheat_", LocalizationManager.MascotOverheatQuoteCount), 1.8f);
            }
            else if (_speed != null && _speed.OverheatProgress >= 0.70f)
            {
                target = panicSprite != null ? panicSprite : overheatedSprite;
                if (_bubbleTimer <= 0f && Random.value < 0.04f)
                    Say(RandomQuote("mascot_panic_", LocalizationManager.MascotPanicQuoteCount), 1.8f);
            }
            else if (_speed != null && _speed.InRedline)
            {
                target = laserSprite != null ? laserSprite : happySprite;
                if (_bubbleTimer <= 0f && Random.value < 0.02f)
                    Say(RandomQuote("mascot_laser_", LocalizationManager.MascotLaserQuoteCount), 1.8f);
            }
            else if (_happyTimer > 0f)
            {
                target = happySprite;
            }
            else if (_idleSeconds > sleepAfterSeconds && sleepingSprite != null)
            {
                target = sleepingSprite;
            }

            Apply(target);

            if (_rect == null) return;

            float y = _baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            if (Mathf.Abs(y - _appliedY) > 0.25f)
            {
                _appliedY = y;
                Vector2 pos = _rect.anchoredPosition;
                pos.y = y;
                _rect.anchoredPosition = pos;
            }

            if (!Mathf.Approximately(_scaleBounce, 1f))
            {
                _scaleBounce = Mathf.Lerp(_scaleBounce, 1f, Time.deltaTime * 12f);
                if (Mathf.Abs(_scaleBounce - 1f) < 0.002f) _scaleBounce = 1f;

                _rect.localScale = new Vector3(_scaleBounce, _scaleBounce, 1f);
            }
        }

        float _appliedY = float.NaN;

        void Apply(Sprite sprite)
        {
            if (sprite == null || image == null || _current == sprite) return;

            _current = sprite;
            image.sprite = sprite;
        }

        public void Bind(Image target, Sprite idle, Sprite happy, Sprite overheated, Sprite laser = null, Sprite sleeping = null, Sprite panic = null)
        {
            image = target;
            idleSprite = idle;
            happySprite = happy;
            overheatedSprite = overheated;
            laserSprite = laser;
            sleepingSprite = sleeping;
            panicSprite = panic;
        }
    }
}
