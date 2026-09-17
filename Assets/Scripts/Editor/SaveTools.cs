using System;
using System.IO;
using SpeedDownload.Core;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Kayit dosyasiyla elle oynamak icin. Kapat-ac ve offline kazanc testleri
    /// bunlar olmadan her seferinde gercekten beklemeyi gerektirirdi.
    /// </summary>
    public static class SaveTools
    {
        [MenuItem("Tools/SpeedDownload/Save/Show Save File", false, 200)]
        public static void ShowSave()
        {
            string path = SaveManager.SavePath;

            if (!File.Exists(path))
            {
                Debug.Log("[SaveTools] Kayit yok.\n  aranan: " + path);
                return;
            }

            var info = new FileInfo(path);
            string raw = File.ReadAllText(path);
            string json = ReadJson(raw);

            Debug.Log("[SaveTools] " + path + "\n" +
                      "  boyut: " + info.Length + " bayt   yazilma: " + info.LastWriteTime + "\n" +
                      "  bicim: " + (SaveCrypto.IsEncrypted(raw) ? "sifreli (v7+)" : "duz JSON (eski)") + "\n\n" +
                      (json ?? "!! COZULEMEDI: " + SaveCrypto.Diagnostics));
        }

        /// <summary>
        /// Diskteki metni JSON'a cevirir — sifreli de olsa eski duz bicimde de.
        /// Editor araclari oyunla ayni PlayerPrefs'i kullandigi icin cozebiliyor.
        /// </summary>
        static string ReadJson(string raw)
        {
            return SaveCrypto.IsEncrypted(raw) ? SaveCrypto.Decrypt(raw) : raw;
        }

        [MenuItem("Tools/SpeedDownload/Save/Delete Save File", false, 201)]
        public static void DeleteSave()
        {
            string path = SaveManager.SavePath;

            if (!File.Exists(path))
            {
                Debug.Log("[SaveTools] Silinecek kayit yok: " + path);
                return;
            }

            if (!EditorUtility.DisplayDialog("Kayit silinsin mi?",
                    "Su dosya ve kurtarma kopyalari kalici olarak silinecek:\n\n" + path,
                    "Sil", "Vazgec"))
                return;

            // Kurtarma dosyalari da gitmeli: yalnizca ana dosyayi silmek, bir
            // sonraki aciliste .tmp veya .bak uzerinden silinen ilerlemenin geri
            // gelmesi demekti (calisma zamanindaki DeleteSave da boyle yapiyor).
            int removed = 0;
            foreach (string suffix in new[] { "", ".tmp", ".bak", ".corrupt" })
            {
                string p = path + suffix;
                if (!File.Exists(p)) continue;
                File.Delete(p);
                removed++;
            }

            Debug.Log("[SaveTools] " + removed + " kayit dosyasi silindi: " + path + " (+ .tmp/.bak/.corrupt)");
        }

        /// <summary>
        /// Kuruluma ozel sifreleme kimligini sifirlar.
        ///
        /// Mevcut kayitlar bundan sonra COZULEMEZ — "baska cihaza kopyalanan
        /// kayit reddediliyor mu?" davranisini elle sinamanin yolu bu.
        /// </summary>
        [MenuItem("Tools/SpeedDownload/Save/Reset Encryption Identity", false, 204)]
        public static void ResetIdentity()
        {
            if (!EditorUtility.DisplayDialog("Sifreleme kimligi sifirlansin mi?",
                    "Kurulum kimligi ve tuz silinecek.\n\n" +
                    "UYARI: Mevcut kayit bundan sonra COZULEMEZ ve karantinaya alinir.\n" +
                    "Bu, 'baska cihazin kaydi reddediliyor mu' testi icindir.",
                    "Sifirla", "Vazgec"))
                return;

            SaveCrypto.ResetIdentity();
            Debug.Log("[SaveTools] Sifreleme kimligi sifirlandi. Mevcut kayit artik cozulemez.");
        }

        /// <summary>
        /// Kaydin zaman damgasini geriye alir — offline kazanci gercekten
        /// saatlerce beklemeden test etmenin tek pratik yolu.
        /// </summary>
        [MenuItem("Tools/SpeedDownload/Save/Rewind Save Clock 4 Hours", false, 202)]
        public static void RewindFourHours()
        {
            Rewind(TimeSpan.FromHours(4));
        }

        [MenuItem("Tools/SpeedDownload/Save/Rewind Save Clock 48 Hours", false, 203)]
        public static void RewindFortyEightHours()
        {
            Rewind(TimeSpan.FromHours(48));
        }

        static void Rewind(TimeSpan amount)
        {
            string path = SaveManager.SavePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning("[SaveTools] Once oyunu bir kez calistirip kayit olustur.");
                return;
            }

            try
            {
                string json = ReadJson(File.ReadAllText(path));
                if (json == null)
                {
                    Debug.LogWarning("[SaveTools] Kayit cozulemedi: " + SaveCrypto.Diagnostics);
                    return;
                }

                SaveData data = JsonUtility.FromJson<SaveData>(json);
                if (data == null) { Debug.LogWarning("[SaveTools] Kayit cozulemedi."); return; }

                var previous = new DateTime(data.lastSaveUtcTicks, DateTimeKind.Utc);
                data.lastSaveUtcTicks = previous.Subtract(amount).Ticks;

                // GERI YAZARKEN TEKRAR SIFRELE. Duz JSON yazmak, yukleyicinin
                // "guncel surumlu ama sifresiz dosya = elle yazilmis" kuralina
                // takilir ve gelistiricinin kendi kaydini karantinaya aldirirdi.
                data.saveVersion = SaveData.CurrentVersion;
                File.WriteAllText(path, SaveCrypto.Encrypt(JsonUtility.ToJson(data, false)));

                Debug.Log("[SaveTools] Kayit saati " + amount.TotalHours + " saat geri alindi.\n" +
                          "  once : " + previous + " UTC\n" +
                          "  simdi: " + new DateTime(data.lastSaveUtcTicks, DateTimeKind.Utc) + " UTC\n" +
                          "  Play'e basinca offline raporu cikmali.");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveTools] Basarisiz: " + e.Message);
            }
        }
    }
}
