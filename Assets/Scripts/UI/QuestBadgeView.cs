using SpeedDownload.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Kupa dugmesinin uzerindeki "alinmayi bekleyen odul var" noktasi.
    ///
    /// Neden gerekli: gunluk gorev odulu ELLE aliniyor (bkz.
    /// <see cref="DailyQuestManager.TryClaim"/>). Paneli acmayan oyuncu gorevi
    /// tamamladigini hic ogrenemez ve odul orada oylece bekler — yani ozellik
    /// tamamlanmis ama gorunmez olur. Nokta, paneli acmak icin gereken tek
    /// isareti veriyor.
    ///
    /// Nabiz atmiyor, boyut degistirmiyor: ust HUD'da surekli hareket eden bir
    /// oge, oyuncunun gozunu kadrandan koparir.
    /// </summary>
    public class QuestBadgeView : MonoBehaviour
    {
        [SerializeField] Image dot;

        DailyQuestManager _quests;
        bool _shown;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) { enabled = false; return; }

            _quests = gm.Quests;

            if (_quests != null) _quests.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_quests != null) _quests.Changed -= Refresh;
        }

        void Refresh()
        {
            bool show = _quests != null && _quests.HasClaimable;
            if (show == _shown) return;

            _shown = show;
            if (dot != null) dot.enabled = show;
        }

        /// <summary>SceneBuilder icin.</summary>
        public void Bind(Image indicator)
        {
            dot = indicator;
            if (dot != null) dot.enabled = false;
        }
    }
}
