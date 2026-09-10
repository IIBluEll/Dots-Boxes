using HM.CodeBase;
using RainbowArt.CleanFlatUI;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.UI
{
    [DisallowMultipleComponent]
    public sealed class Bootstrap_view : AView
    {
        [Header("Loading")]
        [SerializeField] private ProgressBarPattern _progressBar;
        [SerializeField] private TMP_Text _statusTxt;

        [Space(5f), Header("Error Popup")]
        [SerializeField] private GameObject _errorRootObj;
        [SerializeField] private TMP_Text _errorMessageTxt;
        [SerializeField] private Button _retryBtn;

        private bool _isInitialized;

        public event Action RetryRequested;

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            if ( _isInitialized && _retryBtn != null )
            {
                _retryBtn.onClick.RemoveListener(OnRetryButtonActioned);
            }

            RetryRequested = null;
            _isInitialized = false;
        }

        public bool Initialize()
        {
            if(_isInitialized )
            {
                return true;
            }
            
            if(!ValidateReferences())
            {
                enabled = false;
                return false;
            }

            _statusTxt.richText = false;
            _errorMessageTxt.richText = false;

            _retryBtn.onClick.AddListener(OnRetryButtonActioned);

            _isInitialized = true;

            Clear();
            return true;
        }

        public override void Clear()
        {
            SetProgress(0f);
            SetStatus("게임을 준비하고 있습니다...");
            SetRetryInteractable(true);
            HideError();
        }

        public void SetProgress(float progress) => _progressBar.CurrentValue = Mathf.Clamp(progress , 0f , 100f);

        public void SetStatus(String statusMessage) => _statusTxt.text = statusMessage;

        public void ShowError(String errorMessage)
        {
            _errorMessageTxt.text = errorMessage;
            _errorRootObj.SetActive(true);
        }

        public void HideError() => _errorRootObj.SetActive(false);
        
        public void SetRetryInteractable(bool isInteractable) => _retryBtn.interactable = isInteractable;

        private void OnRetryButtonActioned() => RetryRequested?.Invoke();

        private bool ValidateReferences()
        {
            bool isValid =
                _progressBar != null &&
                _statusTxt != null &&
                _errorRootObj != null &&
                _errorMessageTxt != null &&
                _retryBtn != null;

            if ( !isValid )
            {
                Debug.LogError("Bootstrap_view의 UI 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}

