using System.Text;
using SpeedDownload.Core;
using SpeedDownload.Data;
using SpeedDownload.Util;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Ust HUD: bakiye, anlik hiz ve aktif dosya (GDD Bolum 5).
    ///
    /// Bakiye olaya bagli guncellenir (her karede degismez). Hiz ve ilerleme
    /// yuzdesi surekli degistigi icin sabit araliklarla tazelenir — her karede
    /// string uretmek bos yere GC baskisi olurdu. Dolgu cubugu ise her karede
    /// guncellenir, cunku o string uretmiyor ve akiciligi dogrudan gorunur.
    /// </summary>
    public class TopHudView : MonoBehaviour
    {
        [Header("Bakiye ve hiz")]
        [SerializeField] TextMeshProUGUI balanceText;
        [SerializeField] TextMeshProUGUI speedText;

        [Header("Kademe rozeti")]
        [SerializeField] Image tierIcon;
        [SerializeField] TextMeshProUGUI tierNameText;
        [SerializeField] TextMeshProUGUI tierRangeText;

        [Header("Aktif dosya")]
        [SerializeField] Image fileIcon;
        [SerializeField] TextMeshProUGUI fileNameText;
        [SerializeField] TextMeshProUGUI fileSizeText;
        [SerializeField] ProgressBarView progressBar;
        [SerializeField] TextMeshProUGUI percentText;
        [SerializeField] TextMeshProUGUI etaText;

        [Header("Bagimliliklar")]
        [SerializeField] DownloadController download;
        [SerializeField] Wallet wallet;
        [SerializeField] SpeedController speedController;

        [Tooltip("Hiz/yuzde metinlerinin saniyedeki tazelenme sayisi.")]
        [SerializeField] float refreshRate = 12f;

        readonly StringBuilder _sb = new StringBuilder(64);
        float _timer;

        void Start()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                enabled = false;
                return;
            }

            if (download == null) download = gm.Download;
            if (wallet == null) wallet = gm.Money;
            if (speedController == null) speedController = gm.Speed;

            if (wallet != null)
            {
                wallet.BalanceChanged += OnBalanceChanged;
                OnBalanceChanged(wallet.Balance);
            }

            if (download != null)
            {
                download.FileStarted += OnFileStarted;
                OnFileStarted(download.CurrentFile);
            }

            // Sikistirma Algoritmasi dosya boyutunu kuculttugu icin boyut
            // yazisinin yukseltme alindigi ANDA tazelenmesi gerekiyor; yoksa
            // etiket bir sonraki dosyaya kadar eski degerde kalir.
            _economy = gm.Economy;
            if (_economy != null) _economy.StatsChanged += RefreshFileSize;

            gm.TierChanged += OnTierChanged;
            OnTierChanged(gm.CurrentTier);

            Refresh();
        }

        EconomyManager _economy;

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
            if (wallet != null) wallet.BalanceChanged -= OnBalanceChanged;
            if (download != null) download.FileStarted -= OnFileStarted;
            if (_economy != null) _economy.StatsChanged -= RefreshFileSize;
            if (GameManager.Instance != null) GameManager.Instance.TierChanged -= OnTierChanged;
        }

        void OnLanguageChanged()
        {
            GameManager gm = GameManager.Instance;
            if (gm != null) OnTierChanged(gm.CurrentTier);
            if (download != null) OnFileStarted(download.CurrentFile);
            Refresh();
        }

        void OnTierChanged(ConnectionTierSO tier)
        {
            if (tier == null) return;

            if (tierNameText != null)
            {
                // Rozet eskiden yalnizca kucuk bir simgeydi ve kademe 0'da
                // simgesi bile yoktu: sol ustte ne oldugu anlasilmayan bos bir
                // kutu kaliyordu. Artik "KADEME 3 · Tam Fiber" yaziyor —
                // simgenin neyi temsil ettigi yanindaki yazidan okunuyor.
                string tName = LocalizationManager.Instance != null
                    ? LocalizationManager.Instance.GetTierName(tier) : tier.tierName;

                _sb.Length = 0;
                _sb.Append(LocalizationManager.TF("tier_badge", "TIER {0}", tier.tierIndex))
                   .Append("  ·  ")
                   .Append(tName);

                tierNameText.SetText(_sb);
            }

            if (tierRangeText != null)
            {
                _sb.Length = 0;
                _sb.Append(NumberFormatter.FormatSpeed(tier.minSpeed))
                   .Append(" - ")
                   .Append(NumberFormatter.FormatSpeed(tier.maxSpeed));
                tierRangeText.SetText(_sb);
            }

            if (tierIcon != null)
            {
                // Kademe varliklarinda connectionIcon bos; o kademeyi acan baglanti
                // yukseltmesinin simgesi ayni gorseli zaten tasiyor.
                Sprite sprite = tier.connectionIcon;
                if (sprite == null)
                {
                    GameManager gm = GameManager.Instance;
                    UpgradeSO conn = gm != null && gm.Database != null
                        ? gm.Database.GetConnectionUpgrade(tier.tierIndex) : null;
                    if (conn != null) sprite = conn.icon;
                }

                // Kademe 0'in acan bir baglanti yukseltmesi yok, dolayisiyla
                // simgesi de yok. Sprite'siz bir Image beyaz bir kare cizer;
                // rozeti tamamen gizlemek yerine yedek simgeye dusuyoruz ki
                // rozetin yeri ve boyu kademeler arasinda oynamasin.
                if (sprite == null) sprite = fallbackTierIcon;

                tierIcon.sprite = sprite;
                tierIcon.enabled = sprite != null;
            }

            if (tierBadgeFrame != null) tierBadgeFrame.enabled = true;
        }

        [Tooltip("Kademe 0 gibi kendi simgesi olmayan kademeler icin yedek gorsel.")]
        [SerializeField] Sprite fallbackTierIcon;

        [Tooltip("Rozetin arkasindaki cerceve — simgenin bir 'rozet' oldugunu belli eder.")]
        [SerializeField] Image tierBadgeFrame;

        void OnBalanceChanged(double balance)
        {
            if (balanceText != null) balanceText.SetText(NumberFormatter.FormatMoney(balance));
        }

        void OnFileStarted(FileDataSO file)
        {
            if (file == null) return;

            if (fileIcon != null)
            {
                fileIcon.sprite = file.icon;
                fileIcon.enabled = file.icon != null;
            }

            if (fileNameText != null)
            {
                string fName = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetFileName(file) : file.displayName;
                fileNameText.SetText(fName);
            }
            RefreshFileSize();

            // Yeni dosya sifirdan baslar; yumusatma geri sarma gibi gorunmesin.
            if (progressBar != null) progressBar.ApplyImmediate(0f);
        }

        /// <summary>
        /// Dosya boyutu etiketi — INDIRILECEK fiili boyut.
        ///
        /// Eskiden ham <c>file.sizeBits</c> yaziliyordu. Sikistirma Algoritmasi
        /// boyutu %30'a kadar kucultuyor ve ilerleme cubugu o kucuk boyuta gore
        /// doluyordu: HUD "250 MB" derken dosya 175 MB'lik yolda bitiyordu.
        /// Ayni ekranda iki farkli boyut, oyuncuya yukseltmenin ise yaramadigini
        /// dusundurecek kadar tutarsizdi.
        /// </summary>
        void RefreshFileSize()
        {
            if (fileSizeText == null || download == null) return;
            if (download.CurrentFile == null) return;

            fileSizeText.SetText(NumberFormatter.FormatSize(download.CurrentSize));
        }

        void Update()
        {
            if (download != null && progressBar != null)
                progressBar.SetProgress(download.Progress01);

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1f / Mathf.Max(1f, refreshRate);

            Refresh();
        }

        void Refresh()
        {
            if (speedController != null && speedText != null)
                speedText.SetText(NumberFormatter.FormatSpeed(speedController.EffectiveSpeed));

            if (download == null) return;

            if (percentText != null)
                percentText.SetText(NumberFormatter.FormatPercent(download.Progress01));

            if (etaText != null)
            {
                double eta = download.EstimatedSeconds;

                _sb.Length = 0;
                string leftStr = LocalizationManager.Instance != null ? LocalizationManager.Instance.Get("eta_left", "left") : "left";
                if (double.IsInfinity(eta) || double.IsNaN(eta)) _sb.Append("-- ").Append(leftStr);
                else _sb.Append(NumberFormatter.FormatDuration(eta)).Append(" ").Append(leftStr);

                etaText.SetText(_sb);
            }
        }

        /// <summary>SceneBuilder'in referanslari baglamasi icin.</summary>
        public void Bind(TextMeshProUGUI balance, TextMeshProUGUI speed,
                         Image tierIconImage, TextMeshProUGUI tierName, TextMeshProUGUI tierRange,
                         Image icon, TextMeshProUGUI fileName, TextMeshProUGUI fileSize,
                         ProgressBarView bar, TextMeshProUGUI percent, TextMeshProUGUI eta)
        {
            balanceText = balance;
            speedText = speed;
            tierIcon = tierIconImage;
            tierNameText = tierName;
            tierRangeText = tierRange;
            fileIcon = icon;
            fileNameText = fileName;
            fileSizeText = fileSize;
            progressBar = bar;
            percentText = percent;
            etaText = eta;
        }

        /// <summary>Kademe rozetinin cercevesi ve yedek simgesi (SceneBuilder icin).</summary>
        public void BindTierBadge(Image frame, Sprite fallbackIcon)
        {
            tierBadgeFrame = frame;
            fallbackTierIcon = fallbackIcon;
        }

        /// <summary>Sahne referanslari (prefab disinda kaldigi icin ayri baglanir).</summary>
        public void BindSystems(DownloadController dl, Wallet w, SpeedController speed)
        {
            download = dl;
            wallet = w;
            speedController = speed;
        }
    }
}
