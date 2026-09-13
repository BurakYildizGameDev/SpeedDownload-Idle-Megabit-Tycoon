using System.Collections;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Kademeye gore evrilen arka plan muzigi — calisma aninda sentezlenir.
    ///
    /// Projede ses varligi yok ve GDD "arka plan muzigi kademeyle birlikte
    /// evrilir" diyor. Hazir klip kullanmak 9 ayri parca + APK'da birkac MB
    /// demekti; sentez ise 0 byte ekliyor ve tema (retro/chiptune) zaten
    /// bu estetigin ta kendisi.
    ///
    /// Uretilen dongu tek parcadir: bas + arpej + ritim ayni tampona toplanir.
    /// Dongu sinirinda tik olmamasi icin uzunluk tam bar katina yuvarlanir ve
    /// her notaya kisa bir attack/release zarfi uygulanir.
    /// </summary>
    public static class ProceduralMusic
    {
        public const int SampleRate = ProceduralAudio.SampleRate;

        public enum Wave { Square, Saw, Triangle }

        public struct Settings
        {
            public float bpm;
            public int bars;              // dongudeki bar sayisi (4/4)
            public int rootMidi;          // arpej kok notasi
            public Wave arpWave;
            public int arpStepsPerBeat;   // 2 = 1/8, 4 = 1/16
            public bool useBass;
            public bool usePercussion;
            public float masterVolume;
        }

        // A minor: i - VI - III - VII. Nostaljik ve yorulmayan bir dongu;
        // her akor bir bar surer, 4 barda bir tekrarlar.
        static readonly int[][] Progression =
        {
            new[] { 0, 3, 7 },    // Am  (kok + m3 + p5)
            new[] { -4, 0, 3 },   // F
            new[] { 3, 7, 10 },   // C
            new[] { -2, 2, 5 }    // G
        };

        /// <summary>MIDI nota numarasini frekansa cevirir (A4 = 69 = 440 Hz).</summary>
        public static float MidiToHz(float midi)
        {
            return 440f * Mathf.Pow(2f, (midi - 69f) / 12f);
        }

        /// <summary>
        /// Kademe indeksinden muzik parametreleri turetir.
        ///
        /// Ust kademelerde tempo ve arpej yogunlugu artiyor, dalga formu
        /// sertlesiyor — "yeni bir caga gectim" hissi muzikte de duyuluyor.
        /// </summary>
        public static Settings SettingsForTier(int tierIndex)
        {
            var s = new Settings
            {
                // 4 bar = akor dongusunun (Am-F-C-G) tam bir turu.
                //
                // Eskiden 8'di, yani ayni dort akor iki kez uretiliyordu: muzikal
                // olarak hicbir sey eklemeden klip suresini, uretim maliyetini ve
                // bellegi IKIYE katliyordu. Kademe 0'da 24,6 sn / 4,1 MB / ~36 ms
                // yerine artik 12,3 sn / 2,1 MB / ~18 ms.
                bars = 4,
                rootMidi = 57,          // A3
                arpStepsPerBeat = 2,
                arpWave = Wave.Square,
                useBass = true,
                usePercussion = false,
                masterVolume = 0.5f
            };

            if (tierIndex <= 1)
            {
                // Sinyal kulubesi / telefon hatti: yalniz ve yavas.
                s.bpm = 78f;
                s.usePercussion = false;
                s.masterVolume = 0.42f;
            }
            else if (tierIndex <= 3)
            {
                s.bpm = 92f;
                s.usePercussion = true;
            }
            else if (tierIndex <= 5)
            {
                s.bpm = 104f;
                s.arpStepsPerBeat = 4;
                s.usePercussion = true;
            }
            else if (tierIndex <= 7)
            {
                s.bpm = 118f;
                s.arpStepsPerBeat = 4;
                s.arpWave = Wave.Saw;
                s.usePercussion = true;
                s.rootMidi = 60;        // bir oktav yukari: parlaklik
            }
            else
            {
                // Veri merkezi ve sonsuz kademeler.
                s.bpm = 130f;
                s.arpStepsPerBeat = 4;
                s.arpWave = Wave.Saw;
                s.usePercussion = true;
                s.rootMidi = 60;
                s.masterVolume = 0.55f;
            }

            return s;
        }

        /// <summary>
        /// Ayarlara gore donguye uygun bir muzik klibi uretir — tek karede.
        ///
        /// Oyun icinde KULLANILMAZ (bkz. <see cref="BeginLoop"/>); editordeki
        /// onizleme araci gibi kare suresinin onemsiz oldugu yerler icin.
        /// </summary>
        public static AudioClip BuildLoop(string name, Settings s)
        {
            LoopBuild build = BeginLoop(name, s);
            IEnumerator steps = build.Steps();
            while (steps.MoveNext()) { }
            return build.Clip;
        }

        /// <summary>
        /// Muzik uretimini kare kare calistirilabilir hale getirir.
        ///
        /// Neden gerekliydi: uretim olculdugunde masaustunde bar basina ~4-6 ms,
        /// toplamda 32-47 ms suruyordu — 60 fps'in 16,7 ms'lik kare butcesinin
        /// iki-uc kati. Orta seviye bir telefonda bu 100-300 ms'lik gorunur bir
        /// donma demek ve TAM OLARAK kademe atlandigi anda, yani konfetinin
        /// patladigi karede oluyordu. Bir bar bir kareye dagitilinca is ayni
        /// kaliyor ama hicbir kare butceyi asmiyor.
        /// </summary>
        public static LoopBuild BeginLoop(string name, Settings s)
        {
            return new LoopBuild(name, s);
        }

        /// <summary>Adim adim ilerleyen dongu uretimi.</summary>
        public sealed class LoopBuild
        {
            /// <summary>Uretim bitince dolar; bitmeden null.</summary>
            public AudioClip Clip { get; private set; }

            readonly string _name;
            readonly Settings _s;

            internal LoopBuild(string name, Settings s)
            {
                _name = name;
                _s = s;
            }

            /// <summary>
            /// Uretimi kucuk parcalara boler ve her parcadan sonra yield eder.
            ///
            /// Bolme KATMAN basina, bar basina degil. Olcum: bar basina bolmek
            /// en kotu kareyi 21,9 ms'de birakiyordu — masaustunde bile 60 fps
            /// butcesinin (16,7 ms) ustunde. Bir barin uc katmani (bas, arpej,
            /// ritim) ayri adimlara alininca en agir adim ucte birine iniyor.
            /// Normalize ve klip olusturma da kendi adimlarinda.
            /// </summary>
            public IEnumerator Steps()
            {
                float bpm = Mathf.Max(40f, _s.bpm);
                int bars = Mathf.Max(1, _s.bars);

                double secondsPerBeat = 60.0 / bpm;
                int samplesPerBeat = Mathf.RoundToInt((float)(SampleRate * secondsPerBeat));
                int samplesPerBar = samplesPerBeat * 4;
                int total = samplesPerBar * bars;

                var data = new float[total];

                for (int bar = 0; bar < bars; bar++)
                {
                    int[] chord = Progression[bar % Progression.Length];
                    int barStart = bar * samplesPerBar;

                    if (_s.useBass)
                    {
                        RenderBass(data, barStart, samplesPerBar, samplesPerBeat, _s, chord);
                        yield return null;
                    }

                    RenderArp(data, barStart, samplesPerBar, samplesPerBeat, _s, chord);
                    yield return null;

                    if (_s.usePercussion)
                    {
                        RenderPercussion(data, barStart, samplesPerBeat);
                        yield return null;
                    }
                }

                // Ustuste binen katmanlar tavani asabilir; kirpmak yerine olcekle,
                // yoksa distorsiyon duyulur.
                Normalize(data, _s.masterVolume);
                yield return null;

                AudioClip clip = AudioClip.Create(_name, total, 1, SampleRate, false);
                clip.SetData(data, 0);
                Clip = clip;
            }
        }

        // ------------------------------------------------------------------
        // Katmanlar
        // ------------------------------------------------------------------

        /// <summary>Kok nota, iki oktav asagida, her vuruşta bir.</summary>
        static void RenderBass(float[] data, int start, int barLen, int beatLen,
                               Settings s, int[] chord)
        {
            float hz = MidiToHz(s.rootMidi + chord[0] - 24);

            for (int beat = 0; beat < 4; beat++)
            {
                int noteStart = start + beat * beatLen;
                // 1. ve 3. vurus uzun, digerleri kisa — yuruyen bas hissi.
                int noteLen = (beat % 2 == 0) ? (int)(beatLen * 0.85f) : (int)(beatLen * 0.45f);

                AddTone(data, noteStart, noteLen, hz, Wave.Triangle, 0.55f, 0.010f, 0.06f);
            }
        }

        /// <summary>Akor notalarini sirayla dolasan arpej.</summary>
        static void RenderArp(float[] data, int start, int barLen, int beatLen,
                              Settings s, int[] chord)
        {
            int steps = Mathf.Max(1, s.arpStepsPerBeat) * 4;
            int stepLen = barLen / steps;
            if (stepLen <= 1) return;

            for (int i = 0; i < steps; i++)
            {
                // Yukari-asagi dolas: duz yukari tekrar dizisinden daha az yorucu.
                int idx = i % (chord.Length * 2 - 2);
                if (idx >= chord.Length) idx = chord.Length * 2 - 2 - idx;

                float hz = MidiToHz(s.rootMidi + chord[idx]);

                // Vurusun basindaki notalar biraz daha guclu — ritim hissi verir.
                float amp = (i % s.arpStepsPerBeat == 0) ? 0.34f : 0.22f;

                AddTone(data, start + i * stepLen, (int)(stepLen * 0.9f), hz,
                        s.arpWave, amp, 0.004f, 0.05f);
            }
        }

        /// <summary>Kick (alcalan sinus) + hi-hat (kisa gurultu).</summary>
        static void RenderPercussion(float[] data, int start, int beatLen)
        {
            for (int beat = 0; beat < 4; beat++)
            {
                int at = start + beat * beatLen;

                // Kick: 1. ve 3. vurus
                if (beat % 2 == 0) AddKick(data, at, beatLen / 2);

                // Hi-hat: her 1/8
                AddNoise(data, at, beatLen / 12, 0.055f);
                AddNoise(data, at + beatLen / 2, beatLen / 12, 0.040f);
            }
        }

        // ------------------------------------------------------------------
        // Temel sentez
        // ------------------------------------------------------------------

        /// <summary>
        /// Tampona bir nota ekler. Attack/release zarfi tiklamayi onler:
        /// dalga sifirdan baslamazsa hoparlorde "klik" duyulur.
        /// </summary>
        static void AddTone(float[] data, int start, int length, float hz, Wave wave,
                            float amplitude, float attack, float release)
        {
            if (length <= 0 || start >= data.Length) return;

            int attackSamples = Mathf.Max(1, (int)(attack * SampleRate));
            int releaseSamples = Mathf.Max(1, (int)(release * SampleRate));

            double phase = 0.0;
            double step = hz / SampleRate;

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length) break;

                phase += step;
                double frac = phase - System.Math.Floor(phase);

                float sample;
                switch (wave)
                {
                    case Wave.Saw:
                        sample = (float)(frac * 2.0 - 1.0);
                        break;
                    case Wave.Triangle:
                        sample = (float)(4.0 * System.Math.Abs(frac - 0.5) - 1.0);
                        break;
                    default:
                        sample = frac < 0.5 ? 1f : -1f;
                        break;
                }

                float env = 1f;
                if (i < attackSamples) env = i / (float)attackSamples;
                else if (i > length - releaseSamples)
                    env = Mathf.Max(0f, (length - i) / (float)releaseSamples);

                data[index] += sample * amplitude * env;
            }
        }

        static void AddKick(float[] data, int start, int length)
        {
            if (length <= 0) return;

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length) break;

                float t = i / (float)length;
                // Frekans hizla duser: klasik kick.
                float hz = Mathf.Lerp(120f, 45f, Mathf.Sqrt(t));
                float env = Mathf.Exp(-6f * t);

                data[index] += Mathf.Sin(2f * Mathf.PI * hz * (i / (float)SampleRate)) * 0.5f * env;
            }
        }

        static void AddNoise(float[] data, int start, int length, float amplitude)
        {
            if (length <= 0) return;

            // Sabit tohum: her calistirmada ayni dongu uretilsin.
            var rng = new System.Random(start * 31 + length);

            for (int i = 0; i < length; i++)
            {
                int index = start + i;
                if (index >= data.Length) break;

                float env = 1f - (i / (float)length);
                data[index] += (float)(rng.NextDouble() * 2.0 - 1.0) * amplitude * env * env;
            }
        }

        /// <summary>Tepe degeri hedefe olcekler — kirpma yerine kazanc ayari.</summary>
        static void Normalize(float[] data, float target)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > peak) peak = a;
            }

            if (peak <= 0.0001f) return;

            float gain = Mathf.Clamp(target, 0f, 1f) / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }
    }
}
