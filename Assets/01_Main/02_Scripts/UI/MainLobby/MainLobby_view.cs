using HM.CodeBase;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.UI
{
    public sealed class MainLobby_view : AView
    {
        [Header("Buttons")]
        [SerializeField] private Button _quickMatchBtn;
        [SerializeField] private Button _localMatchBtn;
        [SerializeField] private Button _matchMakingCancelBtn;
        [SerializeField] private Button _matchMakingErrorConfirmBtn;

        [Space(5f), Header("Roots")]
        [SerializeField] private GameObject _waitMatchingRootObj;
        [SerializeField] private GameObject _matchMakingErrorRootObj;

        [Space(5f), Header("Texts")]
        [SerializeField] private TMP_Text _matchMakingErrorTxt;

        public event Action QuickMatchRequested;
        public event Action LocalMatchRequested;
        public event Action MatchMakingCancelRequested;

        private void Awake()
        {
            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _quickMatchBtn.onClick.AddListener(OnQuickMatchActioned);
            _localMatchBtn.onClick.AddListener(OnLocalMatchActioned);
            _matchMakingCancelBtn.onClick.AddListener(OnMatchMakingCancelActioned);
            _matchMakingErrorConfirmBtn.onClick.AddListener(OnMatchMakingErrorConfirmActioned);

            Clear();
        }

        private void OnDestroy()
        {
            if ( _quickMatchBtn != null )
            {
                _quickMatchBtn.onClick.RemoveListener(OnQuickMatchActioned);
            }

            if ( _localMatchBtn != null )
            {
                _localMatchBtn.onClick.RemoveListener(OnLocalMatchActioned);
            }

            if ( _matchMakingCancelBtn != null )
            {
                _matchMakingCancelBtn.onClick.RemoveListener(OnMatchMakingCancelActioned);
            }

            if ( _matchMakingErrorConfirmBtn != null )
            {
                _matchMakingErrorConfirmBtn.onClick.RemoveListener(OnMatchMakingErrorConfirmActioned);
            }

            QuickMatchRequested = null;
            LocalMatchRequested = null;
            MatchMakingCancelRequested = null;
        }

        public override void Clear()
        {
            SetQuickMatchInteractable(true);
            SetLocalMatchInteractable(true);
            SetMatchMakingCancelInteractable(true);
            SetWaitMatchingVisible(false);
            SetMatchMakingErrorVisible(false);
        }

        public void SetQuickMatchInteractable(bool isInteractable)
        {
            _quickMatchBtn.interactable = isInteractable;
        }

        public void SetLocalMatchInteractable(bool isInteractable)
        {
            _localMatchBtn.interactable = isInteractable;
        }

        public void SetMatchMakingCancelInteractable(bool isInteractable)
        {
            _matchMakingCancelBtn.interactable = isInteractable;
        }

        public void SetWaitMatchingVisible(bool isVisible)
        {
            _waitMatchingRootObj.SetActive(isVisible);
        }

        public void SetMatchMakingErrorVisible(bool isVisible)
        {
            _matchMakingErrorRootObj.SetActive(isVisible);
        }

        public void ShowMatchMakingError(string errorMessage)
        {
            _matchMakingErrorTxt.text = string.IsNullOrWhiteSpace(errorMessage)
                ? "매칭 중 오류가 발생했습니다."
                : errorMessage;

            SetMatchMakingErrorVisible(true);
        }

        private void OnQuickMatchActioned()
        {
            QuickMatchRequested?.Invoke();
        }

        private void OnLocalMatchActioned()
        {
            LocalMatchRequested?.Invoke();
        }

        private void OnMatchMakingCancelActioned()
        {
            MatchMakingCancelRequested?.Invoke();
        }

        private void OnMatchMakingErrorConfirmActioned()
        {
            SetMatchMakingErrorVisible(false);
        }

        private bool ValidateReferences()
        {
            bool isValid =
                _quickMatchBtn != null &&
                _localMatchBtn != null &&
                _matchMakingCancelBtn != null &&
                _matchMakingErrorConfirmBtn != null &&
                _waitMatchingRootObj != null &&
                _matchMakingErrorRootObj != null &&
                _matchMakingErrorTxt != null;

            if ( !isValid )
            {
                Debug.LogError("MainLobby_view의 UI 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}
