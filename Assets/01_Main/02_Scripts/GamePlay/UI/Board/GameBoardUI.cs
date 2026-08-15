using DotsAndBoxes.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameBoardUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameBoard_View _gameBoardView;
        [SerializeField] private ResultUI _resultUI;

        [Header("Game Mode")]
        [SerializeField] private bool _useOnlineSession;

        [Header("Online Development")]
        [SerializeField] private string _serverUrl = "http://localhost:5049";
        [SerializeField] private string _matchId;
        [SerializeField] private string _userId;

        private GameBoard_Model _gameBoardModel;
        private GameBoard_Presenter _gameBoardPresenter;
        private IGameSession _gameSession;
        private CancellationTokenSource _destroyCancellationTokenSource;
        private bool _hasStarted;

        private void Awake()
        {
            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _destroyCancellationTokenSource = new CancellationTokenSource();
            _resultUI.RestartRequested += OnRestartRequested;

            CreateGameBoard();
        }

        private void Start()
        {
            _hasStarted = true;
            Open();

            if ( _gameSession != null )
            {
                _ = StartOnlineSession_async();
            }
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
            _destroyCancellationTokenSource?.Cancel();

            if ( _resultUI != null )
            {
                _resultUI.RestartRequested -= OnRestartRequested;
            }

            ReleaseGameBoard();

            _destroyCancellationTokenSource?.Dispose();
            _destroyCancellationTokenSource = null;
        }

        public void Open()
        {
            _gameBoardPresenter?.Open();

            if ( _gameBoardModel != null && _gameBoardModel.IsGameFinished )
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

            if ( _useOnlineSession )
            {
                Guid matchId = Guid.Parse(_matchId);
                Guid userId = Guid.Parse(_userId);

                _gameSession = new SignalRGameSession(_serverUrl , matchId , userId);
                _gameSession.ConnectionStateChanged += OnConnectionStateChanged;
                _gameBoardPresenter = new GameBoard_Presenter(_gameBoardModel , _gameBoardView , _gameSession);
                _gameBoardPresenter.SessionFailed += OnSessionFailed;
            }
            else
            {
                _gameBoardPresenter = new GameBoard_Presenter(_gameBoardModel , _gameBoardView);
            }

            _gameBoardPresenter.GameFinished += OnGameFinished;
        }

        private void ReleaseGameBoard()
        {
            if ( _gameBoardPresenter != null )
            {
                _gameBoardPresenter.GameFinished -= OnGameFinished;
                _gameBoardPresenter.SessionFailed -= OnSessionFailed;
                _gameBoardPresenter.Dispose();
                _gameBoardPresenter = null;
            }

            if ( _gameSession != null )
            {
                _gameSession.ConnectionStateChanged -= OnConnectionStateChanged;
                _gameSession.Dispose();
                _gameSession = null;
            }

            _gameBoardModel = null;
        }

        private async Task StartOnlineSession_async()
        {
            try
            {
                await _gameSession.Start_async(_destroyCancellationTokenSource.Token);
            }
            catch ( OperationCanceledException )
            {
                // GameObject가 파괴되면서 취소된 경우이므로 오류로 처리하지 않습니다.
            }
            catch ( Exception exception )
            {
                if ( this != null )
                {
                    Debug.LogException(exception , this);
                }
            }
        }

        private void ShowCurrentResult()
        {
            _resultUI.ShowResult(
                _gameBoardModel.GameResult ,
                _gameBoardModel.PlayerOneScore ,
                _gameBoardModel.PlayerTwoScore);
        }

        private void OnGameFinished(
            GAME_RESULT_ENUM gameResult ,
            int playerOneScore ,
            int playerTwoScore)
        {
            _resultUI.ShowResult(gameResult , playerOneScore , playerTwoScore);
        }

        private void OnSessionFailed(Exception exception)
        {
            Debug.LogException(exception , this);
        }

        private void OnConnectionStateChanged(GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            Debug.Log($"[Game Session] ConnectionState={connectionState}" , this);
        }

        private void OnRestartRequested()
        {
            if ( _gameSession != null )
            {
                Debug.LogWarning("온라인 재대전은 아직 구현되지 않았습니다." , this);
                return;
            }

            ReleaseGameBoard();
            _gameBoardView.Clear();

            CreateGameBoard();
            _gameBoardPresenter.Open();
        }

        private bool ValidateReferences()
        {
            if ( _gameBoardView == null || _resultUI == null )
            {
                Debug.LogError("GameBoardUI의 UI 참조가 설정되지 않았습니다." , this);
                return false;
            }

            if ( !_useOnlineSession )
            {
                return true;
            }

            if ( !Uri.TryCreate(_serverUrl , UriKind.Absolute , out _) )
            {
                Debug.LogError("GameBoardUI의 Server URL이 올바르지 않습니다." , this);
                return false;
            }

            if ( !Guid.TryParse(_matchId , out Guid matchId) || matchId == Guid.Empty )
            {
                Debug.LogError("GameBoardUI의 MatchId가 올바르지 않습니다." , this);
                return false;
            }

            if ( !Guid.TryParse(_userId , out Guid userId) || userId == Guid.Empty )
            {
                Debug.LogError("GameBoardUI의 UserId가 올바르지 않습니다." , this);
                return false;
            }

            return true;
        }
    }
}
