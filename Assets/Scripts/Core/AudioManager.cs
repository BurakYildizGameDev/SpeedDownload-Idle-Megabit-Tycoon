using System.Collections;
using SpeedDownload.Data;
using UnityEngine;

namespace SpeedDownload.Core
{
    /// <summary>
    /// Sesin tek sahibi (PLAN Bolum 2.5). Klipler acilista bir kez sentezlenir.
    ///
    /// Ibre ugultusu donguli tek bir kaynak; hiza gore pitch ve ses seviyesi
    /// degisiyor — her kare yeni klip calmak yerine tek kaynagi module etmek
    /// hem ucuz hem de kulaga surekli geliyor.
    /// </summary>
    [DefaultExecutionOrder(-60)]
    public class AudioManager : MonoBehaviour
    {
        [Header("Seviyeler")]
        [Range(0f, 1f)] [SerializeField] float clickVolume = 0.35f;
        [Range(0f, 1f)] [SerializeField] float sfxVolume = 0.5f;
        [Range(0f, 1f)] [SerializeField] float whineVolume = 0.22f;

        [Tooltip("Ibre ugultusunun en dusuk/en yuksek pitch'i.")]
        [SerializeField] float whineMinPitch = 0.6f;
        [SerializeField] float whineMaxPitch = 2.4f;

        [Header("Overheat uyarisi")]
        [Tooltip("Isi sayacinin doluluk orani bu esigi gecince uyari bipi baslar.")]
        [Range(0f, 1f)] [SerializeField] float warningStartAt = 0.5f;

        [Tooltip("Esikte bipler arasi sure (sn).")]
        [SerializeField] float warningSlowInterval = 0.55f;

        [Tooltip("Sayac dolmak uzereyken bipler arasi sure (sn).")]
        [SerializeField] float warningFastInterval = 0.12f;

        [Tooltip("Arka plan muzigi yuvasi. Gercek klip verilirse O calinir ve " +
                 "prosedurel muzik devre disi kalir. Bos birakmak beklenen durum.")]
        [SerializeField] AudioClip backgroundMusic;
        [Range(0f, 1f)] [SerializeField] float musicVolume = 0.3f;

        [Header("Prosedurel muzik")]
        [Tooltip("Kapali: hic muzik uretilmez (denge testi icin sessizlik).")]
        [SerializeField] bool proceduralMusicEnabled = true;

        [Tooltip("Kademe degisiminde muzigin yeni parcaya gecis suresi (sn).")]
        [SerializeField] float musicCrossfade = 1.2f;

        AudioSource _oneShot;

        /// <summary>
        /// Overheat uyari bipi icin AYRI kaynak.
        ///
        /// <see cref="AudioSource.PlayOneShot"/> kaynagin O ANKI pitch'ini
        /// kullanir ve pitch degisince HALA CALAN sesler de kayar. Uyari bipi
        /// tehlikeye gore 1.0-1.35 arasi pitch aliyordu, tiklama sesi ise her
        /// seferinde 0.94-1.08 arasi rastgele bir deger yaziyordu: bip caldigi
        /// sirada bir tiklama olunca bip ortasinda perde degistiriyordu.
        /// </summary>
        AudioSource _warningSource;

        AudioSource _whine;
        AudioSource _music;

        AudioClip _click;
        AudioClip _critical;
        AudioClip _complete;
        AudioClip _overheat;
        AudioClip _tierUp;
        AudioClip _purchase;
        AudioClip _warning;
        AudioClip _holoDrop;
        AudioClip _comboMilestone;

        SpeedController _speed;
        DownloadController _download;
        EconomyManager _economy;
        CollectionManager _collection;
        GameManager _gm;

        float _warningTimer;

