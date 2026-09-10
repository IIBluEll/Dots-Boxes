using DotsAndBoxes.Shared;
using DotsAndBoxes.Gameplay.Audio;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameBoardUI : MonoBehaviour
    {
        private const string LOBBY_SCENE_NAME = "Lobby";
        private const string CONNECTION_LOST_TITLE = "CONNECTION LOST";
        private const string CONNECTION_LOST_MESSAGE = "서버와 연결이 끊어졌습니다.\nLobby로 이동해 주세요.";

        [Header("References")]
        [SerializeField] private GameBoard_View _gameBoardView;
        [SerializeField] private GameBoardAudioFeedback _audioFeedback;
        [SerializeField] private ResultUI _resultUI;
        [SerializeField] private PauseUI _pauseUI;

        [Header("Game Mode")]
        [SerializeField] private bool _useOnlineSession;

        [Header("Online Development")]
        [SerializeField] private string _serverUrl = "http://localhost:5049";
        [SerializeField] private string _matchId;
        [SerializeField] private string _userId;
        [SerializeField] private bool _simulateConfirmResponseLossOnce;

        private GameBoard_Model _gameBoardModel;
        private GameBoard_Presenter _gameBoardPresenter;
        private IGameSession _gameSession;
        private CancellationTokenSource _destroyCancellationTokenSource;

        private bool _hasStarted;
        private bool _ownsGameSession;
        private bool _isLeaving;

        private void Awake()
        {
            if ( !ApplyCommandLineOptions() )
            {
                enabled = false;
                return;
            }

            ApplyPreparedGameSession();

            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _destroyCancellationTokenSource = new CancellationTokenSource();
            _resultUI.LobbyRequested += OnLobbyRequested;
            _pauseUI.ExitRequested += OnExitRequestedActioned;

            CreateGameBoard();
        }

        private void Start()
        {
            _hasStarted = true;
            Open();

            if ( _gameSession != null && _ownsGameSession )
            {
                _ = StartOnlineSession_async();
            }
        }

        private void Update()
        {
            if ( _gameBoardPresenter == null )
            {
                return;
            }

            _gameBoardPresenter.Tick(DateTimeOffset.UtcNow);
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
                _resultUI.LobbyRequested -= OnLobbyRequested;
            }

            if ( _pauseUI != null )
            {
                _pauseUI.ExitRequested -= OnExitRequestedActioned;
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

            if ( _resultUI != null )
            {
                _resultUI.Close();
            }

            if ( _pauseUI != null )
            {
                _pauseUI.Close();
            }
        }

        private void CreateGameBoard()
        {
            _gameBoardModel = new GameBoard_Model();

            if ( _gameSession == null && _useOnlineSession )
            {
                Guid matchId = Guid.Parse(_matchId);
                Guid userId = Guid.Parse(_userId);

                _gameSession = new SignalRGameSession(
                    _serverUrl ,
                    matchId ,
                    userId ,
                    _simulateConfirmResponseLossOnce);

                _ownsGameSession = true;
            }

            if ( _gameSession != null )
            {
                _gameSession.ConnectionStateChanged += OnConnectionStateChanged;
                _gameBoardPresenter = new GameBoard_Presenter(
                    _gameBoardModel ,
                    _gameBoardView ,
                    _gameSession ,
                    _audioFeedback);
                _gameBoardPresenter.SessionFailed += OnSessionFailed;
            }
            else
            {
                _gameBoardPresenter = new GameBoard_Presenter(
                    _gameBoardModel ,
                    _gameBoardView ,
                    null ,
                    _audioFeedback);
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

                if ( _ownsGameSession )
                {
                    _gameSession.Dispose();
                }

                _gameSession = null;
                _ownsGameSession = false;
            }

            _gameBoardModel = null;
        }

        private void ApplyPreparedGameSession()
        {
            OnlineSessionProvider sessionProvider = OnlineSessionProvider.Instance;

            if ( sessionProvider == null || !sessionProvider.HasGameSession )
            {
                return;
            }

            _gameSession = sessionProvider.GameSession;
            _ownsGameSession = false;
            _useOnlineSession = true;
        }

        private async Task StartOnlineSession_async()
        {
            IGameSession session = _gameSession;
            CancellationToken cancellationToken = _destroyCancellationTokenSource.Token;

            try
            {
                await session.Start_async(cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                await session.Ready_async(cancellationToken);
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
            if ( IsSharedLocalGame() )
            {
                _resultUI.ShowSharedLocalResult(
                    _gameBoardModel.GameResult ,
                    _gameBoardModel.PlayerOneScore ,
                    _gameBoardModel.PlayerTwoScore);
                return;
            }

            _resultUI.ShowResult(
                _gameBoardModel.GameResult ,
                GetLocalPlayerIndex() ,
                _gameBoardModel.PlayerOneScore ,
                _gameBoardModel.PlayerTwoScore);
        }

        private void OnGameFinished(
            GAME_RESULT_ENUM gameResult ,
            int playerOneScore ,
            int playerTwoScore)
        {
            if ( IsSharedLocalGame() )
            {
                _resultUI.ShowSharedLocalResult(gameResult , playerOneScore , playerTwoScore);
                return;
            }

            _resultUI.ShowResult(
                gameResult ,
                GetLocalPlayerIndex() ,
                playerOneScore ,
                playerTwoScore);
        }

        private PLAYER_INDEX_ENUM GetLocalPlayerIndex()
        {
            if ( _gameSession != null && _gameSession.LocalPlayerIndex != PLAYER_INDEX_ENUM.NONE )
            {
                return _gameSession.LocalPlayerIndex;
            }

            return PLAYER_INDEX_ENUM.PLAYER_ONE;
        }

        private bool IsSharedLocalGame()
        {
            return _gameSession is LocalGameSession;
        }

        private void OnSessionFailed(Exception exception)
        {
            Debug.LogException(exception , this);
            ShowConnectionErrorIfNeeded();
        }

        private void OnConnectionStateChanged(GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            Debug.Log($"[Game Session] ConnectionState={connectionState}" , this);
            ShowConnectionErrorIfNeeded();
        }

        private void ShowConnectionErrorIfNeeded()
        {
            if ( _isLeaving ||
                 _gameSession == null ||
                 _gameBoardModel == null ||
                 _gameBoardModel.IsGameFinished )
            {
                return;
            }

            bool isConnectionLost = _gameSession.ConnectionState == GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED || _gameSession.ConnectionState == GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED;

            if ( !isConnectionLost )
            {
                return;
            }

            _resultUI.ShowMessage(CONNECTION_LOST_TITLE , CONNECTION_LOST_MESSAGE);
        }

        private void OnExitRequestedActioned()
        {
            if ( _isLeaving )
            {
                return;
            }

            _ = LeaveGame_async();
        }

        private void OnLobbyRequested()
        {
            if ( _isLeaving )
            {
                return;
            }

            _isLeaving = true;
            ReturnToLobby();
        }

        private async Task LeaveGame_async()
        {
            _isLeaving = true;

            CancellationToken cancellationToken = _destroyCancellationTokenSource.Token;

            try
            {
                if ( _gameSession != null )
                {
                    await _gameSession.Leave_async(cancellationToken);
                }
            }
            catch ( OperationCanceledException )
            {
                // Scene 종료로 취소된 경우에도 아래 finally에서 안전하게 정리합니다.
            }
            catch ( Exception exception )
            {
                if ( this != null )
                {
                    Debug.LogException(exception , this);
                }
            }
            finally
            {
                if ( this != null )
                {
                    Close();
                    ReturnToLobby();
                }
            }
        }

        private void ReturnToLobby()
        {
            if ( this == null )
            {
                return;
            }

            OnlineSessionProvider sessionProvider = OnlineSessionProvider.Instance;

            if ( _gameSession != null && sessionProvider != null && ReferenceEquals(sessionProvider.GameSession , _gameSession) )
            {
                sessionProvider.ResetGameSession();
            }

            SceneManager.LoadScene(LOBBY_SCENE_NAME);
        }

        private bool ValidateReferences()
        {
            if ( _gameBoardView == null || _audioFeedback == null || _resultUI == null || _pauseUI == null )
            {
                Debug.LogError("GameBoardUI의 UI 참조가 설정되지 않았습니다." , this);
                return false;
            }

            if ( _gameSession != null || !_useOnlineSession )
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

        private bool ApplyCommandLineOptions()
        {
            if ( !OnlineSessionLaunchOptions.TryCreate(
                Environment.GetCommandLineArgs() ,
                out OnlineSessionLaunchOptions launchOptions ,
                out string errorMessage) )
            {
                Debug.LogError(errorMessage , this);
                return false;
            }

            if ( launchOptions == null )
            {
                return true;
            }

            _useOnlineSession = true;
            _serverUrl = launchOptions.ServerUrl;
            _matchId = launchOptions.MatchId.ToString("D");
            _userId = launchOptions.UserId.ToString("D");
            _simulateConfirmResponseLossOnce = launchOptions.SimulateConfirmResponseLossOnce;
            return true;
        }
    }

    public sealed class OnlineSessionLaunchOptions
    {
        private const string SERVER_URL_PREFIX = "--server-url=";
        private const string MATCH_ID_PREFIX = "--match-id=";
        private const string USER_ID_PREFIX = "--user-id=";
        private const string SIMULATE_RESPONSE_LOSS_ARGUMENT = "--simulate-confirm-response-loss-once";

        public string ServerUrl { get; }
        public Guid MatchId { get; }
        public Guid UserId { get; }
        public bool SimulateConfirmResponseLossOnce { get; }

        private OnlineSessionLaunchOptions(
            string serverUrl ,
            Guid matchId ,
            Guid userId ,
            bool simulateConfirmResponseLossOnce)
        {
            ServerUrl = serverUrl;
            MatchId = matchId;
            UserId = userId;
            SimulateConfirmResponseLossOnce = simulateConfirmResponseLossOnce;
        }

        public static bool TryCreate(
            string[] arguments ,
            out OnlineSessionLaunchOptions launchOptions ,
            out string errorMessage)
        {
            launchOptions = null;
            errorMessage = string.Empty;

            if ( arguments == null )
            {
                errorMessage = "실행 인자 목록이 null입니다.";
                return false;
            }

            bool hasServerUrl = TryGetValue(arguments , SERVER_URL_PREFIX , out string serverUrl);
            bool hasMatchId = TryGetValue(arguments , MATCH_ID_PREFIX , out string matchIdText);
            bool hasUserId = TryGetValue(arguments , USER_ID_PREFIX , out string userIdText);
            bool shouldSimulateResponseLoss = HasArgument(arguments , SIMULATE_RESPONSE_LOSS_ARGUMENT);

            if ( !hasServerUrl && !hasMatchId && !hasUserId && !shouldSimulateResponseLoss )
            {
                return true;
            }

            if ( !hasServerUrl || !hasMatchId || !hasUserId )
            {
                errorMessage = "온라인 실행 인자는 server-url, match-id, user-id를 모두 입력해야 합니다.";
                return false;
            }

            if ( !Uri.TryCreate(serverUrl , UriKind.Absolute , out _) )
            {
                errorMessage = "server-url 실행 인자가 올바른 절대 URL이 아닙니다.";
                return false;
            }

            if ( !Guid.TryParse(matchIdText , out Guid matchId) || matchId == Guid.Empty )
            {
                errorMessage = "match-id 실행 인자가 올바른 Guid가 아닙니다.";
                return false;
            }

            if ( !Guid.TryParse(userIdText , out Guid userId) || userId == Guid.Empty )
            {
                errorMessage = "user-id 실행 인자가 올바른 Guid가 아닙니다.";
                return false;
            }

            launchOptions = new OnlineSessionLaunchOptions(
                serverUrl.TrimEnd('/') ,
                matchId ,
                userId ,
                shouldSimulateResponseLoss);

            return true;
        }

        private static bool TryGetValue(
            string[] arguments ,
            string prefix ,
            out string value)
        {
            for ( int index = 0; index < arguments.Length; index++ )
            {
                string argument = arguments[ index ];

                if ( argument != null && argument.StartsWith(prefix , StringComparison.OrdinalIgnoreCase) )
                {
                    value = argument.Substring(prefix.Length);
                    return !string.IsNullOrWhiteSpace(value);
                }
            }

            value = string.Empty;
            return false;
        }

        private static bool HasArgument(string[] arguments , string expectedArgument)
        {
            for ( int index = 0; index < arguments.Length; index++ )
            {
                if ( string.Equals(
                    arguments[ index ] ,
                    expectedArgument ,
                    StringComparison.OrdinalIgnoreCase) )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
