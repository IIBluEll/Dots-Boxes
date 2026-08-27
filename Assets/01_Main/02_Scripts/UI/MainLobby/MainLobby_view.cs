using UnityEngine;
using HM.CodeBase;
using UnityEngine.UI;
using System;

namespace DotsAndBoxes.UI
{
    public sealed class MainLobby_view : AView
    {
        [Header("Buttons")]
        [SerializeField] private Button _quickMatchBtn;
        [SerializeField] private Button _matchMakingCancelBtn;

        [Space(5f), Header("Roots")]
        [SerializeField] private GameObject _waitMatchingRootObj;

        public event Action QuickMatchRequested;
        public event Action MatchMakingCancelRequested;

        private void Awake()
        {
            if(!ValidateReferences())
            {
                enabled = false;
                return;
            }

            _quickMatchBtn.onClick.AddListener(OnQuickMatchActioned);
            _matchMakingCancelBtn.onClick.AddListener(OnMatchMakingCancelActioned);

            Clear();
        }

        private void OnDestroy()
        {
            if(_quickMatchBtn != null)
            {
                _quickMatchBtn.onClick.RemoveListener(OnQuickMatchActioned);
            }

            if ( _matchMakingCancelBtn != null )
            {
                _matchMakingCancelBtn.onClick.RemoveListener(OnMatchMakingCancelActioned);
            }

            QuickMatchRequested = null;
            MatchMakingCancelRequested = null;
        }

        public override void Clear()
        {
            SetQuickMatchInteractable(true);
            SetMatchMakingCancelInteractable(true);
            SetWaitMatchingVisible(false);
        }

        public void SetQuickMatchInteractable(bool isInteractable)
        {
            _quickMatchBtn.interactable = isInteractable;
        }

        public void SetMatchMakingCancelInteractable(bool isInteractable)
        {
            _matchMakingCancelBtn.interactable = isInteractable;
        }

        public void SetWaitMatchingVisible(bool isVisible)
        {
            _waitMatchingRootObj.SetActive(isVisible);
        }

        private void OnQuickMatchActioned()
        {
            QuickMatchRequested?.Invoke();
        }

        private void OnMatchMakingCancelActioned()
        {
            MatchMakingCancelRequested?.Invoke();
        }

        private bool ValidateReferences()
        {
            bool isValid = _quickMatchBtn != null && _matchMakingCancelBtn != null && _waitMatchingRootObj != null;

            if ( !isValid )
            {
                Debug.LogError("MainLobby_view의 UI 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}

