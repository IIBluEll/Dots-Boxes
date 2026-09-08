using System.Collections.Generic;
using UnityEngine;

namespace DotsAndBoxes.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public sealed class BgmPlaylistPlayer : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource _bgmSourceA;
        [SerializeField] private AudioSource _bgmSourceB;

        [Header("Playlist")]
        [SerializeField] private List<AudioClip> _bgmClips = new();
        [SerializeField] private BGM_PLAY_MODE_ENUM _playMode;
        [SerializeField] private bool _playOnStart = true;

        [Header("Transition")]
        [SerializeField , Min(0f)] private float _crossFadeDuration = 1.5f;

        private readonly List<int> _shuffleOrder = new();

        private AudioSource _currentSource;
        private AudioSource _standbySource;

        private int _currentClipIndex = -1;
        private int _shufflePosition;

        private float _crossFadeElapsed;
        private float _activeCrossFadeDuration;
        private float _outputVolume = 1f;

        private bool _isRunning;
        private bool _isPaused;
        private bool _isCrossFading;
        private bool _skipRequested;

        public bool IsRunning => _isRunning;
        public bool IsPaused => _isPaused;
        public AudioClip CurrentClip => _currentSource != null ? _currentSource.clip : null;

        private void Awake()
        {
            _currentSource = _bgmSourceA;
            _standbySource = _bgmSourceB;

            ConfigureSource(_bgmSourceA);
            ConfigureSource(_bgmSourceB);
        }

        private void Start()
        {
            if (_playOnStart)
            {
                PlayPlaylist();
            }
        }

        private void Update()
        {
            if (!_isRunning || _isPaused)
            {
                return;
            }

            if (_isCrossFading)
            {
                UpdateCrossFade();
                return;
            }

            if (_skipRequested)
            {
                BeginTransition();
                return;
            }

            if (_currentSource == null || !_currentSource.isPlaying)
            {
                PlayNextImmediately();
                return;
            }

            float allowedFadeDuration = GetAllowedFadeDuration(_currentSource.clip);

            if (allowedFadeDuration <= 0f)
            {
                return;
            }

            float remainingTime = GetRemainingTime(_currentSource);

            if (remainingTime <= allowedFadeDuration)
            {
                BeginTransition();
            }
        }

        public void PlayPlaylist()
        {
            if (_isRunning)
            {
                if (_isPaused)
                {
                    Resume();
                }

                return;
            }

            if (_bgmSourceA == null || _bgmSourceB == null)
            {
                Debug.LogError("BGM 재생에 서로 다른 AudioSource 두 개가 필요합니다." , this);
                return;
            }

            if (!HasValidClip())
            {
                Debug.LogWarning("BGM 플레이리스트에 재생 가능한 AudioClip이 없습니다." , this);
                return;
            }

            _isRunning = true;
            _isPaused = false;
            _isCrossFading = false;
            _skipRequested = false;

            PlayNextImmediately();
        }

        public void Pause()
        {
            if (!_isRunning || _isPaused)
            {
                return;
            }

            _isPaused = true;

            _currentSource?.Pause();
            _standbySource?.Pause();
        }

        public void Resume()
        {
            if (!_isRunning || !_isPaused)
            {
                return;
            }

            _isPaused = false;

            _currentSource?.UnPause();

            if (_isCrossFading)
            {
                _standbySource?.UnPause();
            }
        }

        public void StopPlaylist()
        {
            _currentSource?.Stop();
            _standbySource?.Stop();

            ResetSourceVolume(_currentSource);
            ResetSourceVolume(_standbySource);

            _isRunning = false;
            _isPaused = false;
            _isCrossFading = false;
            _skipRequested = false;

            _currentClipIndex = -1;
            _shufflePosition = 0;
            _shuffleOrder.Clear();
        }

        public void Skip()
        {
            if (_isRunning)
            {
                _skipRequested = true;
            }
        }

        public void SetPlayMode(BGM_PLAY_MODE_ENUM playMode)
        {
            if (_playMode == playMode)
            {
                return;
            }

            _playMode = playMode;
            _shufflePosition = 0;
            _shuffleOrder.Clear();
        }

        public void SetOutputVolume(float normalizedVolume)
        {
            _outputVolume = Mathf.Clamp01(normalizedVolume);

            if (_isCrossFading && _activeCrossFadeDuration > 0f)
            {
                float progress = Mathf.Clamp01(_crossFadeElapsed / _activeCrossFadeDuration);

                if (_currentSource != null)
                {
                    _currentSource.volume = (1f - progress) * _outputVolume;
                }

                if (_standbySource != null)
                {
                    _standbySource.volume = progress * _outputVolume;
                }

                return;
            }

            if (_currentSource != null)
            {
                _currentSource.volume = _outputVolume;
            }
        }

        private void BeginTransition()
        {
            _skipRequested = false;

            AudioClip nextClip = GetNextClip();

            if (nextClip == null)
            {
                StopPlaylist();
                return;
            }

            float remainingTime = GetRemainingTime(_currentSource);

            _activeCrossFadeDuration = Mathf.Min(
                _crossFadeDuration ,
                remainingTime ,
                nextClip.length * 0.5f);

            if (_currentSource == null || !_currentSource.isPlaying || _activeCrossFadeDuration <= 0f)
            {
                PlayClip(_standbySource , nextClip , 1f);
                _currentSource?.Stop();
                SwapSources();
                return;
            }

            PlayClip(_standbySource , nextClip , 0f);

            _crossFadeElapsed = 0f;
            _isCrossFading = true;
        }

        private void UpdateCrossFade()
        {
            if (_activeCrossFadeDuration <= 0f)
            {
                CompleteCrossFade();
                return;
            }

            _crossFadeElapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(_crossFadeElapsed / _activeCrossFadeDuration);

            if (_currentSource != null)
            {
                _currentSource.volume = (1f - progress) * _outputVolume;
            }

            if (_standbySource != null)
            {
                _standbySource.volume = progress * _outputVolume;
            }

            if (progress >= 1f)
            {
                CompleteCrossFade();
            }
        }

        private void CompleteCrossFade()
        {
            if (_currentSource != null)
            {
                _currentSource.Stop();
                _currentSource.volume = 0f;
            }

            if (_standbySource != null)
            {
                _standbySource.volume = _outputVolume;
            }

            SwapSources();

            _isCrossFading = false;
            _crossFadeElapsed = 0f;
            _activeCrossFadeDuration = 0f;
        }

        private void PlayNextImmediately()
        {
            AudioClip nextClip = GetNextClip();

            if (nextClip == null)
            {
                StopPlaylist();
                return;
            }

            PlayClip(_currentSource , nextClip , 1f);
        }

        private AudioClip GetNextClip()
        {
            return _playMode == BGM_PLAY_MODE_ENUM.SHUFFLE
                ? GetNextShuffleClip()
                : GetNextSequentialClip();
        }

        private AudioClip GetNextSequentialClip()
        {
            if (_bgmClips == null || _bgmClips.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < _bgmClips.Count; i++)
            {
                _currentClipIndex = (_currentClipIndex + 1) % _bgmClips.Count;

                AudioClip clip = _bgmClips[_currentClipIndex];

                if (clip != null)
                {
                    return clip;
                }
            }

            return null;
        }

        private AudioClip GetNextShuffleClip()
        {
            if (_bgmClips == null || _bgmClips.Count == 0)
            {
                return null;
            }

            if (_shufflePosition >= _shuffleOrder.Count)
            {
                BuildShuffleOrder();
            }

            if (_shuffleOrder.Count == 0)
            {
                return null;
            }

            int clipIndex = _shuffleOrder[_shufflePosition];

            _shufflePosition++;
            _currentClipIndex = clipIndex;

            return _bgmClips[clipIndex];
        }

        private void BuildShuffleOrder()
        {
            _shuffleOrder.Clear();
            _shufflePosition = 0;

            for (int i = 0; i < _bgmClips.Count; i++)
            {
                if (_bgmClips[i] != null)
                {
                    _shuffleOrder.Add(i);
                }
            }

            for (int i = _shuffleOrder.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0 , i + 1);

                (_shuffleOrder[i] , _shuffleOrder[randomIndex]) =
                    (_shuffleOrder[randomIndex] , _shuffleOrder[i]);
            }

            PreventImmediateRepeat();
        }

        private void PreventImmediateRepeat()
        {
            if (_shuffleOrder.Count <= 1 || _shuffleOrder[0] != _currentClipIndex)
            {
                return;
            }

            int swapIndex = Random.Range(1 , _shuffleOrder.Count);

            (_shuffleOrder[0] , _shuffleOrder[swapIndex]) =
                (_shuffleOrder[swapIndex] , _shuffleOrder[0]);
        }

        private void SwapSources()
        {
            (_currentSource , _standbySource) = (_standbySource , _currentSource);
        }

        private void PlayClip(AudioSource audioSource , AudioClip clip , float volume)
        {
            if (audioSource == null || clip == null)
            {
                return;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = volume * _outputVolume;
            audioSource.Play();
        }

        private void ConfigureSource(AudioSource audioSource)
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.volume = _outputVolume;
        }

        private void ResetSourceVolume(AudioSource audioSource)
        {
            if (audioSource != null)
            {
                audioSource.volume = _outputVolume;
            }
        }

        private float GetAllowedFadeDuration(AudioClip clip)
        {
            if (clip == null)
            {
                return 0f;
            }

            return Mathf.Min(_crossFadeDuration , clip.length * 0.5f);
        }

        private float GetRemainingTime(AudioSource audioSource)
        {
            if (audioSource == null || audioSource.clip == null)
            {
                return 0f;
            }

            return Mathf.Max(0f , audioSource.clip.length - audioSource.time);
        }

        private bool HasValidClip()
        {
            if (_bgmClips == null)
            {
                return false;
            }

            for (int i = 0; i < _bgmClips.Count; i++)
            {
                if (_bgmClips[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _crossFadeDuration = Mathf.Max(0f , _crossFadeDuration);

            if (_bgmSourceA != null && _bgmSourceA == _bgmSourceB)
            {
                Debug.LogWarning("BGM Source A와 B에는 서로 다른 AudioSource가 필요합니다." , this);
            }
        }
#endif
    }
}
