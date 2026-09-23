using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// itch.io / GitHub Pages icin WebGL build'i.
    ///
    /// Iki hosting de sunucu tarafinda Content-Encoding basligi ayarlatmiyor;
    /// Gzip + decompression fallback, dosyalari tarayicida JS ile acarak her
    /// iki yerde de ayar gerektirmeden calisiyor.
    ///
    /// Batchmode:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod SpeedDownload.EditorTools.WebGLBuildTool.BuildFromCommandLine
    /// </summary>
    public static class WebGLBuildTool
    {
        public const string OutputDir = "Builds/WebGL";
        public const string ZipPath = "Builds/MegabitTycoon-WebGL.zip";

        // Portre oyun: itch.io'daki "viewport dimensions" ile ayni tutulmali.
        const int CanvasWidth = 540;
        const int CanvasHeight = 960;

        [MenuItem("Tools/SpeedDownload/6. Build WebGL (itch.io + GitHub Pages)", false, 60)]
        public static void BuildMenu()
        {
            if (!Build())
                EditorUtility.DisplayDialog("WebGL Build", "Build basarisiz — Console'a bak.", "Tamam");
            else
                EditorUtility.RevealInFinder(ZipPath);
        }

        public static void BuildFromCommandLine()
        {
            EditorApplication.Exit(Build() ? 0 : 1);
        }

        static bool Build()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                Debug.LogError("[WebGLBuildTool] WebGL Build Support modulu kurulu degil (Unity Hub > Installs > Add modules).");
                return false;
            }

            ApplySettings();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[WebGLBuildTool] Build Settings'te etkin sahne yok.");
                return false;
            }

            if (Directory.Exists(OutputDir)) Directory.Delete(OutputDir, true);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OutputDir,
                target = BuildTarget.WebGL,
                targetGroup = BuildTargetGroup.WebGL,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[WebGLBuildTool] Build basarisiz: " + report.summary.result);
                return false;
            }

            // itch.io index.html'in zip kokunde olmasini istiyor.
            if (File.Exists(ZipPath)) File.Delete(ZipPath);
            ZipFile.CreateFromDirectory(OutputDir, ZipPath, System.IO.Compression.CompressionLevel.Optimal, false);

            Debug.Log($"[WebGLBuildTool] Tamam: {OutputDir} ({report.summary.totalSize / (1024f * 1024f):F1} MB) -> {ZipPath}");
            return true;
        }

        static void ApplySettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.defaultWebScreenWidth = CanvasWidth;
            PlayerSettings.defaultWebScreenHeight = CanvasHeight;
            PlayerSettings.runInBackground = true;
        }
    }
}
