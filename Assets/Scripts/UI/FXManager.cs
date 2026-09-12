using SpeedDownload.Core;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>Havuzlanan efekt turleri (PLAN Bolum 7.1, 7.2).</summary>
    public enum FXType
    {
        ClickRipple = 0,
        CriticalBurst = 1,
        CoinBurst = 2,
        TurboFlame = 3,
        OverheatFlash = 4,
        OverheatSmoke = 5,
        Confetti = 6
    }

    /// <summary>
    /// Efekt havuzu. Faz 9 test olcutu "GC allocation'siz oynuyor" oldugu icin
    /// tasarim buna gore:
    ///
    ///   - Tum ornekler Start'ta uretilir, oyun sirasinda Instantiate YOK.
    ///   - Play() yolunda new / kapanis (closure) / LINQ / string yok.
    ///   - Havuz dizisi sabit boyutlu; dolduysa en eski ornek geri donusturulur
    ///     (buyume yok, dolayisiyla dizi yeniden tahsisi de yok).
    ///
    /// Efektler tek bir FXLayer altinda, en ustte cizilir.
    /// </summary>
    public class FXManager : MonoBehaviour
    {
        [System.Serializable]
        public struct FXDef
        {
            public FXType type;
            public Sprite sprite;
            public float duration;
            public float startScale;
            public float endScale;
            public float startAlpha;
            public float riseDistance;   // yukari suzulme (px)
            public float spin;           // sn basina donme (derece)
            public Color tint;
            public int poolSize;
        }

        [SerializeField] RectTransform layer;
        [SerializeField] FXDef[] definitions;

        // Havuz: her tur icin sabit boyutlu dilim. Butun state paralel
        // dizilerde tutuluyor — MonoBehaviour basina bir bilesen aramak yerine
        // duz dizi erisimi, hem hizli hem tahsisatsiz.
        RectTransform[] _rects;
        Image[] _images;
        int[] _defIndex;      // ornek -> tanim
        float[] _elapsed;     // -1 = bosta
        float[] _spinSeed;
        Vector2[] _origin;

        int[] _sliceStart;    // tanim -> havuzdaki ilk ornek
        int[] _sliceCount;
        int[] _cursor;        // tanim -> siradaki aday (round-robin)

        bool _ready;

        // ------------------------------------------------------------------
        // Yuzen odul yazisi
        //
        // Sprite havuzuyla ayni desen. Tek farki: para bicimlemesi kacinilmaz
        // olarak string uretiyor. Dosya tamamlama saniyede birkac kez oldugu
        // icin bu kabul edilebilir — tiklama yolunda (her karede) olsaydi olmazdi.
        // ------------------------------------------------------------------

        [Header("Yuzen odul yazisi")]
        [SerializeField] int floatingTextPool = 8;
        [SerializeField] float floatingTextDuration = 1.1f;
        [SerializeField] float floatingTextRise = 170f;
        [SerializeField] float floatingTextSize = 42f;
        [SerializeField] Color floatingTextColor = new Color(0.29f, 0.95f, 0.45f, 1f);

        TextMeshProUGUI[] _texts;
        RectTransform[] _textRects;
        float[] _textElapsed;
        Vector2[] _textOrigin;
        int _textCursor;

        void BuildFloatingTexts()
        {
            if (layer == null) return;

            int count = Mathf.Max(1, floatingTextPool);
            _texts = new TextMeshProUGUI[count];
            _textRects = new RectTransform[count];
            _textElapsed = new float[count];
            _textOrigin = new Vector2[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("FX_Text_" + i, typeof(RectTransform));
                go.transform.SetParent(layer, false);

                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(420f, 70f);

                var text = go.AddComponent<TextMeshProUGUI>();
                text.fontSize = floatingTextSize;
                text.alignment = TextAlignmentOptions.Center;
                text.fontStyle = FontStyles.Bold;
                text.raycastTarget = false;
                text.enabled = false;

                _texts[i] = text;
                _textRects[i] = rect;
                _textElapsed[i] = -1f;
            }
        }

        /// <summary>Verilen konumda yukari suzulen bir yazi baslatir.</summary>
        public void PlayText(string content, Vector2 anchoredPosition)
        {
            if (_texts == null || string.IsNullOrEmpty(content)) return;

            int slot = _textCursor;
            _textCursor = (_textCursor + 1) % _texts.Length;

            _textElapsed[slot] = 0f;
            _textOrigin[slot] = anchoredPosition;

            _textRects[slot].anchoredPosition = anchoredPosition;
            _texts[slot].SetText(content);
            _texts[slot].color = floatingTextColor;
            _texts[slot].enabled = true;
        }

        void TickFloatingTexts(float dt)
        {
            if (_texts == null) return;

            float duration = Mathf.Max(0.05f, floatingTextDuration);

            for (int i = 0; i < _texts.Length; i++)
            {
                if (_textElapsed[i] < 0f) continue;

                _textElapsed[i] += dt;
                float t = _textElapsed[i] / duration;

                if (t >= 1f)
                {
                    _textElapsed[i] = -1f;
                    _texts[i].enabled = false;
                    continue;
                }

                Vector2 pos = _textOrigin[i];
                pos.y += floatingTextRise * t;
                _textRects[i].anchoredPosition = pos;

                Color c = floatingTextColor;
                // Son ucte solsun; basta tam opak dursun ki okunabilsin.
                c.a = t < 0.65f ? 1f : Mathf.InverseLerp(1f, 0.65f, t);
                _texts[i].color = c;
            }
        }

        void Start()
        {
            Build();
            BuildFloatingTexts();
            Subscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        // ------------------------------------------------------------------
        // Havuz kurulumu (yalnizca bir kez)
        // ------------------------------------------------------------------

        void Build()
        {
            if (layer == null || definitions == null || definitions.Length == 0) return;

            int total = 0;
            for (int i = 0; i < definitions.Length; i++)
                total += Mathf.Max(1, definitions[i].poolSize);

            _rects = new RectTransform[total];
            _images = new Image[total];
            _defIndex = new int[total];
            _elapsed = new float[total];
            _spinSeed = new float[total];
            _origin = new Vector2[total];

            _sliceStart = new int[definitions.Length];
            _sliceCount = new int[definitions.Length];
            _cursor = new int[definitions.Length];

            int cursor = 0;
            for (int d = 0; d < definitions.Length; d++)
            {
                int count = Mathf.Max(1, definitions[d].poolSize);
                _sliceStart[d] = cursor;
                _sliceCount[d] = count;

                for (int k = 0; k < count; k++)
                {
                    var go = new GameObject("FX_" + definitions[d].type + "_" + k, typeof(RectTransform));
                    go.transform.SetParent(layer, false);

                    var rect = (RectTransform)go.transform;
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.sizeDelta = new Vector2(256f, 256f);

                    var img = go.AddComponent<Image>();
                    img.sprite = definitions[d].sprite;
                    img.raycastTarget = false;
                    img.enabled = false;

                    _rects[cursor] = rect;
                    _images[cursor] = img;
                    _defIndex[cursor] = d;
                    _elapsed[cursor] = -1f;
                    cursor++;
                }
            }

            _ready = true;
        }

        // ------------------------------------------------------------------
        // Oynatma — bu yol tahsisat yapmaz
        // ------------------------------------------------------------------

        public void Play(FXType type, Vector2 anchoredPosition)
        {
            if (!_ready) return;

            int d = FindDefinition(type);
            if (d < 0) return;

            int slot = NextSlot(d);
            if (slot < 0) return;

            _elapsed[slot] = 0f;
            _origin[slot] = anchoredPosition;
            _spinSeed[slot] = Random.Range(-1f, 1f);

            RectTransform rect = _rects[slot];
            rect.anchoredPosition = anchoredPosition;
            rect.localRotation = Quaternion.identity;

            FXDef def = definitions[d];
            float s = def.startScale;
            rect.localScale = new Vector3(s, s, 1f);

            Image img = _images[slot];
            Color c = def.tint;
            c.a = def.startAlpha;
            img.color = c;
            img.enabled = true;
        }

        int FindDefinition(FXType type)
        {
            for (int i = 0; i < definitions.Length; i++)
                if (definitions[i].type == type) return i;
            return -1;
        }

        /// <summary>
        /// Once bostaki bir ornegi arar; hepsi mesgulse en eskiyi geri donusturur.
        /// Havuz hic buyumez — kare basina tahsisat sifir kalir.
        /// </summary>
        int NextSlot(int d)
        {
            int start = _sliceStart[d];
            int count = _sliceCount[d];
            if (count <= 0) return -1;

            for (int k = 0; k < count; k++)
            {
                int index = start + (_cursor[d] + k) % count;
                if (_elapsed[index] < 0f)
                {
                    _cursor[d] = (_cursor[d] + k + 1) % count;
                    return index;
                }
            }

            int oldest = start;
            float best = -1f;
            for (int k = 0; k < count; k++)
            {
                int index = start + k;
                if (_elapsed[index] > best) { best = _elapsed[index]; oldest = index; }
            }
            return oldest;
        }

        [Tooltip("Redline'dayken turbo alevinin tekrarlanma araligi (sn). " +
                 "Surekli bir nesne yerine havuzdan kisa parlamalar — titrek alev " +
                 "etkisi veriyor ve ayri bir kod yolu gerektirmiyor.")]
        [SerializeField] float flameInterval = 0.32f;

        float _flameTimer;

        void TickRedlineFlame(float dt)
        {
            if (_speed == null || !_speed.InRedline || _speed.IsOverheated)
            {
                _flameTimer = 0f;
                return;
            }

            _flameTimer -= dt;
            if (_flameTimer > 0f) return;

            _flameTimer = flameInterval;
            Play(FXType.TurboFlame, FlameAnchor);
        }

        static readonly Vector2 FlameAnchor = new Vector2(0f, -150f);

        void Update()
        {
            if (!_ready) return;

            float dt = Time.deltaTime;
            TickRedlineFlame(dt);
            TickFloatingTexts(dt);

            for (int i = 0; i < _rects.Length; i++)
            {
                if (_elapsed[i] < 0f) continue;

                FXDef def = definitions[_defIndex[i]];
                float duration = Mathf.Max(0.01f, def.duration);

                _elapsed[i] += dt;
                float t = _elapsed[i] / duration;

                if (t >= 1f)
                {
                    _elapsed[i] = -1f;
                    _images[i].enabled = false;
                    continue;
                }

                float scale = Mathf.Lerp(def.startScale, def.endScale, t);
                _rects[i].localScale = new Vector3(scale, scale, 1f);

                if (def.riseDistance != 0f)
                {
                    Vector2 pos = _origin[i];
                    pos.y += def.riseDistance * t;
                    _rects[i].anchoredPosition = pos;
                }

                if (def.spin != 0f)
                {
                    _rects[i].localRotation = Quaternion.Euler(
                        0f, 0f, def.spin * _spinSeed[i] * _elapsed[i]);
                }

                Color c = def.tint;
                c.a = def.startAlpha * (1f - t);
                _images[i].color = c;
            }
        }

        // ------------------------------------------------------------------
        // Oyun olaylarina baglanma
        // ------------------------------------------------------------------

        SpeedController _speed;
        DownloadController _download;
        PrestigeManager _prestige;
        CollectionManager _collection;

        void Subscribe()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            _speed = gm.Speed;
            _download = gm.Download;
            _prestige = gm.Prestige;
            _collection = gm.Collection;

            if (_speed != null)
            {
                _speed.Clicked += OnClicked;
                _speed.ClickedWithData += OnClickedWithData;
                _speed.CriticalHit += OnCritical;
                _speed.OverheatStarted += OnOverheat;
                _speed.ComboUpdated += OnComboUpdated;
            }

            if (_download != null) _download.FileCompleted += OnFileCompleted;
            if (_prestige != null) _prestige.Prestiged += OnPrestiged;
            if (_collection != null) _collection.HoloDropped += OnHoloDropped;

            // Satin alma ve kademe atlama: seyrek ama oyunun en onemli iki ani.
            if (gm.Economy != null) gm.Economy.Purchased += OnPurchased;
            gm.TierChanged += OnTierChanged;
        }

        void OnPurchased(Data.UpgradeSO upgrade)
        {
            HapticManager.MediumImpact();
        }

        void OnTierChanged(Data.ConnectionTierSO tier)
        {
            // Baslangic kademesi bir kutlama degil, oyunun acilisi.
            if (tier == null || tier.tierIndex == 0) return;

            // Yalnizca oyuncu GERCEKTEN yeni bir kademeye gectiyse kutla.
            // Kayittan yukleme ve prestij sifirlamasi da TierChanged yayinliyor;
            // bu kontrol olmadan oyun her acilista konfeti patlatip telefonu
            // titretiyordu (kademe 0'dan buyuk kayitli her oyuncuda).
            GameManager gm = GameManager.Instance;
            if (gm != null && !gm.IsCelebratedTierChange) return;

            Play(FXType.Confetti, Vector2.zero);
            HapticManager.HeavyImpact();
        }

        void OnHoloDropped(Data.FileDataSO file, double bonus)
        {
            Play(FXType.Confetti, Vector2.zero);
            HapticManager.HeavyImpact();
        }

        void OnComboUpdated(int combo, float multiplier)
        {
            if (combo == 10 || combo == 25 || combo == 50)
            {
                HapticManager.MediumImpact();
                Play(FXType.CriticalBurst, Vector2.zero);
            }
        }

        void Unsubscribe()
        {
            if (_speed != null)
            {
                _speed.Clicked -= OnClicked;
                _speed.ClickedWithData -= OnClickedWithData;
                _speed.CriticalHit -= OnCritical;
                _speed.OverheatStarted -= OnOverheat;
                _speed.ComboUpdated -= OnComboUpdated;
            }

            if (_download != null) _download.FileCompleted -= OnFileCompleted;
            if (_prestige != null) _prestige.Prestiged -= OnPrestiged;
            if (_collection != null) _collection.HoloDropped -= OnHoloDropped;

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                if (gm.Economy != null) gm.Economy.Purchased -= OnPurchased;
                gm.TierChanged -= OnTierChanged;
            }
        }

        void OnClicked() { }

        void OnClickedWithData(Vector2 screenPos, double gain, bool isCrit)
        {
            // Konumu olmayan tiklamalar (otomasyon, test) SpeedController.NoPosition
            // gonderiyor; onlarda efekt merkeze dusuyor. Kontrol NaN uzerinden:
            // eskiden (0,0) "konum yok" sayiliyordu ve ekranin sol alt kosesine
            // dokunan oyuncunun dalgasi da merkeze kaciyordu.
            Vector2 localPos = Vector2.zero;
            if (layer != null && !float.IsNaN(screenPos.x))
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPos, null, out localPos);
            }

            Play(FXType.ClickRipple, localPos);

            if (isCrit)
            {
                Play(FXType.CriticalBurst, localPos);

                // Kritik vurusta YALNIZCA agir darbe.
                //
                // Eskiden once ClickTick sonra HeavyImpact caliyordu. Android'de
                // ard arda gelen iki vibrate() cagrisinda ikincisi birincisini
                // iptal ediyor: tik bosa gidiyor, bazi ROM'larda da araya bir
                // kekemelik giriyordu. Kritik vurus zaten hissedilmesi gereken
                // an; onu tek ve net bir darbeyle vermek daha dogru.
                HapticManager.HeavyImpact();
                return;
            }

            // Tiklama titresimi geri geldi. Onceki surumde pil kaygisiyla
            // tamamen kaldirilmisti, ama bir clicker'in tek girdisi dokunmak:
            // dokunusun karsiliginda hicbir sey hissedilmeyince oyuncu titresimi
            // "bozuk" sayiyor. ClickTick kendi bekleme suresini tasiyor
            // (saniyede en fazla ~8 darbe), yani hizli tiklamada motor
            // kesintisiz calismiyor.
            HapticManager.ClickTick();
        }

        void OnCritical() { }

        void OnOverheat()
        {
            Play(FXType.OverheatFlash, Vector2.zero);
            Play(FXType.OverheatSmoke, Vector2.zero);
            HapticManager.HeavyImpact();
        }

        void OnFileCompleted(Data.FileDataSO file, double reward)
        {
            Play(FXType.CoinBurst, CoinBurstAnchor);

            // Odul miktarini gostermek, tiklama-kazanc dongusunu somutlastiriyor;
            // yalnizca para efekti patlatmak "ne kazandim?" sorusunu cevapsiz birakiyordu.
            if (reward > 0.0)
                PlayText("+" + NumberFormatter.FormatMoney(reward), CoinBurstAnchor);
        }

        void OnPrestiged(int earned, int total)
        {
            Play(FXType.Confetti, Vector2.zero);
        }

        /// <summary>Para efekti ust HUD'a dogru cikar.</summary>
        static readonly Vector2 CoinBurstAnchor = new Vector2(0f, 220f);

        public void Bind(RectTransform fxLayer, FXDef[] defs)
        {
            layer = fxLayer;
            definitions = defs;
        }
    }
}
