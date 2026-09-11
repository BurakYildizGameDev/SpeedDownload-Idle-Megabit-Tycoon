using SpeedDownload.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// x1 / x10 / MAKS satin alma modu dugmesi.
    ///
    /// Sinirsiz yukseltmeleri elli kez tiklamak eziyetti. Mod
    /// <see cref="UpgradeCardView.BulkMode"/> uzerinde paylasiliyor; tek dugme
    /// gorunurdeki tum kartlari birden etkiliyor.
    /// </summary>
    public class BulkBuyToggleView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] UpgradeListView list;

        // -1 = MAKS
        static readonly int[] Modes = { 1, 10, -1 };
        int _index;

        void Awake()
        {
            if (button != null) button.onClick.AddListener(Cycle);
        }

        void Start()
        {
            Apply();
            LocalizationManager.LanguageChanged += Apply;
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Cycle);
            LocalizationManager.LanguageChanged -= Apply;
        }

        void Cycle()
        {
            _index = (_index + 1) % Modes.Length;
            Apply();
        }

        void Apply()
        {
            UpgradeCardView.BulkMode = Modes[_index];

            if (label != null)
            {
                label.SetText(Modes[_index] < 0
                    ? LocalizationManager.T("bulk_max", "MAX")
                    : "x" + Modes[_index]);
            }

            // Kartlarin maliyet yazilari moda gore degistigi icin tazelenmeli.
            if (list != null) list.RefreshCards();
        }

        public void Bind(Button btn, TextMeshProUGUI text, UpgradeListView listView)
        {
            button = btn;
            label = text;
            list = listView;
        }
    }
}
