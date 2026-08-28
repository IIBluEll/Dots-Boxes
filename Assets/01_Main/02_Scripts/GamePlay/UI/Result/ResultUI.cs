using DotsAndBoxes.Shared;
using System;
using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ResultUI : MonoBehaviour
    {
        private const string LEAVE_CONFIRMATION_TITLE = "LEAVE GAME?";
        private const string LEAVE_CONFIRMATION_MESSAGE = "게임 진행 중 나가면 기권 패배로 처리될 수 있습니다.\n정말 나가시겠습니까?";

        [SerializeField] private Result_View _resultView;

        private Result_Model _resultModel;
        private Result_Presenter _resultPresenter;
        private bool _isLeaveConfirmationOpen;

        public event Action LobbyRequested;
        public event Action LeaveConfirmed;

        private void Awake()
        {
            if ( _resultView == null )
            {
                Debug.LogError("Result_View 참조가 설정되지 않았습니다." , this);
                enabled = false;
                return;
            }

            _resultModel = new Result_Model();
            _resultPresenter = new Result_Presenter(_resultModel , _resultView);

            _resultPresenter.LobbyRequested += OnLobbyRequested;
            _resultPresenter.CancelRequested += OnCancelRequestedActioned;

            _resultPresenter.Close();
        }

        private void OnDestroy()
        {
            if ( _resultPresenter == null )
            {
                return;
            }

            _resultPresenter.LobbyRequested -= OnLobbyRequested;
            _resultPresenter.CancelRequested -= OnCancelRequestedActioned;

            _resultPresenter.Dispose();
            LeaveConfirmed = null;
        }

        public void ShowResult(GAME_RESULT_ENUM gameResult , int playerOneScore , int playerTwoScore)
        {
            _isLeaveConfirmationOpen = false;
            _resultModel.SetResult(gameResult , playerOneScore , playerTwoScore);
            _resultPresenter.Open();
        }

        public void ShowMessage(string title , string message)
        {
            _isLeaveConfirmationOpen = false;
            _resultPresenter.OpenMessage(title , message);
        }

        public void ShowLeaveConfirmation()
        {
            _isLeaveConfirmationOpen = true;
            _resultPresenter.OpenConfirmation(LEAVE_CONFIRMATION_TITLE , LEAVE_CONFIRMATION_MESSAGE);
        }

        public void Close()
        {
            _isLeaveConfirmationOpen = false;
            _resultPresenter?.Close();
        }

        private void OnLobbyRequested()
        {
            if ( _isLeaveConfirmationOpen )
            {
                _isLeaveConfirmationOpen = false;
                LeaveConfirmed?.Invoke();
                return;
            }

            LobbyRequested?.Invoke();
        }

        private void OnCancelRequestedActioned()
        {
            _isLeaveConfirmationOpen = false;
        }
    }
}
