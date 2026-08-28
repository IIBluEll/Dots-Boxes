using DotsAndBoxes.Shared;
using System;
using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class ResultUI : MonoBehaviour
    {
        [SerializeField] private Result_View _resultView;

        private Result_Model _resultModel;
        private Result_Presenter _resultPresenter;

        public event Action RestartRequested;
        public event Action LobbyRequested;

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

            _resultPresenter.RestartRequested += OnRestartRequested;
            _resultPresenter.LobbyRequested += OnLobbyRequested;

            _resultPresenter.Close();
        }

        private void OnDestroy()
        {
            if ( _resultPresenter == null )
            {
                return;
            }

            _resultPresenter.RestartRequested -= OnRestartRequested;
            _resultPresenter.LobbyRequested -= OnLobbyRequested;

            _resultPresenter.Dispose();
        }

        public void ShowResult(GAME_RESULT_ENUM gameResult , int playerOneScore , int playerTwoScore)
        {
            _resultModel.SetResult(gameResult , playerOneScore , playerTwoScore);
            _resultPresenter.Open();
        }

        public void Close()
        {
            _resultPresenter?.Close();
        }

        private void OnRestartRequested()
        {
            RestartRequested?.Invoke();
        }

        private void OnLobbyRequested()
        {
            LobbyRequested?.Invoke();
        }
    }
}