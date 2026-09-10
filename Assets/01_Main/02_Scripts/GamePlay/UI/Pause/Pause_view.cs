using System;
using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Pause_view : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _pausePopupObj;

        [Space(5f), Header("Buttons")]
        [SerializeField] private Button _pauseBtn;
        [SerializeField] private Button _continueBtn;
        [SerializeField] private Button _exitBtn;

        private bool _isInitialized;

        public event Action PauseRequested;
        public event Action ContinueRequested;
        public event Action ExitRequested;

        public bool Initialize()
        {
            if ( _isInitialized )
            {
                return true;
            }

            if ( !ValidateReferences() )
            {
                enabled = false;
                return false;
            }

            _pauseBtn.onClick.AddListener(OnPauseButtonActioned);
            _continueBtn.onClick.AddListener(OnContinueButtonActioned);
            _exitBtn.onClick.AddListener(OnExitButtonActioned);

            _isInitialized = true;
            Close();
            return true;
        }

        private void OnDestroy()
        {
            if ( !_isInitialized )
            {
                return;
            }

            _pauseBtn.onClick.RemoveListener(OnPauseButtonActioned);
            _continueBtn.onClick.RemoveListener(OnContinueButtonActioned);
            _exitBtn.onClick.RemoveListener(OnExitButtonActioned);

            PauseRequested = null;
            ContinueRequested = null;
            ExitRequested = null;
            _isInitialized = false;
        }

        public void Open()
        {
            _pausePopupObj.SetActive(true);
        }

        public void Close()
        {
            _pausePopupObj.SetActive(false);
        }

        private void OnPauseButtonActioned()
        {
            PauseRequested?.Invoke();
        }

        private void OnContinueButtonActioned()
        {
            ContinueRequested?.Invoke();
        }

        private void OnExitButtonActioned()
        {
            ExitRequested?.Invoke();
        }

        private bool ValidateReferences()
        {
            bool isValid = _pausePopupObj != null &&
               _pauseBtn != null &&
               _continueBtn != null &&
               _exitBtn != null;

            if ( !isValid )
            {
                Debug.LogError("Pause_view의 UI 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}
