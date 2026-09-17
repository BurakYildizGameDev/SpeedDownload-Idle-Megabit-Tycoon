#if UNITY_ANDROID
using System.IO;
using System.Xml;
using UnityEditor.Android;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Android manifest'ine <c>android.permission.VIBRATE</c> ekler.
    ///
    /// TITRESIMIN CALISMAMASININ GERCEK SEBEBI BUYDU.
    ///
    /// HapticManager iki kez yeniden yazildi (once cok siddetli, sonra
    /// hissedilemeyecek kadar kisik) ama sorun hicbir zaman kodda degildi:
    /// yayinlanan APK'nin manifest'inde YALNIZCA android.permission.INTERNET
    /// vardi. VIBRATE izni olmadan <c>Vibrator.vibrate(...)</c> cagrisi Android
    /// tarafindan SecurityException ile reddediliyor; HapticManager.AndroidVibrate
    /// bu istisnayi sessizce yuttugu icin ne oyunda titresim oluyor ne de
    /// konsolda hata gorunuyordu. Disaridan bakinca "titresim calismiyor".
    ///
    /// Unity izni kendiliginden EKLEMEZ: manifest ureticisi <c>Handheld.Vibrate</c>
    /// cagrisini tarayarak izin ekler, ama HapticManager o cagriyi yalnizca
    /// <c>#elif UNITY_IOS</c> dalinda kullaniyor — Android derlemesinde o satir
    /// hic yok. AndroidJavaObject uzerinden yapilan dogrudan vibrator cagrilari
    /// da taramaya takilmiyor.
    ///
    /// Neden ozel manifest degil de bu kanca:
    /// Publishing Settings > Custom Main Manifest, Unity'nin urettigi manifest'in
    /// TAMAMINI (activity, intent-filter, tema...) elle tasimayi gerektirir ve
    /// Unity surumleri arasinda degisir; bir satir izin icin oyunun acilisini
    /// riske atar. Bu kanca ise Unity'nin kendi urettigi manifest'e dokunmadan
    /// yalnizca eksik izni ekler.
    /// </summary>
    public class AndroidManifestTool : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string VibratePermission = "android.permission.VIBRATE";

        public int callbackOrder { get { return 1; } }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = FindManifest(path);

            if (string.IsNullOrEmpty(manifestPath))
            {
                Debug.LogWarning("[AndroidManifestTool] Manifest bulunamadi (" + path +
                                 "). Titresim izni EKLENEMEDI — oyunda haptik calismayacak.");
                return;
            }

            if (AddPermission(manifestPath, VibratePermission))
                Debug.Log("[AndroidManifestTool] VIBRATE izni eklendi: " + manifestPath);
        }

        /// <summary>
        /// Unity surumune gore kanca ya unityLibrary modulunun kokunu ya da
        /// gradle projesinin kokunu veriyor; ikisini de dene, son care olarak
        /// agacta ara.
        /// </summary>
        static string FindManifest(string path)
        {
            string direct = Path.Combine(path, "src/main/AndroidManifest.xml");
            if (File.Exists(direct)) return direct;

            string library = Path.Combine(path, "unityLibrary/src/main/AndroidManifest.xml");
            if (File.Exists(library)) return library;

            string[] found = Directory.GetFiles(path, "AndroidManifest.xml", SearchOption.AllDirectories);
            for (int i = 0; i < found.Length; i++)
            {
                // launcher modulunun manifest'i degil, unityLibrary'ninki hedef:
                // izinler modullerden birlestigi icin biri yeterli, ama oyunun
                // kendi modulune yazmak en ongorulebilir olani.
                if (found[i].Replace('\\', '/').Contains("/unityLibrary/")) return found[i];
            }

            return found.Length > 0 ? found[0] : null;
        }

        /// <summary>
        /// Izni ekler. Zaten varsa dokunmaz (kanca birden fazla kez calisabilir).
        /// </summary>
        static bool AddPermission(string manifestPath, string permission)
        {
            try
            {
                var doc = new XmlDocument();
                doc.Load(manifestPath);

                XmlElement root = doc.DocumentElement;
                if (root == null) return false;

                XmlNodeList existing = root.SelectNodes("uses-permission");
                if (existing != null)
                {
                    for (int i = 0; i < existing.Count; i++)
                    {
                        var element = existing[i] as XmlElement;
                        if (element != null &&
                            element.GetAttribute("name", AndroidNamespace) == permission)
                            return false;   // zaten var
                    }
                }

                XmlElement node = doc.CreateElement("uses-permission");
                node.SetAttribute("name", AndroidNamespace, permission);

                // Izinler <application>'dan once gelmeli; manifest semasi bunu
                // sart kosmuyor ama arac zincirleri bu sirayi bekliyor.
                XmlNode application = root.SelectSingleNode("application");
                if (application != null) root.InsertBefore(node, application);
                else root.AppendChild(node);

                doc.Save(manifestPath);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AndroidManifestTool] Manifest yazilamadi: " + e.Message);
                return false;
            }
        }
    }
}
#endif
