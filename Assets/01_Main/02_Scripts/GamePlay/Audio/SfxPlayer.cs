using UnityEngine;

namespace DotsAndBoxes.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource _sfxSource;
        [SerializeField] private AudioSource _uiSource;

        private float _outputVolume = 1f;

        private void Awake()
        {
            ConfigureSource(_sfxSource , false);
            ConfigureSource(_uiSource , true);
        }

        public void PlaySfx(AudioCueData cueData)
        {
            PlayOneShot(_sfxSource , cueData);
        }

        public void PlayUiSfx(AudioCueData cueData)
        {
            PlayOneShot(_uiSource , cueData);
        }

        public void StopAll()
        {
            _sfxSource?.Stop();
            _uiSource?.Stop();
        }

        public void SetOutputVolume(float normalizedVolume)
        {
            _outputVolume = Mathf.Clamp01(normalizedVolume);
        }

        private void PlayOneShot(AudioSource audioSource , AudioCueData cueData)
        {
            if (audioSource == null || cueData == null || !cueData.IsValid)
            {
                return;
            }

            audioSource.PlayOneShot(cueData.Clip , cueData.Volume * _outputVolume);
        }

        private void ConfigureSource(AudioSource audioSource , bool ignoreListenerPause)
        {
            if (audioSource == null)
            {
                return;
            }

            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.ignoreListenerPause = ignoreListenerPause;
        }
    }
}
