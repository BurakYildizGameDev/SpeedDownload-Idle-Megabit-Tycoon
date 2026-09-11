using System.Collections.Generic;
using SpeedDownload.Core;
using SpeedDownload.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Secili sekmedeki yukseltme kartlarini uretir ve guncel tutar.
    ///
    /// Kartlar sekme degisince yeniden kurulur; bakiye degisince yalnizca
    /// tazelenir. Yukseltme sayisi 13 oldugu icin havuzlamaya gerek yok, ama
    /// her bakiye degisiminde Instantiate etmek de bos yere GC olurdu — bu
    /// yuzden ikisi ayri yollar.
    /// </summary>
    public class UpgradeListView : MonoBehaviour
    {
        [SerializeField] RectTransform content;
        [SerializeField] GameObject cardPrefab;
        [SerializeField] UpgradeTab currentTab = UpgradeTab.Infrastructure;

        readonly List<UpgradeCardView> _cards = new List<UpgradeCardView>();

        EconomyManager _economy;
        Wallet _wallet;
        GameDatabaseSO _database;

        public UpgradeTab CurrentTab { get { return currentTab; } }

        GameObject _undoButtonGo;
        Button _undoButton;
        Sprite _undoSprite;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { enabled = false; return; }

            _economy = gm.Economy;
            _wallet = gm.Money;
            _database = gm.Database;

            _undoSprite = Resources.Load<Sprite>("ui_icon_undo");
            EnsureUndoButton();

            if (_wallet != null) _wallet.BalanceChanged += OnBalanceChanged;
            if (_economy != null)
            {
                _economy.Purchased += OnPurchased;
                _economy.StatsChanged += RefreshCards;
                _economy.UndoStateChanged += RefreshUndoButton;
            }

            Rebuild();
        }

        void EnsureUndoButton()
        {
            if (_undoButtonGo != null) return;

            _undoButtonGo = new GameObject("UndoButton", typeof(RectTransform));
            _undoButtonGo.transform.SetParent(transform, false);

            var rt = (RectTransform)_undoButtonGo.transform;
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -10f);
            rt.sizeDelta = new Vector2(80f, 80f);

            var img = _undoButtonGo.AddComponent<UnityEngine.UI.Image>();
            img.sprite = _undoSprite;
            img.preserveAspect = true;

            _undoButton = _undoButtonGo.AddComponent<Button>();
            _undoButton.targetGraphic = img;
            _undoButton.onClick.AddListener(OnUndoClicked);

            _undoButtonGo.SetActive(false);
        }

        void OnUndoClicked()
        {
            if (_economy == null) return;
            if (_economy.TryUndo())
            {
                HapticManager.LightTap();
                RefreshCards();
                if (_undoButtonGo != null) _undoButtonGo.SetActive(false);
            }
        }

        void RefreshUndoButton()
        {
            if (_undoButtonGo == null) EnsureUndoButton();
            if (_undoButtonGo != null && _economy != null)
            {
                _undoButtonGo.SetActive(_economy.CanUndo);
            }
        }

        void OnDestroy()
        {
            if (_wallet != null) _wallet.BalanceChanged -= OnBalanceChanged;
            if (_economy != null)
            {
                _economy.Purchased -= OnPurchased;
                _economy.StatsChanged -= RefreshCards;
                _economy.UndoStateChanged -= RefreshUndoButton;
            }
        }

        [Tooltip("Kartlarin saniyede en fazla kac kez tazelenecegi.")]
        [SerializeField] float maxRefreshRate = 6f;

        bool _dirty;
        float _cooldown;

        void OnBalanceChanged(double balance)
        {
            _dirty = true;
        }

        void Update()
        {
            if (_undoButtonGo != null && _undoButtonGo.activeSelf)
            {
                if (_economy == null || !_economy.CanUndo)
                    _undoButtonGo.SetActive(false);
            }

            if (!_dirty) return;

            _cooldown -= Time.unscaledDeltaTime;
            if (_cooldown > 0f) return;

            _cooldown = 1f / Mathf.Max(1f, maxRefreshRate);
            _dirty = false;
            RefreshCards();
        }

        void OnPurchased(UpgradeSO upgrade)
        {
            // Baglanti alinca hem kilitli kartlar acilir hem de sonsuz kademelerde
            // bir SONRAKI uretilmis yukseltme listeye girmeli — ikisi de yeniden
            // kurmayi gerektiriyor.
            if (upgrade != null && upgrade.type == UpgradeType.Connection) Rebuild();
            else RefreshCards();
        }

        public void SetTab(UpgradeTab tab)
        {
            if (currentTab == tab && _cards.Count > 0) return;
            currentTab = tab;
            Rebuild();
        }

        public void Rebuild()
        {
            if (content == null || cardPrefab == null || _database == null) return;

            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] != null) Destroy(_cards[i].gameObject);
            _cards.Clear();

            List<UpgradeSO> list = _database.GetUpgradesForTab(currentTab);

            // Sonsuz kademelerin baglanti yukseltmesi calisma aninda uretiliyor,
            // sabit dizide yok. Altyapi sekmesinde bir sonrakini basa ekle.
            if (currentTab == UpgradeTab.Infrastructure && _database.HasInfiniteTiers)
            {
                GameManager gm = GameManager.Instance;
                int next = gm != null ? gm.CurrentTierIndex + 1 : 0;

                if (next > _database.HighestFixedTierIndex)
                {
                    UpgradeSO infinite = _database.GetInfiniteConnectionUpgrade(next);
                    if (infinite != null) list.Insert(0, infinite);
                }
            }

            for (int i = 0; i < list.Count; i++)
            {
                GameObject go = Instantiate(cardPrefab, content);
                go.name = "Card_" + list[i].name;
                go.SetActive(true);

                UpgradeCardView card = go.GetComponent<UpgradeCardView>();
                if (card == null) continue;

                card.Setup(list[i], _economy);
                _cards.Add(card);
            }
        }

        public void RefreshCards()
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] != null) _cards[i].Refresh();
        }

        public void Bind(RectTransform contentRect, GameObject prefab)
        {
            content = contentRect;
            cardPrefab = prefab;
        }
    }
}
