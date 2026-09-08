using UnityEngine;

namespace DotsAndBoxes.Gameplay.Audio
{
    [DisallowMultipleComponent]
    public sealed class GameBoardAudioFeedback : MonoBehaviour
    {
        [Header("Board")]
        [SerializeField] private AudioCueData _edgePreviewCue;
        [SerializeField] private AudioCueData _edgeConfirmedCue;
        [SerializeField] private AudioCueData _boxCompletedCue;

        [Header("Turn")]
        [SerializeField] private AudioCueData _turnChangedCue;
        [SerializeField] private AudioCueData _timeWarningCue;

        [Header("Result")]
        [SerializeField] private AudioCueData _winCue;
        [SerializeField] private AudioCueData _loseCue;
        [SerializeField] private AudioCueData _drawCue;

        public void PlayEdgePreview()
        {
            Play(_edgePreviewCue);
        }

        public void PlayEdgeConfirmed()
        {
            Play(_edgeConfirmedCue);
        }

        public void PlayBoxCompleted()
        {
            Play(_boxCompletedCue);
        }

        public void PlayTurnChanged()
        {
            Play(_turnChangedCue);
        }

        public void PlayTimeWarning()
        {
            Play(_timeWarningCue);
        }

        public void PlayWin()
        {
            Play(_winCue);
        }

        public void PlayLose()
        {
            Play(_loseCue);
        }

        public void PlayDraw()
        {
            Play(_drawCue);
        }

        private void Play(AudioCueData cueData)
        {
            if (AudioProvider.HasInstance)
            {
                AudioProvider.Instance.PlaySfx(cueData);
            }
        }
    }
}
