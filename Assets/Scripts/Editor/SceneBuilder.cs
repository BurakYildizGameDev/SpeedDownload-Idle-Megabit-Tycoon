using System.Collections.Generic;
using System.IO;
using SpeedDownload.Core;
using SpeedDownload.Data;
using SpeedDownload.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Faz 2 sahne kurucusu. Game.unity'yi ve DialRoot.prefab'i sifirdan uretir;
    /// elle surukle-birak yok, boylece sahne her zaman plandaki yapiyla ayni olur.
    ///
    /// Katman sirasi (Canvas cocuk sirasi = cizim sirasi):
    ///     RoomBackground -> ClickCatcher -> DialArea/DialRoot -> DebugPanel
    ///
    /// Kadran katmanlarinin hicbirinde raycastTarget yok; tiklamalar altta duran
    /// tam ekran ClickCatcher'a duser (Asset_Integration_Guide Bolum 3).
    /// </summary>
    public static class SceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Game.unity";
        const string PrefabDir = "Assets/Prefabs/Dial";
        const string PrefabPath = PrefabDir + "/DialRoot.prefab";

        const string DatabasePath = "Assets/ScriptableObjects/GameDatabase.asset";
        const string NeedleMaterialPath = "Assets/Materials/NeedleSolidTint.mat";
        const string HueMaterialPath = "Assets/Materials/DialHueShift.mat";
        const string ActionsPath = "Assets/InputSystem_Actions.inputactions";

        const string DialBgPath = "Assets/Sprites/Dial/dial_tier0_signal_bg.png";
        const string RedlinePath = "Assets/Sprites/Dial/dial_redline_overlay.png";
        const string TickPath = "Assets/Sprites/Dial/dial_tick_marks.png";
        const string NeedlePath = "Assets/Sprites/Dial/dial_needle.png";
        const string HeatGaugePath = "Assets/Sprites/UI/ui_heat_gauge.png";
        const string HeatFillPath = "Assets/Sprites/UI/ui_heat_fill.png";
        const string RoomBgPath = "Assets/Sprites/Backgrounds/bg_tier0_signal_room.png";
        const string PanelPath = "Assets/Sprites/UI/ui_panel_bg.png";
        const string BarFramePath = "Assets/Sprites/UI/ui_progressbar_frame.png";
        const string BarFillPath = "Assets/Sprites/UI/ui_progressbar_fill.png";
        const string IconBalancePath = "Assets/Sprites/UI/ui_icon_balance.png";
        const string IconSpeedPath = "Assets/Sprites/UI/ui_icon_speed.png";
        const string IconLockPath = "Assets/Sprites/UI/ui_icon_lock.png";

        // Kademe rozetinin cercevesi. Bu sprite uretilmisti ama sahnede hicbir
        // yerde kullanilmiyordu; rozeti "rozet" gibi gostermek tam da isi.
        const string NodeFramePath = "Assets/Sprites/UI/ui_node_frame.png";
        const string BadgeMilestonePath = "Assets/Sprites/UI/ui_badge_milestone.png";
        const string ButtonNormalPath = "Assets/Sprites/UI/ui_button_normal.png";
        const string ButtonPressedPath = "Assets/Sprites/UI/ui_button_pressed.png";
        const string ButtonDisabledPath = "Assets/Sprites/UI/ui_button_disabled.png";
        const string TabInfraPath = "Assets/Sprites/UI/ui_tab_infrastructure.png";
        const string TabHardwarePath = "Assets/Sprites/UI/ui_tab_hardware.png";
        const string TabSoftwarePath = "Assets/Sprites/UI/ui_tab_software.png";
        const string TabPrestigePath = "Assets/Sprites/UI/ui_tab_prestige.png";
        const string IconFiberPath = "Assets/Sprites/UI/ui_icon_fiberkredisi.png";

        const string EventHappyBannerPath = "Assets/Sprites/Events/event_happyhour_banner.png";
        const string EventWifiIconPath = "Assets/Sprites/Events/event_neighbor_wifi_icon.png";
        const string EventPasswordIconPath = "Assets/Sprites/Events/event_password_lock_icon.png";
        const string EventQuotaIconPath = "Assets/Sprites/Events/event_quota_icon.png";

        const string IconSettingsPath = "Assets/Sprites/UI/ui_icon_settings.png";
        const string IconClosePath = "Assets/Sprites/UI/ui_icon_close.png";
        const string IconSoundOnPath = "Assets/Sprites/UI/ui_icon_sound_on.png";
        const string IconSoundOffPath = "Assets/Sprites/UI/ui_icon_sound_off.png";
        // Titresim simgeleri: projeye sonradan eklenen "Vibration On/Of" gorselleri
        // hicbir yerde kullanilmiyordu, eski ui_icon_haptic_* ikilisi baglanmisti.
        // Yenileri baglandi; eskiler yedek olarak duruyor (dosya bulunamazsa
        // LoadSprite null doner ve rapor listesine duser).
        const string IconHapticOnPath = "Assets/Sprites/Vibration On.png";
        const string IconHapticOffPath = "Assets/Sprites/Vibration Of.png";
        const string IconRewardAdPath = "Assets/Sprites/UI/ui_icon_reward_ad.png";

        // Arsiv dugmesi prestij yetenek ikonunu paylasiyor (ayni "arsiv kutusu"
        // temasi); UI klasorunde bu is icin ayri bir simge yok.
        const string IconArchivePath = "Assets/Sprites/UpgradeIcons/icon_prestige_archive.png";


        const string MascotIdlePath = "Assets/Sprites/Mascot/mascot_idle.png";
        const string MascotHappyPath = "Assets/Sprites/Mascot/mascot_happy.png";
        const string MascotOverheatedPath = "Assets/Sprites/Mascot/mascot_overheated.png";

        const string FxClickPath = "Assets/Sprites/FX/fx_click_ripple.png";
        const string FxCriticalPath = "Assets/Sprites/FX/fx_critical_burst.png";
        const string FxCoinPath = "Assets/Sprites/FX/fx_coin_burst.png";
        const string FxFlamePath = "Assets/Sprites/FX/fx_turbo_flame.png";
        const string FxFlashPath = "Assets/Sprites/FX/fx_overheat_flash.png";
        const string FxSmokePath = "Assets/Sprites/FX/fx_overheat_smoke.png";
        const string FxConfettiPath = "Assets/Sprites/FX/fx_confetti.png";

        const string UiPrefabDir = "Assets/Prefabs/UI";
        const string CardPrefabPath = UiPrefabDir + "/UpgradeCard.prefab";

        // Referans cozunurluk: 1080x1920 portre (GDD Bolum 12).
        const float RefWidth = 1080f;
        const float RefHeight = 1920f;

        // Kadran gorselleri 512x512 uretildi; olcek 1:1 kalsin ki 9-slice/kenar
        // netligi bozulmasin. Ekranin ustune dogru kaydirilir, alt yariyi
        // Faz 3'teki dosya paneli kullanacak.
        const float DialSize = 512f;

        // Isi gostergesi kadranin biraz disinda dursun ki kadran yuzunu ortmesin.
        // Tik halkasiyla ayni mantik (o da 1.08 kullaniyor).
        const float HeatGaugeScale = 1.10f;

        // Hedef bolge isareti: kadran yarıcapinin %78'inden baslayan kisa cizgi.
        const float SweetSpotLength = 46f;
        const float SweetSpotWidth = 7f;

        // Sekme cubugu ile kart listesi arasindaki toplu alim seridi.
        const float BulkRowHeight = 56f;

        // Ipucu, ust panelin alt kenari (+440) ile kadranin ust kenari (+336)
        // arasindaki bosluga oturuyor — ikisini de ortmuyor.
        const float OnboardingHintY = 390f;

        // Prestij sekmesinde Hat Degisimi paneli ustte bu kadar yer kaplar;
        // kalani Fiber Kredisi yetenek kartlarina birakilir.
        /// <summary>
        /// Prestij panelinin yuksekligi; kalani Fiber Kredisi yetenek
        /// kartlarina birakilir.
        ///
        /// 340'TA KALIYOR — VE BUNUN BIR SEBEBI VAR
        /// ----------------------------------------
        /// Aciklama metni butonun uzerine biniyordu:
        ///     ExplainText     ust -172, yukseklik 96   -> -172 .. -268
        ///     PrestigeButton  alttan 28, yukseklik 120 -> -192 .. -312
        ///     CAKISMA                                     -192 .. -268  (76 birim)
        ///
        /// Ilk cozum denemesi bu sabiti 420'ye cikarmakti. OLCULDU VE GERI
        /// ALINDI: alt panelde sekme cubugundan sonra toplam 583 birim var;
        /// panel 420 olunca yetenek karti listesine 144 birimden 64 birime
        /// dusuyordu — yani cakisma giderilirken kartlar gorunmez oluyordu.
        /// Bir hatayi baska bir hatayla degistirmek olurdu.
        ///
        /// Dogru cozum panelin ICINI sikistirmak: aciklama metni en fazla IKI
        /// satir ("Format unlocks at Tier {0}.\nCurrent Tier: {1}."), yani 96
        /// birim zaten gereginden fazlaydi. Yerlesim su sekilde yeniden
        /// dagitildi (hepsi ustten, negatif):
        ///
        ///     FiberIcon    -12  boyut 64
        ///     Credits      -14  h 52
        ///     Multiplier   -66  h 34
        ///     Pending     -104  h 44
        ///     Explain     -150  h 48     -> -198'de bitiyor
        ///     Button      alttan 22, h 112 -> ust kenari -(340-134) = -206
        ///     NEFES PAYI                     8 birim
        ///
        /// Liste de 144 birimlik eski alanini geri aliyor.
        /// </summary>
        const float PrestigePanelHeight = 340f;

        // Dikey butce (1080x1920, merkez 0):
        //   ust panel   +940 .. +440   (500 yuksek)
        //   olay banner +430 .. +320   (110, ustten kayarak girer)
        //   kadran      +336 .. -176   (512, merkez +80)
        //   alt panel   -220 .. -940   (720 yuksek)
        // Banner kadranin USTUNDE duruyor; redline yayini (saat 12 -> 3)
        // kapatmamasi icin kadran +120'den +80'e indirildi.
        const float DialAnchorY = 80f;

        // --- ust HUD olculeri ---
        const float TopPanelHeight = 500f;
        const float TopPanelMargin = 20f;
        const float Pad = 40f;          // panel ic kenar boslugu
        const float RowTop = 28f;       // 1. satirin ust boslugu
        const float RowHeight = 72f;
        const float IconSize = 60f;
        const float TierRowTop = 112f;  // kademe rozeti satiri
        // Rozet buyutuldu (58/50 -> 74/68). Eski olcude sol ustte ne oldugu
        // secilemeyen kucuk bir kare duruyordu; satir 1 (28..100) ile dosya
        // blogu (188) arasindaki bosluk bu boya rahatca yetiyor.
        const float TierRowHeight = 74f;
        const float TierIconSize = 68f;
        const float TierFrameSize = 74f;

        /// <summary>
        /// Ust HUD'un SAG UST kosesinde yuzen dort dugme (kupa, reklam, arsiv,
        /// ayarlar) icin TopPanel'in 1. satirinda ayrilan genislik.
        ///
        /// BU SABIT OLMADAN NE OLUYORDU
        /// ----------------------------
        /// Dugmeler safeArea'ya, panel de safeArea'ya ekleniyor — yani dugmeler
        /// panelin USTUNE ciziliyor ama panel onlara yer AYIRMIYORDU. Sonuc:
        /// hiz gostergesi (SpeedText) x[633..1014] ile dugme seridi x[665..1042]
        /// 349 px boyunca ust uste biniyordu. Ekranda "1 bps" yazisinin uzerinde
        /// kupa simgesi duruyordu.
        ///
        /// Dugmeler kucultulerek cozulmedi: 72 canvas birimi bu referans
        /// cozunurlukte ~29 dp'ye denk geliyor ve Android'in 48 dp dokunma
        /// hedefi esiginin ZATEN altinda. Daha da kucultmek eristirilebilirligi
        /// bozardi. Dogru taraf panel icerigi.
        ///
        /// Deger nasil turetildi (canvas birimi):
        ///   en soldaki dugmenin sol kenari = safeAreaSag - 376
        ///   panelin sag kenari              = safeAreaSag - TopPanelMargin(20)
        ///   panel koordinatinda dugme kenari = -356
        ///   16 px nefes payi                 = -372
        ///
        /// ORANLA DEGIL, MUTLAK DEGERLE: dugmeler saga mutlak konumlaniyor.
        /// Oran kullanilsaydi (or. anchorRight 0.60) dar canvas'larda cakisma
        /// geri gelirdi — 1080x2400 gibi uzun ekranlarda canvas genisligi 966
        /// birime dusuyor ve oran hesabi tam orada tutmuyor.
        /// </summary>
        const float HudButtonStripReserve = 372f;
        const float FileRowTop = 188f;  // dosya blogunun ust boslugu
        const float FileIconSize = 112f;
        const float BarTop = 318f;
        const float BarHeight = 96f;

        // --- alt panel olculeri ---
        const float BottomPanelHeight = 720f;

        // --- olay banneri ---
        const float BannerHeight = 110f;
        const float BannerTop = 530f;   // Canvas tepesinden asagi
        const float BannerIconSize = 72f;

        // --- maskot ---
        const float MascotSize = 260f;
        const float MascotY = -300f;   // kadranin sag alti
        const float TabBarHeight = 120f;
        const float TabIconSize = 88f;
        const float CardHeight = 200f;
        const float CardSpacing = 14f;
        const float CardIconSize = 110f;
        const float CardTextLeft = 172f;   // simgeden sonra metnin basladigi yer
        const float CardRightBlock = 420f; // sagda seviye/maliyet icin ayrilan genislik
        const float CardTopPad = 26f;

        static readonly Color TextBright = Color.white;
        static readonly Color TextDim = Color.white;
        static readonly Color TextAccent = new Color(0.35f, 0.95f, 0.55f, 1f);

        // Olculen degerler (Asset_Integration_Guide Bolum 8):
        // ibre pivot->uc mesafesi 444.8 px, kadran yaricapi 256 px.
        // (0.80 * 256) / 444.8 = 0.461 -> 512x86 * 0.461 = 236x40
        static readonly Vector2 NeedleSize = new Vector2(236f, 56f);
        static readonly Vector2 NeedlePivot = new Vector2(0.1f, 0.5f);

        // dial_tier1_phoneline_bg'nin merkezi ve boncuk delikleri saydam.
        // Arkaya koyu bir disk konmazsa oda arka plani kadranin icinden gorunur.
        static readonly Color BackplateColor = new Color(0.09f, 0.10f, 0.13f, 1f);
        const float BackplateScale = 0.90f;

        static readonly List<string> _missing = new List<string>();

        [MenuItem("Tools/SpeedDownload/3. Build Game Scene", false, 30)]
        public static void BuildGameScene()
        {
            _missing.Clear();

            if (AssetDatabase.LoadAssetAtPath<GameDatabaseSO>(DatabasePath) == null)
            {
                EditorUtility.DisplayDialog(
                    "GameDatabase yok",
                    "Once Tools/SpeedDownload/2. Generate Data Assets calistirilmali.\n\nAranan: " + DatabasePath,
                    "Tamam");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // NewScene(Single) kullanilmayan asset'leri bosaltabilir. Veri tabani
            // referansi sahne acildiktan SONRA alinmazsa yok edilmis bir sarmalayici
            // elde ederiz: alan dogru guid ile serilesir ama == null true doner.
            GameDatabaseSO database = AssetDatabase.LoadAssetAtPath<GameDatabaseSO>(DatabasePath);

            CreateCamera();
            SpeedController speed;
            Wallet wallet;
            DownloadController download;
            CreateSystems(database, out speed, out wallet, out download);

            Canvas canvas = CreateCanvas();

            // Oda ve tiklama alani centigin ALTINDA kalmali — tum ekrani kaplarlar,
            // bu yuzden SafeArea'nin disinda dururlar.
            RoomBackgroundView room = CreateRoomBackground(canvas.transform);
            ClickCatcher catcher = CreateClickCatcher(canvas.transform);

            GameObject safeArea = NewUI("SafeArea", canvas.transform);
            Stretch((RectTransform)safeArea.transform);
            safeArea.AddComponent<SafeAreaFitter>();

            TopHudView topHud = CreateTopPanel(safeArea.transform);

            GameObject dialArea = NewUI("DialArea", safeArea.transform);
            RectTransform dialAreaRect = (RectTransform)dialArea.transform;
            SetAnchor(dialAreaRect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, DialAnchorY));

            // Ibre ve isi gostergesi surekli hareket ediyor; dokunuslari
            // arkadaki ClickCatcher aliyor, bu yuzden raycaster gerekmiyor.
            MakeSubCanvas(dialArea, false);

            // dialRoot sahnedeki nesnedir ve SavePrefab'dan sonra da oyle kalir
            // (prefab'a baglanir). SaveAsPrefabAssetAndConnect'in DONUS DEGERI
            // proje icindeki prefab asset'idir — sahne referansi kabul etmez.
            GameObject dialRoot = CreateDial(dialAreaRect);
            SavePrefab(dialRoot);

            CreateBottomPanel(safeArea.transform);

            // Kadranin hemen altinda, alt panelin ustunde: ilk dokunus ipucu.
            CreateOnboardingHint(safeArea.transform);

            CreateMascot(safeArea.transform);

            // Banner kadranin ustunde cizilsin diye DialArea'dan SONRA eklenir.
            CreateEventBanner(safeArea.transform);

            // FX katmani arayuzun ustunde ama menu/overlay'lerin altinda.
            CreateFXLayer(safeArea.transform);

            // Yuzen odul balonunun katmani. FX katmanina konamaz: orada
            // blocksRaycasts kapali, yani balona dokunulamazdi.
            CreateFloatingEventLayer(safeArea.transform);

            // Hata ayiklama paneli en son eklenir ki alt panelin USTUNDE cizilsin.
            DebugHud hud = CreateDebugPanel(safeArea.transform);

            // Offline raporu SafeArea'nin DISINDA ve en sonda: tum ekrani
            // karartip her seyin ustune biner.
            CreateOfflineReport(canvas.transform);

            // Kademe atlama kutlamasi — offline raporunun uzerinde ama
            // reklam panelinin altinda.
            CreateTierUpModal(canvas.transform);

            // Ana menu yok: oyun dogrudan basliyor.
            CreateSettings(canvas.transform, safeArea.transform);
            CreateCollection(canvas.transform, safeArea.transform);
            CreateTrophyPanel(canvas.transform, safeArea.transform);

            // Reklam paneli GERCEKTEN her seyin ustunde olmali: acikken oyuna
            // dokunulamamali. Eskiden Ayarlar ve Arsiv'den ONCE olusturuluyordu,
            // yani UGUL siralamasinda onlarin ALTINDA kaliyordu — yorum "her
            // seyin ustunde" dedigi halde tam tersi. Overlay'lerden biri acikken
            // reklam gosterilirse panel onlarin arkasinda kalir ve oyuncu
            // ekraninda hicbir sey olmamis gibi gorunurdu.
            CreateAdPanel(canvas.transform);

            // Odullu reklam boost dugmesi — arsivin solunda, ayni izgarada.
            CreateBoostAdButton(safeArea.transform);

            CreateEventSystem();

            // Sahne referanslari prefab kaydedildikten SONRA baglanir; boylece
            // prefab asset'i temiz kalir, baglanti instance override'i olur.
            BindSceneReferences(dialRoot, speed, catcher, hud);
            if (topHud != null) topHud.BindSystems(download, wallet, speed);

            VerifyBindings();

            EditorSceneManager.MarkSceneDirty(scene);
            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            RegisterInBuildSettings();

            AssetDatabase.SaveAssets();
            Report();
        }

        // ------------------------------------------------------------------
        // Sahne parcalari
        // ------------------------------------------------------------------

        static void CreateCamera()
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);

            Camera cam = go.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            // URP'nin UniversalAdditionalCameraData'yi kendisi eklemesine izin ver.
        }

        static void CreateSystems(GameDatabaseSO database, out SpeedController speed,
                                  out Wallet wallet, out DownloadController download)
        {
            var go = new GameObject("[Game]");
            GameManager manager = go.AddComponent<GameManager>();
            speed = go.AddComponent<SpeedController>();
            wallet = go.AddComponent<Wallet>();
            download = go.AddComponent<DownloadController>();
            go.AddComponent<EconomyManager>();
            go.AddComponent<TierManager>();
            go.AddComponent<PrestigeManager>();
            go.AddComponent<GameEventManager>();
            go.AddComponent<AudioManager>();
            go.AddComponent<SaveManager>();
            go.AddComponent<LocalizationManager>();
            go.AddComponent<CollectionManager>();

            // Sayaclar basarim ve gorevlerden ONCE eklenmeli degil — sira
            // onemsiz, cozumleme GameManager.Bootstrap'ta FindAnyObjectByType
            // ile yapiliyor. Yine de okunurluk icin mantiksal sirada duruyorlar.
            go.AddComponent<PlayerStats>();
            go.AddComponent<AchievementManager>();
            go.AddComponent<DailyQuestManager>();
            // ProceduralAudioManager kaldirildi: AudioManager ile ayni olaylara ses
            // caliyordu, ikisi sahnede birlikte durdugu icin her tiklamada iki klip
            // ust uste biniyordu. Ses artik tek yerden yonetiliyor.
            go.AddComponent<FloatingEventController>();
            go.AddComponent<GameBootstrapper>();

            manager.EditorBind(database, 0);
            EditorUtility.SetDirty(manager);
        }

        static Canvas CreateCanvas()
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            // 0.5 = hem cok dar hem cok genis telefonlarda kadran ekrandan tasmaz.
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            return canvas;
        }

        static RoomBackgroundView CreateRoomBackground(Transform parent)
        {
            GameObject go = NewUI("RoomBackground", parent);
            Image img = go.AddComponent<Image>();
            img.sprite = LoadSprite(RoomBgPath);
            img.type = Image.Type.Simple;
            img.raycastTarget = false;

            // Gercek boyut RoomBackgroundView.ResizeToCover() tarafindan verilir.
            RectTransform rect = (RectTransform)go.transform;
            SetAnchor(rect, new Vector2(0.5f, 0.5f), new Vector2(RefHeight * (1024f / 576f), RefHeight), Vector2.zero);

            RoomBackgroundView view = go.AddComponent<RoomBackgroundView>();
            view.Bind(img);
            return view;
        }

        static ClickCatcher CreateClickCatcher(Transform parent)
        {
            GameObject go = NewUI("ClickCatcher", parent);
            Image img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f); // gorunmez ama raycast alir
            img.raycastTarget = true;

            Stretch((RectTransform)go.transform);

            return go.AddComponent<ClickCatcher>();
        }

        static GameObject CreateDial(RectTransform parent)
        {
            GameObject root = NewUI("DialRoot", parent);
            RectTransform rootRect = (RectTransform)root.transform;
            SetAnchor(rootRect, new Vector2(0.5f, 0.5f), new Vector2(DialSize, DialSize), Vector2.zero);

            Image backplate = AddLayer(rootRect, "DialBackplate", BuiltinKnob(), DialSize * BackplateScale);
            backplate.color = BackplateColor;

            Image background = AddLayer(rootRect, "Background", LoadSprite(DialBgPath), DialSize);

            // Kadran arka planlarinda cizgiler zaten cizili; bu katman kademe
            // sonrasi ince ayar gerekirse diye duruyor, varsayilan kapali.
            Image ticks = AddLayer(rootRect, "TickMarksOverlay", LoadSprite(TickPath), DialSize * 1.08f);
            ticks.gameObject.SetActive(false);

            Image redline = AddLayer(rootRect, "RedlineOverlay", LoadSprite(RedlinePath), DialSize);
            redline.color = new Color(1f, 1f, 1f, 0.85f);

            GameObject pivotGo = NewUI("NeedlePivot", rootRect);
            RectTransform pivot = (RectTransform)pivotGo.transform;
            SetAnchor(pivot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject needleGo = NewUI("NeedleSprite", pivot);
            Image needle = needleGo.AddComponent<Image>();
            needle.sprite = LoadSprite(NeedlePath);
            needle.type = Image.Type.Simple;
            needle.raycastTarget = false;

            RectTransform needleRect = (RectTransform)needleGo.transform;
            needleRect.anchorMin = new Vector2(0.5f, 0.5f);
            needleRect.anchorMax = new Vector2(0.5f, 0.5f);
            // UI Image sprite pivot'unu yok sayar; donme merkezi RectTransform.pivot.
            needleRect.pivot = NeedlePivot;
            needleRect.sizeDelta = NeedleSize;
            needleRect.anchoredPosition = Vector2.zero;

            Material needleMat = AssetDatabase.LoadAssetAtPath<Material>(NeedleMaterialPath);
            if (needleMat == null) _missing.Add(NeedleMaterialPath);
            else needle.material = needleMat;

            Material hueMat = AssetDatabase.LoadAssetAtPath<Material>(HueMaterialPath);
            if (hueMat == null) _missing.Add(HueMaterialPath);

            DialView view = root.AddComponent<DialView>();
            view.Bind(backplate, background, ticks, redline, pivot, needle, null, needleMat, hueMat);

            CreateSweetSpot(view, rootRect);
            CreateHeatGauge(root, rootRect);

            return root;
        }

        /// <summary>
        /// Hedef bolge isareti — kadranin dis kenarinda duran ince bir cizgi.
        ///
        /// Ibrenin pivot mantiginin aynisi: merkezde bos bir donme noktasi,
        /// altinda disa dogru uzanan bir cocuk. Sprite gerekmiyor; sprite'siz
        /// bir Image duz beyaz dortgen cizer, renk kodla veriliyor.
        /// </summary>
        static void CreateSweetSpot(DialView view, RectTransform parent)
        {
            GameObject pivotGo = NewUI("SweetSpotPivot", parent);
            RectTransform pivot = (RectTransform)pivotGo.transform;
            SetAnchor(pivot, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            GameObject markGo = NewUI("SweetSpotMark", pivot);
            Image mark = markGo.AddComponent<Image>();
            mark.raycastTarget = false;

            RectTransform markRect = (RectTransform)markGo.transform;
            markRect.anchorMin = new Vector2(0.5f, 0.5f);
            markRect.anchorMax = new Vector2(0.5f, 0.5f);
            // Pivot solda: ibre gibi disa dogru uzasin.
            markRect.pivot = new Vector2(0f, 0.5f);
            markRect.sizeDelta = new Vector2(SweetSpotLength, SweetSpotWidth);
            // Kadranin dis kenarina, redline bandinin uzerine otursun.
            markRect.anchoredPosition = new Vector2(DialSize * 0.5f * 0.78f, 0f);

            view.BindSweetSpot(pivot, mark);
        }

        /// <summary>
        /// Isi gostergesi (PLAN Bolum 28.2). Kadranin ustunu saran yarim halka.
        ///
        /// En son eklenir ki ibrenin de ustunde cizilsin — gosterge kadranin dis
        /// kenarinda durdugu icin ibreyi kapatmaz, ama kademe gecisinde ibre
        /// savrulurken arkasinda kalmasi istenmez.
        /// </summary>
        static void CreateHeatGauge(GameObject dialRoot, RectTransform parent)
        {
            GameObject go = NewUI("HeatGauge", parent);
            RectTransform rect = (RectTransform)go.transform;
            SetAnchor(rect, new Vector2(0.5f, 0.5f),
                      new Vector2(DialSize * HeatGaugeScale, DialSize * HeatGaugeScale),
                      Vector2.zero);

            // Editorde gorunur birakiliyor ki konum/olcek sahne goruntusunden
            // dogrulanabilsin; HeatGaugeView.Start onu oyunda hemen saydamlastirir.
            Image track = AddLayer(rect, "Track", LoadSprite(HeatGaugePath),
                                   DialSize * HeatGaugeScale);

            Image fill = AddLayer(rect, "Fill", LoadSprite(HeatFillPath),
                                  DialSize * HeatGaugeScale);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            // Origin ve doluluk araligi sprite'in alfa kanalindan olculdu —
            // gerekce HeatGaugeView'daki yorumda. Yay 258 derece; ne tam daire
            // ne yarim halka oldugu icin dolgu 0..1 arasinda surulemiyor.
            fill.fillOrigin = (int)Image.Origin360.Bottom;
            fill.fillClockwise = true;

            // Editorde tam yay ciziliyor ki dolgunun kanala oturup oturmadigi
            // gorulebilsin. HeatGaugeView.Start bunu oyunda bos degere cekiyor,
            // yani oyuncu hicbir zaman dolu baslangic gormez.
            fill.fillAmount = 0.859f;
            fill.color = new Color(0.29f, 0.87f, 0.42f, 1f);

            HeatGaugeView gauge = dialRoot.AddComponent<HeatGaugeView>();
            gauge.Bind(track, fill, null);
        }

        /// <summary>
        /// Ust HUD (GDD Bolum 5). Panel yatayda esner, cocuklar anchor ile
        /// hizalanir — sabit piksel konumu yok, boylece SafeArea daralinca
        /// icerik de daralir.
        /// </summary>
        static TopHudView CreateTopPanel(Transform parent)
        {
            GameObject panel = NewUI("TopPanel", parent);
            Image bg = panel.AddComponent<Image>();
            bg.sprite = LoadSprite(PanelPath);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false; // tiklamalar ClickCatcher'a gecsin

            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(TopPanelMargin, 0f);
            rect.offsetMax = new Vector2(-TopPanelMargin, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, TopPanelHeight);
            rect.anchoredPosition = new Vector2(0f, -TopPanelMargin);

            // --- satir 1: bakiye (sol) | hiz (sag) ---

            RectTransform balanceGroup = AddGroup(rect, "BalanceGroup", 0f, 0.36f,
                                                  Pad, -10f, -RowTop, RowHeight);
            AddIcon(balanceGroup, "BalanceIcon", LoadSprite(IconBalancePath), IconSize);
            TextMeshProUGUI balanceText = AddText(balanceGroup, "BalanceText", 46f,
                                                  TextAlignmentOptions.MidlineLeft, TextBright);
            InsetLeft((RectTransform)balanceText.transform, IconSize + 14f);

            RectTransform speedGroup = AddGroup(rect, "SpeedGroup", 0.36f, 1f,
                                                10f, -HudButtonStripReserve, -RowTop, RowHeight);
            AddIcon(speedGroup, "SpeedIcon", LoadSprite(IconSpeedPath), IconSize);
            TextMeshProUGUI speedText = AddText(speedGroup, "SpeedText", 46f,
                                                TextAlignmentOptions.MidlineLeft, TextAccent);
            InsetLeft((RectTransform)speedText.transform, IconSize + 14f);

            AutoShrink(balanceText, 46f, 26f);
            AutoShrink(speedText, 46f, 26f);

            // --- satir 2: kademe rozeti ---

            RectTransform tierGroup = AddGroup(rect, "TierGroup", 0f, 1f,
                                               Pad, -Pad, -TierRowTop, TierRowHeight);

            // Cerceve simgeden ONCE ekleniyor ki arkada cizilsin.
            Image tierFrame = AddIcon(tierGroup, "TierBadgeFrame",
                                      LoadSprite(NodeFramePath), TierFrameSize);
            Image tierIcon = AddIcon(tierGroup, "TierIcon", null, TierIconSize);

            // Simge cercevenin icinde ortalansin.
            ((RectTransform)tierIcon.transform).anchoredPosition =
                new Vector2((TierFrameSize - TierIconSize) * 0.5f, 0f);

            TextMeshProUGUI tierName = AddText(tierGroup, "TierNameText", 34f,
                                               TextAlignmentOptions.MidlineLeft, new Color(0.20f, 0.25f, 0.35f, 1f));
            InsetLeft((RectTransform)tierName.transform, TierFrameSize + 18f);

            TextMeshProUGUI tierRange = AddText(tierGroup, "TierRangeText", 28f,
                                                TextAlignmentOptions.MidlineRight, new Color(0.35f, 0.40f, 0.50f, 1f));

            // --- satir 3: aktif dosya ---

            GameObject iconGo = NewUI("FileIcon", rect);
            Image fileIcon = iconGo.AddComponent<Image>();
            fileIcon.raycastTarget = false;
            fileIcon.preserveAspect = true;
            RectTransform iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(0f, 1f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 1f);
            iconRect.sizeDelta = new Vector2(FileIconSize, FileIconSize);
            iconRect.anchoredPosition = new Vector2(Pad, -FileRowTop);

            TextMeshProUGUI fileName = AddRow(rect, "FileNameText", -FileRowTop, 58f, 42f,
                                              TextAlignmentOptions.MidlineLeft, new Color(0.15f, 0.20f, 0.30f, 1f));
            InsetLeft((RectTransform)fileName.transform, Pad + FileIconSize + 20f);

            TextMeshProUGUI fileSize = AddRow(rect, "FileSizeText", -(FileRowTop + 60f), 46f, 30f,
                                              TextAlignmentOptions.MidlineLeft, new Color(0.40f, 0.45f, 0.55f, 1f));
            InsetLeft((RectTransform)fileSize.transform, Pad + FileIconSize + 20f);

            ProgressBarView bar = CreateProgressBar(rect, -BarTop);

            TextMeshProUGUI percent = AddRow(rect, "PercentText", -(BarTop + BarHeight + 8f), 44f, 32f,
                                             TextAlignmentOptions.MidlineLeft, new Color(0.05f, 0.65f, 0.40f, 1f));
            TextMeshProUGUI eta = AddRow(rect, "EtaText", -(BarTop + BarHeight + 8f), 44f, 32f,
                                         TextAlignmentOptions.MidlineRight, new Color(0.35f, 0.40f, 0.50f, 1f));

            TopHudView view = panel.AddComponent<TopHudView>();
            view.Bind(balanceText, speedText, tierIcon, tierName, tierRange,
                      fileIcon, fileName, fileSize, bar, percent, eta);

            // Kademe 0'in acan bir baglanti yukseltmesi ve dolayisiyla simgesi
            // yok; hiz simgesi yedek olarak kullaniliyor.
            view.BindTierBadge(tierFrame, LoadSprite(IconSpeedPath));

            // Ust HUD saniyede 12 kez metin, her karede ilerleme cubugu
            // guncelliyor — en cok kirlenen bolge. Dokunulabilir ogesi yok.
            MakeSubCanvas(panel, false);
            return view;
        }

        /// <summary>
        /// Cerceve + dolgu. Olculen oyuk: soldan 21, sagdan 22, ustten 11,
        /// alttan 16 px. Cerceve 9-slice (75,20,75,20) oldugu icin bu degerler
        /// her genislikte piksel olarak sabit kalir.
        /// </summary>
        static ProgressBarView CreateProgressBar(RectTransform parent, float y)
        {
            GameObject barGo = NewUI("ProgressBar", parent);
            RectTransform bar = (RectTransform)barGo.transform;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(Pad, 0f);
            bar.offsetMax = new Vector2(-Pad, 0f);
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, BarHeight);
            bar.anchoredPosition = new Vector2(0f, y);

            GameObject frameGo = NewUI("Frame", bar);
            Image frame = frameGo.AddComponent<Image>();
            frame.sprite = LoadSprite(BarFramePath);
            frame.type = Image.Type.Sliced;
            frame.raycastTarget = false;
            Stretch((RectTransform)frameGo.transform);

            GameObject fillGo = NewUI("Fill", bar);
            Image fill = fillGo.AddComponent<Image>();
            fill.sprite = LoadSprite(BarFillPath);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;

            RectTransform fillRect = (RectTransform)fillGo.transform;
            Stretch(fillRect);
            fillRect.offsetMin = new Vector2(21f, 16f);
            fillRect.offsetMax = new Vector2(-22f, -11f);

            // Dolgu cercevenin ustunde cizilsin.
            fillGo.transform.SetAsLastSibling();

            ProgressBarView view = barGo.AddComponent<ProgressBarView>();
            view.Bind(frame, fill);
            return view;
        }

        // --- ust HUD yardimcilari ---

        static RectTransform AddGroup(RectTransform parent, string name,
                                      float anchorLeft, float anchorRight,
                                      float left, float right, float top, float height)
        {
            GameObject go = NewUI(name, parent);
            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(anchorLeft, 1f);
            r.anchorMax = new Vector2(anchorRight, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(left, 0f);
            r.offsetMax = new Vector2(right, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, height);
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, top);
            return r;
        }

        static Image AddIcon(RectTransform parent, string name, Sprite sprite, float size)
        {
            GameObject go = NewUI(name, parent);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            img.preserveAspect = true;

            RectTransform r = (RectTransform)go.transform;
            r.anchorMin = new Vector2(0f, 0.5f);
            r.anchorMax = new Vector2(0f, 0.5f);
            r.pivot = new Vector2(0f, 0.5f);
            r.sizeDelta = new Vector2(size, size);
            r.anchoredPosition = Vector2.zero;
            return img;
        }

        static TextMeshProUGUI AddText(RectTransform parent, string name, float fontSize,
                                       TextAlignmentOptions align, Color color)
        {
            GameObject go = NewUI(name, parent);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = fontSize;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.text = "";

            Stretch((RectTransform)go.transform);
            return t;
        }

        /// <summary>
        /// Metni kutusuna sigacak kadar kucultur (asla buyutmez).
        ///
        /// Neden Ellipsis yeterli degil: proje fontu LiberationSans SDF "…"
        /// glifini icermiyor, TMP bunu konsola uyari olarak yaziyor ve overflow
        /// modunu sessizce Truncate'e dusuruyor. Yani tasan sayi UYARISIZ
        /// kesiliyor — "$1.23M" yerine "$1.2" gibi. Sayilarin surekli buyudugu
        /// bir idle oyununda bu kabul edilemez.
        /// </summary>
        static void AutoShrink(TextMeshProUGUI t, float max, float min)
        {
            if (t == null) return;
            t.enableAutoSizing = true;
            t.fontSizeMax = max;
            t.fontSizeMin = min;
        }

        /// <summary>Panelin tam genisligini kaplayan, ustten y konumlu tek satir.</summary>
        static TextMeshProUGUI AddRow(RectTransform parent, string name, float y, float height,
                                      float fontSize, TextAlignmentOptions align, Color color)
        {
            TextMeshProUGUI t = AddText(parent, name, fontSize, align, color);

            RectTransform r = (RectTransform)t.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(Pad, 0f);
            r.offsetMax = new Vector2(-Pad, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, height);
            r.anchoredPosition = new Vector2(0f, y);
            return t;
        }

        static void InsetLeft(RectTransform rect, float left)
        {
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
        }

        /// <summary>
        /// Alt panel (GDD Bolum 9): sekme cubugu + kaydirilabilir yukseltme listesi.
        ///
        /// Ust HUD'un aksine burasi raycast'i YAKALAR — kaydirma ve kart tiklamasi
        /// icin gerekli. Yani alt panelin uzerine dokunmak hiz kazandirmaz;
        /// tiklama alani kadranin oldugu orta bolge.
        /// </summary>
        static void CreateBottomPanel(Transform parent)
        {
            GameObject panel = NewUI("BottomPanel", parent);
            Image bg = panel.AddComponent<Image>();
            bg.sprite = LoadSprite(PanelPath);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(TopPanelMargin, 0f);
            rect.offsetMax = new Vector2(-TopPanelMargin, 0f);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, BottomPanelHeight);
            rect.anchoredPosition = new Vector2(0f, TopPanelMargin);

            // Kartlar her bakiye degisiminde tazeleniyor; dugmeler ve kaydirma
            // burada oldugu icin kendi GraphicRaycaster'i sart.
            MakeSubCanvas(panel, true);

            // --- kaydirma alani ---

            GameObject scrollGo = NewUI("ScrollView", rect);
            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(20f, 36f);
            // Sekme cubugu ile kart listesi arasinda toplu alim seridine yer acilir.
            scrollRect.offsetMax = new Vector2(-20f, -(TabBarHeight + 24f + BulkRowHeight));

            GameObject viewportGo = NewUI("Viewport", scrollRect);
            RectTransform viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport);
            viewportGo.AddComponent<RectMask2D>();

            // Kaydirmanin surukleme olayini alabilmesi icin gorunmez ama
            // raycast alan bir grafik gerekiyor.
            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;

            GameObject contentGo = NewUI("Content", viewport);
            RectTransform content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            content.anchoredPosition = Vector2.zero;

            var layout = contentGo.AddComponent<VerticalLayoutGroup>();
            layout.spacing = CardSpacing;
            layout.padding = new RectOffset(8, 8, 8, 8);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;
            scroll.viewport = viewport;
            scroll.content = content;

            UpgradeListView list = scrollGo.AddComponent<UpgradeListView>();
            GameObject cardPrefab = CreateUpgradeCardPrefab(content);
            list.Bind(content, cardPrefab);

            GameObject bulkToggle = CreateBulkBuyToggle(rect, list);

            // --- prestij sayfasi (yukseltme listesiyle ayni alani paylasir) ---

            GameObject prestigePage = CreatePrestigePage(rect, scrollRect);

            // --- sekme cubugu ---

            CreateTabBar(rect, list, scrollGo, prestigePage, bulkToggle);
        }

        /// <summary>
        /// Ilk dokunus ipucu. Kadranin altinda, alt panelin ustunde durur ve
        /// birkac tiklamadan sonra kalici olarak kaybolur.
        /// </summary>
        static void CreateOnboardingHint(Transform parent)
        {
            GameObject go = NewUI("OnboardingHint", parent);
            RectTransform rect = (RectTransform)go.transform;
            SetAnchor(rect, new Vector2(0.5f, 0.5f), new Vector2(900f, 70f),
                      new Vector2(0f, OnboardingHintY));

            CanvasGroup group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;   // tiklama ClickCatcher'a gecmeli
            group.interactable = false;

            TextMeshProUGUI label = AddText(rect, "HintText", 40f,
                                            TextAlignmentOptions.Center,
                                            new Color(1f, 1f, 1f, 1f));
            label.fontStyle = FontStyles.Bold;
            label.raycastTarget = false;

            OnboardingHintView view = go.AddComponent<OnboardingHintView>();
            view.Bind(group, label);

            // Ipucu ilk oturum boyunca nabiz gibi atiyor (CanvasGroup alfasi her
            // karede degisiyor). Kendi Canvas'inda olmazsa o nabiz tum arayuzu
            // her karede yeniden ordururdu.
            MakeSubCanvas(go, false);
        }

        /// <summary>
        /// x1 / x10 / MAKS dugmesi. Sekme cubugunun hemen altinda, sag kenarda.
        /// Tek dugme gorunurdeki tum kartlarin satin alma modunu degistiriyor.
        /// </summary>
        static GameObject CreateBulkBuyToggle(RectTransform parent, UpgradeListView list)
        {
            GameObject go = NewUI("BulkBuyToggle", parent);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(150f, BulkRowHeight - 8f);
            rect.anchoredPosition = new Vector2(-24f, -(TabBarHeight + 20f));

            Image bg = go.AddComponent<Image>();
            bg.sprite = LoadSprite(ButtonNormalPath);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.SpriteSwap;

            SpriteState state = new SpriteState();
            state.pressedSprite = LoadSprite(ButtonPressedPath);
            state.selectedSprite = LoadSprite(ButtonNormalPath);
            state.disabledSprite = LoadSprite(ButtonDisabledPath);
            button.spriteState = state;

            TextMeshProUGUI label = AddText(rect, "BulkLabel", 30f,
                                            TextAlignmentOptions.Center, Color.white);
            label.fontStyle = FontStyles.Bold;

            BulkBuyToggleView view = go.AddComponent<BulkBuyToggleView>();
            view.Bind(button, label, list);
            return go;
        }

        /// <summary>
        /// Hat Degisimi sayfasi. Kaydirma alaniyla ayni dikdortgeni kaplar;
        /// TabBarView hangisinin acik olacagina karar verir.
        /// </summary>
        static GameObject CreatePrestigePage(RectTransform parent, RectTransform scrollRect)
        {
            GameObject page = NewUI("PrestigePage", parent);
            RectTransform rect = (RectTransform)page.transform;
            Stretch(rect);

            // Panel ustte sabit yukseklikte duruyor, altindaki alani yetenek
            // kartlari kullaniyor. Yukseklik = parentH + offsetMax.y - offsetMin.y
            // oldugu icin alt kenar buradan turetiliyor.
            float bottom = BottomPanelHeight + scrollRect.offsetMax.y - PrestigePanelHeight;
            rect.offsetMin = new Vector2(scrollRect.offsetMin.x, bottom);
            rect.offsetMax = scrollRect.offsetMax;

            // Fiber Kredisi rozeti
            Image fiber = AddIcon(rect, "FiberIcon", LoadSprite(IconFiberPath), 64f);
            RectTransform fiberRect = (RectTransform)fiber.transform;
            fiberRect.anchorMin = new Vector2(0f, 1f);
            fiberRect.anchorMax = new Vector2(0f, 1f);
            fiberRect.pivot = new Vector2(0f, 1f);
            fiberRect.anchoredPosition = new Vector2(38f, -12f);

            TextMeshProUGUI credits = AddCardTextStretched(rect, "CreditsText", 44f,
                TextAlignmentOptions.TopLeft, new Color(0.05f, 0.65f, 0.40f, 1f), 118f, 40f, -14f, 52f);

            TextMeshProUGUI multiplier = AddCardTextStretched(rect, "MultiplierText", 26f,
                TextAlignmentOptions.TopLeft, new Color(0.35f, 0.40f, 0.50f, 1f), 118f, 40f, -66f, 34f);

            TextMeshProUGUI pending = AddCardTextStretched(rect, "PendingText", 32f,
                TextAlignmentOptions.Top, new Color(0.15f, 0.20f, 0.30f, 1f), 38f, 38f, -104f, 44f);

            // Aciklama en fazla IKI satir; 20 punto ile 48 birim yetiyor.
            // Eskiden 96 birim ayrilmisti ve buton bolgesine tasiyordu.
            TextMeshProUGUI explain = AddCardTextStretched(rect, "ExplainText", 20f,
                TextAlignmentOptions.Top, new Color(0.25f, 0.30f, 0.40f, 1f), 38f, 38f, -150f, 48f);
            explain.textWrappingMode = TextWrappingModes.Normal;
            explain.overflowMode = TextOverflowModes.Ellipsis;

            // Hat Degistir butonu
            GameObject btnGo = NewUI("PrestigeButton", rect);
            Image btnImage = btnGo.AddComponent<Image>();
            btnImage.sprite = LoadSprite(ButtonNormalPath);
            btnImage.type = Image.Type.Sliced;
            btnImage.raycastTarget = true;
            btnImage.pixelsPerUnitMultiplier = 2f;

            RectTransform btnRect = (RectTransform)btnGo.transform;
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.sizeDelta = new Vector2(520f, 112f);
            btnRect.anchoredPosition = new Vector2(0f, 22f);

            Button button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.transition = Selectable.Transition.SpriteSwap;

            SpriteState state = new SpriteState();
            state.pressedSprite = LoadSprite(ButtonPressedPath);
            state.selectedSprite = LoadSprite(ButtonNormalPath);
            state.disabledSprite = LoadSprite(ButtonDisabledPath);
            button.spriteState = state;

            TextMeshProUGUI label = AddText(btnRect, "Label", 36f,
                TextAlignmentOptions.Center, TextBright);
            label.SetText("HAT DEGISTIR");

            PrestigePanelView view = page.AddComponent<PrestigePanelView>();
            view.Bind(credits, multiplier, pending, explain, button, label);

            page.SetActive(false);
            return page;
        }

        static void CreateTabBar(RectTransform parent, UpgradeListView list,
                                 GameObject listRoot, GameObject prestigeRoot,
                                 GameObject bulkToggle)
        {
            GameObject barGo = NewUI("TabBar", parent);
            RectTransform bar = (RectTransform)barGo.transform;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.offsetMin = new Vector2(20f, 0f);
            bar.offsetMax = new Vector2(-20f, 0f);
            bar.sizeDelta = new Vector2(bar.sizeDelta.x, TabBarHeight);
            bar.anchoredPosition = new Vector2(0f, -18f);

            string[] names = { "Altyapi", "Donanim", "Yazilim", "Prestij" };
            string[] sprites = { TabInfraPath, TabHardwarePath, TabSoftwarePath, TabPrestigePath };

            int count = names.Length;
            var buttons = new Button[count];
            var icons = new Image[count];

            for (int i = 0; i < count; i++)
            {
                GameObject slot = NewUI("Tab_" + names[i], bar);
                RectTransform slotRect = (RectTransform)slot.transform;
                slotRect.anchorMin = new Vector2(i / (float)count, 0f);
                slotRect.anchorMax = new Vector2((i + 1) / (float)count, 1f);
                slotRect.offsetMin = new Vector2(6f, 0f);
                slotRect.offsetMax = new Vector2(-6f, 0f);

                // Butonun kendisi saydam; tiklama alani tum sekme genisligi.
                Image hit = slot.AddComponent<Image>();
                hit.color = new Color(1f, 1f, 1f, 0f);
                hit.raycastTarget = true;

                Button btn = slot.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.targetGraphic = hit;
                buttons[i] = btn;

                GameObject iconGo = NewUI("Icon", slotRect);
                Image icon = iconGo.AddComponent<Image>();
                icon.sprite = LoadSprite(sprites[i]);
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                SetAnchor((RectTransform)iconGo.transform, new Vector2(0.5f, 0.5f),
                          new Vector2(TabIconSize, TabIconSize), Vector2.zero);
                icons[i] = icon;
            }

            TabBarView view = barGo.AddComponent<TabBarView>();
            view.Bind(list, listRoot, prestigeRoot, buttons, icons, bulkToggle);

            // Prestij sekmesinde liste, Hat Degisimi panelinin altina iner.
            var listRect = (RectTransform)listRoot.transform;
            float normalTop = listRect.offsetMax.y;
            view.BindListArea(listRect, normalTop, normalTop - PrestigePanelHeight);
        }

        /// <summary>
        /// Kart sablonu sahnede kurulup prefab'a yazilir, sonra sahneden silinir —
        /// kartlari calisma aninda UpgradeListView uretir.
        /// </summary>
        static GameObject CreateUpgradeCardPrefab(RectTransform tempParent)
        {
            GameObject card = NewUI("UpgradeCard", tempParent);
            RectTransform rect = (RectTransform)card.transform;
            rect.sizeDelta = new Vector2(1000f, CardHeight);

            Image bg = card.AddComponent<Image>();
            bg.sprite = LoadSprite(ButtonNormalPath);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            var element = card.AddComponent<LayoutElement>();
            element.preferredHeight = CardHeight;
            element.minHeight = CardHeight;

            Button button = card.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.SpriteSwap;

            SpriteState state = new SpriteState();
            state.pressedSprite = LoadSprite(ButtonPressedPath);
            state.selectedSprite = LoadSprite(ButtonNormalPath);
            state.disabledSprite = LoadSprite(ButtonDisabledPath);
            button.spriteState = state;

            // Simge
            GameObject iconGo = NewUI("Icon", rect);
            Image icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            RectTransform iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(CardIconSize, CardIconSize);
            iconRect.anchoredPosition = new Vector2(38f, 0f);

            // Kilit rozeti — simgenin uzerine biner
            GameObject lockGo = NewUI("LockIcon", rect);
            Image lockIcon = lockGo.AddComponent<Image>();
            lockIcon.sprite = LoadSprite(IconLockPath);
            lockIcon.raycastTarget = false;
            lockIcon.preserveAspect = true;
            RectTransform lockRect = (RectTransform)lockGo.transform;
            lockRect.anchorMin = new Vector2(0f, 0.5f);
            lockRect.anchorMax = new Vector2(0f, 0.5f);
            lockRect.pivot = new Vector2(0f, 0.5f);
            lockRect.sizeDelta = new Vector2(64f, 64f);
            lockRect.anchoredPosition = new Vector2(38f + CardIconSize - 44f, -32f);

            // Sol blok: ad + aciklama. Sag kenarda seviye/maliyet icin bol yer birakilir.
            TextMeshProUGUI title = AddCardTextStretched(rect, "NameText", 34f,
                TextAlignmentOptions.BottomLeft, Color.white,
                CardTextLeft, CardRightBlock, -18f, 48f);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI desc = AddCardTextStretched(rect, "DescText", 23f,
                TextAlignmentOptions.TopLeft, Color.white,
                CardTextLeft, CardRightBlock, -68f, 100f);
            desc.textWrappingMode = TextWrappingModes.Normal;

            // Sag blok: seviye UST-SAG'a, maliyet ALT-SAG'a sabitli.
            // x = -160f verilerek oval kapsulun kavisli koselerinden tamamen kurtarilir!
            TextMeshProUGUI level = AddText(rect, "LevelText", 25f,
                TextAlignmentOptions.TopRight, new Color(1.00f, 0.85f, 0.30f, 1f));
            level.textWrappingMode = TextWrappingModes.NoWrap;
            level.overflowMode = TextOverflowModes.Truncate;
            level.fontStyle = FontStyles.Bold;
            {
                RectTransform lr = (RectTransform)level.transform;
                lr.anchorMin = new Vector2(1f, 1f);  // ust-sag
                lr.anchorMax = new Vector2(1f, 1f);
                lr.pivot = new Vector2(1f, 1f);
                lr.sizeDelta = new Vector2(240f, 40f);
                lr.anchoredPosition = new Vector2(-160f, -22f);
            }

            TextMeshProUGUI cost = AddText(rect, "CostText", 36f,
                TextAlignmentOptions.BottomRight, new Color(1.00f, 0.38f, 0.35f, 1f));
            cost.textWrappingMode = TextWrappingModes.NoWrap;
            cost.overflowMode = TextOverflowModes.Truncate;
            cost.fontStyle = FontStyles.Bold;
            {
                RectTransform cr = (RectTransform)cost.transform;
                cr.anchorMin = new Vector2(1f, 0f);  // alt-sag
                cr.anchorMax = new Vector2(1f, 0f);
                cr.pivot = new Vector2(1f, 0f);
                cr.sizeDelta = new Vector2(240f, 50f);
                cr.anchoredPosition = new Vector2(-160f, 22f);
            }

            // Kilometre tasi rozeti — simgenin sag-USTUNE biner (kilit sag-altta,
            // ikisi ayni anda gorunebilir: kilitli bir kartta rozet zaten cikmaz).
            GameObject badgeGo = NewUI("MilestoneBadge", rect);
            Image badge = badgeGo.AddComponent<Image>();
            badge.sprite = LoadSprite(BadgeMilestonePath);
            badge.raycastTarget = false;
            badge.preserveAspect = true;
            RectTransform badgeRect = (RectTransform)badgeGo.transform;
            badgeRect.anchorMin = new Vector2(0f, 0.5f);
            badgeRect.anchorMax = new Vector2(0f, 0.5f);
            badgeRect.pivot = new Vector2(0f, 0.5f);
            badgeRect.sizeDelta = new Vector2(72f, 72f);
            badgeRect.anchoredPosition = new Vector2(38f + CardIconSize - 48f, 34f);

            TextMeshProUGUI multText = AddText(badgeRect, "MilestoneText", 24f,
                TextAlignmentOptions.Center, Color.white);
            multText.fontStyle = FontStyles.Bold;
            multText.textWrappingMode = TextWrappingModes.NoWrap;
            {
                RectTransform mr = (RectTransform)multText.transform;
                mr.anchorMin = Vector2.zero;
                mr.anchorMax = Vector2.one;
                mr.offsetMin = Vector2.zero;
                mr.offsetMax = Vector2.zero;
            }

            UpgradeCardView view = card.AddComponent<UpgradeCardView>();
            view.Bind(button, bg, icon, lockIcon, title, desc, level, cost, badge, multText);

            Directory.CreateDirectory(UiPrefabDir);
            AssetDatabase.Refresh();

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(card, CardPrefabPath);
            Object.DestroyImmediate(card);
            return asset;
        }

        /// <summary>Kartin genisligi boyunca esneyen, ustten y konumlu metin.</summary>
        static TextMeshProUGUI AddCardTextStretched(RectTransform parent, string name, float fontSize,
                                                    TextAlignmentOptions align, Color color,
                                                    float left, float rightReserve, float y, float height)
        {
            TextMeshProUGUI t = AddText(parent, name, fontSize, align, color);

            RectTransform r = (RectTransform)t.transform;
            r.anchorMin = new Vector2(0f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.offsetMin = new Vector2(left, 0f);
            r.offsetMax = new Vector2(-rightReserve, 0f);
            r.sizeDelta = new Vector2(r.sizeDelta.x, height);
            r.anchoredPosition = new Vector2(r.anchoredPosition.x, y);
            return t;
        }

        /// <summary>Kartin sag kenarina sabitli, sabit genislikte metin.</summary>
        static TextMeshProUGUI AddCardTextRight(RectTransform parent, string name, float fontSize,
                                                TextAlignmentOptions align, Color color,
                                                float y, float height)
        {
            TextMeshProUGUI t = AddText(parent, name, fontSize, align, color);

            RectTransform r = (RectTransform)t.transform;
            r.anchorMin = new Vector2(1f, 1f);
            r.anchorMax = new Vector2(1f, 1f);
            r.pivot = new Vector2(1f, 1f);
            r.sizeDelta = new Vector2(CardRightBlock - 34f, height);
            r.anchoredPosition = new Vector2(-34f, y);
            return t;
        }

        static DebugHud CreateDebugPanel(Transform parent)
        {
            GameObject panel = NewUI("DebugPanel", parent);
            Image bg = panel.AddComponent<Image>();
            bg.sprite = LoadSprite(PanelPath);
            bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.92f);
            bg.raycastTarget = false;

            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(620f, 640f);
            rect.anchoredPosition = new Vector2(30f, 30f);

            GameObject labelGo = NewUI("DebugLabel", rect);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 30f;
            label.color = new Color(0.90f, 0.94f, 1f, 1f);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.raycastTarget = false;
            label.richText = true;
            label.text = "...";

            RectTransform labelRect = (RectTransform)labelGo.transform;
            Stretch(labelRect);
            labelRect.offsetMin = new Vector2(44f, 40f);
            labelRect.offsetMax = new Vector2(-40f, -50f);

            DebugHud hud = panel.AddComponent<DebugHud>();
            // Alfa ile gizlenir, SetActive ile degil — panel kapaliyken de
            // DebugHud.Update calisip 1-9 ve F1 kisayollarini dinlemeli.
            CanvasGroup group = panel.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            hud.Bind(label, null, group);
            return hud;
        }

        /// <summary>
        /// "Yokken de indirdik" raporu. Baslangicta alfa 0; SaveManager.Loaded
        /// gercekten para kazanildiysa acar.
        /// </summary>
        static void CreateOfflineReport(Transform parent)
        {
            GameObject root = NewUI("OfflineReport", parent);
            Stretch((RectTransform)root.transform);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            // Karartma — arkadaki arayuze dokunmayi da engeller.
            GameObject dimGo = NewUI("Dimmer", root.transform);
            Image dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;
            Stretch((RectTransform)dimGo.transform);

            GameObject panelGo = NewUI("Panel", root.transform);
            Image panel = panelGo.AddComponent<Image>();
            panel.sprite = LoadSprite(PanelPath);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            SetAnchor((RectTransform)panelGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(860f, 660f), Vector2.zero);

            RectTransform panelRect = (RectTransform)panelGo.transform;

            TextMeshProUGUI title = AddCardTextStretched(panelRect, "TitleText", 48f,
                TextAlignmentOptions.Top, TextBright, 48f, 48f, -52f, 64f);

            TextMeshProUGUI amount = AddCardTextStretched(panelRect, "AmountText", 88f,
                TextAlignmentOptions.Top, TextAccent, 48f, 48f, -140f, 110f);

            TextMeshProUGUI body = AddCardTextStretched(panelRect, "BodyText", 30f,
                TextAlignmentOptions.Top, TextDim, 48f, 48f, -268f, 200f);
            body.textWrappingMode = TextWrappingModes.Normal;

            // Kapat butonu
            GameObject btnGo = NewUI("CloseButton", panelRect);
            Image btnImage = btnGo.AddComponent<Image>();
            btnImage.sprite = LoadSprite(ButtonNormalPath);
            btnImage.type = Image.Type.Sliced;
            btnImage.raycastTarget = true;

            RectTransform btnRect = (RectTransform)btnGo.transform;
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.sizeDelta = new Vector2(440f, 132f);
            btnRect.anchoredPosition = new Vector2(0f, 56f);

            Button button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.transition = Selectable.Transition.SpriteSwap;

            SpriteState state = new SpriteState();
            state.pressedSprite = LoadSprite(ButtonPressedPath);
            state.selectedSprite = LoadSprite(ButtonNormalPath);
            state.disabledSprite = LoadSprite(ButtonDisabledPath);
            button.spriteState = state;

            TextMeshProUGUI label = AddText(btnRect, "Label", 40f,
                TextAlignmentOptions.Center, TextBright);
            // Dugme yazisi dogrudan Turkce gomuluydu; Ingilizce oynayan biri
            // "TAMAM" goruyordu.
            label.gameObject.AddComponent<LocalizedText>().SetKey("offline_claim", "COLLECT");

            // --- Odullu reklam: kazanci ikiye katla ---
            //
            // Kapat butonunun USTUNDE duruyor: oyuncunun gozu once odule,
            // sonra cikisa gitsin. Kazanc yoksa veya reklam hazir degilse
            // OfflineReportView bu nesneyi kapatiyor.
            GameObject dblGo = NewUI("DoubleButton", panelRect);
            Image dblImage = dblGo.AddComponent<Image>();
            dblImage.sprite = LoadSprite(ButtonNormalPath);
            dblImage.type = Image.Type.Sliced;
            dblImage.raycastTarget = true;

            RectTransform dblRect = (RectTransform)dblGo.transform;
            dblRect.anchorMin = new Vector2(0.5f, 0f);
            dblRect.anchorMax = new Vector2(0.5f, 0f);
            dblRect.pivot = new Vector2(0.5f, 0f);
            dblRect.sizeDelta = new Vector2(440f, 116f);
            dblRect.anchoredPosition = new Vector2(0f, 200f);

            Button dblButton = dblGo.AddComponent<Button>();
            dblButton.targetGraphic = dblImage;
            dblButton.transition = Selectable.Transition.SpriteSwap;

            SpriteState dblState = new SpriteState();
            dblState.pressedSprite = LoadSprite(ButtonPressedPath);
            dblState.selectedSprite = LoadSprite(ButtonNormalPath);
            dblState.disabledSprite = LoadSprite(ButtonDisabledPath);
            dblButton.spriteState = dblState;

            // Reklam simgesi — dugmenin bir REKLAM oldugu basmadan once anlasilmali.
            Image dblIcon = AddIcon(dblRect, "AdIcon", LoadSprite(IconRewardAdPath), 64f);
            RectTransform dblIconRect = dblIcon.rectTransform;
            dblIconRect.anchorMin = new Vector2(0f, 0.5f);
            dblIconRect.anchorMax = new Vector2(0f, 0.5f);
            dblIconRect.pivot = new Vector2(0f, 0.5f);
            dblIconRect.anchoredPosition = new Vector2(28f, 0f);

            TextMeshProUGUI dblLabel = AddText(dblRect, "Label", 34f,
                TextAlignmentOptions.Center, TextBright);

            OfflineReportView view = root.AddComponent<OfflineReportView>();
            view.Bind(group, title, body, amount, button, dblButton, dblLabel);
        }

        /// <summary>
        /// Kademe atlama kutlamasi.
        ///
        /// <see cref="TierUpModalView"/> yazilmisti ama sahnede hicbir yerde
        /// olusturulmuyordu — yani oyunun en buyuk ilerleme ani ("yeni caga
        /// gectin") sessizce gecip gidiyordu. Yeni kademe adini, simgesini ve
        /// hiz araligini gostermek, yukseltmelere harcanan paranin karsiligini
        /// gorunur kiliyor.
        /// </summary>
        static void CreateTierUpModal(Transform parent)
        {
            GameObject root = NewUI("TierUpModal", parent);
            Stretch((RectTransform)root.transform);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject dimGo = NewUI("Dimmer", root.transform);
            Image dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.72f);
            dim.raycastTarget = true;
            Stretch((RectTransform)dimGo.transform);

            GameObject panelGo = NewUI("Panel", root.transform);
            Image panel = panelGo.AddComponent<Image>();
            panel.sprite = LoadSprite(PanelPath);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            SetAnchor((RectTransform)panelGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(860f, 720f), Vector2.zero);

            RectTransform panelRect = (RectTransform)panelGo.transform;

            TextMeshProUGUI title = AddCardTextStretched(panelRect, "TitleText", 44f,
                TextAlignmentOptions.Top, TextAccent, 48f, 48f, -48f, 60f);

            GameObject iconGo = NewUI("TierIcon", panelRect);
            Image icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            SetAnchor((RectTransform)iconGo.transform, new Vector2(0.5f, 1f),
                      new Vector2(200f, 200f), new Vector2(0f, -130f));

            TextMeshProUGUI tierName = AddCardTextStretched(panelRect, "TierNameText", 56f,
                TextAlignmentOptions.Top, TextBright, 40f, 40f, -348f, 76f);

            TextMeshProUGUI range = AddCardTextStretched(panelRect, "RangeText", 34f,
                TextAlignmentOptions.Top, new Color(0.55f, 0.70f, 0.90f, 1f), 40f, 40f, -424f, 50f);

            GameObject btnGo = NewUI("ContinueButton", panelRect);
            Image btnImage = btnGo.AddComponent<Image>();
            btnImage.sprite = LoadSprite(ButtonNormalPath);
            btnImage.type = Image.Type.Sliced;
            btnImage.raycastTarget = true;

            RectTransform btnRect = (RectTransform)btnGo.transform;
            btnRect.anchorMin = new Vector2(0.5f, 0f);
            btnRect.anchorMax = new Vector2(0.5f, 0f);
            btnRect.pivot = new Vector2(0.5f, 0f);
            btnRect.sizeDelta = new Vector2(440f, 132f);
            btnRect.anchoredPosition = new Vector2(0f, 56f);

            Button button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.transition = Selectable.Transition.SpriteSwap;

            SpriteState btnState = new SpriteState();
            btnState.pressedSprite = LoadSprite(ButtonPressedPath);
            btnState.selectedSprite = LoadSprite(ButtonNormalPath);
            btnState.disabledSprite = LoadSprite(ButtonDisabledPath);
            button.spriteState = btnState;

            TextMeshProUGUI btnLabel = AddText(btnRect, "Label", 40f,
                TextAlignmentOptions.Center, TextBright);
            btnLabel.gameObject.AddComponent<LocalizedText>().SetKey("tierup_continue", "CONTINUE");

            TierUpModalView view = root.AddComponent<TierUpModalView>();
            view.Bind(group, title, tierName, icon, range, button);
        }

        /// <summary>
        /// Olay banneri (GDD Bolum 8). Ustten kayarak girer; kadranin ustunde
        /// ama ust HUD'un altinda durur.
        /// </summary>
        static void CreateEventBanner(Transform parent)
        {
            GameObject root = NewUI("EventBanner", parent);
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = new Vector2(0f, 1f);
            rootRect.anchorMax = new Vector2(1f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.offsetMin = new Vector2(TopPanelMargin, 0f);
            rootRect.offsetMax = new Vector2(-TopPanelMargin, 0f);
            rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, BannerHeight);
            rootRect.anchoredPosition = new Vector2(0f, -BannerTop);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Image bg = root.AddComponent<Image>();
            bg.sprite = LoadSprite(PanelPath);
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;

            Image icon = AddIcon(rootRect, "EventIcon", null, BannerIconSize);
            ((RectTransform)icon.transform).anchoredPosition = new Vector2(26f, 0f);

            TextMeshProUGUI title = AddCardTextStretched(rootRect, "EventTitle", 32f,
                TextAlignmentOptions.BottomLeft, TextBright, 116f, 430f, -16f, 42f);

            TextMeshProUGUI body = AddCardTextStretched(rootRect, "EventBody", 26f,
                TextAlignmentOptions.TopLeft, TextDim, 116f, 430f, -58f, 40f);

            // Aksiyon butonu (Sifreyi Degistir / Kotayi Ac)
            GameObject btnGo = NewUI("ActionButton", rootRect);
            Image btnImage = btnGo.AddComponent<Image>();
            btnImage.sprite = LoadSprite(ButtonNormalPath);
            btnImage.type = Image.Type.Sliced;
            btnImage.raycastTarget = true;

            RectTransform btnRect = (RectTransform)btnGo.transform;
            btnRect.anchorMin = new Vector2(1f, 0.5f);
            btnRect.anchorMax = new Vector2(1f, 0.5f);
            btnRect.pivot = new Vector2(1f, 0.5f);
            btnRect.sizeDelta = new Vector2(400f, 84f);
            btnRect.anchoredPosition = new Vector2(-18f, 0f);

            Button button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImage;
            button.transition = Selectable.Transition.SpriteSwap;

            SpriteState state = new SpriteState();
            state.pressedSprite = LoadSprite(ButtonPressedPath);
            state.selectedSprite = LoadSprite(ButtonNormalPath);
            state.disabledSprite = LoadSprite(ButtonDisabledPath);
            button.spriteState = state;

            TextMeshProUGUI label = AddText(btnRect, "Label", 26f,
                TextAlignmentOptions.Center, TextBright);
            label.SetText("");

            // Kota icin ikinci yol: odullu reklam (GDD Bolum 8).
            GameObject adGo = NewUI("AdButton", rootRect);
            Image adImage = adGo.AddComponent<Image>();
            adImage.sprite = LoadSprite(IconRewardAdPath);
            adImage.raycastTarget = true;
            adImage.preserveAspect = true;

            RectTransform adRect = (RectTransform)adGo.transform;
            adRect.anchorMin = new Vector2(1f, 0.5f);
            adRect.anchorMax = new Vector2(1f, 0.5f);
            adRect.pivot = new Vector2(1f, 0.5f);
            adRect.sizeDelta = new Vector2(76f, 76f);
            adRect.anchoredPosition = new Vector2(-430f, 0f);

            Button adButton = adGo.AddComponent<Button>();
            adButton.targetGraphic = adImage;
            adButton.transition = Selectable.Transition.None;
            adGo.SetActive(false);

            EventBannerView view = root.AddComponent<EventBannerView>();
            view.Bind(rootRect, group, bg, icon, title, body, button, label, adButton,
                      LoadSprite(EventHappyBannerPath), LoadSprite(PanelPath),
                      LoadSprite(EventWifiIconPath), LoadSprite(EventPasswordIconPath),
                      LoadSprite(EventQuotaIconPath));

            // Banner kayarak girip cikiyor ve Gece Tarifesi'nde geri sayiyor;
            // dugmesi de var. Kendi Canvas'i olmazsa bu hareket tum arayuzu
            // yeniden ordururdu.
            MakeSubCanvas(root, true);
        }

        /// <summary>Maskot — kadrana daha yakin, ust-sag bolgede ve tiklanabilir.</summary>
        static void CreateMascot(Transform parent)
        {
            GameObject go = NewUI("Mascot", parent);
            Image img = go.AddComponent<Image>();
            img.sprite = LoadSprite(MascotIdlePath);
            img.raycastTarget = true;
            img.preserveAspect = true;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(220f, 220f);
            rect.anchoredPosition = new Vector2(-20f, -540f);

            MascotController controller = go.AddComponent<MascotController>();
            controller.Bind(img,
                LoadSprite(MascotIdlePath),
                LoadSprite(MascotHappyPath),
                LoadSprite(MascotOverheatedPath));

            // Maskot bosta bile salinip duruyor; kendi Canvas'ina alinmazsa bu
            // salinim her karede tum SafeArea'yi yeniden ordururdu.
            // Dokunulabilir (tiklayinca seviniyor), o yuzden raycaster'i var.
            MakeSubCanvas(go, true);
        }

        /// <summary>
        /// Efekt katmani + havuz tanimlari. Havuz boyutlari efektin ne siklikta
        /// ust uste binebilecegine gore: tiklama en sik, konfeti en seyrek.
        /// </summary>
        static void CreateFXLayer(Transform parent)
        {
            GameObject go = NewUI("FXLayer", parent);
            RectTransform rect = (RectTransform)go.transform;
            Stretch(rect);

            // Efektler asla tiklamayi yakalamamali.
            var group = go.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;

            var defs = new FXManager.FXDef[]
            {
                MakeFX(FXType.ClickRipple,    FxClickPath,    0.45f, 0.35f, 1.15f, 0.75f, 0f,    0f,   Color.white,                          12),
                MakeFX(FXType.CriticalBurst,  FxCriticalPath, 0.55f, 0.50f, 1.60f, 1.00f, 0f,    140f, new Color(1f, 0.85f, 0.3f, 1f),        4),
                MakeFX(FXType.CoinBurst,      FxCoinPath,     0.80f, 0.60f, 1.25f, 1.00f, 120f,  0f,   Color.white,                           6),
                MakeFX(FXType.TurboFlame,     FxFlamePath,    0.40f, 0.85f, 1.30f, 0.70f, 60f,   0f,   new Color(1f, 0.55f, 0.2f, 1f),        6),
                MakeFX(FXType.OverheatFlash,  FxFlashPath,    0.35f, 0.80f, 2.20f, 0.95f, 0f,    0f,   Color.white,                           2),
                MakeFX(FXType.OverheatSmoke,  FxSmokePath,    1.40f, 0.60f, 1.80f, 0.85f, 200f,  40f,  Color.white,                           3),
                MakeFX(FXType.Confetti,       FxConfettiPath, 1.80f, 0.70f, 1.60f, 1.00f, 260f,  90f,  Color.white,                           4)
            };

            FXManager manager = go.AddComponent<FXManager>();
            manager.Bind(rect, defs);

            // Efektler her karede olcek/konum/alfa degistiriyor: izole edilmezse
            // tek bir tiklama halkasi bile tum arayuzu yeniden orduruyor.
            MakeSubCanvas(go, false);
        }

        /// <summary>
        /// "Sinyal Yakalandi" balonunun katmani.
        ///
        /// <see cref="FloatingEventController"/> [Game] nesnesinde duruyor, yani
        /// Canvas'in altinda DEGIL. Kendi kendine bir Canvas bulmaya calisiyor ve
        /// bulamiyordu; ozellik bu yuzden hic calismamisti. Katman burada acikca
        /// olusturulup baglaniyor.
        /// </summary>
        static void CreateFloatingEventLayer(Transform parent)
        {
            var controller = Object.FindAnyObjectByType<FloatingEventController>();
            if (controller == null) return;

            GameObject go = NewUI("FloatingEventLayer", parent);
            RectTransform rect = (RectTransform)go.transform;
            Stretch(rect);

            // Balona dokunulabilmesi icin kendi raycaster'i sart; alt Canvas
            // olmasi da hareketinin diger panelleri kirletmesini engelliyor.
            MakeSubCanvas(go, true);

            controller.Bind(rect, LoadSprite(FxCriticalPath));
            EditorUtility.SetDirty(controller);
        }

        static FXManager.FXDef MakeFX(FXType type, string spritePath, float duration,
                                      float startScale, float endScale, float startAlpha,
                                      float rise, float spin, Color tint, int poolSize)
        {
            var def = new FXManager.FXDef();
            def.type = type;
            def.sprite = LoadSprite(spritePath);
            def.duration = duration;
            def.startScale = startScale;
            def.endScale = endScale;
            def.startAlpha = startAlpha;
            def.riseDistance = rise;
            def.spin = spin;
            def.tint = tint;
            def.poolSize = poolSize;
            return def;
        }

        /// <summary>
        /// Sahte odullu reklam paneli (PLAN Bolum 2.7). Var olan sprite'larla:
        /// panel zemini + reklam simgesi + kapat simgesi + geri sayim.
        /// </summary>
        static void CreateAdPanel(Transform parent)
        {
            GameObject root = NewUI("AdPanel", parent);
            Stretch((RectTransform)root.transform);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject dimGo = NewUI("Dimmer", root.transform);
            Image dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.92f);
            dim.raycastTarget = true;
            Stretch((RectTransform)dimGo.transform);

            GameObject panelGo = NewUI("Panel", root.transform);
            Image panel = panelGo.AddComponent<Image>();
            panel.sprite = LoadSprite(PanelPath);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            SetAnchor((RectTransform)panelGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(880f, 900f), Vector2.zero);

            RectTransform panelRect = (RectTransform)panelGo.transform;

            Image adIcon = AddIcon(panelRect, "AdIcon", LoadSprite(IconRewardAdPath), 280f);
            RectTransform adIconRect = (RectTransform)adIcon.transform;
            adIconRect.anchorMin = new Vector2(0.5f, 0.5f);
            adIconRect.anchorMax = new Vector2(0.5f, 0.5f);
            adIconRect.pivot = new Vector2(0.5f, 0.5f);
            adIconRect.anchoredPosition = new Vector2(0f, 90f);

            TextMeshProUGUI countdown = AddCardTextStretched(panelRect, "CountdownText", 84f,
                TextAlignmentOptions.Top, TextAccent, 44f, 44f, -560f, 110f);

            TextMeshProUGUI caption = AddCardTextStretched(panelRect, "CaptionText", 28f,
                TextAlignmentOptions.Top, TextDim, 44f, 44f, -680f, 90f);
            caption.textWrappingMode = TextWrappingModes.Normal;

            GameObject closeGo = NewUI("CloseButton", panelRect);
            Image closeImage = closeGo.AddComponent<Image>();
            closeImage.sprite = LoadSprite(IconClosePath);
            closeImage.raycastTarget = true;
            closeImage.preserveAspect = true;

            RectTransform closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(76f, 76f);
            closeRect.anchoredPosition = new Vector2(-26f, -26f);

            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.transition = Selectable.Transition.None;

            // --- izleme ilerleme cubugu ---
            //
            // Panelin en buyuk eksigi buydu: ekranda HICBIR SEY oynamiyordu.
            // Sabit bir simge + bir sayi, oyuncu tarafindan "reklam cikmadi"
            // diye okunuyordu. Dolan bir cubuk, beklemenin ilerledigini tek
            // bakista gosteriyor.
            GameObject barGo = NewUI("ProgressTrack", panelRect);
            Image barTrack = barGo.AddComponent<Image>();
            barTrack.sprite = LoadSprite(BarFramePath);
            barTrack.type = Image.Type.Sliced;
            barTrack.raycastTarget = false;

            RectTransform barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(70f, 0f);
            barRect.offsetMax = new Vector2(-70f, 0f);
            barRect.sizeDelta = new Vector2(barRect.sizeDelta.x, 34f);

            // Y = -510: SIMGENIN ALTINDA, ustunde degil.
            //
            // Onceki deger -400'du ve cubuk simgenin TAM ORTASINDAN geciyordu.
            // Hesap (panel 900 birim, tepe = 0, asagi negatif):
            //     AdIcon  280 birim, merkezden +90 -> -220 .. -500
            //     Cubuk   34 birim,  -400        -> -400 .. -434   <-- ICERDE
            // Olculen cakisma: 309 x 38 px. Ekranda oynat simgesinin uzerinden
            // gecen bir cizgi olarak gorunuyordu.
            //
            // -510'da: simge -500'de bitiyor (10 birim bosluk), CountdownText
            // -560'ta basliyor (cubugun alti -544, 16 birim bosluk). Cubuk artik
            // "video oynuyor" isaretini simgenin hemen altinda veriyor.
            barRect.anchoredPosition = new Vector2(0f, -510f);

            GameObject fillGo = NewUI("ProgressFill", barRect);
            Image barFill = fillGo.AddComponent<Image>();
            barFill.sprite = LoadSprite(BarFillPath);
            barFill.type = Image.Type.Filled;
            barFill.fillMethod = Image.FillMethod.Horizontal;
            barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            barFill.fillAmount = 0f;
            barFill.raycastTarget = false;
            Stretch((RectTransform)fillGo.transform);

            // Gercek saglayici (AdMob). Sahte panelle ayni nesnede duruyor:
            // gercek reklam doldurulamadiginda ona devredebilmesi gerekiyor.
            // GameManager ikisinden yuksek oncelikli olani (AdMob) seciyor.
            root.AddComponent<AdMobService>();

            StubAdService service = root.AddComponent<StubAdService>();
            service.Bind(group, countdown, caption, closeButton, closeImage);
            service.BindPlayback(barFill, adIcon);
        }

        /// <summary>Ayarlar overlay'i + onu acan disli butonu (ust HUD kosesinde).</summary>
        /// <summary>
        /// Indirilenler Arsivi overlay'i (Faz 16).
        ///
        /// Panel bileseni Faz 9'da yazilmisti ama sahneye HIC eklenmemisti —
        /// yani ozellik kodda duruyor, oyunda goruntusu yoktu. Ayarlar ile ayni
        /// desende bir overlay: sag ustte acma dugmesi, ortada panel, icinde
        /// kaydirilabilir simge izgarasi.
        /// </summary>
        /// <summary>
        /// "Reklam izle, hiz boost'u al" dugmesi — ust HUD'un sag ustunde,
        /// arsiv ve ayarlarin solunda (ayni 72 px izgara, 90 px aralik).
        ///
        /// Kadranin uzerine konmadi bilincli olarak: kadran oyunun tek
        /// etkilesim alani ve orayi bir reklam dugmesiyle daraltmak, GDD'nin
        /// "oyun akisini bolmeyecek" kuralini ihlal ederdi.
        /// </summary>
        static void CreateBoostAdButton(Transform hudParent)
        {
            GameObject go = NewUI("BoostAdButton", hudParent);
            Image image = go.AddComponent<Image>();
            image.sprite = LoadSprite(IconRewardAdPath);
            image.raycastTarget = true;
            image.preserveAspect = true;

            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(72f, 72f);
            rect.anchoredPosition = new Vector2(-214f, -30f);

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            // Boost/bekleme suresince simgenin yerinde geri sayim gorunur.
            TextMeshProUGUI countdown = AddText(rect, "Countdown", 26f,
                TextAlignmentOptions.Center, TextAccent);

            var view = go.AddComponent<BoostAdButtonView>();
            view.Bind(button, image, countdown);
        }

        static void CreateCollection(Transform overlayParent, Transform hudParent)
        {
            // Acma dugmesi — ayarlarin soluna
            GameObject openGo = NewUI("ArchiveButton", hudParent);
            Image openImage = openGo.AddComponent<Image>();
            openImage.sprite = LoadSprite(IconArchivePath);
            openImage.raycastTarget = true;
            openImage.preserveAspect = true;

            RectTransform openRect = (RectTransform)openGo.transform;
            openRect.anchorMin = new Vector2(1f, 1f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.sizeDelta = new Vector2(72f, 72f);
            openRect.anchoredPosition = new Vector2(-124f, -30f);

            Button openButton = openGo.AddComponent<Button>();
            openButton.targetGraphic = openImage;
            openButton.transition = Selectable.Transition.None;

            // Overlay
            GameObject root = NewUI("CollectionOverlay", overlayParent);
            Stretch((RectTransform)root.transform);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject dimGo = NewUI("Dimmer", root.transform);
            Image dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;
            Stretch((RectTransform)dimGo.transform);

            GameObject panelGo = NewUI("Panel", root.transform);
            Image panel = panelGo.AddComponent<Image>();
            panel.sprite = LoadSprite(PanelPath);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            SetAnchor((RectTransform)panelGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(900f, 1120f), Vector2.zero);

            RectTransform panelRect = (RectTransform)panelGo.transform;

            TextMeshProUGUI titleText = AddCardTextStretched(panelRect, "TitleText", 46f,
                TextAlignmentOptions.Top, TextBright, 44f, 44f, -40f, 60f);

            TextMeshProUGUI bonusText = AddCardTextStretched(panelRect, "BonusText", 28f,
                TextAlignmentOptions.Top, new Color(0.35f, 0.85f, 0.50f, 1f), 44f, 44f, -104f, 44f);

            // Kaydirilabilir izgara
            GameObject scrollGo = NewUI("ArchiveScroll", panelRect);
            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(40f, 150f);
            scrollRect.offsetMax = new Vector2(-40f, -160f);

            GameObject viewportGo = NewUI("Viewport", scrollRect);
            RectTransform viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport);
            viewportGo.AddComponent<RectMask2D>();

            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;

            GameObject contentGo = NewUI("Grid", viewport);
            RectTransform content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(112f, 112f);
            grid.spacing = new Vector2(16f, 16f);
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperCenter;

            var gridFitter = contentGo.AddComponent<ContentSizeFitter>();
            gridFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.viewport = viewport;
            scroll.content = content;

            // Kapat
            GameObject closeGo = NewUI("CloseButton", panelRect);
            Image closeImage = closeGo.AddComponent<Image>();
            closeImage.sprite = LoadSprite(IconClosePath);
            closeImage.raycastTarget = true;
            closeImage.preserveAspect = true;

            RectTransform closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(64f, 64f);
            closeRect.anchoredPosition = new Vector2(-28f, -28f);

            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.transition = Selectable.Transition.None;

            FileCollectionPanelView view = root.AddComponent<FileCollectionPanelView>();
            view.Bind(titleText, bonusText, content);

            var toggle = root.AddComponent<OverlayToggleView>();
            toggle.Bind(group, openButton, closeButton);
        }

        /// <summary>
        /// Kupa paneli: gunluk gorevler + basarimlar.
        ///
        /// Simge <c>Resources</c>'tan geliyor, <c>Assets/Sprites</c>'tan degil:
        /// ui_icon_trophy oraya konmustu ve calisma zamaninda yuklenen diger
        /// yeni varliklarla ayni yerde durmasi, ikisini ayri yollardan
        /// yonetmekten daha az sasirtici.
        /// </summary>
        static void CreateTrophyPanel(Transform overlayParent, Transform hudParent)
        {
            Sprite trophySprite = Resources.Load<Sprite>("ui_icon_trophy");

            // --- acma dugmesi: reklam dugmesinin soluna, ayni 90 px izgara ---
            GameObject openGo = NewUI("TrophyButton", hudParent);
            Image openImage = openGo.AddComponent<Image>();
            openImage.sprite = trophySprite;
            openImage.raycastTarget = true;
            openImage.preserveAspect = true;

            RectTransform openRect = (RectTransform)openGo.transform;
            openRect.anchorMin = new Vector2(1f, 1f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.sizeDelta = new Vector2(72f, 72f);
            openRect.anchoredPosition = new Vector2(-304f, -30f);

            Button openButton = openGo.AddComponent<Button>();
            openButton.targetGraphic = openImage;
            openButton.transition = Selectable.Transition.None;

            // Alinmayi bekleyen gorev odulu varsa yanan nokta.
            GameObject dotGo = NewUI("ClaimDot", openRect);
            Image dot = dotGo.AddComponent<Image>();
            dot.color = new Color(1f, 0.30f, 0.25f, 1f);
            dot.raycastTarget = false;

            RectTransform dotRect = (RectTransform)dotGo.transform;
            dotRect.anchorMin = new Vector2(1f, 1f);
            dotRect.anchorMax = new Vector2(1f, 1f);
            dotRect.pivot = new Vector2(1f, 1f);
            dotRect.sizeDelta = new Vector2(20f, 20f);
            dotRect.anchoredPosition = new Vector2(-2f, -2f);

            // --- overlay ---
            GameObject root = NewUI("TrophyOverlay", overlayParent);
            Stretch((RectTransform)root.transform);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject dimGo = NewUI("Dimmer", root.transform);
            Image dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;
            Stretch((RectTransform)dimGo.transform);

            GameObject panelGo = NewUI("Panel", root.transform);
            Image panel = panelGo.AddComponent<Image>();
            panel.sprite = LoadSprite(PanelPath);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            SetAnchor((RectTransform)panelGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(920f, 1400f), Vector2.zero);

            RectTransform panelRect = (RectTransform)panelGo.transform;

            TextMeshProUGUI titleText = AddCardTextStretched(panelRect, "TitleText", 44f,
                TextAlignmentOptions.Top, TextBright, 44f, 44f, -36f, 58f);

            TextMeshProUGUI summaryText = AddCardTextStretched(panelRect, "SummaryText", 26f,
                TextAlignmentOptions.Top, TextDim, 44f, 44f, -96f, 40f);

            // --- gunluk gorevler bloğu (ust) ---
            TextMeshProUGUI questHeader = AddCardTextStretched(panelRect, "QuestHeader", 28f,
                TextAlignmentOptions.TopLeft, TextAccent, 44f, 44f, -150f, 40f);

            GameObject questRootGo = NewUI("QuestList", panelRect);
            RectTransform questRoot = (RectTransform)questRootGo.transform;
            questRoot.anchorMin = new Vector2(0f, 1f);
            questRoot.anchorMax = new Vector2(1f, 1f);
            questRoot.pivot = new Vector2(0.5f, 1f);
            questRoot.offsetMin = new Vector2(36f, 0f);
            questRoot.offsetMax = new Vector2(-36f, 0f);
            questRoot.sizeDelta = new Vector2(questRoot.sizeDelta.x, 320f);
            questRoot.anchoredPosition = new Vector2(0f, -196f);

            var questLayout = questRootGo.AddComponent<VerticalLayoutGroup>();
            questLayout.spacing = 10f;
            questLayout.childControlWidth = true;
            questLayout.childControlHeight = false;
            questLayout.childForceExpandWidth = true;
            questLayout.childForceExpandHeight = false;
            questLayout.childAlignment = TextAnchor.UpperCenter;

            // --- basarim listesi (alt, kaydirilabilir) ---
            TextMeshProUGUI achHeader = AddCardTextStretched(panelRect, "AchHeader", 28f,
                TextAlignmentOptions.TopLeft, TextAccent, 44f, 44f, -530f, 40f);

            GameObject scrollGo = NewUI("AchScroll", panelRect);
            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(36f, 40f);
            scrollRect.offsetMax = new Vector2(-36f, -580f);

            GameObject viewportGo = NewUI("Viewport", scrollRect);
            RectTransform viewport = (RectTransform)viewportGo.transform;
            Stretch(viewport);
            viewportGo.AddComponent<RectMask2D>();

            Image viewportImage = viewportGo.AddComponent<Image>();
            viewportImage.color = new Color(1f, 1f, 1f, 0f);
            viewportImage.raycastTarget = true;

            GameObject contentGo = NewUI("Content", viewport);
            RectTransform content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.anchoredPosition = Vector2.zero;

            var achLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            achLayout.spacing = 8f;
            achLayout.padding = new RectOffset(0, 0, 4, 4);
            achLayout.childControlWidth = true;
            achLayout.childControlHeight = false;
            achLayout.childForceExpandWidth = true;
            achLayout.childForceExpandHeight = false;
            achLayout.childAlignment = TextAnchor.UpperCenter;

            var achFitter = contentGo.AddComponent<ContentSizeFitter>();
            achFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            achFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.viewport = viewport;
            scroll.content = content;

            // --- kapat ---
            GameObject closeGo = NewUI("CloseButton", panelRect);
            Image closeImage = closeGo.AddComponent<Image>();
            closeImage.sprite = LoadSprite(IconClosePath);
            closeImage.raycastTarget = true;
            closeImage.preserveAspect = true;

            RectTransform closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(64f, 64f);
            closeRect.anchoredPosition = new Vector2(-28f, -28f);

            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.transition = Selectable.Transition.None;

            TrophyPanelView view = root.AddComponent<TrophyPanelView>();
            view.Bind(titleText, summaryText, questHeader, questRoot, achHeader, content);

            var toggle = root.AddComponent<OverlayToggleView>();
            toggle.Bind(group, openButton, closeButton);

            var badge = openGo.AddComponent<QuestBadgeView>();
            badge.Bind(dot);
        }

        static void CreateSettings(Transform overlayParent, Transform hudParent)
        {
            // Acma butonu — SafeArea'nin sag ust kosesi
            GameObject openGo = NewUI("SettingsButton", hudParent);
            Image openImage = openGo.AddComponent<Image>();
            openImage.sprite = LoadSprite(IconSettingsPath);
            openImage.raycastTarget = true;
            openImage.preserveAspect = true;

            RectTransform openRect = (RectTransform)openGo.transform;
            openRect.anchorMin = new Vector2(1f, 1f);
            openRect.anchorMax = new Vector2(1f, 1f);
            openRect.pivot = new Vector2(1f, 1f);
            openRect.sizeDelta = new Vector2(72f, 72f);
            openRect.anchoredPosition = new Vector2(-34f, -30f);

            Button openButton = openGo.AddComponent<Button>();
            openButton.targetGraphic = openImage;
            openButton.transition = Selectable.Transition.None;

            // Overlay
            GameObject root = NewUI("Settings", overlayParent);
            Stretch((RectTransform)root.transform);

            CanvasGroup group = root.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            GameObject dimGo = NewUI("Dimmer", root.transform);
            Image dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.78f);
            dim.raycastTarget = true;
            Stretch((RectTransform)dimGo.transform);

            GameObject panelGo = NewUI("Panel", root.transform);
            Image panel = panelGo.AddComponent<Image>();
            panel.sprite = LoadSprite(PanelPath);
            panel.type = Image.Type.Sliced;
            panel.raycastTarget = true;
            SetAnchor((RectTransform)panelGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(860f, 780f), Vector2.zero);

            RectTransform panelRect = (RectTransform)panelGo.transform;

            TextMeshProUGUI titleText = AddCardTextStretched(panelRect, "TitleText", 48f,
                TextAlignmentOptions.Top, TextBright, 44f, 44f, -44f, 62f);
            titleText.SetText("AYARLAR");
            titleText.gameObject.AddComponent<LocalizedText>().SetKey("settings_title", "SETTINGS");

            // 1. SES ac/kapa
            GameObject soundGo = NewUI("SoundButton", panelRect);
            Image soundImage = soundGo.AddComponent<Image>();
            soundImage.sprite = LoadSprite(IconSoundOnPath);
            soundImage.raycastTarget = true;
            soundImage.preserveAspect = true;
            SetAnchor((RectTransform)soundGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(110f, 110f), new Vector2(-220f, 150f));

            Button soundButton = soundGo.AddComponent<Button>();
            soundButton.targetGraphic = soundImage;
            soundButton.transition = Selectable.Transition.None;

            TextMeshProUGUI soundLabel = AddCardTextStretched(panelRect, "SoundLabel", 32f,
                TextAlignmentOptions.MidlineLeft, Color.white, 280f, 44f, -200f, 60f);
            soundLabel.SetText("SES");
            soundLabel.gameObject.AddComponent<LocalizedText>().SetKey("settings_audio", "SOUND");

            // 2. TİTREŞİM (Haptics)
            GameObject hapticsGo = NewUI("HapticsButton", panelRect);
            Image hapticsImage = hapticsGo.AddComponent<Image>();
            hapticsImage.sprite = LoadSprite(IconHapticOnPath);
            hapticsImage.raycastTarget = true;
            hapticsImage.preserveAspect = true;
            SetAnchor((RectTransform)hapticsGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(110f, 110f), new Vector2(-220f, 10f));

            Button hapticsButton = hapticsGo.AddComponent<Button>();
            hapticsButton.targetGraphic = hapticsImage;
            hapticsButton.transition = Selectable.Transition.None;

            TextMeshProUGUI hapticsText = AddCardTextStretched(panelRect, "HapticsLabel", 32f,
                TextAlignmentOptions.MidlineLeft, Color.white, 280f, 44f, -340f, 60f);
            hapticsText.SetText("TİTREŞİM: AÇIK");

            // 3. DİL (Language)
            GameObject langGo = NewUI("LanguageButton", panelRect);
            Image langImage = langGo.AddComponent<Image>();
            langImage.sprite = LoadSprite(ButtonNormalPath);
            langImage.type = Image.Type.Sliced;
            langImage.raycastTarget = true;
            SetAnchor((RectTransform)langGo.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(740f, 100f), new Vector2(0f, -140f));

            Button langButton = langGo.AddComponent<Button>();
            langButton.targetGraphic = langImage;
            langButton.transition = Selectable.Transition.None;

            TextMeshProUGUI langText = AddText((RectTransform)langGo.transform, "Label", 32f,
                TextAlignmentOptions.Center, Color.white);
            langText.SetText("DİL: TÜRKÇE");

            // Kapat
            GameObject closeGo = NewUI("CloseButton", panelRect);
            Image closeImage = closeGo.AddComponent<Image>();
            closeImage.sprite = LoadSprite(IconClosePath);
            closeImage.raycastTarget = true;
            closeImage.preserveAspect = true;

            RectTransform closeRect = (RectTransform)closeGo.transform;
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.sizeDelta = new Vector2(72f, 72f);
            closeRect.anchoredPosition = new Vector2(-26f, -26f);

            Button closeButton = closeGo.AddComponent<Button>();
            closeButton.targetGraphic = closeImage;
            closeButton.transition = Selectable.Transition.None;

            SettingsView view = root.AddComponent<SettingsView>();
            view.Bind(group, openButton, closeButton, soundButton, soundImage,
                      LoadSprite(IconSoundOnPath), LoadSprite(IconSoundOffPath),
                      titleText, soundLabel,
                      langButton, langText, hapticsButton, hapticsImage,
                      LoadSprite(IconHapticOnPath), LoadSprite(IconHapticOffPath), hapticsText);
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            if (actions == null)
            {
                _missing.Add(ActionsPath);
                return;
            }

            // actionsAsset setter'i UI haritasindaki Point/Click/... aksiyonlarini
            // isimle bulup baglar.
            go.GetComponent<InputSystemUIInputModule>().actionsAsset = actions;
        }

        // ------------------------------------------------------------------
        // Prefab + referans baglama
        // ------------------------------------------------------------------

        static void SavePrefab(GameObject dialRoot)
        {
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();

            PrefabUtility.SaveAsPrefabAssetAndConnect(
                dialRoot, PrefabPath, InteractionMode.AutomatedAction);
        }

        static void BindSceneReferences(GameObject dialInstance, SpeedController speed,
                                        ClickCatcher catcher, DebugHud hud)
        {
            if (catcher != null) catcher.Bind(speed);

            if (hud != null)
            {
                var label = hud.GetComponentInChildren<TextMeshProUGUI>(true);
                hud.Bind(label, speed, hud.GetComponent<CanvasGroup>());
            }

            if (dialInstance == null)
            {
                _missing.Add("DialRoot sahne ornegi bulunamadi");
                return;
            }

            DialView view = dialInstance.GetComponent<DialView>();
            if (view == null)
            {
                _missing.Add("DialRoot uzerinde DialView yok");
                return;
            }

            view.BindSpeed(speed);
            // Prefab ornegindeki degisiklik override olarak kaydedilsin.
            PrefabUtility.RecordPrefabInstancePropertyModifications(view);

            HeatGaugeView gauge = dialInstance.GetComponent<HeatGaugeView>();
            if (gauge == null)
            {
                _missing.Add("DialRoot uzerinde HeatGaugeView yok");
                return;
            }

            gauge.BindSpeed(speed);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gauge);
        }

        static void RegisterInBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    scenes[i].enabled = true;
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        static GameObject NewUI(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>
        /// Bir alt agaci KENDI Canvas'ina alir.
        ///
        /// Butun arayuz tek bir Canvas'taydi. uGUI'da bir Canvas'in icindeki tek
        /// bir grafik degisince O CANVAS'IN TAMAMI yeniden orulur. Pratikte:
        /// ilerleme cubugu her karede doluyor, ust HUD saniyede 12 kez metin
        /// yaziyor, ibre donuyor ve her dosya bitiminde tum yukseltme kartlari
        /// tazeleniyordu — yani ekranda ne varsa saniyede onlarca kez bastan
        /// insa ediliyordu. Telefonun isinmasinin ikinci buyuk sebebi buydu.
        ///
        /// Alt Canvas'lar bu zinciri kesiyor: kadran donerken kartlar, kartlar
        /// tazelenirken kadran yeniden orulmuyor.
        ///
        /// <paramref name="raycaster"/> yalnizca dokunulabilir ogeler iceren
        /// alt agaclarda gerekli: grafikler en yakin Canvas'a kaydoldugu icin
        /// kok GraphicRaycaster onlari artik gormez.
        /// </summary>
        static void MakeSubCanvas(GameObject go, bool raycaster)
        {
            if (go == null) return;

            go.AddComponent<Canvas>();
            if (raycaster) go.AddComponent<GraphicRaycaster>();
        }

        static Image AddLayer(RectTransform parent, string name, Sprite sprite, float size)
        {
            GameObject go = NewUI(name, parent);
            Image img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.raycastTarget = false; // tiklamalar ClickCatcher'a gecsin

            SetAnchor((RectTransform)go.transform, new Vector2(0.5f, 0.5f),
                      new Vector2(size, size), Vector2.zero);
            return img;
        }

        static void SetAnchor(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Sprite LoadSprite(string path)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) _missing.Add(path);
            return s;
        }

        static Sprite BuiltinKnob()
        {
            // Proje kurali: PNG uretilmez. Kadran arkasi icin Unity'nin dahili
            // Knob sprite'i kullanilir (yeni gorsel dosyasi olusturmadan).
            Sprite s = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            if (s == null) _missing.Add("builtin: UI/Skin/Knob.psd");
            return s;
        }

        /// <summary>
        /// Kaydedilen sahneyi diskten okumadan, canli nesneler uzerinden kritik
        /// referanslari dogrular. Sessizce kopuk kalan bir referans Play mode'da
        /// ancak NullReference olarak ortaya cikardi.
        /// </summary>
        static void VerifyBindings()
        {
            var manager = Object.FindAnyObjectByType<GameManager>();
            if (manager == null) _missing.Add("sahnede GameManager yok");
            else if (manager.Database == null) _missing.Add("GameManager.database baglanmadi");

            if (Object.FindAnyObjectByType<SpeedController>() == null)
                _missing.Add("sahnede SpeedController yok");

            if (Object.FindAnyObjectByType<Wallet>() == null)
                _missing.Add("sahnede Wallet yok");

            if (Object.FindAnyObjectByType<DownloadController>() == null)
                _missing.Add("sahnede DownloadController yok");

            var top = Object.FindAnyObjectByType<TopHudView>();
            if (top == null)
            {
                _missing.Add("sahnede TopHudView yok");
            }
            else
            {
                var tso = new SerializedObject(top);
                CheckRef(tso, "balanceText", "TopHudView.balanceText");
                CheckRef(tso, "speedText", "TopHudView.speedText");
                CheckRef(tso, "tierIcon", "TopHudView.tierIcon");
                CheckRef(tso, "tierNameText", "TopHudView.tierNameText");
                CheckRef(tso, "tierRangeText", "TopHudView.tierRangeText");
                CheckRef(tso, "fileIcon", "TopHudView.fileIcon");
                CheckRef(tso, "fileNameText", "TopHudView.fileNameText");
                CheckRef(tso, "fileSizeText", "TopHudView.fileSizeText");
                CheckRef(tso, "progressBar", "TopHudView.progressBar");
                CheckRef(tso, "percentText", "TopHudView.percentText");
                CheckRef(tso, "etaText", "TopHudView.etaText");
                CheckRef(tso, "download", "TopHudView.download");
                CheckRef(tso, "wallet", "TopHudView.wallet");
                CheckRef(tso, "speedController", "TopHudView.speedController");
            }

            if (Object.FindAnyObjectByType<EconomyManager>() == null)
                _missing.Add("sahnede EconomyManager yok");

            if (Object.FindAnyObjectByType<TierManager>() == null)
                _missing.Add("sahnede TierManager yok");

            if (Object.FindAnyObjectByType<SaveManager>() == null)
                _missing.Add("sahnede SaveManager yok");

            if (Object.FindAnyObjectByType<PrestigeManager>() == null)
                _missing.Add("sahnede PrestigeManager yok");

            if (Object.FindAnyObjectByType<GameEventManager>() == null)
                _missing.Add("sahnede GameEventManager yok");

            var fx = Object.FindAnyObjectByType<FXManager>(FindObjectsInactive.Include);
            if (fx == null)
            {
                _missing.Add("sahnede FXManager yok");
            }
            else
            {
                var fso = new SerializedObject(fx);
                CheckRef(fso, "layer", "FXManager.layer");

                SerializedProperty defs = fso.FindProperty("definitions");
                if (defs == null || !defs.isArray || defs.arraySize != 7)
                {
                    _missing.Add("FXManager.definitions 7 olmali (" +
                                 (defs != null ? defs.arraySize.ToString() : "yok") + ")");
                }
                else
                {
                    for (int i = 0; i < defs.arraySize; i++)
                    {
                        SerializedProperty sprite = defs.GetArrayElementAtIndex(i)
                            .FindPropertyRelative("sprite");
                        if (sprite == null || sprite.objectReferenceValue == null)
                            _missing.Add("FXManager.definitions[" + i + "].sprite baglanmadi");
                    }
                }
            }

            if (Object.FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede AudioManager yok");

            var ads = Object.FindAnyObjectByType<StubAdService>(FindObjectsInactive.Include);
            if (ads == null)
            {
                _missing.Add("sahnede StubAdService yok");
            }
            else
            {
                var aso = new SerializedObject(ads);
                CheckRef(aso, "group", "StubAd.group");
                CheckRef(aso, "countdownText", "StubAd.countdownText");
                CheckRef(aso, "captionText", "StubAd.captionText");
                CheckRef(aso, "closeButton", "StubAd.closeButton");
                CheckRef(aso, "closeIcon", "StubAd.closeIcon");
                CheckRef(aso, "progressFill", "StubAd.progressFill");
                CheckRef(aso, "adIcon", "StubAd.adIcon");
            }

            // --- basarimlar / gunluk gorevler ---
            //
            // Uc bilesen de sahnede OLMAK ZORUNDA: SaveManager onlari
            // GameManager uzerinden ariyor ve bulamazsa kayit sessizce
            // eksik yuklenir (sayaclar sifirlanir, gorevler hic kurulmaz).
            if (Object.FindAnyObjectByType<PlayerStats>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede PlayerStats yok");

            if (Object.FindAnyObjectByType<AchievementManager>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede AchievementManager yok");

            if (Object.FindAnyObjectByType<DailyQuestManager>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede DailyQuestManager yok");

            var trophy = Object.FindAnyObjectByType<TrophyPanelView>(FindObjectsInactive.Include);
            if (trophy == null)
            {
                _missing.Add("sahnede TrophyPanelView yok");
            }
            else
            {
                var tso = new SerializedObject(trophy);
                CheckRef(tso, "titleText", "Trophy.titleText");
                CheckRef(tso, "summaryText", "Trophy.summaryText");
                CheckRef(tso, "questContainer", "Trophy.questContainer");
                CheckRef(tso, "achievementContainer", "Trophy.achievementContainer");
            }

            var mascot = Object.FindAnyObjectByType<MascotController>(FindObjectsInactive.Include);
            if (mascot == null)
            {
                _missing.Add("sahnede MascotController yok");
            }
            else
            {
                var mso = new SerializedObject(mascot);
                CheckRef(mso, "image", "Mascot.image");
                CheckRef(mso, "idleSprite", "Mascot.idleSprite");
                CheckRef(mso, "happySprite", "Mascot.happySprite");
                CheckRef(mso, "overheatedSprite", "Mascot.overheatedSprite");
            }

            var settings = Object.FindAnyObjectByType<SettingsView>(FindObjectsInactive.Include);
            if (settings == null)
            {
                _missing.Add("sahnede SettingsView yok");
            }
            else
            {
                var sso = new SerializedObject(settings);
                CheckRef(sso, "group", "Settings.group");
                CheckRef(sso, "openButton", "Settings.openButton");
                CheckRef(sso, "closeButton", "Settings.closeButton");
                CheckRef(sso, "soundButton", "Settings.soundButton");
                CheckRef(sso, "soundIcon", "Settings.soundIcon");
                CheckRef(sso, "soundOnSprite", "Settings.soundOnSprite");
                CheckRef(sso, "soundOffSprite", "Settings.soundOffSprite");
            }

            var banner = Object.FindAnyObjectByType<EventBannerView>(FindObjectsInactive.Include);
            if (banner == null)
            {
                _missing.Add("sahnede EventBannerView yok");
            }
            else
            {
                var bso = new SerializedObject(banner);
                CheckRef(bso, "slider", "EventBanner.slider");
                CheckRef(bso, "group", "EventBanner.group");
                CheckRef(bso, "background", "EventBanner.background");
                CheckRef(bso, "icon", "EventBanner.icon");
                CheckRef(bso, "titleText", "EventBanner.titleText");
                CheckRef(bso, "bodyText", "EventBanner.bodyText");
                CheckRef(bso, "actionButton", "EventBanner.actionButton");
                CheckRef(bso, "actionLabel", "EventBanner.actionLabel");
                CheckRef(bso, "adButton", "EventBanner.adButton");
                CheckRef(bso, "happyHourBackground", "EventBanner.happyHourBackground");
                CheckRef(bso, "wifiIcon", "EventBanner.wifiIcon");
                CheckRef(bso, "passwordIcon", "EventBanner.passwordIcon");
                CheckRef(bso, "quotaIcon", "EventBanner.quotaIcon");
            }

            var prestigePanel = Object.FindAnyObjectByType<PrestigePanelView>(FindObjectsInactive.Include);
            if (prestigePanel == null)
            {
                _missing.Add("sahnede PrestigePanelView yok");
            }
            else
            {
                var pso = new SerializedObject(prestigePanel);
                CheckRef(pso, "creditsText", "PrestigePanel.creditsText");
                CheckRef(pso, "multiplierText", "PrestigePanel.multiplierText");
                CheckRef(pso, "pendingText", "PrestigePanel.pendingText");
                CheckRef(pso, "explainText", "PrestigePanel.explainText");
                CheckRef(pso, "prestigeButton", "PrestigePanel.prestigeButton");
                CheckRef(pso, "buttonLabel", "PrestigePanel.buttonLabel");
            }

            var offline = Object.FindAnyObjectByType<OfflineReportView>(FindObjectsInactive.Include);
            if (offline == null)
            {
                _missing.Add("sahnede OfflineReportView yok");
            }
            else
            {
                var oso = new SerializedObject(offline);
                CheckRef(oso, "group", "OfflineReportView.group");
                CheckRef(oso, "titleText", "OfflineReportView.titleText");
                CheckRef(oso, "bodyText", "OfflineReportView.bodyText");
                CheckRef(oso, "amountText", "OfflineReportView.amountText");
                CheckRef(oso, "closeButton", "OfflineReportView.closeButton");
            }

            var listView = Object.FindAnyObjectByType<UpgradeListView>();
            if (listView == null)
            {
                _missing.Add("sahnede UpgradeListView yok");
            }
            else
            {
                var lso = new SerializedObject(listView);
                CheckRef(lso, "content", "UpgradeListView.content");
                CheckRef(lso, "cardPrefab", "UpgradeListView.cardPrefab");
            }

            var tabs = Object.FindAnyObjectByType<TabBarView>();
            if (tabs == null)
            {
                _missing.Add("sahnede TabBarView yok");
            }
            else
            {
                var tabso = new SerializedObject(tabs);
                CheckRef(tabso, "list", "TabBarView.list");
                CheckRef(tabso, "listRoot", "TabBarView.listRoot");
                CheckRef(tabso, "prestigeRoot", "TabBarView.prestigeRoot");
                CheckArray(tabso, "buttons", 4, "TabBarView.buttons");
                CheckArray(tabso, "icons", 4, "TabBarView.icons");
            }

            var cardAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefabPath);
            if (cardAsset == null)
            {
                _missing.Add(CardPrefabPath + " uretilmedi");
            }
            else
            {
                var cardView = cardAsset.GetComponent<UpgradeCardView>();
                if (cardView == null)
                {
                    _missing.Add("UpgradeCard prefab'inda UpgradeCardView yok");
                }
                else
                {
                    var cso = new SerializedObject(cardView);
                    CheckRef(cso, "button", "UpgradeCard.button");
                    CheckRef(cso, "background", "UpgradeCard.background");
                    CheckRef(cso, "icon", "UpgradeCard.icon");
                    CheckRef(cso, "lockIcon", "UpgradeCard.lockIcon");
                    CheckRef(cso, "nameText", "UpgradeCard.nameText");
                    CheckRef(cso, "descText", "UpgradeCard.descText");
                    CheckRef(cso, "levelText", "UpgradeCard.levelText");
                    CheckRef(cso, "costText", "UpgradeCard.costText");
                }
            }

            var barView = Object.FindAnyObjectByType<ProgressBarView>();
            if (barView == null)
            {
                _missing.Add("sahnede ProgressBarView yok");
            }
            else
            {
                var bso = new SerializedObject(barView);
                CheckRef(bso, "frame", "ProgressBarView.frame");
                CheckRef(bso, "fill", "ProgressBarView.fill");
            }

            var dial = Object.FindAnyObjectByType<DialView>();
            if (dial == null)
            {
                _missing.Add("sahnede DialView yok");
            }
            else
            {
                // Okuma icin SerializedObject guvenli; private alanlar da gorunur.
                var so = new SerializedObject(dial);
                CheckRef(so, "background", "DialView.background");
                CheckRef(so, "redlineOverlay", "DialView.redlineOverlay");
                CheckRef(so, "needlePivot", "DialView.needlePivot");
                CheckRef(so, "needle", "DialView.needle");
                CheckRef(so, "needleMaterialSource", "DialView.needleMaterialSource");
                CheckRef(so, "speedController", "DialView.speedController");
            }

            if (Object.FindAnyObjectByType<BulkBuyToggleView>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede BulkBuyToggleView yok");

            if (Object.FindAnyObjectByType<OnboardingHintView>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede OnboardingHintView yok");

            if (Object.FindAnyObjectByType<CollectionManager>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede CollectionManager yok");

            if (Object.FindAnyObjectByType<FileCollectionPanelView>(FindObjectsInactive.Include) == null)
                _missing.Add("sahnede FileCollectionPanelView yok");

            var heat = Object.FindAnyObjectByType<HeatGaugeView>();
            if (heat == null)
            {
                _missing.Add("sahnede HeatGaugeView yok");
            }
            else
            {
                var so = new SerializedObject(heat);
                CheckRef(so, "track", "HeatGaugeView.track");
                CheckRef(so, "fill", "HeatGaugeView.fill");
                CheckRef(so, "speedController", "HeatGaugeView.speedController");
            }

            if (Object.FindAnyObjectByType<ClickCatcher>() == null)
                _missing.Add("sahnede ClickCatcher yok");

            if (Object.FindAnyObjectByType<EventSystem>() == null)
                _missing.Add("sahnede EventSystem yok");
        }

        static void CheckRef(SerializedObject so, string field, string label)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null) _missing.Add(label + " alani bulunamadi");
            else if (p.objectReferenceValue == null) _missing.Add(label + " baglanmadi");
        }

        static void CheckArray(SerializedObject so, string field, int expected, string label)
        {
            SerializedProperty p = so.FindProperty(field);
            if (p == null || !p.isArray) { _missing.Add(label + " dizisi bulunamadi"); return; }

            if (p.arraySize != expected)
            {
                _missing.Add(label + " " + p.arraySize + " eleman (beklenen " + expected + ")");
                return;
            }

            for (int i = 0; i < p.arraySize; i++)
            {
                if (p.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    _missing.Add(label + "[" + i + "] baglanmadi");
            }
        }

        static void Report()
        {
            string msg = "[SceneBuilder] Sahne kuruldu.\n" +
                         "  sahne : " + ScenePath + "\n" +
                         "  prefab: " + PrefabPath + "\n" +
                         "  canvas: " + RefWidth + "x" + RefHeight + " (ScaleWithScreenSize, match 0.5)\n" +
                         "  kadran: " + DialSize + " px, y=+" + DialAnchorY + "\n" +
                         "  ibre  : " + NeedleSize.x + "x" + NeedleSize.y +
                         " pivot(" + NeedlePivot.x + ", " + NeedlePivot.y + ")\n";

            if (_missing.Count == 0)
            {
                Debug.Log(msg + "  eksik gorsel yok.");
                return;
            }

            msg += "  EKSIK (" + _missing.Count + "):\n";
            for (int i = 0; i < _missing.Count; i++) msg += "    - " + _missing[i] + "\n";
            Debug.LogWarning(msg);
        }
    }
}