        void Awake()
        {
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _oneShot.spatialBlend = 0f;

            _warningSource = gameObject.AddComponent<AudioSource>();
            _warningSource.playOnAwake = false;
            _warningSource.spatialBlend = 0f;

            _whine = gameObject.AddComponent<AudioSource>();
            _whine.playOnAwake = false;
            _whine.loop = true;
            _whine.spatialBlend = 0f;
            _whine.volume = 0f;

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.volume = musicVolume;

            BuildClips();
        }

        void BuildClips()
        {
            _click = ProceduralAudio.Square("sfx_click", 880f, 0.08f, 0.5f, 26f);
            _critical = ProceduralAudio.Arpeggio("sfx_critical",
                new float[] { 880f, 1174f, 1568f }, 0.05f, 0.5f);

            // Do-Mi-Sol: tamamlanma jingle'i
            _complete = ProceduralAudio.Arpeggio("sfx_complete",
                new float[] { 523.25f, 659.25f, 783.99f }, 0.07f, 0.45f);

            _overheat = ProceduralAudio.Sweep("sfx_overheat", 260f, 90f, 0.55f, 0.5f, 0.35f);

            _tierUp = ProceduralAudio.Arpeggio("sfx_tierup",
                new float[] { 523.25f, 587.33f, 659.25f, 783.99f, 1046.5f }, 0.075f, 0.45f);

            _purchase = ProceduralAudio.Square("sfx_purchase", 1318.5f, 0.10f, 0.4f, 18f);

            // Overheat yaklasma bipi — kisa ve keskin, sayac doldukca sikliyor.
            _warning = ProceduralAudio.Square("sfx_warning", 1760f, 0.05f, 0.5f, 40f);

            // Holo altin dosya dusu arpeji (parilti efekti)
            _holoDrop = ProceduralAudio.Arpeggio("sfx_holo",
                new float[] { 1046.5f, 1318.5f, 1567.98f, 1975.53f, 2093.0f }, 0.06f, 0.55f);

            // Kombo esigi zafer bip'i
            _comboMilestone = ProceduralAudio.Arpeggio("sfx_combo",
                new float[] { 880f, 1108.73f, 1318.51f }, 0.045f, 0.45f);

            _whine.clip = ProceduralAudio.LoopingSaw("sfx_whine", 220f, whineVolume);
        }

        void Start()
        {
            _gm = GameManager.Instance;
            if (_gm == null) { enabled = false; return; }

            _speed = _gm.Speed;
            _download = _gm.Download;
            _economy = _gm.Economy;
            _collection = _gm.Collection;

            if (_speed != null)
            {
                _speed.Clicked += OnClicked;
                _speed.CriticalHit += OnCritical;
                _speed.OverheatStarted += OnOverheat;
                _speed.ComboUpdated += OnComboUpdated;
            }

            if (_download != null) _download.FileCompleted += OnFileCompleted;
            if (_economy != null) _economy.Purchased += OnPurchased;
            if (_collection != null) _collection.HoloDropped += OnHoloDropped;
            _gm.TierChanged += OnTierChanged;

            if (backgroundMusic != null)
            {
                // Gercek klip verilmisse prosedurel uretim devreye girmez.
                _music.clip = backgroundMusic;
                _music.volume = musicVolume;
                _music.Play();
            }
            else if (proceduralMusicEnabled)
            {
                ApplyTierMusic(_gm.CurrentTierIndex, false);
            }

            _whine.Play();
        }

        void OnDestroy()
        {
            if (_speed != null)
            {
                _speed.Clicked -= OnClicked;
                _speed.CriticalHit -= OnCritical;
                _speed.OverheatStarted -= OnOverheat;
                _speed.ComboUpdated -= OnComboUpdated;
            }

            if (_download != null) _download.FileCompleted -= OnFileCompleted;
            if (_economy != null) _economy.Purchased -= OnPurchased;
            if (_collection != null) _collection.HoloDropped -= OnHoloDropped;
            if (_gm != null) _gm.TierChanged -= OnTierChanged;

            // Calisma aninda uretilen klipler asset degil; elle birakilmalari gerekiyor.
            if (_proceduralTrack != null) Destroy(_proceduralTrack);
            if (_pendingTrack != null) Destroy(_pendingTrack);
        }

