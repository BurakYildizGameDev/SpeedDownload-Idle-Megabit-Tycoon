using SpeedDownload.Core;
using SpeedDownload.Data;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Tek bir yukseltme karti (GDD Bolum 9 alt panel).
    ///
    /// Buton gorsel durumlari Unity'nin SpriteSwap gecisiyle yonetiliyor
    /// (normal / pressed / disabled sprite'lari zaten elimizde). Kart yalnizca
    /// interactable bayragini ve metinleri gunceller.
    /// </summary>
    public class UpgradeCardView : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] Image background;
        [SerializeField] Image icon;
        [SerializeField] Image lockIcon;
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] TextMeshProUGUI descText;
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] TextMeshProUGUI costText;

        [Header("Kilometre tasi")]
        [Tooltip("En az bir esik asilinca gorunen rozet.")]
        [SerializeField] Image milestoneBadge;

        [Tooltip("Rozetin uzerindeki carpan yazisi (x2, x4 ...).")]
        [SerializeField] TextMeshProUGUI milestoneText;

        [Header("Renkler")]
        [SerializeField] Color costAffordable = new Color(0.25f, 0.95f, 0.45f, 1f);
        [SerializeField] Color costTooExpensive = new Color(1.00f, 0.38f, 0.35f, 1f);
        [SerializeField] Color costMaxed = new Color(1.00f, 1.00f, 1.00f, 1f);

        UpgradeSO _upgrade;
        EconomyManager _economy;

        public UpgradeSO Upgrade { get { return _upgrade; } }

        void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        void OnEnable()
        {
            LocalizationManager.LanguageChanged += OnLanguageChanged;
        }

        void OnDisable()
        {
            LocalizationManager.LanguageChanged -= OnLanguageChanged;
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        void OnLanguageChanged()
        {
            if (_upgrade != null) Setup(_upgrade, _economy);
        }

        /// <summary>
        /// Tum kartlarin paylastigi satin alma modu: 1, 10 veya -1 (MAKS).
        ///
        /// Sinirsiz yukseltmeleri elli kez tiklamak eziyetti. Mod tek bir yerde
        /// tutuluyor cunku alt paneldeki tek dugme tum kartlari birden etkiliyor.
        /// </summary>
        public static int BulkMode = 1;

        void OnClick()
        {
            if (_economy == null || _upgrade == null) return;

            // Satin alma sesini AudioManager caliyor — o zaten Purchased olayina
            // abone. Burada ayrica calmak her alimda cift ses uretiyordu; ustelik
            // bu cagri TryBuy'dan ONCE oldugu icin basarisiz alimlarda bile oterdi.
            if (_upgrade.type == UpgradeType.Connection || BulkMode == 1)
            {
                _economy.TryBuy(_upgrade);
                return;
            }

            int want = BulkMode < 0 ? _economy.GetMaxAffordable(_upgrade) : BulkMode;
            if (want <= 0) return;

            _economy.TryBuyBulk(_upgrade, want);
        }

        public void Setup(UpgradeSO upgrade, EconomyManager economy)
        {
            _upgrade = upgrade;
            _economy = economy;

            if (_upgrade == null) return;

            if (icon != null)
            {
                icon.sprite = _upgrade.icon;
                icon.enabled = _upgrade.icon != null;
            }

            if (nameText != null)
            {
                string n = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetUpgradeName(_upgrade) : _upgrade.displayName;
                string tag = GetGainTag(_upgrade);
                if (!string.IsNullOrEmpty(tag)) n += " <color=#40F273>" + tag + "</color>";
                nameText.SetText(n);
            }

            // Aciklama artik Refresh'te kuruluyor: yanina eklenen "su anki toplam"
            // seviye degistikce guncellenmeli.
            Refresh();
        }

        /// <summary>
        /// Kart basligindaki "seviye basina kazanc" etiketi.
        ///
        /// Bu metinler once dogrudan koda TR/EN olarak gomuluydu; Almanca,
        /// Ispanyolca veya Rusca oynayan biri kartlarda Ingilizce goruyordu.
        /// Artik hepsi sozlukten geliyor.
        /// </summary>
        string GetGainTag(UpgradeSO u)
        {
            if (u == null || u.effectPerLevel <= 0f) return "";

            int percent = Mathf.RoundToInt(u.effectPerLevel * 100f);

            switch (u.type)
            {
                case UpgradeType.PassiveSpeed:
                    return LocalizationManager.TF("gain_speed", "(+{0}% Speed)", percent);
                case UpgradeType.ClickPower:
                    return LocalizationManager.TF("gain_click", "(+{0}% Click)", percent);
                case UpgradeType.OverheatTolerance:
                    return LocalizationManager.TF("gain_heat", "(+{0}% Heat)", percent);
                case UpgradeType.CritChance:
                    return LocalizationManager.TF("gain_crit", "(+{0}% Crit)", percent);
                case UpgradeType.RewardMultiplier:
                    return LocalizationManager.TF("gain_reward", "(+{0}% Reward)", percent);
                default:
                    return "";
            }
        }

        /// <summary>
        /// Kilometre tasi rozeti ve carpan yazisi. Rozet yalnizca en az bir esik
        /// asildiginda gorunur — henuz kazanilmamis bir odulu gostermek kartlari
        /// gereksiz kalabalik yapardi.
        /// </summary>
        void RefreshMilestone(int level, bool unlocked)
        {
            int reached = _upgrade != null ? _upgrade.MilestonesReached(level) : 0;
            bool show = unlocked && reached > 0;

            if (milestoneBadge != null) milestoneBadge.enabled = show;

            if (milestoneText != null)
            {
                milestoneText.enabled = show;
                if (show)
                {
                    double mult = _upgrade.MilestoneMultiplierAt(level);
                    milestoneText.SetText("x" + mult.ToString("0.#"));
                }
            }
        }

        /// <summary>
        /// Aciklama + o kartin SU ANA KADARKI toplam katkisi.
        ///
        /// Kart basligindaki etiket seviye BASINA kazanci gosteriyor; oyuncunun
        /// goremedigi sey birikmis toplamdi. Ozellikle kilometre tasi asildiginda
        /// (etki ikiye katlanir) bunun gorunur olmasi gerekiyor.
        /// </summary>
        void RefreshDescription(int level)
        {
            if (descText == null || _upgrade == null) return;

            string d = LocalizationManager.Instance != null
                ? LocalizationManager.Instance.GetUpgradeDesc(_upgrade)
                : _upgrade.description;

            string total = FormatTotalEffect(level);
            if (!string.IsNullOrEmpty(total))
            {
                d += "\n<size=85%><color=#9FD8A0>"
                   + LocalizationManager.T("upgrade_current", "Now") + ": " + total
                   + "</color></size>";
            }

            descText.SetText(d);
        }

        /// <summary>Kartin birikmis etkisi, tipine uygun birimle.</summary>
        string FormatTotalEffect(int level)
        {
            if (_upgrade == null || level <= 0 || _upgrade.effectPerLevel <= 0f) return "";

            double total = level * (double)_upgrade.effectPerLevel
                         * _upgrade.MilestoneMultiplierAt(level);

            switch (_upgrade.type)
            {
                case UpgradeType.OverheatTolerance:
                    return "+" + total.ToString("0.#") + " " +
                           LocalizationManager.T("unit_second", "s");

                case UpgradeType.AutoClick:
                    return "+" + total.ToString("0.#") + " " +
                           LocalizationManager.T("unit_clicks_per_sec", "clicks/s");

                case UpgradeType.Connection:
                case UpgradeType.OfflineCapacity:
                case UpgradeType.QueueAutomation:
                    // Bunlar oran degil, seviye kademesi — sayi gostermek yaniltir.
                    return "";

                default:
                    // Kalan tiplerin hepsi oransal (tiklama gucu, taban hiz,
                    // kritik sans, odul, sikistirma, olay direnci...).
                    //
                    // FormatPercent DEGIL: o 100'e kirpiyor ve birikmis bonuslar
                    // 100'u cok asiyor. 25 seviye Modeme Vurmak %1500 ederken
                    // kart "+%100" yaziyordu.
                    return "+" + NumberFormatter.FormatBonusPercent(total);
            }
        }

        public void Refresh()
        {
            if (_upgrade == null || _economy == null) return;

            int level = _economy.GetLevel(_upgrade);
            bool unlocked = _economy.IsUnlocked(_upgrade);
            bool maxed = _economy.IsMaxed(_upgrade);
            bool affordable = _economy.CanAfford(_upgrade);

            if (lockIcon != null) lockIcon.enabled = !unlocked;

            // Kilitli kartta ikon ve metinler soluklasin ki goz once acik olanlara gitsin.
            float dim = unlocked ? 1f : 0.35f;
            if (icon != null) icon.color = new Color(1f, 1f, 1f, dim);

            if (levelText != null)
            {
                // Bu dort satir daha once dogrudan Turkce yaziyordu ("KADEME",
                // "MAKS", "Sv") — sozlukte karsiliklari olmasina ragmen. Ingilizce
                // oynayan biri kartlarda Turkce goruyordu.
                string lv = LocalizationManager.T("upgrade_level", "Lv");

                if (!unlocked)
                    levelText.SetText(LocalizationManager.TF("tier_req", "Req. Tier: {0}",
                                                             _upgrade.requiredTierIndex));
                else if (maxed)
                    levelText.SetText(LocalizationManager.T("upgrade_max", "MAX"));
                else if (_upgrade.maxLevel > 0)
                    levelText.SetText(lv + " " + level + " / " + _upgrade.maxLevel);
                else
                {
                    // Sinirsiz yukseltmede seviye sayisi tek basina hedef vermiyor;
                    // bir sonraki kilometre tasi oyuncuya nisan alacagi nokta veriyor.
                    //
                    // OK ISARETI '›' (U+203A), '▸' (U+25B8) DEGIL.
                    // Fontta (LiberationSans SDF) U+25B8 yok; TMP onu bulamayinca
                    // sessizce bos kare (U+25A1) ile degistiriyordu. Yani oyuncu
                    // "Sv 12 › 25" yerine "Sv 12 [] 25" goruyordu. Konsolda uyari
                    // olarak gecmesine ragmen gozden kacmisti cunku bu satir
                    // yalnizca SINIRSIZ bir yukseltme 1+ seviyeye ciktiginda
                    // olusuyor — taze kayitta hic gorunmuyor.
                    int next = _upgrade.NextMilestone(level);
                    if (next > 0)
                        levelText.SetText(lv + " " + level + "  <color=#7FA8D8>› " + next + "</color>");
                    else
                        levelText.SetText(lv + " " + level);
                }
            }

            RefreshMilestone(level, unlocked);
            RefreshDescription(level);

            if (costText != null)
            {
                if (maxed)
                {
                    costText.SetText("—");
                    costText.color = costMaxed;
                }
                else if (EconomyManager.UsesFiberCredits(_upgrade))
                {
                    // Prestij yetenekleri para degil Fiber Kredisi ile aliniyor;
                    // para bicimleyicisi burada yaniltici olurdu.
                    costText.SetText((int)_economy.GetCost(_upgrade) + " " +
                                     LocalizationManager.T("prestige_unit", "FP"));
                    costText.color = (unlocked && affordable) ? costAffordable : costTooExpensive;
                }
                else
                {
                    int bulk = ResolveBulkCount();
                    if (bulk > 1)
                    {
                        // Toplu modda tek birim fiyati degil, gercekte odenecek
                        // tutari gostermek gerekiyor — yoksa buton yaniltici olur.
                        costText.SetText(NumberFormatter.FormatMoney(
                                             _economy.GetBulkCost(_upgrade, bulk))
                                         + "  <size=75%>x" + bulk + "</size>");
                    }
                    else
                    {
                        costText.SetText(NumberFormatter.FormatMoney(_economy.GetCost(_upgrade)));
                    }

                    costText.color = (unlocked && affordable) ? costAffordable : costTooExpensive;
                }
            }

            if (button != null) button.interactable = _economy.CanBuy(_upgrade);
        }

        /// <summary>
        /// Bu kart icin gecerli toplu alim adedi. Baglantilar tek seferlik
        /// oldugu icin her zaman 1; MAKS modunda butcenin izin verdigi kadar.
        /// </summary>
        int ResolveBulkCount()
        {
            if (_upgrade == null || _economy == null) return 1;
            if (_upgrade.type == UpgradeType.Connection) return 1;
            if (BulkMode == 1) return 1;

            int max = _economy.GetMaxAffordable(_upgrade);
            if (max <= 0) return 1;

            return BulkMode < 0 ? max : Mathf.Min(BulkMode, max);
        }

        public void Bind(Button btn, Image bg, Image iconImage, Image lockImage,
                         TextMeshProUGUI title, TextMeshProUGUI desc,
                         TextMeshProUGUI level, TextMeshProUGUI cost,
                         Image badge = null, TextMeshProUGUI badgeText = null)
        {
            button = btn;
            background = bg;
            icon = iconImage;
            lockIcon = lockImage;
            nameText = title;
            descText = desc;
            levelText = level;
            costText = cost;
            milestoneBadge = badge;
            milestoneText = badgeText;
        }
    }
}
