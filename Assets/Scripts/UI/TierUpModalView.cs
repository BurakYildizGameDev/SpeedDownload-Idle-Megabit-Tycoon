using SpeedDownload.Core;
using SpeedDownload.Data;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Yeni baglanti kademesine (Telefon Hatti, 56K, Fiber...) yukseltildiginde
    /// ekranda beliren "YENİ ÇAĞA GEÇİLDİ!" kutlama penceresi.
    /// </summary>
    public class TierUpModalView : MonoBehaviour
    {
        [SerializeField] CanvasGroup group;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI tierNameText;
        [SerializeField] Image tierIconImage;
        [SerializeField] TextMeshProUGUI speedRangeText;
        [SerializeField] Button continueButton;

        bool _visible;

        void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(Close);
            SetVisible(false);
        }

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            gm.TierChanged += OnTierChanged;
        }

        void OnDestroy()
        {
            if (continueButton != null) continueButton.onClick.RemoveListener(Close);

            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            gm.TierChanged -= OnTierChanged;
        }

        void OnTierChanged(ConnectionTierSO tier)
        {
            if (tier == null || tier.tierIndex == 0) return; // Başlangıç kulübesinde tetiklenme

            // Kayittan yukleme ve prestij sifirlamasi da TierChanged yayinliyor;
            // ikisinde de pencere acilmamali (oyuncu yeni bir kademeye GECMEDI).
            //
            // Bu kontrol eskiden bu sinifa ozel bir bayrakla (SaveManager.Loaded
            // olayina abone olarak) yapiliyordu. Ayni korumanin FXManager ve
            // AudioManager'da OLMAMASI, kutlamanin yarisinin (konfeti, titresim,
            // fanfar) yine de tetiklenmesine yol aciyordu. Karar artik tek yerde:
            // GameManager.LastTierChangeReason.
            GameManager owner = GameManager.Instance;
            if (owner != null && !owner.IsCelebratedTierChange) return;

            // Kademe atlama sesini AudioManager caliyor (o da TierChanged'e abone).
            if (titleText != null)
                titleText.SetText(LocalizationManager.T("tierup_title", "NEW ERA UNLOCKED!"));

            if (tierNameText != null)
            {
                string tName = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetTierName(tier) : tier.tierName;
                tierNameText.SetText(tName);
            }

            if (speedRangeText != null)
                speedRangeText.SetText(NumberFormatter.FormatSpeed(tier.minSpeed) + " - " +
                                       NumberFormatter.FormatSpeed(tier.maxSpeed));

            if (tierIconImage != null)
            {
                // Kademe varliklarinda connectionIcon bos; kademeyi acan
                // baglanti yukseltmesinin simgesi ayni gorseli tasiyor.
                Sprite sprite = tier.connectionIcon;
                if (sprite == null)
                {
                    GameManager gm = GameManager.Instance;
                    UpgradeSO conn = gm != null && gm.Database != null
                        ? gm.Database.GetConnectionUpgrade(tier.tierIndex) : null;
                    if (conn != null) sprite = conn.icon;
                }

                tierIconImage.sprite = sprite;
                tierIconImage.enabled = sprite != null;
            }

            SetVisible(true);
        }

        public void Close() => SetVisible(false);

        void SetVisible(bool on)
        {
            _visible = on;
            if (group != null)
            {
                group.alpha = on ? 1f : 0f;
                group.blocksRaycasts = on;
                group.interactable = on;
            }
        }

        public void Bind(CanvasGroup cg, TextMeshProUGUI title, TextMeshProUGUI name, Image icon, TextMeshProUGUI range, Button btn)
        {
            group = cg;
            titleText = title;
            tierNameText = name;
            tierIconImage = icon;
            speedRangeText = range;
            continueButton = btn;
            if (continueButton != null) continueButton.onClick.AddListener(Close);
            SetVisible(false);
        }
    }
}
