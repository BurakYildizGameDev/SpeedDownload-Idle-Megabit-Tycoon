using System.IO;
using System.Text;
using SpeedDownload.Core;
using UnityEditor;
using UnityEngine;

namespace SpeedDownload.EditorTools
{
    /// <summary>
    /// Prosedurel muzigi WAV olarak disa aktarir — oyunu calistirmadan dinlemek icin.
    ///
    /// Muzik calisma aninda sentezlendigi icin projede dinlenebilecek bir dosya
    /// yok. Bu arac her kademe grubu icin bir dongu uretip proje KOKUNDE
    /// (Assets disinda) bir klasore yaziyor; Unity onlari import etmesin diye
    /// bilincli olarak Assets'in disina.
    /// </summary>
    public static class MusicPreviewTool
    {
        const string OutputFolder = "MusicPreview";

        // SettingsForTier'daki gruplarin temsilci kademeleri.
        static readonly int[] RepresentativeTiers = { 0, 2, 4, 6, 8 };

        static readonly string[] GroupNames =
        {
            "grup0_sinyal_telefon",
            "grup1_dialup_isdn",
            "grup2_adsl_vdsl",
            "grup3_fiber_kuantum",
            "grup4_veri_merkezi"
        };

        [MenuItem("Tools/SpeedDownload/6. Export Music Preview (WAV)", false, 60)]
        public static void Export()
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string dir = Path.Combine(root, OutputFolder);
            Directory.CreateDirectory(dir);

            var log = new StringBuilder();
            log.AppendLine("[MusicPreview] Klasor: " + dir);

            for (int i = 0; i < RepresentativeTiers.Length; i++)
            {
                int tier = RepresentativeTiers[i];
                ProceduralMusic.Settings settings = ProceduralMusic.SettingsForTier(tier);

                AudioClip clip = ProceduralMusic.BuildLoop("preview_" + i, settings);

                var samples = new float[clip.samples * clip.channels];
                clip.GetData(samples, 0);

                float peak = 0f;
                double sum = 0.0;
                for (int s = 0; s < samples.Length; s++)
                {
                    float a = Mathf.Abs(samples[s]);
                    if (a > peak) peak = a;
                    sum += samples[s] * (double)samples[s];
                }
                float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, samples.Length)));

                string path = Path.Combine(dir, GroupNames[i] + ".wav");
                WriteWav(path, samples, clip.frequency, clip.channels);

                log.AppendFormat(
                    "  {0,-24} {1,5:0.0} sn  {2,3:0} BPM  tepe {3:0.00}  rms {4:0.000}  {5:n0} KB\n",
                    GroupNames[i], clip.length, settings.bpm, peak, rms,
                    new FileInfo(path).Length / 1024);

                Object.DestroyImmediate(clip);
            }

            Debug.Log(log.ToString());
            EditorUtility.RevealInFinder(dir);
        }

        /// <summary>16-bit PCM WAV yazar (44 bayt basliк + veri).</summary>
        static void WriteWav(string path, float[] samples, int sampleRate, int channels)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;

                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' });

                w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16);                                  // alt parca boyutu
                w.Write((short)1);                            // PCM
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(sampleRate * channels * 2);           // bayt/sn
                w.Write((short)(channels * 2));               // blok hizalama
                w.Write((short)16);                           // bit derinligi

                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(dataBytes);

                for (int i = 0; i < samples.Length; i++)
                {
                    short v = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
                    w.Write(v);
                }
            }
        }
    }
}
