using System.Collections.Generic;
using System.Text;
using SpeedDownload.Core;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Kupa paneli: gunluk gorevler (ust) + basarimlar (alt liste).
    ///
    /// NEDEN TEK PANEL
    /// ---------------
    /// Ikisi ayri overlay olabilirdi ama ust HUD'da zaten dort dugme var
    /// (reklam, arsiv, ayarlar ve bu). Besinci bir dugme, sag ust kosede
    /// parmakla isabet edilemeyecek kadar sik bir sira olustururdu. Ustelik
    /// ikisi de "ilerlemem nerede?" sorusuna cevap veriyor; ayni yerde
    /// durmalari dogru.
    ///
    /// Satirlar bir kez uretilip sonrasinda yalnizca tazeleniyor: gorev/basarim
    /// ilerlemesi her dosyada degisiyor ve her seferinde Instantiate etmek
    /// panel acikken surekli GC uretirdi.
    /// </summary>
    public class TrophyPanelView : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI summaryText;

        [Tooltip("Gunluk gorev satirlarinin konacagi kap.")]
        [SerializeField] RectTransform questContainer;

        [Tooltip("Basarim satirlarinin konacagi kap (kaydirilabilir).")]
        [SerializeField] RectTransform achievementContainer;

        [SerializeField] TextMeshProUGUI questHeaderText;
        [SerializeField] TextMeshProUGUI achievementHeaderText;

        GameManager _gm;
        DailyQuestManager _quests;
        AchievementManager _achievements;

        readonly List<QuestRow> _questRows = new List<QuestRow>();
        readonly List<AchievementRow> _achievementRows = new List<AchievementRow>();
        readonly StringBuilder _sb = new StringBuilder(96);

        Sprite _trophySprite;
        Sprite _starSprite;

        /// <summary>Bir gunluk gorev satiri.</summary>
        class QuestRow
        {
            public TextMeshProUGUI label;
            public Image barFill;
            public Button claimButton;
            public TextMeshProUGUI claimLabel;
            public int index;
        }

        /// <summary>Bir basarim satiri.</summary>
        class AchievementRow
        {
            public GameObject root;
            public Image icon;
            public TextMeshProUGUI label;
            public Image barFill;
        }

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) { enabled = false; return; }

            _quests = _gm.Quests;
            _achievements = _gm.Achievements;

            _trophySprite = Resources.Load<Sprite>("ui_icon_trophy");
            _starSprite = Resources.Load<Sprite>("ui_icon_star_holo");

            BuildQuestRows();
            BuildAchievementRows();

            if (_quests != null) _quests.Changed += Refresh;
            if (_achievements != null) _achievements.Changed += Refresh;
            LocalizationManager.LanguageChanged += Refresh;

            Refresh();
        }

        void OnDestroy()
        {
            if (_quests != null) _quests.Changed -= Refresh;
            if (_achievements != null) _achievements.Changed -= Refresh;
            LocalizationManager.LanguageChanged -= Refresh;
        }

        // ------------------------------------------------------------------
        // Kurulum
        // ------------------------------------------------------------------

        void BuildQuestRows()
        {
            if (questContainer == null) return;

            for (int i = 0; i < DailyQuestManager.QuestsPerDay; i++)
                _questRows.Add(CreateQuestRow(i));
        }

        QuestRow CreateQuestRow(int index)
        {
            var row = new QuestRow { index = index };

            GameObject go = NewRow(questContainer, "Quest_" + index, 96f);
            var rt = (RectTransform)go.transform;

            row.label = AddLabel(rt, 26f, new Vector2(16f, 44f), new Vector2(-190f, -8f),
                                 TextAlignmentOptions.TopLeft, new Color(0.92f, 0.95f, 1f, 1f));

            row.barFill = AddBar(rt, new Vector2(16f, 14f), new Vector2(-190f, 34f));

            // "AL" dugmesi — satirin sagi
            GameObject btnGo = new GameObject("Claim", typeof(RectTransform));
            btnGo.transform.SetParent(rt, false);

            var btnRt = (RectTransform)btnGo.transform;
            btnRt.anchorMin = new Vector2(1f, 0.5f);
            btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.sizeDelta = new Vector2(160f, 64f);
            btnRt.anchoredPosition = new Vector2(-12f, 0f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.16f, 0.72f, 0.42f, 1f);
            btnImg.raycastTarget = true;

            row.claimButton = btnGo.AddComponent<Button>();
            row.claimButton.targetGraphic = btnImg;
            row.claimButton.transition = Selectable.Transition.None;

            int captured = index;
            row.claimButton.onClick.AddListener(delegate { OnClaim(captured); });

            row.claimLabel = AddLabel(btnRt, 26f, Vector2.zero, Vector2.zero,
                                      TextAlignmentOptions.Center, Color.white);

            return row;
        }

        void BuildAchievementRows()
        {
            if (achievementContainer == null) return;

            AchievementDef[] defs = AchievementManager.Definitions;

            for (int i = 0; i < defs.Length; i++)
            {
                var row = new AchievementRow();

                GameObject go = NewRow(achievementContainer, "Ach_" + defs[i].id, 84f);
                row.root = go;

                var rt = (RectTransform)go.transform;

                // Kupa simgesi — solda
                GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(rt, false);

                var iconRt = (RectTransform)iconGo.transform;
                iconRt.anchorMin = new Vector2(0f, 0.5f);
                iconRt.anchorMax = new Vector2(0f, 0.5f);
                iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.sizeDelta = new Vector2(56f, 56f);
                iconRt.anchoredPosition = new Vector2(14f, 0f);

                row.icon = iconGo.AddComponent<Image>();
                row.icon.sprite = _trophySprite;
                row.icon.preserveAspect = true;
                row.icon.raycastTarget = false;
                row.icon.enabled = _trophySprite != null;

                row.label = AddLabel(rt, 24f, new Vector2(84f, 36f), new Vector2(-16f, -6f),
                                     TextAlignmentOptions.TopLeft, new Color(0.88f, 0.92f, 1f, 1f));

                row.barFill = AddBar(rt, new Vector2(84f, 12f), new Vector2(-16f, 26f));

                _achievementRows.Add(row);
            }
        }

        // ------------------------------------------------------------------
        // Kucuk UI yardimcilari
        //
        // SceneBuilder'daki yardimcilar Editor derlemesinde; bu bilesen calisma
        // zamaninda satir uretmek zorunda oldugu icin kendi minimal surumleri.
        // ------------------------------------------------------------------

        static GameObject NewRow(RectTransform parent, string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(0f, height);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.05f);
            bg.raycastTarget = false;

            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;

            return go;
        }

        static TextMeshProUGUI AddLabel(RectTransform parent, float size, Vector2 offsetMin,
                                        Vector2 offsetMax, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;

            // Truncate, Ellipsis DEGIL: projenin fontunda (LiberationSans SDF)
            // "…" karakteri yok. Ellipsis secilirse TMP her cizimde uyari
            // basiyor ve zaten Truncate'e dusuyordu — dogrudan onu istemek
            // hem ayni sonucu veriyor hem de konsolu temiz birakiyor.
            t.overflowMode = TextOverflowModes.Truncate;
            return t;
        }

        static Image AddBar(RectTransform parent, Vector2 offsetMin, Vector2 offsetMax)
        {
            var trackGo = new GameObject("BarTrack", typeof(RectTransform));
            trackGo.transform.SetParent(parent, false);

            var trackRt = (RectTransform)trackGo.transform;
            trackRt.anchorMin = new Vector2(0f, 0f);
            trackRt.anchorMax = new Vector2(1f, 0f);
            trackRt.pivot = new Vector2(0.5f, 0f);
            trackRt.offsetMin = new Vector2(offsetMin.x, offsetMin.y);
            trackRt.offsetMax = new Vector2(offsetMax.x, offsetMin.y + 10f);

            var trackImg = trackGo.AddComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, 0.12f);
            trackImg.raycastTarget = false;

            var fillGo = new GameObject("BarFill", typeof(RectTransform));
            fillGo.transform.SetParent(trackGo.transform, false);

            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;

            var fill = fillGo.AddComponent<Image>();
            fill.color = new Color(0.25f, 0.95f, 0.45f, 1f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            return fill;
        }

        // ------------------------------------------------------------------
        // Tazeleme
        // ------------------------------------------------------------------

        void OnClaim(int index)
        {
            if (_quests == null) return;
            if (_quests.TryClaim(index)) Refresh();
        }

        public void Refresh()
        {
            if (titleText != null)
                titleText.SetText(LocalizationManager.T("trophy_title", "TROPHIES & QUESTS"));

            if (questHeaderText != null)
                questHeaderText.SetText(LocalizationManager.T("quest_header", "DAILY QUESTS"));

            if (achievementHeaderText != null)
                achievementHeaderText.SetText(LocalizationManager.T("ach_header", "ACHIEVEMENTS"));

            RefreshSummary();
            RefreshQuests();
            RefreshAchievements();
        }

        void RefreshSummary()
        {
            if (summaryText == null) return;

            int unlocked = _achievements != null ? _achievements.UnlockedCount : 0;
            int total = _achievements != null ? _achievements.TotalCount : 0;
            int holo = _gm != null && _gm.Collection != null ? _gm.Collection.HoloCount : 0;

            _sb.Length = 0;
            _sb.Append(LocalizationManager.TF("trophy_summary", "Achievements {0}/{1}", unlocked, total));

            // Holo sayaci yalnizca en az bir tane dustuyse gosteriliyor: sifir
            // gosteren bir sayac, oyuncuya var oldugunu bile anlatmadan yer
            // kaplar ve "eksik bir sey mi var?" hissi verir.
            if (holo > 0)
            {
                _sb.Append("   <color=#FFD638>")
                   .Append(LocalizationManager.TF("trophy_holo", "Holo {0}", holo))
                   .Append("</color>");
            }

            summaryText.SetText(_sb);
        }

        void RefreshQuests()
        {
            for (int i = 0; i < _questRows.Count; i++)
            {
                QuestRow row = _questRows[i];
                if (_quests == null) continue;

                QuestDef def = _quests.GetQuest(i);

                // Gorevler HENUZ KURULMAMIS olabilir.
                //
                // Bu panel varsayilan calisma sirasinda (0), SaveManager ise
                // 100'de: yani ilk Refresh, gunun gorevleri secilmeden ONCE
                // calisiyor ve o anda def.key null. Satiri sessizce bos
                // birakmak dogru davranis — kayit yuklenince Changed olayi
                // zaten yeniden tazeleyecek.
                if (string.IsNullOrEmpty(def.key))
                {
                    if (row.label != null) row.label.SetText("");
                    if (row.barFill != null) row.barFill.fillAmount = 0f;
                    if (row.claimButton != null) row.claimButton.interactable = false;
                    if (row.claimLabel != null) row.claimLabel.SetText("");
                    continue;
                }
                long done = _quests.ProgressOf(i);
                bool complete = _quests.IsComplete(i);
                bool claimed = _quests.IsClaimed(i);

                if (row.label != null)
                {
                    _sb.Length = 0;
                    _sb.Append(LocalizationManager.TF(def.key, def.key, def.amount))
                       .Append("   <alpha=#88>").Append(done).Append(" / ").Append(def.amount);
                    row.label.SetText(_sb);
                }

                if (row.barFill != null)
                {
                    row.barFill.fillAmount = def.amount <= 0 ? 1f
                        : Mathf.Clamp01((float)((double)done / def.amount));
                    row.barFill.color = complete
                        ? new Color(1f, 0.84f, 0.22f, 1f)
                        : new Color(0.25f, 0.95f, 0.45f, 1f);
                }

                bool canClaim = complete && !claimed;

                if (row.claimButton != null)
                {
                    row.claimButton.interactable = canClaim;

                    var img = row.claimButton.targetGraphic as Image;
                    if (img != null)
                    {
                        img.color = claimed ? new Color(0.30f, 0.34f, 0.40f, 1f)
                                  : canClaim ? new Color(0.16f, 0.72f, 0.42f, 1f)
                                             : new Color(0.20f, 0.24f, 0.30f, 1f);
                    }
                }

                if (row.claimLabel == null) continue;

                if (claimed)
                {
                    row.claimLabel.SetText(LocalizationManager.T("quest_claimed", "DONE"));
                }
                else if (canClaim)
                {
                    // Odulu YAZ: "AL" tek basina ne kazanildigini soylemiyor.
                    row.claimLabel.SetText(NumberFormatter.FormatMoney(_quests.RewardOf(i)));
                }
                else
                {
                    row.claimLabel.SetText(NumberFormatter.FormatMoney(_quests.RewardOf(i)));
                }
            }
        }

        void RefreshAchievements()
        {
            AchievementDef[] defs = AchievementManager.Definitions;

            for (int i = 0; i < _achievementRows.Count && i < defs.Length; i++)
            {
                AchievementRow row = _achievementRows[i];
                AchievementDef def = defs[i];

                bool unlocked = _achievements != null && _achievements.IsUnlocked(def.id);
                float progress = _achievements != null ? _achievements.Progress01(def) : 0f;
                long current = _achievements != null ? _achievements.CurrentValue(def) : 0L;

                if (row.icon != null)
                {
                    // Kilitli basarim soluk: liste tek bakista "nerede
                    // duruyorum" sorusuna cevap vermeli.
                    row.icon.color = unlocked ? Color.white : new Color(1f, 1f, 1f, 0.22f);
                }

                if (row.label != null)
                {
                    _sb.Length = 0;
                    _sb.Append(LocalizationManager.TF("ach_" + def.id, def.id, def.target));

                    if (unlocked)
                    {
                        _sb.Append("   <color=#FFD638>+")
                           .Append(def.creditReward).Append(' ')
                           .Append(LocalizationManager.T("unit_credits", "FC")).Append("</color>");
                    }
                    else
                    {
                        _sb.Append("   <alpha=#77>").Append(current).Append(" / ").Append(def.target);
                    }

                    row.label.SetText(_sb);
                }

                if (row.barFill == null) continue;

                row.barFill.fillAmount = progress;
                row.barFill.color = unlocked
                    ? new Color(1f, 0.84f, 0.22f, 1f)
                    : new Color(0.35f, 0.65f, 0.95f, 1f);
            }
        }

        /// <summary>SceneBuilder icin.</summary>
        public void Bind(TextMeshProUGUI title, TextMeshProUGUI summary,
                         TextMeshProUGUI questHeader, RectTransform questRoot,
                         TextMeshProUGUI achHeader, RectTransform achRoot)
        {
            titleText = title;
            summaryText = summary;
            questHeaderText = questHeader;
            questContainer = questRoot;
            achievementHeaderText = achHeader;
            achievementContainer = achRoot;
        }
    }
}