        float _whinePitch = -1f;
        float _whineVolume = -1f;

        void Update()
        {
            if (_speed == null || _whine == null) return;

            // Ugultu yalnizca ibre tabanin uzerindeyken duyulsun.
            float t = _speed.IsOverheated ? 0f : _speed.NormalizedT;

            // AudioSource.pitch/volume yerli (native) cagrilar. Her karede
            // duyulamayacak kadar kucuk farklarla yazmak bos yere is;
            // 0,01'lik esik kulaga fark ettirmiyor ama cagri sayisini
            // kadranin gercekten hareket ettigi karelere indiriyor.
            float pitch = Mathf.Lerp(whineMinPitch, whineMaxPitch, t);
            if (Mathf.Abs(pitch - _whinePitch) > 0.01f)
            {
                _whinePitch = pitch;
                _whine.pitch = pitch;
            }

            float volume = whineVolume * Mathf.Clamp01(t * 1.4f);
            if (Mathf.Abs(volume - _whineVolume) > 0.005f)
            {
                _whineVolume = volume;
                _whine.volume = volume;
            }

            TickOverheatWarning(Time.deltaTime);
            TickMusicFade(Time.deltaTime);
        }

        /// <summary>
        /// Overheat sayaci dolarken hizlanan uyari bipi.
        ///
        /// Onceki surumde bu ses redline'a GIRINCE calmaya basliyordu; ama redline
        /// bolgesi kadranin sag yarisinin tamami (t >= 0.50) oldugu icin oyuncu
        /// zamanin cogunu orada geciriyor ve bip surekli otuyordu. Artik olcut
        /// redline degil, gercek tehlike: isi sayacinin doluluk orani.
        /// </summary>
        void TickOverheatWarning(float dt)
        {
            if (_speed.IsOverheated)
            {
                _warningTimer = 0f;
                return;
            }

            float heat = _speed.OverheatProgress;
            if (heat < warningStartAt)
            {
                _warningTimer = 0f;
                return;
            }

            // Sayac doldukca hem aralik kisalir hem pitch yukselir — Geiger
            // sayaci hissi, oyuncu bakmadan da tehlikeyi duyuyor.
            float k = Mathf.InverseLerp(warningStartAt, 1f, heat);

            _warningTimer -= dt;
            if (_warningTimer > 0f) return;

            _warningTimer = Mathf.Lerp(warningSlowInterval, warningFastInterval, k);

            if (_warning != null && _warningSource != null)
            {
                _warningSource.pitch = Mathf.Lerp(1f, 1.35f, k);
                _warningSource.PlayOneShot(_warning, sfxVolume * 0.7f);
            }
        }

        void OnClicked()
        {
            if (_click != null && _oneShot != null)
            {
                float pitch = UnityEngine.Random.Range(0.94f, 1.08f);
                if (_speed != null && _speed.NormalizedT > 0.5f)
                {
                    pitch += (_speed.NormalizedT - 0.5f) * 0.3f;
                }
                _oneShot.pitch = pitch;
                _oneShot.PlayOneShot(_click, clickVolume);
            }
        }

        void OnCritical()
        {
            if (_critical != null && _oneShot != null)
            {
                _oneShot.pitch = 1.0f;
                _oneShot.PlayOneShot(_critical, sfxVolume);
            }
        }

        void OnOverheat()
        {
            if (_overheat != null && _oneShot != null)
            {
                _oneShot.pitch = 1.0f;
                _oneShot.PlayOneShot(_overheat, sfxVolume);
            }
        }

        void OnPurchased(UpgradeSO upgrade)
        {
            if (_purchase != null && _oneShot != null)
            {
                _oneShot.pitch = 1.0f;
                _oneShot.PlayOneShot(_purchase, sfxVolume);
            }
        }

