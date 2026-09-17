using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// APK boyutunu 76 MB'tan 20 MB altina düşürmek için otomatik optimizasyon araci.
    ///
    /// 1. Tum Sprite/Texture'lara Android ASTC 6x6 Sıkıştırması uygular (%80 küçülme).
    /// 2. Android Player Settings: ARM64 mimarisi, IL2CPP ve High Code Stripping ayarlar.
    /// </summary>
    public static class BuildSizeOptimizerTool
    {
        [MenuItem("Tools/SpeedDownload/Optimize APK Build Size (Target ~20MB)", false, 100)]
        public static void OptimizeAll()
        {
            int texturesOptimized = CompressAllTexturesAndroid();
            ConfigurePlayerSettings();
            LogLargestAssets();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BuildSizeOptimizer] DERİN OPTİMİZASYON TAMAMLANDI!\n" +
                      $"  - {texturesOptimized} kaplamada MipMap kapatıldı ve ASTC (8x8/6x6) 512px sıkıştırma uygulandı.\n" +
                      $"  - Android mimarisi ARM64, IL2CPP ve High Code Stripping ayarlandı.\n" +
                      $"  - Şimdi tekrar Build alabilirsin!");
        }

        static int CompressAllTexturesAndroid()
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            int count = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                // 2D UI ve Arka planlar icin MipMap kapali olmali (%33 ek boyut tasarrufu)
                importer.mipmapEnabled = false;

                bool isBackground = path.Contains("Backgrounds") || path.Contains("Dial") || path.Contains("Branding");

                TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
                androidSettings.overridden = true;
                androidSettings.maxTextureSize = isBackground ? 512 : 512;
                androidSettings.format = isBackground ? TextureImporterFormat.ASTC_8x8 : TextureImporterFormat.ASTC_6x6;
                androidSettings.compressionQuality = 50;

                importer.SetPlatformTextureSettings(androidSettings);
                importer.SaveAndReimport();
                count++;
            }

            return count;
        }

        static void ConfigurePlayerSettings()
        {
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.Android, ManagedStrippingLevel.High);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.buildApkPerCpuArchitecture = false;
#pragma warning disable CS0618
            EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Disabled;
#pragma warning restore CS0618
        }

        static void LogLargestAssets()
        {
            string[] files = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories);
            var list = new System.Collections.Generic.List<FileInfo>();

            foreach (string f in files)
            {
                if (f.EndsWith(".meta")) continue;
                list.Add(new FileInfo(f));
            }

            list.Sort((a, b) => b.Length.CompareTo(a.Length));

            StringBuilder sb = new StringBuilder("[BuildSizeOptimizer] EN BÜYÜK 15 VARLIK:\n");
            int showCount = Mathf.Min(15, list.Count);
            for (int i = 0; i < showCount; i++)
            {
                double mb = list[i].Length / (1024.0 * 1024.0);
                sb.AppendLine($"  {i + 1}. {list[i].FullName.Replace(Application.dataPath, "Assets")} -> {mb:F2} MB");
            }
            Debug.Log(sb.ToString());
        }
    }
}
