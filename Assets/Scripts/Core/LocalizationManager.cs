using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpeedDownload.Core
{
    public enum Language
    {
        English = 0,
        Turkish = 1,
        German = 2,
        Spanish = 3,
        Russian = 4
    }

    /// <summary>
    /// Coklu dil destegi yoneticisi (EN / TR / DE / ES / RU).
    ///
    /// Sozluk her dil icin AYRI bloklar halinde yaziliyordu; bir anahtari
    /// Ingilizceye ekleyip Almancaya eklemeyi unutmak gorunmez bir hataydi ve
    /// oyunda "sadece ilk yukseltmeler ceviriliyor, gerisi Ingilizce kaliyor"
    /// olarak ortaya cikiyordu. Artik her anahtar TEK satirda bes dilin
    /// tamamiyla kaydediliyor (<see cref="Add"/> / <see cref="Upg"/>), yani
    /// eksik bir dil derleme aninda gozle gorulur.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        public static event Action LanguageChanged;

        const string PrefsKey = "SpeedDownload_Language";

        public const int LanguageCount = 5;

        public Language CurrentLanguage { get; private set; }

        readonly Dictionary<string, string> _en = new Dictionary<string, string>();
        readonly Dictionary<string, string> _tr = new Dictionary<string, string>();
        readonly Dictionary<string, string> _de = new Dictionary<string, string>();
        readonly Dictionary<string, string> _es = new Dictionary<string, string>();
        readonly Dictionary<string, string> _ru = new Dictionary<string, string>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeDictionary();
            LoadLanguage();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void LoadLanguage()
        {
            if (PlayerPrefs.HasKey(PrefsKey))
            {
                CurrentLanguage = (Language)PlayerPrefs.GetInt(PrefsKey, (int)Language.English);
                return;
            }

            // Cihaz dilini kontrol et (varsayilan Ingilizce)
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Turkish: CurrentLanguage = Language.Turkish; break;
                case SystemLanguage.German: CurrentLanguage = Language.German; break;
                case SystemLanguage.Spanish: CurrentLanguage = Language.Spanish; break;
                case SystemLanguage.Russian: CurrentLanguage = Language.Russian; break;
                default: CurrentLanguage = Language.English; break;
            }
        }

        public void SetLanguage(Language lang)
        {
            if (CurrentLanguage == lang) return;

            CurrentLanguage = lang;
            PlayerPrefs.SetInt(PrefsKey, (int)lang);
            PlayerPrefs.Save();

            if (LanguageChanged != null) LanguageChanged();
        }

        /// <summary>Dilin kendi adiyla gosterimi ("TÜRKÇE", "DEUTSCH" ...).</summary>
        public static string NativeName(Language lang)
        {
            switch (lang)
            {
                case Language.Turkish: return "TÜRKÇE";
                case Language.German: return "DEUTSCH";
                case Language.Spanish: return "ESPAÑOL";
                case Language.Russian: return "РУССКИЙ";
                default: return "ENGLISH";
            }
        }

        Dictionary<string, string> GetDict(Language lang)
        {
            switch (lang)
            {
                case Language.Turkish: return _tr;
                case Language.German: return _de;
                case Language.Spanish: return _es;
                case Language.Russian: return _ru;
                default: return _en;
            }
        }

        /// <summary>
        /// Instance null kontrolunu tekrarlamadan cevirir.
        ///
        /// Onceki kod her cagri yerinde
        /// <c>Instance != null ? Instance.Get(k, f) : f</c> yaziyordu; bu
        /// tekrar yuzunden bircok yerde ceviri hic baglanmamis, metin dogrudan
        /// koda gomulu kalmisti. Tek satirlik erisim o tuzagi kapatiyor.
        /// </summary>
        public static string T(string key, string fallback = "")
        {
            return Instance != null ? Instance.Get(key, fallback) : fallback;
        }

        /// <summary>Bicimli ceviri: <c>T("k", "f")</c> sonucuna string.Format uygular.</summary>
        public static string TF(string key, string fallback, params object[] args)
        {
            string pattern = T(key, fallback);

            // Desen null olabilir: anahtar da yedek metin de bos gecilmis
            // olabilir (or. henuz kurulmamis bir gorev satiri). string.Format
            // bu durumda FormatException DEGIL ArgumentNullException firlatiyor
            // ve asagidaki yakalama onu gormuyordu.
            //
            // Bunun bedeli buyuktu: ceviri cagrisi bir UI tazelemesinin
            // icindeydi, o tazeleme de kayit yuklemesinin tetikledigi bir
            // olaydan geliyordu. Istisna cagri yiginini yukari tirmanip
            // SaveManager.Load'u YARIDA KESIYOR, geri kalan sistemler hic
            // yuklenmiyordu. Ceviri bir metin isidir; hicbir kosulda oyunun
            // durumunu bozmamali.
            if (string.IsNullOrEmpty(pattern)) return fallback ?? "";

            if (args == null || args.Length == 0) return pattern;

            try { return string.Format(pattern, args); }
            catch (FormatException)
            {
                // Ceviride bozuk bir {0} varsa oyun cokmemeli.
                if (string.IsNullOrEmpty(fallback)) return pattern;

                try { return string.Format(fallback, args); }
                catch (FormatException) { return fallback; }
            }
        }

        public string Get(string key, string fallback = "")
        {
            if (string.IsNullOrEmpty(key)) return fallback;

            string val;
            if (GetDict(CurrentLanguage).TryGetValue(key, out val)) return val;
            if (_en.TryGetValue(key, out val)) return val;

            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        public string GetUpgradeName(Data.UpgradeSO u)
        {
            if (u == null) return "";
            if (CurrentLanguage == Language.Turkish) return u.displayName;
            return Get("upg_name_" + UpgradeKey(u), u.displayName);
        }

        public string GetUpgradeDesc(Data.UpgradeSO u)
        {
            if (u == null) return "";
            if (CurrentLanguage == Language.Turkish) return u.description;
            return Get("upg_desc_" + UpgradeKey(u), u.description);
        }

        /// <summary>
        /// Sozluk anahtarindaki varlik adi. Baglanti yukseltmeleri asset adiyla
        /// degil kademe numarasiyla anahtarlaniyor: sonsuz kademelerdekiler
        /// calisma aninda uretiliyor ve sabit bir asset adlari yok.
        /// </summary>
        static string UpgradeKey(Data.UpgradeSO u)
        {
            return (u.type == Data.UpgradeType.Connection && u.connectionTierIndex >= 1)
                ? "Upg_Conn_Tier" + u.connectionTierIndex
                : u.name;
        }

        public string GetTierName(Data.ConnectionTierSO tier)
        {
            if (tier == null) return "";
            if (CurrentLanguage == Language.Turkish) return tier.tierName;
            return Get("tier_name_" + tier.tierIndex, tier.tierName);
        }

        public string GetFileName(Data.FileDataSO file)
        {
            if (file == null) return "";
            return Get("file_name_" + file.name, file.displayName);
        }

        // ------------------------------------------------------------------
        // Kayit yardimcilari
        // ------------------------------------------------------------------

        /// <summary>Bir anahtari bes dilin tamamiyla kaydeder.</summary>
        void Add(string key, string en, string tr, string de, string es, string ru)
        {
            _en[key] = en;
            _tr[key] = tr;
            _de[key] = de;
            _es[key] = es;
            _ru[key] = ru;
        }

        /// <summary>
        /// Bir yukseltmenin adi + aciklamasi. Turkce YOK: o dilde metin
        /// dogrudan UpgradeSO'dan okunuyor, sozlukte ikinci bir kopya tutmak
        /// iki kaynagi zamanla ayrisTirirdi.
        /// </summary>
        void Upg(string asset,
                 string enName, string enDesc,
                 string deName, string deDesc,
                 string esName, string esDesc,
                 string ruName, string ruDesc)
        {
            string nameKey = "upg_name_" + asset;
            string descKey = "upg_desc_" + asset;

            _en[nameKey] = enName; _en[descKey] = enDesc;
            _de[nameKey] = deName; _de[descKey] = deDesc;
            _es[nameKey] = esName; _es[descKey] = esDesc;
            _ru[nameKey] = ruName; _ru[descKey] = ruDesc;
        }

        void InitializeDictionary()
        {
            RegisterInterface();
            RegisterEvents();
            RegisterMascot();
            RegisterTrophies();
            RegisterTiersAndFiles();
            RegisterUpgrades();
            RegisterConnections();
        }

        // ------------------------------------------------------------------
        // Maskot replikleri
        //
        // Bunlar MascotController icinde SABIT TURKCE dizi olarak duruyordu:
        // Almanca oynayan biri de Turkce espri goruyordu. Oyunun geri kalani
        // bes dilde; maskot en cok goze carpan bilesenlerden biri oldugu icin
        // bu tutarsizlik en gorunur yerdeydi.
        //
        // Espriler birebir cevrilmedi — mizah cevrilmez, YERELLESTIRILIR:
        // her dilde o kulturun kendi modem/internet nostaljisine oturuyor.
        // ------------------------------------------------------------------

        /// <summary>Maskotun her replik grubundaki secenek sayisi.</summary>
        public const int MascotTapQuoteCount = 7;
        public const int MascotOverheatQuoteCount = 4;
        public const int MascotPanicQuoteCount = 3;
        public const int MascotLaserQuoteCount = 3;

        void RegisterMascot()
        {
            // --- dokunma ---
            Add("mascot_tap_0", "Smacked the modem — it works!", "Modeme vurunca düzeldi!",
                "Einmal draufhauen, läuft wieder!", "¡Un golpe al módem y listo!",
                "Стукнул по модему — заработало!");

            Add("mascot_tap_1", "Fiber go brrrr!", "Fiber go brrrr!",
                "Glasfaser macht brrrr!", "¡La fibra hace brrrr!", "Оптика гудит: бррр!");

            Add("mascot_tap_2", "We're connected to NASA!", "NASA'ya bağlandık!",
                "Wir sind mit der NASA verbunden!", "¡Conectados con la NASA!",
                "Мы подключились к NASA!");

            Add("mascot_tap_3", "Ping 999ms, I'm crying...", "Ping 999ms ağlicam...",
                "Ping 999 ms, ich weine...", "Ping 999 ms, voy a llorar...",
                "Пинг 999 мс, я сейчас заплачу...");

            Add("mascot_tap_4", "The neighbor started 4K again!", "Komşu yine 4K video açtı!",
                "Der Nachbar streamt wieder in 4K!", "¡El vecino puso 4K otra vez!",
                "Сосед опять включил 4K!");

            Add("mascot_tap_5", "Open Winamp, let's get nostalgic!", "Winamp aç nostalji yapalım!",
                "Winamp an, Zeit für Nostalgie!", "¡Abre Winamp, pura nostalgia!",
                "Включай Winamp, поностальгируем!");

            Add("mascot_tap_6", "We're at light speed!", "Işık hızındayız!",
                "Wir sind lichtschnell!", "¡Vamos a la velocidad de la luz!",
                "Мы на скорости света!");

            // --- asiri isinma ---
            Add("mascot_overheat_0", "THE MODEM IS ON FIRE!", "MODEM ALEV ALDI!",
                "DAS MODEM BRENNT!", "¡EL MÓDEM SE INCENDIÓ!", "МОДЕМ ЗАГОРЕЛСЯ!");

            Add("mascot_overheat_1", "Call the fire brigade!", "İtfaiyeyi arayın!",
                "Ruft die Feuerwehr!", "¡Llamen a los bomberos!", "Вызывайте пожарных!");

            Add("mascot_overheat_2", "The transformer blew!", "Trafo patladı!",
                "Der Trafo ist geplatzt!", "¡Explotó el transformador!", "Трансформатор рванул!");

            Add("mascot_overheat_3", "Cool it down, quick!", "Soğut çabuk!",
                "Schnell abkühlen!", "¡Enfríalo, rápido!", "Охлаждай, быстро!");

            // --- panik (isi tehlikeli) ---
            Add("mascot_panic_0", "Easy, you'll burn it!", "Sakin ol yakacan!",
                "Ruhig, du grillst es!", "¡Calma, lo vas a quemar!", "Полегче, спалишь!");

            Add("mascot_panic_1", "Temperature is off the charts!", "Hararet tavan yaptı!",
                "Die Temperatur sprengt die Skala!", "¡La temperatura se salió de la escala!",
                "Температура зашкаливает!");

            Add("mascot_panic_2", "The fans can't keep up!", "Fanlar yetmiyor!",
                "Die Lüfter schaffen es nicht!", "¡Los ventiladores no dan abasto!",
                "Вентиляторы не справляются!");

            // --- redline / turbo ---
            Add("mascot_laser_0", "TURBO ENGAGED!", "TURBO DEVREDE!",
                "TURBO AKTIV!", "¡TURBO ACTIVADO!", "ТУРБО ВКЛЮЧЁН!");

            Add("mascot_laser_1", "FIBER GO BRRR!", "FİBER GO BRRR!",
                "GLASFASER BRRR!", "¡FIBRA BRRR!", "ОПТИКА БРРР!");

            Add("mascot_laser_2", "MAXIMUM OVERDRIVE!", "AŞIRI HIZ!",
                "MAXIMALE ÜBERLAST!", "¡SOBREMARCHA MÁXIMA!", "МАКСИМАЛЬНЫЙ РАЗГОН!");
        }

        // ------------------------------------------------------------------
        // Kupa paneli: basarimlar + gunluk gorevler
        //
        // Basarim ve gorev metinleri {0} ile HEDEFI aliyor. Sayiyi metne
        // gommemek onemli: denge ayari degistiginde (ornegin "250 dosya" ->
        // "300 dosya") yalnizca tanim degisiyor, bes dildeki metin degil.
        // ------------------------------------------------------------------

        void RegisterTrophies()
        {
            Add("trophy_title", "TROPHIES & QUESTS", "KUPALAR VE GÖREVLER",
                "TROPHÄEN & AUFGABEN", "TROFEOS Y MISIONES", "ТРОФЕИ И ЗАДАНИЯ");

            Add("trophy_summary", "Achievements {0}/{1}", "Başarımlar {0}/{1}",
                "Erfolge {0}/{1}", "Logros {0}/{1}", "Достижения {0}/{1}");

            Add("trophy_holo", "Holo {0}", "Holo {0}", "Holo {0}", "Holo {0}", "Holo {0}");

            Add("quest_header", "DAILY QUESTS", "GÜNLÜK GÖREVLER",
                "TAGESAUFGABEN", "MISIONES DIARIAS", "ЕЖЕДНЕВНЫЕ ЗАДАНИЯ");

            Add("ach_header", "ACHIEVEMENTS", "BAŞARIMLAR",
                "ERFOLGE", "LOGROS", "ДОСТИЖЕНИЯ");

            Add("quest_claimed", "DONE", "ALINDI", "ERLEDIGT", "HECHO", "ГОТОВО");

            Add("unit_credits", "FC", "FK", "FC", "CF", "ФК");

            Add("trophy_badge_new", "!", "!", "!", "!", "!");

            // --- gunluk gorevler ---
            Add("quest_files_15", "Download {0} files", "{0} dosya indir",
                "Lade {0} Dateien herunter", "Descarga {0} archivos", "Скачай {0} файлов");

            Add("quest_files_40", "Download {0} files", "{0} dosya indir",
                "Lade {0} Dateien herunter", "Descarga {0} archivos", "Скачай {0} файлов");

            Add("quest_clicks_150", "Tap {0} times", "{0} kez dokun",
                "Tippe {0} Mal", "Toca {0} veces", "Нажми {0} раз");

            Add("quest_clicks_400", "Tap {0} times", "{0} kez dokun",
                "Tippe {0} Mal", "Toca {0} veces", "Нажми {0} раз");

            Add("quest_upgrades_3", "Buy {0} upgrades", "{0} yükseltme al",
                "Kaufe {0} Upgrades", "Compra {0} mejoras", "Купи {0} улучшения");

            Add("quest_upgrades_8", "Buy {0} upgrades", "{0} yükseltme al",
                "Kaufe {0} Upgrades", "Compra {0} mejoras", "Купи {0} улучшений");

            Add("quest_overheat_2", "Overheat {0} times", "{0} kez aşırı ısın",
                "Überhitze {0} Mal", "Sobrecalienta {0} veces", "Перегрейся {0} раза");

            Add("quest_vent_3", "Use emergency vent {0} times", "{0} kez acil soğutma yap",
                "Nutze {0}× die Notkühlung", "Usa el purgado de emergencia {0} veces",
                "Используй аварийный сброс {0} раза");

            Add("quest_signal_1", "Catch {0} floating signal", "{0} yüzen sinyal yakala",
                "Fange {0} schwebendes Signal", "Atrapa {0} señal flotante",
                "Поймай {0} плавающий сигнал");

            Add("quest_event_2", "Resolve {0} incidents", "{0} olayı çöz",
                "Löse {0} Störungen", "Resuelve {0} incidentes", "Устрани {0} инцидента");

            // --- basarimlar ---
            Add("ach_files_10", "Download {0} files", "{0} dosya indir",
                "Lade {0} Dateien herunter", "Descarga {0} archivos", "Скачай {0} файлов");
            Add("ach_files_250", "Download {0} files", "{0} dosya indir",
                "Lade {0} Dateien herunter", "Descarga {0} archivos", "Скачай {0} файлов");
            Add("ach_files_5000", "Download {0} files", "{0} dosya indir",
                "Lade {0} Dateien herunter", "Descarga {0} archivos", "Скачай {0} файлов");
            Add("ach_files_100000", "Download {0} files", "{0} dosya indir",
                "Lade {0} Dateien herunter", "Descarga {0} archivos", "Скачай {0} файлов");

            Add("ach_clicks_100", "Tap {0} times", "{0} kez dokun",
                "Tippe {0} Mal", "Toca {0} veces", "Нажми {0} раз");
            Add("ach_clicks_2500", "Tap {0} times", "{0} kez dokun",
                "Tippe {0} Mal", "Toca {0} veces", "Нажми {0} раз");
            Add("ach_clicks_50000", "Tap {0} times", "{0} kez dokun",
                "Tippe {0} Mal", "Toca {0} veces", "Нажми {0} раз");
            Add("ach_combo_25", "Reach {0}x rhythm combo", "{0}x ritmik kombo yap",
                "Erreiche {0}x Rhythmus-Kombo", "Alcanza un combo rítmico de {0}x", "Сделай комбо x{0}");

            Add("ach_overheat_1", "Overheat for the first time", "İlk kez aşırı ısın",
                "Überhitze zum ersten Mal", "Sobrecalienta por primera vez",
                "Перегрейся впервые");
            Add("ach_overheat_50", "Overheat {0} times", "{0} kez aşırı ısın",
                "Überhitze {0} Mal", "Sobrecalienta {0} veces", "Перегрейся {0} раз");
            Add("ach_vent_10", "Use emergency vent {0} times", "{0} kez acil soğutma yap",
                "Nutze {0}× die Notkühlung", "Usa el purgado {0} veces",
                "Используй аварийный сброс {0} раз");
            Add("ach_vent_25", "Use emergency vent {0} times", "{0} kez acil soğutma yap",
                "Nutze {0}× die Notkühlung", "Usa el purgado {0} veces",
                "Используй аварийный сброс {0} раз");

            Add("ach_upgrades_25", "Buy {0} upgrades", "{0} yükseltme al",
                "Kaufe {0} Upgrades", "Compra {0} mejoras", "Купи {0} улучшений");
            Add("ach_upgrades_400", "Buy {0} upgrades", "{0} yükseltme al",
                "Kaufe {0} Upgrades", "Compra {0} mejoras", "Купи {0} улучшений");

            Add("ach_prestige_1", "Change your line for the first time",
                "İlk kez hat değişimi yap", "Wechsle zum ersten Mal die Leitung",
                "Cambia de línea por primera vez", "Смени линию впервые");
            Add("ach_prestige_10", "Change your line {0} times", "{0} kez hat değişimi yap",
                "Wechsle {0}× die Leitung", "Cambia de línea {0} veces", "Смени линию {0} раз");

            Add("ach_signal_5", "Catch {0} floating signals", "{0} yüzen sinyal yakala",
                "Fange {0} schwebende Signale", "Atrapa {0} señales flotantes",
                "Поймай {0} плавающих сигналов");

            Add("ach_holo_1", "Find your first Holo file", "İlk Holo dosyanı bul",
                "Finde deine erste Holo-Datei", "Encuentra tu primer archivo Holo",
                "Найди свой первый Holo-файл");
            Add("ach_holo_10", "Find {0} Holo files", "{0} Holo dosya bul",
                "Finde {0} Holo-Dateien", "Encuentra {0} archivos Holo",
                "Найди {0} Holo-файлов");
            Add("ach_holo_25", "Find {0} Holo files", "{0} Holo dosya bul",
                "Finde {0} Holo-Dateien", "Encuentra {0} archivos Holo",
                "Найди {0} Holo-файлов");

            Add("ach_events_20", "Resolve {0} incidents", "{0} olay çöz",
                "Löse {0} Störungen", "Resuelve {0} incidentes", "Устрани {0} инцидентов");

            Add("ach_quests_10", "Complete {0} daily quests", "{0} günlük görev tamamla",
                "Schließe {0} Tagesaufgaben ab", "Completa {0} misiones diarias",
                "Выполни {0} ежедневных заданий");
            Add("ach_quests_100", "Complete {0} daily quests", "{0} günlük görev tamamla",
                "Schließe {0} Tagesaufgaben ab", "Completa {0} misiones diarias",
                "Выполни {0} ежедневных заданий");
        }

        // ------------------------------------------------------------------
        // Arayuz
        // ------------------------------------------------------------------

        void RegisterInterface()
        {
            Add("app_name",
                "SpeedDownload Idle Megabit Tycoon", "SpeedDownload Idle Megabit Tycoon",
                "SpeedDownload Idle Megabit Tycoon", "SpeedDownload Idle Megabit Tycoon",
                "SpeedDownload Idle Megabit Tycoon");

            Add("settings_title", "SETTINGS", "AYARLAR", "EINSTELLUNGEN", "AJUSTES", "НАСТРОЙКИ");
            Add("settings_audio", "SOUND", "SES", "TON", "SONIDO", "ЗВУК");
            Add("settings_haptics", "VIBRATION", "TİTREŞİM", "VIBRATION", "VIBRACIÓN", "ВИБРАЦИЯ");

            // Titresim acildiginda kisa sureligine gorunen hatirlatma. Cihazin
            // sistem genelindeki dokunsal geri bildirimi kapaliysa oyun titresim
            // istese de hicbir sey hissedilmez; oyuncu bunu bilmeden oyunu bozuk
            // sanabiliyor.
            Add("haptics_hint",
                "Feel nothing? Enable haptic feedback in your phone settings.",
                "Hissetmiyor musun? Telefon ayarlarından dokunsal geri bildirimi aç.",
                "Nichts gespürt? Aktiviere die haptische Rückmeldung in den Telefoneinstellungen.",
                "¿No sientes nada? Activa la respuesta háptica en los ajustes del teléfono.",
                "Ничего не чувствуете? Включите виброотдачу в настройках телефона.");
            Add("settings_language", "LANGUAGE", "DİL", "SPRACHE", "IDIOMA", "ЯЗЫК");
            Add("settings_on", "ON", "AÇIK", "AN", "ACTIVADO", "ВКЛ");
            Add("settings_off", "OFF", "KAPALI", "AUS", "DESACTIVADO", "ВЫКЛ");

            Add("eta_left", "left", "kaldı", "verbleibend", "restante", "осталось");

            Add("tab_network", "Network & Lines", "Ağ & Hatlar", "Netzwerk & Leitungen",
                "Red y Líneas", "Сеть и линии");
            Add("tab_upgrades", "Upgrades", "Geliştirmeler", "Upgrades", "Mejoras", "Улучшения");
            Add("tab_prestige", "Format (Prestige)", "Format (Prestij)", "Format (Prestige)",
                "Formato (Prestigio)", "Формат (Престиж)");

            Add("upgrade_max", "MAX", "MAKS", "MAX", "MÁX", "МАКС");
            Add("upgrade_locked", "LOCKED", "KİLİTLİ", "GESPERRT", "BLOQUEADO", "ЗАКРЫТО");
            Add("upgrade_buy", "BUY", "SATIN AL", "KAUFEN", "COMPRAR", "КУПИТЬ");
            Add("upgrade_level", "Lv", "Sv", "St", "Nv", "Ур");
            Add("upgrade_current", "Now", "Şu an", "Jetzt", "Ahora", "Сейчас");
            Add("bulk_max", "MAX", "MAKS", "MAX", "MÁX", "МАКС");

            Add("unit_second", "s", "sn", "s", "s", "с");
            Add("unit_clicks_per_sec", "clicks/s", "tık/sn", "Klicks/s", "clics/s", "кликов/с");

            Add("onboarding_tap", "TAP TO SPEED UP", "HIZLANMAK İÇİN DOKUN",
                "ZUM BESCHLEUNIGEN TIPPEN", "TOCA PARA ACELERAR", "НАЖМИ, ЧТОБЫ УСКОРИТЬ");

            Add("tier_req", "Req. Tier: {0}", "Gerekli Kademe: {0}", "Benötigt Stufe: {0}",
                "Nivel requerido: {0}", "Нужен уровень: {0}");

            // Ust HUD'daki kademe rozeti. Rozet daha once yalnizca bir simgeydi
            // ve simge bos gelince sol ustte anlamsiz kucuk bir kutu kaliyordu.
            Add("tier_badge", "TIER {0}", "KADEME {0}", "STUFE {0}", "NIVEL {0}", "УРОВЕНЬ {0}");

            Add("speed_unit_bps", "bps", "bps", "bps", "bps", "бит/с");
            Add("speed_unit_kbps", "Kbps", "Kbps", "Kbps", "Kbps", "Кбит/с");
            Add("speed_unit_mbps", "Mbps", "Mbps", "Mbps", "Mbps", "Мбит/с");
            Add("speed_unit_gbps", "Gbps", "Gbps", "Gbps", "Gbps", "Гбит/с");
            Add("speed_unit_tbps", "Tbps", "Tbps", "Tbps", "Tbps", "Тбит/с");

            // Kart basligindaki "seviye basina kazanc" etiketi. Bunlar daha once
            // UpgradeCardView icinde TR/EN olarak gomuluydu; Almanca oynayan biri
            // kartlarda Ingilizce goruyordu.
            Add("gain_speed", "(+{0}% Speed)", "(+%{0} Hız)", "(+{0} % Tempo)",
                "(+{0} % Velocidad)", "(+{0}% скорости)");
            // Tiklama gucu artik mutlak bit/sn degil oran; etiket de yuzde.
            Add("gain_click", "(+{0}% Click)", "(+%{0} Tık)", "(+{0} % Klick)",
                "(+{0} % Clic)", "(+{0}% клика)");
            Add("gain_heat", "(+{0}% Heat)", "(+%{0} Isı)", "(+{0} % Hitze)",
                "(+{0} % Calor)", "(+{0}% нагрева)");
            Add("gain_crit", "(+{0}% Crit)", "(+%{0} Kritik)", "(+{0} % Krit)",
                "(+{0} % Crítico)", "(+{0}% крита)");
            Add("gain_reward", "(+{0}% Reward)", "(+%{0} Ödül)", "(+{0} % Belohnung)",
                "(+{0} % Recompensa)", "(+{0}% награды)");

            Add("collection_title", "FILE ARCHIVE", "İNDİRİLENLER ARŞİVİ", "DATEI-ARCHIV",
                "ARCHIVO DE ARCHIVOS", "АРХИВ ФАЙЛОВ");
            Add("collection_bonus_label",
                "Archive: {0}/{1}  ·  +{2}% speed",
                "Arşiv: {0}/{1}  ·  +%{2} hız",
                "Archiv: {0}/{1}  ·  +{2} % Tempo",
                "Archivo: {0}/{1}  ·  +{2} % velocidad",
                "Архив: {0}/{1}  ·  +{2}% скорости");

            RegisterOffline();
            RegisterPrestige();
            RegisterAds();
            RegisterAdPlacements();
#if UNITY_WEBGL && !UNITY_EDITOR
            RegisterWebBonusOverrides();
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        /// <summary>
        /// Tarayicida gercek reklam yok (AdMob devre disi); sahte panel "AD"
        /// dediginde oyuncu reklam bekleyip hicbir sey gormuyor. Ayni akis
        /// burada bekleme karsiligi bir BONUS olarak adlandiriliyor.
        /// </summary>
        void RegisterWebBonusOverrides()
        {
            Add("ad_caption",
                "BONUS  ·  wait until the end for your reward",
                "BONUS  ·  ödül için sonuna kadar bekle",
                "BONUS  ·  bis zum Ende warten für die Belohnung",
                "BONUS  ·  espera hasta el final para tu recompensa",
                "БОНУС  ·  дождись конца ради награды");

            Add("ad_skip_hint",
                "BONUS  ·  close now and you forfeit the reward",
                "BONUS  ·  şimdi kapatırsan ödülü kaybedersin",
                "BONUS  ·  jetzt schließen heißt keine Belohnung",
                "BONUS  ·  si cierras ahora pierdes la recompensa",
                "БОНУС  ·  закроешь сейчас — потеряешь награду");

            Add("offline_double",
                "BONUS  ·  2x",
                "BONUS  ·  2x",
                "BONUS  ·  2x",
                "BONUS  ·  2x",
                "БОНУС  ·  2x");
        }
#endif

        void RegisterOffline()
        {
            Add("offline_report_title", "WELCOME BACK", "TEKRAR HOŞ GELDİN",
                "WILLKOMMEN ZURÜCK", "BIENVENIDO DE NUEVO", "С ВОЗВРАЩЕНИЕМ");

            Add("offline_away", "You were away for {0}.", "{0} yoktun.",
                "Du warst {0} weg.", "Estuviste ausente {0}.", "Тебя не было {0}.");

            Add("offline_capped",
                "Your line downloaded for {0} (cap reached).",
                "Hattın {0} boyunca indirdi (tavan doldu).",
                "Deine Leitung lud {0} lang (Limit erreicht).",
                "Tu línea descargó durante {0} (límite alcanzado).",
                "Твоя линия качала {0} (лимит исчерпан).");

            Add("offline_running",
                "Your line kept downloading in the background.",
                "Hattın arka planda indirmeye devam etti.",
                "Deine Leitung lud im Hintergrund weiter.",
                "Tu línea siguió descargando en segundo plano.",
                "Твоя линия продолжала качать в фоне.");

            // Download Manager alinmadan da offline kazanc VAR (temel verim);
            // yukseltme onu iyilestiriyor. Metinler bu uc kademeyi anlatiyor.
            Add("offline_basic_hint",
                "Basic line: {0}% efficiency, {1}h cap.",
                "Temel hat: %{0} verim, {1} saat tavan.",
                "Basisleitung: {0} % Effizienz, {1} Std. Limit.",
                "Línea básica: {0} % de eficiencia, límite de {1} h.",
                "Базовая линия: {0} % эффективности, лимит {1} ч.");

            Add("offline_dm_hint",
                "Download Manager: {0}% efficiency, {1}h cap.",
                "Download Manager: %{0} verim, {1} saat tavan.",
                "Download-Manager: {0} % Effizienz, {1} Std. Limit.",
                "Gestor de Descargas: {0} % de eficiencia, límite de {1} h.",
                "Менеджер загрузок: {0} % эффективности, лимит {1} ч.");

            Add("offline_pro_hint",
                "Upgrade to Pro: {0}% efficiency, {1}h cap.",
                "Pro'ya yükselt: %{0} verim, {1} saat tavan.",
                "Auf Pro upgraden: {0} % Effizienz, {1} Std. Limit.",
                "Mejora a Pro: {0} % de eficiencia, límite de {1} h.",
                "Улучшить до Pro: {0} % эффективности, лимит {1} ч.");

            Add("offline_pro_active",
                "Pro: {0}% efficiency, {1}h cap.",
                "Pro: %{0} verim, {1} saat tavan.",
                "Pro: {0} % Effizienz, {1} Std. Limit.",
                "Pro: {0} % de eficiencia, límite de {1} h.",
                "Pro: {0} % эффективности, лимит {1} ч.");

            Add("offline_claim", "COLLECT", "TOPLA", "EINSAMMELN", "RECOGER", "ЗАБРАТЬ");

            Add("tierup_title", "NEW ERA UNLOCKED!", "YENİ ÇAĞA GEÇİLDİ!",
                "NEUE ÄRA FREIGESCHALTET!", "¡NUEVA ERA DESBLOQUEADA!", "НОВАЯ ЭПОХА ОТКРЫТА!");

            Add("tierup_continue", "CONTINUE", "DEVAM", "WEITER", "CONTINUAR", "ПРОДОЛЖИТЬ");
        }

        void RegisterPrestige()
        {
            Add("prestige_title", "FORMAT POINTS", "FORMAT PUANI", "FORMAT-PUNKTE",
                "PUNTOS DE FORMATO", "ОЧКИ ФОРМАТА");

            Add("prestige_desc",
                "Line Transfer and Format system unlocks at Tier {0}.",
                "Hat Değişimi ve Format sistemi kademe {0}'de açılır.",
                "Leitungswechsel und Format werden auf Stufe {0} freigeschaltet.",
                "El Cambio de Línea y el Formato se desbloquean en el nivel {0}.",
                "Смена линии и формат открываются на уровне {0}.");

            Add("prestige_button_ready", "FORMAT SYSTEM", "SİSTEMİ FORMATLA",
                "SYSTEM FORMATIEREN", "FORMATEAR SISTEMA", "ФОРМАТИРОВАТЬ СИСТЕМУ");

            Add("prestige_button_not_ready", "NOT YET", "HENÜZ OLMAZ", "NOCH NICHT",
                "AÚN NO", "ПОКА НЕТ");

            Add("prestige_unit", "FP", "FK", "FP", "PF", "ОФ");

            Add("prestige_boost_label", "permanent boost", "kalıcı kazanç",
                "dauerhafter Bonus", "bonus permanente", "постоянный бонус");

            Add("prestige_pending_prefix", "Format now: +", "Şimdi formatla: +",
                "Jetzt formatieren: +", "Formatear ahora: +", "Форматировать сейчас: +");

            Add("prestige_locked_info",
                "Format unlocks at Tier {0}.\nCurrent Tier: {1}.",
                "Format kademe {0}'de açılır.\nMevcut kademe: {1}.",
                "Format wird auf Stufe {0} freigeschaltet.\nAktuelle Stufe: {1}.",
                "El formato se desbloquea en el nivel {0}.\nNivel actual: {1}.",
                "Формат открывается на уровне {0}.\nТекущий уровень: {1}.");

            Add("prestige_threshold_info",
                "Requires at least {0} lifetime earnings.\nCurrent: {1}",
                "En az {0} toplam kazanç gerekir.\nŞu an: {1}",
                "Benötigt mindestens {0} Gesamteinnahmen.\nAktuell: {1}",
                "Requiere al menos {0} de ganancias totales.\nActual: {1}",
                "Нужно минимум {0} общего дохода.\nСейчас: {1}");

            Add("prestige_ready_info",
                "<b>Resets:</b> balance, upgrades, tier.\n<b>Retains:</b> {1} and its permanent bonus.\n\nNew multiplier: {0}",
                "<b>Sıfırlanır:</b> bakiye, yükseltmeler, kademe.\n<b>Korunur:</b> {1} ve kalıcı bonusu.\n\nYeni çarpan: {0}",
                "<b>Zurückgesetzt:</b> Guthaben, Upgrades, Stufe.\n<b>Bleibt:</b> {1} und der dauerhafte Bonus.\n\nNeuer Multiplikator: {0}",
                "<b>Se reinicia:</b> saldo, mejoras, nivel.\n<b>Se conserva:</b> {1} y su bonus permanente.\n\nNuevo multiplicador: {0}",
                "<b>Сбрасывается:</b> баланс, улучшения, уровень.\n<b>Сохраняется:</b> {1} и постоянный бонус.\n\nНовый множитель: {0}");

            Add("prestige_earned", "FP Earned: +{0}", "Kazanılacak FK: +{0}",
                "Verdiente FP: +{0}", "PF ganados: +{0}", "Получено ОФ: +{0}");

            Add("prestige_multiplier", "Permanent Earnings: {0}x", "Kalıcı Kazanç Çarpanı: {0}x",
                "Dauerhafte Einnahmen: {0}x", "Ganancias permanentes: {0}x",
                "Постоянный доход: {0}x");
        }

        /// <summary>Odullu reklam yerlestirmelerinin metinleri (GDD Bolum 11).</summary>
        void RegisterAdPlacements()
        {
            Add("offline_double",
                "WATCH AD  ·  2x",
                "REKLAM İZLE  ·  2x",
                "WERBUNG ANSEHEN  ·  2x",
                "VER ANUNCIO  ·  2x",
                "СМОТРЕТЬ РЕКЛАМУ  ·  2x");

            Add("boost_ad_active",
                "SPEED BOOST ACTIVE",
                "HIZ BOOST'U AKTİF",
                "SPEED-BOOST AKTIV",
                "IMPULSO DE VELOCIDAD ACTIVO",
                "УСКОРЕНИЕ АКТИВНО");
        }

        void RegisterAds()
        {
            Add("ad_caption",
                "AD  ·  watch to the end for your reward",
                "REKLAM  ·  ödül için sonuna kadar izle",
                "WERBUNG  ·  bis zum Ende ansehen für die Belohnung",
                "ANUNCIO  ·  míralo hasta el final para tu recompensa",
                "РЕКЛАМА  ·  досмотри до конца ради награды");

            Add("ad_ready", "REWARD READY", "ÖDÜL HAZIR", "BELOHNUNG BEREIT",
                "RECOMPENSA LISTA", "НАГРАДА ГОТОВА");

            Add("ad_close_hint", "Close to claim your reward.", "Kapatarak ödülünü al.",
                "Schließen, um die Belohnung zu erhalten.", "Cierra para reclamar tu recompensa.",
                "Закрой, чтобы забрать награду.");

            // Atlama penceresi acildiginda: dugme artik CALISIYOR ama odulu
            // goturuyor. Bunu yazmadan kapatan oyuncu odulun neden gelmedigini
            // anlamaz ve hatayi oyunda arar.
            Add("ad_skip_hint",
                "AD  ·  close now and you forfeit the reward",
                "REKLAM  ·  şimdi kapatırsan ödülü kaybedersin",
                "WERBUNG  ·  jetzt schließen heißt keine Belohnung",
                "ANUNCIO  ·  si cierras ahora pierdes la recompensa",
                "РЕКЛАМА  ·  закроешь сейчас — потеряешь награду");

            // Ilk saniyelerde kapatmaya basildiginda. Sessiz kalmak "buton
            // bozuk" izlenimi veriyordu.
            Add("ad_wait_hint",
                "Please wait {0}s before closing.",
                "Kapatmak için {0} sn bekle.",
                "Bitte noch {0}s warten.",
                "Espera {0}s para cerrar.",
                "Подожди {0}с, чтобы закрыть.");

            Add("unit_seconds", "s", "sn", "s", "s", "с");

            Add("collection_master", "100% MASTER", "%100 TAMAMLANDI",
                "100% MEISTER", "100% MAESTRO", "100% МАСТЕР");
        }

        void RegisterEvents()
        {
            Add("event_wifi_title", "NEIGHBOR STOLE WI-FI", "KOMŞU WI-FI ÇALDI",
                "NACHBAR STIEHLT WLAN", "EL VECINO ROBÓ EL WI-FI", "СОСЕД УКРАЛ WI-FI");

            Add("event_wifi_body", "Your speed dropped by {0}.", "Hızın {0} düştü.",
                "Deine Geschwindigkeit sank um {0}.", "Tu velocidad bajó un {0}.",
                "Скорость упала на {0}.");

            Add("event_wifi_action", "CHANGE PASSWORD ({0})", "ŞİFREYİ DEĞİŞTİR ({0})",
                "PASSWORT ÄNDERN ({0})", "CAMBIAR CONTRASEÑA ({0})", "СМЕНИТЬ ПАРОЛЬ ({0})");

            Add("event_quota_title", "DATA CAP EXCEEDED", "KOTA AŞILDI",
                "DATENLIMIT ERREICHT", "LÍMITE DE DATOS SUPERADO", "ЛИМИТ ТРАФИКА ПРЕВЫШЕН");

            Add("event_quota_body", "Speed throttled to base level.", "Hız taban seviyeye çekildi.",
                "Geschwindigkeit auf Basiswert gedrosselt.", "Velocidad reducida al nivel base.",
                "Скорость снижена до базовой.");

            Add("event_quota_action", "LIFT CAP  {0}", "KOTAYI AÇ  {0}",
                "LIMIT AUFHEBEN  {0}", "QUITAR LÍMITE  {0}", "СНЯТЬ ЛИМИТ  {0}");

            Add("event_happy_title", "NIGHT RATE", "GECE TARİFESİ", "NACHTTARIF",
                "TARIFA NOCTURNA", "НОЧНОЙ ТАРИФ");

            Add("event_happy_body", "All earnings {0}  ·  {1}s", "Tüm kazançlar {0}  ·  {1} sn",
                "Alle Einnahmen {0}  ·  {1}s", "Todas las ganancias {0}  ·  {1}s",
                "Весь доход {0}  ·  {1}с");
        }

        // ------------------------------------------------------------------
        // Kademeler ve dosyalar
        // ------------------------------------------------------------------

        void RegisterTiersAndFiles()
        {
            Add("tier_name_0", "Signal Hut", "Sinyal Kulübesi", "Signalhütte",
                "Cabaña de Señal", "Сигнальная будка");
            Add("tier_name_1", "Phone Line", "Telefon Hattı", "Telefonleitung",
                "Línea Telefónica", "Телефонная линия");
            Add("tier_name_2", "Dial-up 56K", "Dial-up 56K", "Dial-up 56K",
                "Dial-up 56K", "Dial-up 56K");
            Add("tier_name_3", "ISDN Dual Line", "ISDN Çift Hat", "ISDN-Doppelleitung",
                "Línea Doble ISDN", "Двойная линия ISDN");
            Add("tier_name_4", "ADSL Era", "ADSL Çağı", "ADSL-Ära",
                "Era ADSL", "Эра ADSL");
            Add("tier_name_5", "VDSL / Early Fiber", "VDSL / Erken Fiber", "VDSL / Frühes Glasfaser",
                "VDSL / Fibra Temprana", "VDSL / ранняя оптика");
            Add("tier_name_6", "Full Fiber", "Tam Fiber", "Voll-Glasfaser",
                "Fibra Óptica Total", "Полная оптика");
            Add("tier_name_7", "Quantum Line", "Kuantum Hat", "Quantenleitung",
                "Línea Cuántica", "Квантовая линия");
            Add("tier_name_8", "Datacenter Gateway", "Veri Merkezi Ağ Geçidi", "Rechenzentrum-Gateway",
                "Pasarela de Centro de Datos", "Шлюз дата-центра");

            // Yalnizca adi Turkce olan dosyalar cevriliyor; ping_test.txt gibi
            // zaten evrensel olanlar UpgradeSO'daki degeriyle kaliyor.
            Add("file_name_File_BosBelge", "blank_document.txt", "bos_belge.txt",
                "leeres_dokument.txt", "documento_en_blanco.txt", "пустой_документ.txt");

            Add("file_name_File_OdevV2", "homework_v2.docx", "odev_v2.docx",
                "hausaufgabe_v2.docx", "tarea_v2.docx", "домашка_v2.docx");

            Add("file_name_File_SiirKoleksiyon", "poetry_collection.txt", "siir_koleksiyonu.txt",
                "gedichtsammlung.txt", "coleccion_poemas.txt", "сборник_стихов.txt");

            Add("file_name_File_TatilFoto", "vacation_photos.jpeg", "tatil_fotograflari.jpeg",
                "urlaubsfotos.jpeg", "fotos_vacaciones.jpeg", "фото_отпуска.jpeg");

            Add("file_name_File_EkranGoruntusu", "screenshot.png", "ekran_goruntusu.png",
                "bildschirmfoto.png", "captura_pantalla.png", "скриншот.png");

            Add("file_name_File_BannerTasarim", "banner_design.jpeg", "banner_tasarim.jpeg",
                "banner_design.jpeg", "diseno_banner.jpeg", "дизайн_баннера.jpeg");
        }

        // ------------------------------------------------------------------
        // Yukseltmeler
        // ------------------------------------------------------------------

        void RegisterUpgrades()
        {
            // --- Altyapi ---
            Upg("Upg_HatBakimi",
                "Line Maintenance", "Reduces line noise. Increases base idle speed (without clicking).",
                "Leitungswartung", "Reduziert das Leitungsrauschen. Erhöht die Grundgeschwindigkeit im Leerlauf (ohne Klicken).",
                "Mantenimiento de Línea", "Reduce el ruido de la línea. Aumenta la velocidad base sin hacer clic.",
                "Обслуживание линии", "Снижает помехи на линии. Повышает базовую скорость без кликов.");

            Upg("Upg_IXP",
                "Local Exchange Point", "Traffic routes without leaving the city. Big base speed boost.",
                "Lokaler Austauschknoten", "Der Datenverkehr verlässt die Stadt nicht mehr. Deutlicher Schub für die Grundgeschwindigkeit.",
                "Punto Neutro Local", "El tráfico se enruta sin salir de la ciudad. Gran aumento de velocidad base.",
                "Локальная точка обмена", "Трафик не покидает город. Заметный прирост базовой скорости.");

            Upg("Upg_OmurgaPeering",
                "Backbone Peering", "Connect straight to the backbone. Huge base speed jump.",
                "Backbone-Peering", "Direkte Anbindung an das Backbone. Riesiger Sprung der Grundgeschwindigkeit.",
                "Peering de Backbone", "Conexión directa a la red troncal. Salto enorme de velocidad base.",
                "Пиринг с магистралью", "Прямое подключение к магистрали. Огромный скачок базовой скорости.");

            Upg("Upg_IkinciHat",
                "Second Line", "Opens another download slot. More lines means more simultaneous downloads.",
                "Zweite Leitung", "Öffnet einen weiteren Download-Slot. Mehr Leitungen, mehr gleichzeitige Downloads.",
                "Segunda Línea", "Abre otra ranura de descarga. Más líneas, más descargas simultáneas.",
                "Вторая линия", "Открывает ещё один слот загрузки. Больше линий — больше одновременных загрузок.");

            // --- Donanim ---
            Upg("Upg_ModemeVurmak",
                "Modem Slap", "The classic fix. Increases speed gain per click.",
                "Modem-Schlag", "Der Klassiker. Erhöht den Geschwindigkeitsgewinn pro Klick.",
                "Golpe al Módem", "La solución clásica. Aumenta la velocidad ganada por clic.",
                "Удар по модему", "Классическое решение. Увеличивает прирост скорости за клик.");

            Upg("Upg_HariciFan",
                "External Cooling Fan", "Keeps modem cool. Increases time you can stay in redline.",
                "Externer Lüfter", "Hält das Modem kühl. Verlängert die Zeit im roten Bereich.",
                "Ventilador Externo", "Mantiene el módem frío. Aumenta el tiempo en zona roja.",
                "Внешний вентилятор", "Охлаждает модем. Увеличивает время в красной зоне.");

            Upg("Upg_SinyalYukselteci",
                "Signal Amplifier", "Boosts a weak signal. Increases speed gained per click.",
                "Signalverstärker", "Verstärkt ein schwaches Signal. Erhöht die Geschwindigkeit pro Klick.",
                "Amplificador de Señal", "Refuerza una señal débil. Aumenta la velocidad por clic.",
                "Усилитель сигнала", "Усиливает слабый сигнал. Увеличивает скорость за клик.");

            Upg("Upg_IsiMacunu",
                "Thermal Paste", "Better heat transfer. Shortens the overheat penalty.",
                "Wärmeleitpaste", "Bessere Wärmeableitung. Verkürzt die Überhitzungsstrafe.",
                "Pasta Térmica", "Mejor transferencia de calor. Acorta la penalización por sobrecalentamiento.",
                "Термопаста", "Лучший отвод тепла. Сокращает штраф за перегрев.");

            Upg("Upg_Cat6Kablo",
                "Cat6 Cable", "Shielded cable instead of bare copper. Raises base speed.",
                "Cat6-Kabel", "Geschirmtes Kabel statt blankem Kupfer. Erhöht die Grundgeschwindigkeit.",
                "Cable Cat6", "Cable apantallado en lugar de cobre desnudo. Sube la velocidad base.",
                "Кабель Cat6", "Экранированный кабель вместо голой меди. Повышает базовую скорость.");

            Upg("Upg_UPS",
                "Uninterruptible Power (UPS)", "The line no longer drops fully on overheat — part of your base speed survives.",
                "Unterbrechungsfreie Stromversorgung (USV)", "Die Leitung bricht bei Überhitzung nicht mehr komplett ab — ein Teil der Grundgeschwindigkeit bleibt.",
                "Sistema de Alimentación Ininterrumpida (SAI)", "La línea ya no cae del todo al sobrecalentarse: conservas parte de tu velocidad base.",
                "Источник бесперебойного питания (ИБП)", "При перегреве линия больше не обрывается полностью — часть базовой скорости сохраняется.");

            Upg("Upg_YonluAnten",
                "Directional Antenna", "Focuses the signal. Raises the redline peak multiplier.",
                "Richtantenne", "Bündelt das Signal. Erhöht den Spitzenmultiplikator im roten Bereich.",
                "Antena Direccional", "Concentra la señal. Sube el multiplicador máximo de la zona roja.",
                "Направленная антенна", "Фокусирует сигнал. Повышает пиковый множитель красной зоны.");

            Upg("Upg_NvmeSSD",
                "NVMe SSD", "No more write bottleneck. Every completed file pays extra.",
                "NVMe-SSD", "Kein Schreib-Flaschenhals mehr. Jede fertige Datei bringt extra.",
                "SSD NVMe", "Se acabó el cuello de botella de escritura. Cada archivo completado paga extra.",
                "NVMe SSD", "Больше нет узкого места записи. Каждый готовый файл приносит больше.");

            Upg("Upg_SiviSogutma",
                "Liquid Cooling", "Radiator kicks in. Stay in the redline far longer.",
                "Wasserkühlung", "Der Radiator springt an. Bleib deutlich länger im roten Bereich.",
                "Refrigeración Líquida", "El radiador entra en acción. Permanece mucho más tiempo en zona roja.",
                "Жидкостное охлаждение", "Включается радиатор. Оставайся в красной зоне намного дольше.");

            Upg("Upg_MekanikKlavye",
                "Mechanical Keyboard", "The keys press themselves. Adds automatic clicks per second.",
                "Mechanische Tastatur", "Die Tasten drücken sich selbst. Fügt automatische Klicks pro Sekunde hinzu.",
                "Teclado Mecánico", "Las teclas se pulsan solas. Añade clics automáticos por segundo.",
                "Механическая клавиатура", "Клавиши нажимаются сами. Добавляет автоклики в секунду.");

            Upg("Upg_RamYukseltmesi",
                "RAM Upgrade", "A second file starts downloading at the same time (extra slot runs at 60% speed).",
                "RAM-Aufrüstung", "Eine zweite Datei lädt gleichzeitig (der Zusatzslot läuft mit 60 % Tempo).",
                "Ampliación de RAM", "Un segundo archivo empieza a descargarse a la vez (la ranura extra va al 60 % de velocidad).",
                "Расширение ОЗУ", "Второй файл качается одновременно (доп. слот работает на 60 % скорости).");

            Upg("Upg_KuantumIslemci",
                "Quantum Processor", "Processes packets in parallel. Multiplies click power.",
                "Quantenprozessor", "Verarbeitet Pakete parallel. Vervielfacht die Klickkraft.",
                "Procesador Cuántico", "Procesa paquetes en paralelo. Multiplica la potencia de clic.",
                "Квантовый процессор", "Обрабатывает пакеты параллельно. Умножает силу клика.");

            // --- Yazilim ---
            Upg("Upg_DnsAyari",
                "DNS Tweak", "Chance to trigger a Critical Speed Burst (5x) on click.",
                "DNS-Optimierung", "Chance, bei einem Klick einen kritischen Geschwindigkeitsschub (5x) auszulösen.",
                "Ajuste DNS", "Probabilidad de activar un Impulso Crítico de Velocidad (5x) al hacer clic.",
                "Настройка DNS", "Шанс вызвать критический всплеск скорости (5x) при клике.");

            Upg("Upg_PremiumSunucu",
                "Premium Server Account", "Permanently increases base reward per file.",
                "Premium-Server-Konto", "Erhöht dauerhaft die Grundbelohnung pro Datei.",
                "Cuenta de Servidor Premium", "Aumenta permanentemente la recompensa base por archivo.",
                "Премиум-аккаунт сервера", "Навсегда повышает базовую награду за файл.");

            Upg("Upg_DownloadManager",
                "Download Manager", "Improves offline earnings. Level 2: Pro (100% / 24h).",
                "Download-Manager", "Verbessert die Offline-Einnahmen. Stufe 2: Pro (100 % / 24 Std.).",
                "Gestor de Descargas", "Mejora las ganancias sin conexión. Nivel 2: Pro (100 % / 24 h).",
                "Менеджер загрузок", "Улучшает офлайн-доход. Уровень 2: Pro (100 % / 24 ч).");

            Upg("Upg_Sikistirma",
                "Compression Algorithm", "Files arrive compressed. Shrinks the size you have to download.",
                "Kompressionsalgorithmus", "Dateien kommen komprimiert an. Verkleinert die Downloadgröße.",
                "Algoritmo de Compresión", "Los archivos llegan comprimidos. Reduce el tamaño a descargar.",
                "Алгоритм сжатия", "Файлы приходят сжатыми. Уменьшает объём загрузки.");

            Upg("Upg_ReklamEngelleyici",
                "Ad Blocker", "Cuts requests that clog the line. Negative events happen less often.",
                "Werbeblocker", "Blockiert Anfragen, die die Leitung verstopfen. Negative Ereignisse treten seltener auf.",
                "Bloqueador de Anuncios", "Corta las peticiones que saturan la línea. Los eventos negativos ocurren menos.",
                "Блокировщик рекламы", "Отсекает запросы, забивающие канал. Негативные события происходят реже.");

            Upg("Upg_OverclockAraci",
                "Overclock Tool", "Pushes the modem past its limits. The redline zone starts earlier.",
                "Overclock-Werkzeug", "Treibt das Modem über seine Grenzen. Der rote Bereich beginnt früher.",
                "Herramienta de Overclock", "Lleva el módem más allá de sus límites. La zona roja empieza antes.",
                "Инструмент разгона", "Выводит модем за пределы. Красная зона начинается раньше.");

            Upg("Upg_OtomatikKuyruk",
                "Auto Queue", "Picks files for you: always the most profitable. Level 2: never settles for less.",
                "Automatische Warteschlange", "Wählt die Dateien für dich: immer die lukrativste. Stufe 2: gibt sich nie mit weniger zufrieden.",
                "Cola Automática", "Elige los archivos por ti: siempre el más rentable. Nivel 2: nunca se conforma con menos.",
                "Автоочередь", "Сама выбирает файлы: всегда самый выгодный. Уровень 2: никогда не соглашается на меньшее.");

            Upg("Upg_MakroScript",
                "Macro Script", "Scripts your clicks. Significantly raises auto-click speed.",
                "Makro-Skript", "Skriptet deine Klicks. Erhöht die Autoklick-Rate deutlich.",
                "Script de Macros", "Automatiza tus clics. Sube notablemente la velocidad de autoclic.",
                "Макрос-скрипт", "Скриптует твои клики. Заметно повышает скорость автокликов.");

            Upg("Upg_TorrentIstemcisi",
                "Torrent Client", "You seed finished files. Earns a share of your idle income on top.",
                "Torrent-Client", "Du seedest fertige Dateien. Bringt zusätzlich einen Anteil deines Leerlauf-Einkommens.",
                "Cliente Torrent", "Compartes los archivos terminados. Ganas un porcentaje extra de tu ingreso pasivo.",
                "Торрент-клиент", "Ты раздаёшь готовые файлы. Приносит долю пассивного дохода сверху.");

            Upg("Upg_VpnTuneli",
                "VPN Tunnel", "Data caps can no longer throttle you. The fee remains, the speed does not drop.",
                "VPN-Tunnel", "Datenlimits können dich nicht mehr drosseln. Die Gebühr bleibt, das Tempo nicht.",
                "Túnel VPN", "Los límites de datos ya no pueden frenarte. La tarifa sigue, la velocidad no baja.",
                "VPN-туннель", "Лимиты трафика больше не режут скорость. Плата остаётся, скорость — нет.");

            Upg("Upg_YzOptimizer",
                "AI Optimizer", "Learns the routes itself. Greatly increases reward per file.",
                "KI-Optimierer", "Lernt die Routen selbst. Erhöht die Belohnung pro Datei stark.",
                "Optimizador con IA", "Aprende las rutas por sí solo. Aumenta mucho la recompensa por archivo.",
                "ИИ-оптимизатор", "Сам изучает маршруты. Сильно повышает награду за файл.");

            // --- Prestij yetenekleri ---
            Upg("Skill_HizliBaslangic",
                "Quick Start", "After a Line Change you start from a higher tier instead of zero.",
                "Schnellstart", "Nach einem Leitungswechsel startest du auf einer höheren Stufe statt bei null.",
                "Inicio Rápido", "Tras un Cambio de Línea empiezas en un nivel superior en vez de cero.",
                "Быстрый старт", "После смены линии ты стартуешь с более высокого уровня, а не с нуля.");

            Upg("Skill_KaliciGuc",
                "Permanent Power", "Click power increases permanently. Never resets.",
                "Dauerhafte Kraft", "Die Klickkraft steigt dauerhaft. Wird nie zurückgesetzt.",
                "Potencia Permanente", "La potencia de clic aumenta para siempre. Nunca se reinicia.",
                "Постоянная мощь", "Сила клика растёт навсегда. Никогда не сбрасывается.");

            Upg("Skill_OfflineUstasi",
                "Offline Master", "Permanently raises offline earning efficiency.",
                "Offline-Meister", "Erhöht dauerhaft die Effizienz der Offline-Einnahmen.",
                "Maestro Sin Conexión", "Sube permanentemente la eficiencia de las ganancias sin conexión.",
                "Мастер офлайна", "Навсегда повышает эффективность офлайн-дохода.");

            Upg("Skill_KrediFaizi",
                "Credit Interest", "Earn more Fiber Credits from every Line Change.",
                "Kreditzinsen", "Verdiene bei jedem Leitungswechsel mehr Faser-Credits.",
                "Interés de Crédito", "Gana más Créditos de Fibra en cada Cambio de Línea.",
                "Проценты по кредиту", "Больше волоконных кредитов за каждую смену линии.");

            Upg("Skill_SogukBaslangic",
                "Cold Start", "The modem permanently heats up more slowly.",
                "Kaltstart", "Das Modem erhitzt sich dauerhaft langsamer.",
                "Arranque en Frío", "El módem se calienta permanentemente más despacio.",
                "Холодный старт", "Модем навсегда нагревается медленнее.");

            Upg("Skill_KritikUstasi",
                "Critical Master", "Critical speed burst chance increases permanently.",
                "Kritik-Meister", "Die Chance auf kritische Geschwindigkeitsschübe steigt dauerhaft.",
                "Maestro Crítico", "La probabilidad de impulso crítico aumenta permanentemente.",
                "Мастер критов", "Шанс критического всплеска скорости растёт навсегда.");
        }

        void RegisterConnections()
        {
            Upg("Upg_Conn_Tier1",
                "Phone Line", "First dial-up connection. Upgrades speed to Tier 1.",
                "Telefonleitung", "Erste Einwahlverbindung. Hebt die Geschwindigkeit auf Stufe 1.",
                "Línea Telefónica", "Primera conexión por marcado. Sube la velocidad al Nivel 1.",
                "Телефонная линия", "Первое коммутируемое соединение. Поднимает скорость до уровня 1.");

            Upg("Upg_Conn_Tier2",
                "Dial-up 56K", "That iconic beep-vzzzt sound. Upgrades speed to Tier 2.",
                "Dial-up 56K", "Dieser ikonische Piep-Zisch-Ton. Hebt die Geschwindigkeit auf Stufe 2.",
                "Dial-up 56K", "Ese icónico pitido chirriante. Sube la velocidad al Nivel 2.",
                "Dial-up 56K", "Тот самый писк модема. Поднимает скорость до уровня 2.");

            Upg("Upg_Conn_Tier3",
                "ISDN Dual Line", "Dual line, double speed. Upgrades speed to Tier 3.",
                "ISDN-Doppelleitung", "Doppelte Leitung, doppeltes Tempo. Hebt die Geschwindigkeit auf Stufe 3.",
                "Línea Doble ISDN", "Línea doble, velocidad doble. Sube la velocidad al Nivel 3.",
                "Двойная линия ISDN", "Две линии — двойная скорость. Поднимает скорость до уровня 3.");

            Upg("Upg_Conn_Tier4",
                "ADSL Era", "Home internet explosion. Upgrades speed to Tier 4.",
                "ADSL-Ära", "Die Explosion des Heiminternets. Hebt die Geschwindigkeit auf Stufe 4.",
                "Era ADSL", "La explosión de internet en casa. Sube la velocidad al Nivel 4.",
                "Эра ADSL", "Взрыв домашнего интернета. Поднимает скорость до уровня 4.");

            Upg("Upg_Conn_Tier5",
                "VDSL / Early Fiber", "City infrastructure renewed. Upgrades speed to Tier 5.",
                "VDSL / Frühes Glasfaser", "Die Stadtinfrastruktur wurde erneuert. Hebt die Geschwindigkeit auf Stufe 5.",
                "VDSL / Fibra Temprana", "Infraestructura urbana renovada. Sube la velocidad al Nivel 5.",
                "VDSL / ранняя оптика", "Городская инфраструктура обновлена. Поднимает скорость до уровня 5.");

            Upg("Upg_Conn_Tier6",
                "Full Fiber", "Optical cable era. Upgrades speed to Tier 6.",
                "Voll-Glasfaser", "Die Ära des Lichtwellenleiters. Hebt die Geschwindigkeit auf Stufe 6.",
                "Fibra Óptica Total", "La era del cable óptico. Sube la velocidad al Nivel 6.",
                "Полная оптика", "Эра оптоволокна. Поднимает скорость до уровня 6.");

            Upg("Upg_Conn_Tier7",
                "Quantum Line", "Experimental line. Packets everywhere. Upgrades speed to Tier 7.",
                "Quantenleitung", "Experimentelle Leitung. Pakete überall zugleich. Hebt die Geschwindigkeit auf Stufe 7.",
                "Línea Cuántica", "Línea experimental. Paquetes por todas partes. Sube la velocidad al Nivel 7.",
                "Квантовая линия", "Экспериментальная линия. Пакеты повсюду. Поднимает скорость до уровня 7.");

            Upg("Upg_Conn_Tier8",
                "Datacenter Gateway", "Enterprise dedicated pipe. Upgrades speed to Tier 8.",
                "Rechenzentrum-Gateway", "Dedizierte Unternehmensleitung. Hebt die Geschwindigkeit auf Stufe 8.",
                "Pasarela de Centro de Datos", "Enlace dedicado empresarial. Sube la velocidad al Nivel 8.",
                "Шлюз дата-центра", "Выделенный корпоративный канал. Поднимает скорость до уровня 8.");
        }
    }
}
