using UnityEngine;
using UnityEngine.Audio;

namespace DotsAndBoxes.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioProvider : MonoBehaviour
    {
        private const string MASTER_VOLUME_PARAMETER = "MasterVolume";
        private const string BGM_VOLUME_PARAMETER = "BgmVolume";
        private const string SFX_VOLUME_PARAMETER = "SfxVolume";

        private const string MASTER_VOLUME_KEY = "Audio.MasterVolume";
        private const string BGM_VOLUME_KEY = "Audio.BgmVolume";
        private const string SFX_VOLUME_KEY = "Audio.SfxVolume";

        private const float DEFAULT_BGM_VOLUME = 0.3f;
        private const float DEFAULT_VOLUME = 1f;
        private const float MINIMUM_DECIBELS = -80f;
        private const float MINIMUM_LINEAR_VOLUME = 0.0001f;

        [Header("Players")]
        [SerializeField] private BgmPlaylistPlayer _bgmPlaylistPlayer;
        [SerializeField] private SfxPlayer _sfxPlayer;

        [Header("Mixer")]
        [SerializeField] private AudioMixer _audioMixer;

        private float _masterVolume = DEFAULT_VOLUME;
        private float _bgmVolume = DEFAULT_BGM_VOLUME;
        private float _sfxVolume = DEFAULT_VOLUME;

        public static AudioProvider Instance { get; private set; }

        public static bool HasInstance => Instance != null;

        public float MasterVolume => _masterVolume;
        public float BgmVolume => _bgmVolume;
        public float SfxVolume => _sfxVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            ValidateReferences();
            LoadVolumeSettings();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                PlayerPrefs.Save();
            }
        }

        private void OnApplicationQuit()
        {
            PlayerPrefs.Save();
        }

        public void PlayPlaylist()
        {
            _bgmPlaylistPlayer?.PlayPlaylist();
        }

        public void PauseBgm()
        {
            _bgmPlaylistPlayer?.Pause();
        }

        public void ResumeBgm()
        {
            _bgmPlaylistPlayer?.Resume();
        }

        public void StopBgm()
        {
            _bgmPlaylistPlayer?.StopPlaylist();
        }

        public void SkipBgm()
        {
            _bgmPlaylistPlayer?.Skip();
        }

        public void SetBgmPlayMode(BGM_PLAY_MODE_ENUM playMode)
        {
            _bgmPlaylistPlayer?.SetPlayMode(playMode);
        }

        public void PlaySfx(AudioCueData cueData)
        {
            _sfxPlayer?.PlaySfx(cueData);
        }

        public void PlayUiSfx(AudioCueData cueData)
        {
            _sfxPlayer?.PlayUiSfx(cueData);
        }

        public void SetMasterVolume(float normalizedVolume)
        {
            _masterVolume = Mathf.Clamp01(normalizedVolume);
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY , _masterVolume);
            ApplyVolumes();
        }

        public void SetBgmVolume(float normalizedVolume)
        {
            _bgmVolume = Mathf.Clamp01(normalizedVolume);
            PlayerPrefs.SetFloat(BGM_VOLUME_KEY , _bgmVolume);
            ApplyVolumes();
        }

        public void SetSfxVolume(float normalizedVolume)
        {
            _sfxVolume = Mathf.Clamp01(normalizedVolume);
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY , _sfxVolume);
            ApplyVolumes();
        }

        public void SaveVolumeSettings()
        {
            PlayerPrefs.Save();
        }

        private void LoadVolumeSettings()
        {
            _masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY , DEFAULT_VOLUME);
            _bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY , DEFAULT_BGM_VOLUME);
            _sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY , DEFAULT_VOLUME);

            ApplyVolumes();
        }

        private void ApplyVolumes()
        {
            bool isMixerControlAvailable = TryApplyMixerVolumes();

            float bgmOutputVolume = isMixerControlAvailable
                ? 1f
                : _masterVolume * _bgmVolume;

            float sfxOutputVolume = isMixerControlAvailable
                ? 1f
                : _masterVolume * _sfxVolume;

            _bgmPlaylistPlayer?.SetOutputVolume(bgmOutputVolume);
            _sfxPlayer?.SetOutputVolume(sfxOutputVolume);
        }

        private bool TryApplyMixerVolumes()
        {
            if (_audioMixer == null)
            {
                return false;
            }

            bool wasMasterApplied = _audioMixer.SetFloat(
                MASTER_VOLUME_PARAMETER ,
                ConvertToDecibels(_masterVolume));

            bool wasBgmApplied = _audioMixer.SetFloat(
                BGM_VOLUME_PARAMETER ,
                ConvertToDecibels(_bgmVolume));

            bool wasSfxApplied = _audioMixer.SetFloat(
                SFX_VOLUME_PARAMETER ,
                ConvertToDecibels(_sfxVolume));

            return wasMasterApplied && wasBgmApplied && wasSfxApplied;
        }

        private static float ConvertToDecibels(float normalizedVolume)
        {
            return normalizedVolume <= 0f
                ? MINIMUM_DECIBELS
                : Mathf.Log10(Mathf.Max(normalizedVolume , MINIMUM_LINEAR_VOLUME)) * 20f;
        }

        private void ValidateReferences()
        {
            if (_bgmPlaylistPlayer == null)
            {
                Debug.LogError("BgmPlaylistPlayer가 연결되지 않았습니다." , this);
            }

            if (_sfxPlayer == null)
            {
                Debug.LogError("SfxPlayer가 연결되지 않았습니다." , this);
            }

        }
    }
}
