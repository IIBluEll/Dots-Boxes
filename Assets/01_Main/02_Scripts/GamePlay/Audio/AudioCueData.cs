using UnityEngine;

namespace DotsAndBoxes.Gameplay.Audio
{
    [CreateAssetMenu(fileName = "AudioCue" , menuName = "Dots And Boxes/Audio/Audio Cue")]
    public sealed class AudioCueData : ScriptableObject
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField , Range(0f , 1f)] private float _volume = 1f;

        public AudioClip Clip => _clip;
        public float Volume => _volume;
        public bool IsValid => _clip != null;
    }
}