        void OnFileCompleted(FileDataSO file, double reward)
        {
            if (_complete != null && _oneShot != null)
            {
                _oneShot.pitch = 1.0f;
                _oneShot.PlayOneShot(_complete, sfxVolume);
            }
        }

        void OnHoloDropped(FileDataSO file, double bonus)
        {
            if (_holoDrop != null && _oneShot != null)
            {
                _oneShot.pitch = 1.0f;
                _oneShot.PlayOneShot(_holoDrop, sfxVolume * 0.95f);
            }
        }

        void OnComboUpdated(int combo, float multiplier)
        {
            // Kombo esiklerinde zafer arpeji (10, 25, 50)
            if ((combo == 10 || combo == 25 || combo == 50) && _comboMilestone != null && _oneShot != null)
            {
                _oneShot.pitch = combo == 50 ? 1.25f : (combo == 25 ? 1.1f : 1.0f);
                _oneShot.PlayOneShot(_comboMilestone, sfxVolume * 0.75f);
            }
        }

        void OnTierChanged(ConnectionTierSO tier)
        {
            // Fanfar yalnizca gercek bir kademe atlamasinda.
            //
            // Kayittan yukleme (her acilis) ve prestij sifirlamasi da
            // TierChanged yayinliyor. Bu kontrol olmadan oyun her acildiginda
            // kutlama sesi caliyor, prestijde ise kademe 8'den 0'a DUSERKEN
            // ayni fanfar otuyordu.
            GameManager gm = GameManager.Instance;
            bool celebrate = gm == null || gm.IsCelebratedTierChange;

            if (celebrate && _tierUp != null && _oneShot != null)
            {
                _oneShot.pitch = 1.0f;
                _oneShot.PlayOneShot(_tierUp, sfxVolume);
            }

            // Muzik gecisi sebepten BAGIMSIZ: kayittan donen oyuncu da,
            // prestij yapan oyuncu da bulundugu kademenin muzigini duymali.
            if (backgroundMusic == null && proceduralMusicEnabled && tier != null)
                ApplyTierMusic(tier.tierIndex, true);
        }

        // ------------------------------------------------------------------
        // Prosedurel muzik
        //
        // Tek klip tutuluyor: bir dongu ~20 sn ve 44,1 kHz'de birkac MB yer
        // kapliyor, dokuz kademeyi birden bellekte tutmak gereksiz. Kademe
        // degisince yenisi uretilir, eskisi yok edilir.
        // ------------------------------------------------------------------

        AudioClip _proceduralTrack;
        int _musicTierIndex = -1;

        // Capraz gecis durumu
        AudioClip _pendingTrack;
        float _fadeTimer;
        bool _fadingOut;
        bool _fadingIn;

        Coroutine _buildRoutine;

        void ApplyTierMusic(int tierIndex, bool crossfade)
        {
            // Muzik kademe GRUPLARINA gore degisiyor; her kademede yeniden
            // sentezlemek hem gereksiz hem de duyulabilir bir kesinti olurdu.
            int group = MusicGroupOf(tierIndex);
            if (group == _musicTierIndex) return;
            _musicTierIndex = group;

            // Onceki uretim yarida kalabilir: henuz AudioClip olusmadigi icin
            // atilan tek sey bir float dizisi.
            if (_buildRoutine != null) StopCoroutine(_buildRoutine);
            _buildRoutine = StartCoroutine(BuildAndApply(tierIndex, group, crossfade));
        }

