using System.Collections.Generic;
using SpeedDownload.Core;
using SpeedDownload.Data;
using SpeedDownload.UI;
using SpeedDownload.Util;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Uzun sureli otomatik oynatma testi.
    ///
    /// Zamana bagli her sey (indirme, overheat, olaylar, autosave) gercekten
    /// calisir ve hatalar yuzeye cikar.
    ///
    /// NEDEN ESKIDEN CALISMIYORDU
    /// --------------------------
    /// Arac, oyun dongusunu editor tick'inden QueuePlayerLoopUpdate ile
    /// pompalamaya calisiyordu ama 3600 istegin yalnizca ~15'i gercek kareye
    /// donusuyordu; test kendini dogru sekilde "GECERSIZ" ilan ediyor, yani
    /// pratikte HIC calistirilamiyordu.
    ///
    /// Sebep pompalama degildi: <see cref="GameBootstrapper"/> acilista
    /// <c>Application.runInBackground = false</c> yaziyor (mobilde pil icin
    /// dogru bir tercih). Editor odakta degilken bu ayar oyun dongusunu
    /// tamamen durduruyor — QueuePlayerLoopUpdate cagrilari da bosa gidiyor.
    ///
    /// Cozum: soak suresince arka planda calismayi ACIK tut, bitince ESKI
    /// DEGERINE geri koy. Boylece test Unity penceresi arkada dururken de
    /// gercekten kosuyor; oyunun kendi pil davranisi degismiyor.
    ///
    /// Yalnizca gelistirme araci — oyuna dahil degil.
    /// </summary>
    public static class SoakTestTool
    {
        const int TargetFrames = 3600;      // ~60 sn (60 fps varsayimi)
        const float ClicksPerSecond = 7f;

        static int _frames;
        static bool _running;
        static double _startTime;
        static int _engineFramesAtStart;

        /// <summary>Test oncesi arka plan ayari — bitince geri konuyor.</summary>
        static bool _prevRunInBackground;

        /// <summary>Bir sonraki tiklamanin zamani (oyun saati, sn).</summary>
        static float _nextClickTime;

        static int _overheats;
        static int _eventsStarted;
        static int _filesAtStart;
        static double _balanceAtStart;
        static readonly List<string> _log = new List<string>();

        static SpeedController _speed;
        static GameEventManager _events;

        [MenuItem("Tools/SpeedDownload/Optimize APK Build Size", false, 100)]
        public static void OptimizeAPKBuildSize()
        {
            BuildSizeOptimizerTool.OptimizeAll();
        }

        [MenuItem("Tools/SpeedDownload/Test/Run All Unit Tests", false, 310)]
        public static void RunAllUnitTests()
        {
            Tests.UnitTests.RunAllUnitTests();
        }

        [MenuItem("Tools/SpeedDownload/Test/Run Soak Test (~60 s)", false, 300)]
        public static void Run()
        {
            if (_running) { Debug.LogWarning("[SoakTest] Zaten calisiyor."); return; }

            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[SoakTest] Once Play moduna gec, sonra tekrar calistir.");
                return;
            }

            GameManager gm = GameManager.Instance;
            if (gm == null) { Debug.LogError("[SoakTest] GameManager yok."); return; }

            _speed = gm.Speed;
            _events = gm.Events;

            _frames = 0;
            _overheats = 0;
            _eventsStarted = 0;
            _filesAtStart = gm.Download != null ? gm.Download.CompletedCount : 0;
            _balanceAtStart = gm.Money != null ? gm.Money.Balance : 0.0;
            _log.Clear();
            _startTime = EditorApplication.timeSinceStartup;
            _engineFramesAtStart = Time.frameCount;
            _nextClickTime = Time.unscaledTime;

            // Arka planda calismayi ac (bkz. sinif yorumu). Bu olmadan editor
            // odakta degilken hicbir kare islenmiyor ve test sessizce bos gecer.
            _prevRunInBackground = Application.runInBackground;
            Application.runInBackground = true;

            if (_speed != null) _speed.OverheatStarted += OnOverheat;
            if (_events != null) _events.EventStarted += OnEventStarted;

            _running = true;
            EditorApplication.update += Pump;

            Debug.Log("[SoakTest] Basladi — hedef " + TargetFrames + " gercek motor karesi. " +
                      "Arka planda calisma gecici olarak acildi.");
        }

        static void OnOverheat() { _overheats++; }

        static void OnEventStarted(GameEventType type)
        {
            _eventsStarted++;
            _log.Add("  kare " + _frames + ": olay -> " + type);
        }

        static void Pump()
        {
            if (!EditorApplication.isPlaying) { Finish("Play modundan cikildi"); return; }

            GameManager gm = GameManager.Instance;
            if (gm == null) { Finish("GameManager kayboldu"); return; }

            // Ilerleme artik GERCEK motor karesiyle olculuyor, "istek sayisi"
            // ile degil. Eski sayac, hicbir kare islenmese bile doluyor ve
            // testin bittigi izlenimini veriyordu.
            _frames = Time.frameCount - _engineFramesAtStart;

            // Tiklamayi OYUN saatine bagla: editor tick hizi degisken oldugu
            // icin "her N tick'te bir" sabit bir tiklama hizi vermiyordu.
            if (gm.Speed != null)
            {
                int budget = 16;   // tek tick'te patlamayi onle
                float step = 1f / ClicksPerSecond;

                while (_nextClickTime <= Time.unscaledTime && budget-- > 0)
                {
                    gm.Speed.RegisterClick();
                    _nextClickTime += step;
                }

                // Cok geri kaldiysak (uzun bir donma) sayaci simdiye cek;
                // yoksa borclanan tiklamalar sonsuza dek birikir.
                if (_nextClickTime < Time.unscaledTime - 1f) _nextClickTime = Time.unscaledTime;
            }

            // Motor kendiliginden ilerliyorsa bu bir dokundurma; ilerlemiyorsa
            // (odak yok + runInBackground kapali) tek umut bu.
            EditorApplication.QueuePlayerLoopUpdate();

            if (_frames >= TargetFrames) Finish("tamamlandi");
        }

        static void Finish(string reason)
        {
            _running = false;
            EditorApplication.update -= Pump;

            // Arka plan ayarini GERI KOY: oyunun pil davranisi bir test aracinin
            // yan etkisiyle kalici olarak degismemeli.
            if (EditorApplication.isPlaying) Application.runInBackground = _prevRunInBackground;

            if (_speed != null) _speed.OverheatStarted -= OnOverheat;
            if (_events != null) _events.EventStarted -= OnEventStarted;

            GameManager gm = GameManager.Instance;
            double wall = EditorApplication.timeSinceStartup - _startTime;

            var sb = new System.Text.StringBuilder(512);
            sb.Append("[SoakTest] ").Append(reason)
              .Append("  —  ").Append(_frames).Append(" kare, ")
              .Append(wall.ToString("0.0")).Append(" sn duvar saati\n");

            if (gm != null)
            {
                sb.Append("  oyun ici sure   : ").Append(Time.time.ToString("0.0")).Append(" sn\n");
                sb.Append("  kare sayaci     : ").Append(Time.frameCount).Append('\n');
                sb.Append("  tamamlanan dosya: ")
                  .Append(gm.Download != null ? gm.Download.CompletedCount - _filesAtStart : 0).Append('\n');
                sb.Append("  kazanilan para  : ")
                  .Append(NumberFormatter.FormatMoney(
                      (gm.Money != null ? gm.Money.Balance : 0.0) - _balanceAtStart)).Append('\n');
                sb.Append("  asiri isinma    : ").Append(_overheats).Append(" kez\n");
                sb.Append("  tetiklenen olay : ").Append(_eventsStarted).Append('\n');
                sb.Append("  son hiz         : ")
                  .Append(NumberFormatter.FormatSpeed(gm.Speed != null ? gm.Speed.CurrentSpeed : 0.0))
                  .Append("  t=").Append(gm.Speed != null ? gm.Speed.NormalizedT.ToString("0.00") : "-").Append('\n');
                sb.Append("  aktif olay      : ").Append(gm.Events != null ? gm.Events.Active.ToString() : "-").Append('\n');
                sb.Append("  kayit dosyasi   : ")
                  .Append(System.IO.File.Exists(SaveManager.SavePath) ? "VAR (autosave calisti)" : "yok").Append('\n');
            }

            if (_log.Count > 0)
            {
                sb.Append("  olay gunlugu:\n");
                for (int i = 0; i < _log.Count; i++) sb.Append(_log[i]).Append('\n');
            }

            // Sessizce yesil isik vermemek icin: gercekten kac kare islendi?
            // Az kare islendiyse zamana bagli hicbir sey sinanmamis demektir.
            int engineFrames = Time.frameCount - _engineFramesAtStart;
            sb.Append("  gercek motor karesi: ").Append(engineFrames)
              .Append(" / ").Append(TargetFrames).Append(" hedef\n");

            if (engineFrames < TargetFrames / 4)
            {
                sb.Append("\n  !!! GECERSIZ TEST !!!\n")
                  .Append("  Karelerin cogu islenmedi. Zamana bagli hicbir sey\n")
                  .Append("  test EDILMEDI. Unity penceresini one getirip tekrar calistir.");
                Debug.LogWarning(sb.ToString());
                return;
            }

            Debug.Log(sb.ToString());
        }
    }
}
