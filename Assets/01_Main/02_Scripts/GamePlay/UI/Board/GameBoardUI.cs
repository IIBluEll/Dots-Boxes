using DotsAndBoxes.Shared;
using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameBoardUI : MonoBehaviour
    {
        [SerializeField] private GameBoard_View _gameBoardView;
        [SerializeField] private ResultUI _resultUI;

        private GameBoard_Model _gameBoardModel;
        private GameBoard_Presenter _gameBoardPresenter;
        private bool _hasStarted;

        private void Awake()
        {
            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _resultUI.RestartRequested += OnRestartRequested;
            CreateGameBoard();
        }

        private void Start()
        {
            _hasStarted = true;
            Open();
        }

        private void OnEnable()
        {
            if ( _hasStarted )
            {
                Open();
            }
        }

        private void OnDisable()
        {
            if ( _hasStarted )
            {
                Close();
            }
        }

        private void OnDestroy()
        {
            if ( _resultUI != null )
            {
                _resultUI.RestartRequested -= OnRestartRequested;
            }

            ReleaseGameBoard();
        }

        public void Open()
        {
            _gameBoardPresenter?.Open();

            if ( _gameBoardModel != null && _gameBoardModel.Board.IsGameFinished )
            {
                ShowCurrentResult();
            }
        }

        public void Close()
        {
            _gameBoardPresenter?.Close();
            _resultUI?.Close();
        }

        private void CreateGameBoard()
        {
            _gameBoardModel = new GameBoard_Model();
            _gameBoardPresenter = new GameBoard_Presenter(_gameBoardModel , _gameBoardView);
            _gameBoardPresenter.GameFinished += OnGameFinished;
        }

        private void ReleaseGameBoard()
        {
            if ( _gameBoardPresenter == null )
            {
                return;
            }

            _gameBoardPresenter.GameFinished -= OnGameFinished;
            _gameBoardPresenter.Dispose();
            _gameBoardPresenter = null;
            _gameBoardModel = null;
        }

        private void ShowCurrentResult()
        {
            _resultUI.ShowResult(
                _gameBoardModel.Board.GameResult ,
                _gameBoardModel.Board.PlayerOneScore ,
                _gameBoardModel.Board.PlayerTwoScore);
        }

        private void OnGameFinished(GAME_RESULT_ENUM gameResult , int playerOneScore , int playerTwoScore)
        {
            _resultUI.ShowResult(gameResult , playerOneScore , playerTwoScore);
        }

        private void OnRestartRequested()
        {
            ReleaseGameBoard();
            _gameBoardView.Clear();

            CreateGameBoard();
            _gameBoardPresenter.Open();
        }

        private bool ValidateReferences()
        {
            bool isValid = _gameBoardView != null &&
                           _resultUI != null;

            if ( !isValid )
            {
                Debug.LogError("GameBoardUI의 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}