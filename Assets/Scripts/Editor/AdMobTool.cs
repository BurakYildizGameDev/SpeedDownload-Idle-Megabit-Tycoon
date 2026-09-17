using System.Collections.Generic;
using SpeedDownload.Core;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Google AdMob SDK yapilandirma ve dogrulama araclari.
    /// </summary>
    public static class AdMobTool
    {
        const string SettingsAssetPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";

        [MenuItem("Tools/SpeedDownload/Open AdMob Settings", false, 42)]
        public static void OpenAdMobSettings()
        {
            EditorApplication.ExecuteMenuItem("Assets/Google Mobile Ads/Settings...");
        }

        [MenuItem("Tools/SpeedDownload/Verify AdMob Configuration", false, 43)]
        public static void VerifyAdMobConfig()
        {
            var issues = new List<string>();
            var settingsAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SettingsAssetPath);

            string androidAppId = "";
            string iosAppId = "";

            if (settingsAsset == null)
            {
                issues.Add("GoogleMobileAdsSettings asset dosyasi bulunamadi: " + SettingsAssetPath);
            }
            else
            {
                var so = new SerializedObject(settingsAsset);
                var androidProp = so.FindProperty("adMobAndroidAppId");
                var iosProp = so.FindProperty("adMobIOSAppId");

                androidAppId = androidProp != null ? androidProp.stringValue : "";
                iosAppId = iosProp != null ? iosProp.stringValue : "";

                if (string.IsNullOrEmpty(androidAppId))
                {
                    issues.Add("AdMob Android App ID bos! 'Assets -> Google Mobile Ads -> Settings' uzerinden tanimlayin.");
                }

                if (string.IsNullOrEmpty(iosAppId))
                {
                    issues.Add("AdMob iOS App ID bos! 'Assets -> Google Mobile Ads -> Settings' uzerinden tanimlayin.");
                }
            }

            var admobService = Object.FindAnyObjectByType<AdMobService>();
            if (admobService == null)
            {
                issues.Add("Sahnede AdMobService bileseni bulunamadi. 'Tools -> SpeedDownload -> 3. Build Game Scene' calistirin.");
            }

            if (issues.Count == 0)
            {
                Debug.Log("<color=#44FF88><b>[AdMobTool] ADMOB SDK YAPILANDIRMASI KUSURSUZ!</b></color>\n" +
                          $"  Android App ID : {androidAppId}\n" +
                          $"  iOS App ID     : {iosAppId}\n" +
                          $"  AdMobService   : Sahneye bagli ve hazir.");
            }
            else
            {
                Debug.LogWarning("[AdMobTool] AdMob yapilandirmasinda bazi eksikler var:\n - " +
                                 string.Join("\n - ", issues));
            }
        }
    }
}
