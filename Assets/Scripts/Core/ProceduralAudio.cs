using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Ses klipleri calisma aninda uretilir (PLAN Bolum 2.5).
    ///
    /// Projede ses varligi yok ve proje kurali geregi uretilmeyecek — ama
    /// chiptune estetigi zaten sentezle birebir ortusuyor, dolayisiyla bu bir
    /// odun degil, dogru cozum.
    ///
    /// Klipler bir kez uretilip AudioManager'da saklanir; her calmada yeniden
    /// sentez yapilmaz.
    /// </summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        /// <summary>Kare dalga: 8-bit "bip" sesinin temeli.</summary>
        public static AudioClip Square(string name, float frequency, float duration,
                                       float volume = 0.5f, float decay = 8f)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[count];

            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float phase = t * frequency;
                float wave = (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;

                data[i] = wave * volume * Mathf.Exp(-decay * t);
            }

            return FromData(name, data);
        }

        /// <summary>Alcalan/yukselen kare dalga — overheat ve kademe atlama icin.</summary>
        public static AudioClip Sweep(string name, float startFrequency, float endFrequency,
                                      float duration, float volume = 0.5f, float noise = 0f)
        {
            int count = Mathf.Max(1, Mathf.RoundToInt(SampleRate * duration));
            var data = new float[count];

            float phase = 0f;
            var rng = new System.Random(1234);

            for (int i = 0; i < count; i++)
            {
                float k = i / (float)count;
                float freq = Mathf.Lerp(startFrequency, endFrequency, k);

                phase += freq / SampleRate;
                float wave = (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;

                if (noise > 0f)
                    wave += ((float)rng.NextDouble() * 2f - 1f) * noise;

                // Sonda yumusak kesim — "tik" sesi olmasin.
                float envelope = Mathf.Min(1f, (1f - k) * 4f);
                data[i] = Mathf.Clamp(wave, -1f, 1f) * volume * envelope;
            }

            return FromData(name, data);
        }

        /// <summary>Arpej: sirayla calinan notalar (indirme bitisi, kademe atlama).</summary>
        public static AudioClip Arpeggio(string name, float[] frequencies, float noteDuration,
                                         float volume = 0.45f)
        {
            int perNote = Mathf.Max(1, Mathf.RoundToInt(SampleRate * noteDuration));
            var data = new float[perNote * frequencies.Length];

            for (int n = 0; n < frequencies.Length; n++)
            {
                float freq = frequencies[n];

                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)SampleRate;
                    float phase = t * freq;
                    float wave = (phase - Mathf.Floor(phase)) < 0.5f ? 1f : -1f;

                    data[n * perNote + i] = wave * volume * Mathf.Exp(-6f * t);
                }
            }

            return FromData(name, data);
        }

        /// <summary>
        /// Donguye uygun testere dalgasi — ibre ugultusu.
        /// Uzunluk tam periyot katina yuvarlanir ki dongude tikirti olmasin.
        /// </summary>
        public static AudioClip LoopingSaw(string name, float frequency, float volume = 0.25f)
        {
            int samplesPerCycle = Mathf.Max(2, Mathf.RoundToInt(SampleRate / frequency));
            int cycles = Mathf.Max(1, Mathf.RoundToInt(frequency / 4f)); // ~0,25 sn
            int count = samplesPerCycle * cycles;

            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float phase = (i % samplesPerCycle) / (float)samplesPerCycle;
                data[i] = (phase * 2f - 1f) * volume;
            }

            return FromData(name, data);
        }

        static AudioClip FromData(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
