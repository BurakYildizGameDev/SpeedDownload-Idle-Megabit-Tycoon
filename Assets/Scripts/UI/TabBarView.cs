using SpeedDownload.Data;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Alt panelin sekme cubugu (GDD Bolum 9): Altyapi · Donanim · Yazilim · Prestij.
    ///
    /// Ilk uc sekme yukseltme listesini filtreler; 4. sekme (Prestij) listenin
    /// yerine Hat Degisimi ekranini acar. Sekme sprite'lari tek durumda geldigi
    /// icin secili sekme renk ve olcekle ayrisiyor.
    /// </summary>
    public class TabBarView : MonoBehaviour
    {
        /// <summary>Prestij sekmesinin indeksi (yukseltme sekmelerinden sonra).</summary>
        public const int PrestigeTabIndex = 3;

        [SerializeField] UpgradeListView list;
        [SerializeField] GameObject listRoot;
        [SerializeField] GameObject prestigeRoot;

        [Tooltip("Toplu alim dugmesi. Prestij sekmesinde kart olmadigi icin gizlenir.")]
        [SerializeField] GameObject bulkToggleRoot;

        [SerializeField] Button[] buttons;
        [SerializeField] Image[] icons;

        [Header("Secili / secili degil")]
        [SerializeField] Color selectedColor = Color.white;
        [SerializeField] Color normalColor = new Color(0.55f, 0.60f, 0.70f, 1f);
        [SerializeField] float selectedScale = 1.12f;

        int _selected;

        void Start()
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int index = i; // closure icin kopya
                buttons[i].onClick.AddListener(delegate { Select(index); });
            }

            Select(list != null ? (int)list.CurrentTab : 0);
        }

        public void Select(int tabIndex)
        {
            _selected = tabIndex;
            bool prestige = tabIndex == PrestigeTabIndex;

            // Prestij sekmesinde de kart listesi acik kaliyor — Fiber Kredisi ile
            // alinan yetenekler orada duruyor. Hat Degisimi paneli ustte kompakt
            // bir blok olarak kaliyor, liste altina cekiliyor.
            if (list != null) list.SetTab((UpgradeTab)tabIndex);

            if (listRoot != null) listRoot.SetActive(true);
            if (prestigeRoot != null) prestigeRoot.SetActive(prestige);

            // Toplu alim yalnizca para ile alinan kartlar icin anlamli.
            if (bulkToggleRoot != null) bulkToggleRoot.SetActive(!prestige);

            ApplyListArea(prestige);

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] == null) continue;

                bool on = i == tabIndex;
                icons[i].color = on ? selectedColor : normalColor;
                icons[i].transform.localScale = Vector3.one * (on ? selectedScale : 1f);
            }
        }

        /// <summary>
        /// Prestij sekmesinde liste, Hat Degisimi panelinin altina iner; diger
        /// sekmelerde tum alani kullanir.
        /// </summary>
        void ApplyListArea(bool prestige)
        {
            if (listRect == null) return;

            Vector2 max = listRect.offsetMax;
            max.y = prestige ? prestigeModeListTop : normalModeListTop;
            listRect.offsetMax = max;
        }

        [Header("Liste alani")]
        [SerializeField] RectTransform listRect;
        [SerializeField] float normalModeListTop;
        [SerializeField] float prestigeModeListTop;

        public void BindListArea(RectTransform rect, float normalTop, float prestigeTop)
        {
            listRect = rect;
            normalModeListTop = normalTop;
            prestigeModeListTop = prestigeTop;
        }

        public void Bind(UpgradeListView listView, GameObject listGo, GameObject prestigeGo,
                         Button[] tabButtons, Image[] tabIcons, GameObject bulkGo = null)
        {
            list = listView;
            listRoot = listGo;
            prestigeRoot = prestigeGo;
            buttons = tabButtons;
            icons = tabIcons;
            bulkToggleRoot = bulkGo;
        }
    }
}