        /// <summary>
        /// Yeni parcayi kare kare uretir, hazir olunca devreye alir.
        ///
        /// Onemli ayrinti: kisilma HEMEN basliyor, uretimi beklemiyor. Boylece
        /// oyuncunun duydugu sey "eski parca kisildi, yenisi geldi" oluyor;
        /// uretim suresi zaten sessize yakin gecen bu araliga saklaniyor.
        /// </summary>
        IEnumerator BuildAndApply(int tierIndex, int group, bool crossfade)
        {
            bool fade = crossfade && _music.clip != null;
            if (fade)
            {
                // Onceki gecisten devrilmemis bir parca kalmis olabilir (iki
                // kademe atlamasi tek capraz gecise denk gelirse). Referansini
                // birakmadan once yok et, yoksa ~2 MB'lik klip bellekte kalir.
                if (_pendingTrack != null) Destroy(_pendingTrack);

                _pendingTrack = null;
                _fadingOut = true;
                _fadingIn = false;
                _fadeTimer = 0f;
            }

            ProceduralMusic.LoopBuild build = ProceduralMusic.BeginLoop(
                "bgm_tier" + group, ProceduralMusic.SettingsForTier(tierIndex));

            IEnumerator steps = build.Steps();
            while (steps.MoveNext()) yield return null;

            if (!fade)
            {
                SwapTrack(build.Clip);
                _music.volume = musicVolume;
            }
            else
            {
                // TickMusicFade kisilma bitmisse burada devri alir; bitmemisse
                // kendi zamaninda alacak.
                _pendingTrack = build.Clip;
            }

            _buildRoutine = null;
        }

        /// <summary>Ayni muzigi paylasan kademe grubu (SettingsForTier ile ayni esikler).</summary>
        static int MusicGroupOf(int tierIndex)
        {
            if (tierIndex <= 1) return 0;
            if (tierIndex <= 3) return 1;
            if (tierIndex <= 5) return 2;
            if (tierIndex <= 7) return 3;
            return 4;
        }

        void SwapTrack(AudioClip track)
        {
            _music.Stop();

            // Onceki dongu calisma aninda uretilmisti; birakmazsak bellekte kalir.
            if (_proceduralTrack != null) Destroy(_proceduralTrack);

            _proceduralTrack = track;
            _music.clip = track;
            _music.loop = true;
            _music.Play();
        }

        void TickMusicFade(float dt)
        {
            if (!_fadingOut && !_fadingIn) return;

            // Toplam gecis suresi iki yariya bolunuyor: once kis, sonra ac.
            float duration = Mathf.Max(0.05f, musicCrossfade * 0.5f);
            _fadeTimer += dt;
            float k = Mathf.Clamp01(_fadeTimer / duration);

            if (_fadingOut)
            {
                _music.volume = musicVolume * (1f - k);
                if (k < 1f) return;

                // Parca hala uretiliyorsa sessizde bekle. Null bir klibe gecmek
                // muzigi tamamen susturur ve geri getirecek kimse olmaz —
                // uretim kare kare yapildigi icin bu yaris gercek.
                if (_pendingTrack == null)
                {
                    _music.volume = 0f;
                    return;
                }

                SwapTrack(_pendingTrack);
                _pendingTrack = null;

                _fadingOut = false;
                _fadingIn = true;
                _fadeTimer = 0f;
                _music.volume = 0f;
                return;
            }

            _music.volume = musicVolume * k;

            if (k >= 1f)
            {
                _fadingIn = false;
                _fadeTimer = 0f;
                _music.volume = musicVolume;
            }
        }

        void Play(AudioClip clip, float volume)
        {
            if (clip == null || _oneShot == null) return;
            _oneShot.pitch = 1.0f;
            _oneShot.PlayOneShot(clip, volume);
        }

        /// <summary>
        /// Bir oyun olayina bagli olmayan odul sesi — offline kazanc penceresi ve
        /// yuzen olay odulu gibi UI tetikli anlar icin.
        ///
        /// UI'nin kendi AudioSource'unu kurmasindansa sesi tek yerden istemesi,
        /// iki ses sisteminin yan yana yasamasi sorununun tekrarlanmasini onler.
        /// </summary>
        public void PlayReward()
        {
            Play(_complete, sfxVolume);
        }
    }
}
