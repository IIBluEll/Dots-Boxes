using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class ButtonSound : MonoBehaviour
    {
        [SerializeField] private AudioCueData _clickCue;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void OnEnable()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(OnButtonClicked);
            }
        }

        private void OnDisable()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void OnButtonClicked()
        {
            if (AudioProvider.HasInstance)
            {
                AudioProvider.Instance.PlayUiSfx(_clickCue);
            }
        }
    }
}
