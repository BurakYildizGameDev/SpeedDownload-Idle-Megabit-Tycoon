using System.Collections.Generic;
using SpeedDownload.Core;
using SpeedDownload.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpeedDownload.UI
{
    /// <summary>
    /// Indirilenler Arsivi. Oyuncunun tamamladigi benzersiz dosyalari gosterir;
    /// dolu simgeler renkli, henuz inmemis olanlar silik.
    ///
    /// Onceki surumde bu panel bir KABUKTU: sayac hep 0, bonus hep "+%0" ve grid
    /// hic doldurulmuyordu. Gercek veri artik CollectionManager'dan geliyor.
    /// </summary>
    public class FileCollectionPanelView : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI bonusText;
        [SerializeField] Transform gridContainer;

        [Tooltip("Grid hucresi olarak uretilecek simge boyutu (px).")]
        [SerializeField] float cellSize = 96f;

        [SerializeField] Color ownedColor = Color.white;
        [SerializeField] Color missingColor = new Color(1f, 1f, 1f, 0.18f);

        GameManager _gm;
        CollectionManager _collection;

        readonly List<Image> _cells = new List<Image>();
        readonly List<FileDataSO> _files = new List<FileDataSO>();

        /// <summary>Her hucrenin Holo yildizi (dosya nadir dustuyse gorunur).</summary>
        readonly List<Image> _holoBadges = new List<Image>();

        Sprite _holoSprite;

        void OnEnable()
        {
            LocalizationManager.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        Image _masteryBadge;
        Sprite _crownSprite;

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) return;

            _collection = _gm.Collection;
            if (_collection != null) _collection.Changed += Refresh;

            _crownSprite = Resources.Load<Sprite>("ui_badge_gold_crown");
            _holoSprite = Resources.Load<Sprite>("ui_icon_star_holo");

            BuildGrid();
            Refresh();
        }

        /// <summary>
        /// Butun dosyalar toplandiginda basligin yanina basilan altin tac.
        /// Metindeki "100% MASTER" satirini gorsel olarak da tasiyor.
        /// </summary>
        void UpdateMasteryBadge(bool mastered)
        {
            if (_crownSprite == null) return;

            if (_masteryBadge == null)
            {
                if (!mastered) return;   // gerekmedikce nesne uretme

                Transform parent = titleText != null ? titleText.transform : transform;

                var go = new GameObject("MasteryBadge", typeof(RectTransform));
                go.transform.SetParent(parent, false);

                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(1f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.anchoredPosition = new Vector2(-8f, 0f);
                rt.sizeDelta = new Vector2(64f, 64f);

                _masteryBadge = go.AddComponent<Image>();
                _masteryBadge.sprite = _crownSprite;
                _masteryBadge.preserveAspect = true;
                _masteryBadge.raycastTarget = false;
            }

            if (_masteryBadge.enabled != mastered) _masteryBadge.enabled = mastered;
        }

        void OnDestroy()
        {
            if (_collection != null) _collection.Changed -= Refresh;
        }

        /// <summary>
        /// Hucreler bir kez uretilir; sonrasinda yalnizca renkleri degisir.
        /// Her tazelemede Instantiate etmek bos yere GC olurdu.
        /// </summary>
        void BuildGrid()
        {
            if (gridContainer == null || _gm == null || _gm.Database == null) return;

            _files.Clear();
            _cells.Clear();
            _holoBadges.Clear();

            List<FileDataSO> all = _gm.Database.GetAllFiles();

            for (int i = 0; i < all.Count; i++)
            {
                FileDataSO f = all[i];
                if (f == null || f.isProceduralTemplate) continue;

                var go = new GameObject("Cell_" + f.name, typeof(RectTransform));
                go.transform.SetParent(gridContainer, false);

                var rect = (RectTransform)go.transform;
                rect.sizeDelta = new Vector2(cellSize, cellSize);

                var img = go.AddComponent<Image>();
                img.sprite = f.icon;
                img.preserveAspect = true;
                img.raycastTarget = false;

                // Holo rozeti — hucrenin sag ust kosesi.
                //
                // Her hucrede uretiliyor ama varsayilan olarak KAPALI: dosya
                // Holo dustugunde acilacak. Sonradan uretmek, arsiv acikken
                // dusen bir Holo'da hiyerarsi degisikligi ve layout yeniden
                // hesabi demek olurdu.
                var badgeGo = new GameObject("Holo", typeof(RectTransform));
                badgeGo.transform.SetParent(go.transform, false);

                var badgeRt = (RectTransform)badgeGo.transform;
                badgeRt.anchorMin = new Vector2(1f, 1f);
                badgeRt.anchorMax = new Vector2(1f, 1f);
                badgeRt.pivot = new Vector2(1f, 1f);
                badgeRt.sizeDelta = new Vector2(40f, 40f);
                badgeRt.anchoredPosition = new Vector2(2f, 2f);

                var badge = badgeGo.AddComponent<Image>();
                badge.sprite = _holoSprite;
                badge.preserveAspect = true;
                badge.raycastTarget = false;
                badge.enabled = false;

                _files.Add(f);
                _cells.Add(img);
                _holoBadges.Add(badge);

                // Hucreye tiklama: dosya nostalji hikayesini/triviasini gosterir
                var btn = go.AddComponent<Button>();
                btn.targetGraphic = img;
                btn.transition = Selectable.Transition.None;

                FileDataSO clickedFile = f;
                btn.onClick.AddListener(delegate { OnCellClicked(clickedFile); });
            }
        }

        FileDataSO _selectedFile;

        void OnCellClicked(FileDataSO file)
        {
            if (file == null) return;

            HapticManager.LightTap();

            if (_selectedFile == file)
                _selectedFile = null; // ikinci tiklamada secimi kaldir
            else
                _selectedFile = file;

            Refresh();
        }

        static string GetFileTrivia(FileDataSO file)
        {
            if (file == null) return "";
            string key = "trivia_" + file.name;
            string fallback = file.displayName;

            switch (file.name)
            {
                case "File_PingTest":
                    fallback = "1983'te Mike Muuss tarafindan yazilan ilk ag yanki testi."; break;
                case "File_BosBelge":
                    fallback = "Masaustunde 'Yeni Metin Belgesi.txt' olarak unutulan dosya."; break;
                case "File_HelloWorld":
                    fallback = "1972'de Brian Kernighan tarafindan yazilan tarihin ilk C kodu."; break;
                case "File_OdevV2":
                    fallback = "odev_son_v3_bitti_gercek.docx olana kadar suren cile."; break;
                case "File_SiirKoleksiyon":
                    fallback = "Gece 02:00'de MSN kisisel iletisine yazilan duygusal satirlar."; break;
                case "File_Avatar":
                    fallback = "96x96 piksel; MSN Messenger'daki ilk havali profil fotomuz."; break;
                case "File_TatilFoto":
                    fallback = "1.3 megapiksel dijital makineyle cekilen piksel piksel anilar."; break;
                case "File_EkranGoruntusu":
                    fallback = "PrintScreen basip Paint'e yapistirilan paha bicilmez anlar."; break;
                case "File_BannerTasarim":
                    fallback = "Flash animasyonlu 'Tebrikler 1.000.000 Ziyaretcisiniz' banner'i."; break;
                case "File_WallpaperHD":
                    fallback = "Windows XP Bliss yesil tepeleri; 1024x768 cozunurluk devrimi."; break;
                case "File_FavoriteSong":
                    fallback = "Winamp 2.91'de 'It really whips the llama's ass' ile dinlenen efsane."; break;
                case "File_PodcastEp1":
                    fallback = "Sikistirilmamis ham WAV formati; 10 dakikasi 100 MB."; break;
                case "File_AlbumFull":
                    fallback = "LimeWire'dan indirirken virus cikmasin diye dua edilen diskografi."; break;
                case "File_FunnyCat":
                    fallback = "360p kalitesinde ilk viral kedi videosu nostaljisi."; break;
                case "File_GameSetup":
                    fallback = "3 CD'lik kurulumun 2. CD'yi istedigi o gerilimli dakikalar."; break;
                case "File_DriverPack":
                    fallback = "Format sonrasi ses kartini tanitmak icin girilen CD arama savasi."; break;
                case "File_Movie4K":
                    fallback = "Indirmesi 4 gun suren kristal netliginde sinema keyfi."; break;
                case "File_SeriesBoxset":
                    fallback = "Part1.rar ... Part18.rar bozuk cikinca yasanan o buyuk huzun."; break;
                case "File_OsIso":
                    fallback = "Nero Burning ROM ile 4x hizda CD'ye yazilan isletim sistemi."; break;
                case "File_DatacenterBak":
                    fallback = "Tum sunucularin tek bir yedek dosyasina emanet oldugu o tehlikeli gunler."; break;
                case "File_InternetArchive":
                    fallback = "Tum internetin dijital hafizasi tek bir mega arsivde."; break;
                case "File_GenomeDb":
                    fallback = "Milyarlarca baz ciftinin kuantum hatlarla cozuldugu cag."; break;
                case "File_GalacticCensus":
                    fallback = "Galaksideki tum gezegenlerin nufus ve baglanti kayitlari."; break;
            }

            return LocalizationManager.T(key, fallback);
        }

        public void Refresh()
        {
            _gm = GameManager.Instance;
            if (_gm == null || _gm.Database == null) return;

            if (_collection == null) _collection = _gm.Collection;

            int owned = _collection != null ? _collection.CollectedCount : 0;
            int total = _files.Count;

            if (titleText != null)
                titleText.SetText(LocalizationManager.T("collection_title", "FILE ARCHIVE"));

            if (bonusText != null)
            {
                if (_selectedFile != null)
                {
                    bool isOwned = _collection != null && _collection.Has(_selectedFile);
                    bool isHolo = isOwned && _collection != null && _collection.IsHolo(_selectedFile);
                    string fileName = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetFileName(_selectedFile) : _selectedFile.displayName;

                    string header = isOwned
                        ? (isHolo ? "★ <color=#FFD638>" + fileName + " (Holo)</color>" : "<color=#4CD3FF>" + fileName + "</color>")
                        : "<color=#888888>" + fileName + " (" + LocalizationManager.T("upgrade_locked", "LOCKED") + ")</color>";

                    string trivia = isOwned
                        ? GetFileTrivia(_selectedFile)
                        : LocalizationManager.TF("tier_req", "Req. Tier: {0}", _selectedFile.minConnectionTier);

                    bonusText.SetText(header + "  ·  " + trivia);
                }
                else
                {
                    double bonus = _collection != null ? _collection.SpeedBonus : 0.0;
                    string label = LocalizationManager.TF(
                        "collection_bonus_label", "Archive: {0}/{1}  ·  +{2}% speed",
                        owned, total, Mathf.RoundToInt((float)(bonus * 100.0)));

                    if (total > 0 && owned >= total)
                    {
                        label += "  <color=#FFD638>" +
                                 LocalizationManager.T("collection_master", "100% MASTER") +
                                 "</color>";
                    }

                    bonusText.SetText(label);
                }
            }

            // Tam koleksiyon mührü: altin tac rozeti.
            //
            // Sprite bastan beri yukleniyordu — ustelik HER tazelemede yeniden,
            // yani arsiv her degistiginde bir Resources.Load — ama sonucu
            // HICBIR YERDE kullanilmiyordu. Yukleme artik bir kez (Start) ve
            // rozet gercekten ekrana ciziliyor.
            UpdateMasteryBadge(total > 0 && owned >= total);

            for (int i = 0; i < _cells.Count; i++)
            {
                if (_cells[i] == null) continue;

                bool has = _collection != null && _collection.Has(_files[i]);
                _cells[i].color = has ? ownedColor : missingColor;

                // Holo yildizi: yalnizca dosya hem toplanmis hem de nadir
                // dustuyse. Toplanmamis bir dosyada yildiz gostermek, oyuncuya
                // sahip olmadigi bir seyi kazanmis gibi gosterirdi.
                if (i >= _holoBadges.Count || _holoBadges[i] == null) continue;

                bool holo = has && _holoSprite != null
                            && _collection != null && _collection.IsHolo(_files[i]);

                if (_holoBadges[i].enabled != holo) _holoBadges[i].enabled = holo;
            }
        }

        /// <summary>SceneBuilder icin.</summary>
        public void Bind(TextMeshProUGUI title, TextMeshProUGUI bonus, Transform grid)
        {
            titleText = title;
            bonusText = bonus;
            gridContainer = grid;
        }
    }
}
