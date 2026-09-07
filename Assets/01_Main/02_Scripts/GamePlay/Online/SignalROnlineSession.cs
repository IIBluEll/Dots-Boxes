using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay
{
    public sealed class SignalROnlineSession : IOnlineSession
    {

        private readonly object ASSIGNMENT_LOCK = new object();
        private readonly string SERVER_URL;
        private readonly Guid USER_ID;
        private readonly Func<Task<string>> ACCESS_TOKEN_PROVIDER;

        private HubConnection _connection;
        private IDisposable _matchFoundSubscription;
        private SynchronizationContext _unitySynchronizationContext;
        private MatchAssignment _currentAssignment;

        private bool _isStarted;
        private bool _isQueueing;
        private bool _isDisposed;

        public event Action<MatchAssignment> MatchFound;
        public event Action<GAME_SESSION_CONNECTION_STATE_ENUM> ConnectionStateChanged;

        public GAME_SESSION_CONNECTION_STATE_ENUM ConnectionState { get; private set; }
            = GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED;

        public bool IsQueueing => _isQueueing;

        public bool HasMatchAssignment
        {
            get
            {
                lock ( ASSIGNMENT_LOCK )
                {
                    return _currentAssignment != null;
                }
            }
        }

        public MatchAssignment CurrentAssignment
        {
            get
            {
                lock ( ASSIGNMENT_LOCK )
                {
                    return _currentAssignment;
                }
            }
        }

        public SignalROnlineSession(string serverUrl , Guid userId , Func<Task<string>> accessTokenProvider = null)
        {
            if ( !Uri.TryCreate(serverUrl , UriKind.Absolute , out _) )
            {
                throw new ArgumentException(
                    "Server URL이 올바르지 않습니다." ,
                    nameof(serverUrl));
            }

            if ( userId == Guid.Empty )
            {
                throw new ArgumentException(
                    "UserId가 비어 있습니다." ,
                    nameof(userId));
            }

            SERVER_URL = serverUrl.TrimEnd('/');
            USER_ID = userId;
            ACCESS_TOKEN_PROVIDER = accessTokenProvider ?? (() => Task.FromResult(GameAccountSession.GetAccessToken(SERVER_URL, USER_ID)));
        }

        public async Task Start_async(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if ( _isStarted )
            {
                return;
            }

            _unitySynchronizationContext = SynchronizationContext.Current;

            if ( _unitySynchronizationContext == null )
            {
                throw new InvalidOperationException(
                    "SignalROnlineSession은 Unity 메인 스레드에서 시작해야 합니다.");
            }

            SetConnectionState(
                GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTING);

            string hubUrl = $"{SERVER_URL}/hubs/game";

            _connection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options => options.AccessTokenProvider = ACCESS_TOKEN_PROVIDER)
                .Build();

            _connection.Closed += OnConnectionClosed;

            _matchFoundSubscription = _connection.On<MatchAssignment>(
                "MatchFound" ,
                OnMatchFound);

            try
            {
                await _connection.StartAsync(cancellationToken);

                _isStarted = true;

                SetConnectionState(
                    GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED);
            }
            catch
            {
                _isStarted = false;

                SetConnectionState(
                    GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED);

                await DisposeConnection_async();
                throw;
            }
        }

        public async Task EnterMatchmaking_async(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            if ( _isQueueing )
            {
                throw new InvalidOperationException(
                    "이미 매칭을 요청한 상태입니다.");
            }

            if ( HasMatchAssignment )
            {
                throw new InvalidOperationException(
                    "이미 Match가 배정되었습니다.");
            }

            _isQueueing = true;

            try
            {
                MatchAssignment assignment =
                    await _connection.InvokeAsync<MatchAssignment>(
                        "EnterMatchmaking",
                        cancellationToken);

                if ( assignment != null )
                {
                    ReceiveMatchAssignment(assignment);
                }
            }
            catch
            {
                _isQueueing = false;
                throw;
            }
        }

        public async Task<bool> CancelMatchmaking_async(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            if ( !_isQueueing )
            {
                return false;
            }

            bool wasCancelled = await _connection.InvokeAsync<bool>(
                "CancelMatchmaking",
                cancellationToken);

            if ( wasCancelled )
            {
                _isQueueing = false;
            }

            return wasCancelled;
        }

        public void Dispose()
        {
            if ( _isDisposed )
            {
                return;
            }

            _isStarted = false;
            _isQueueing = false;
            _isDisposed = true;

            MatchFound = null;
            ConnectionStateChanged = null;

            lock ( ASSIGNMENT_LOCK )
            {
                _currentAssignment = null;
            }

            _ = DisposeConnection_async();
        }

        private void OnMatchFound(MatchAssignment assignment)
        {
            ReceiveMatchAssignment(assignment);
        }

        private void ReceiveMatchAssignment(MatchAssignment assignment)
        {
            if ( _isDisposed || !IsValidAssignment(assignment) )
            {
                return;
            }

            lock ( ASSIGNMENT_LOCK )
            {
                if ( _currentAssignment != null )
                {
                    return;
                }

                _currentAssignment = assignment;
                _isQueueing = false;
            }

            PostToUnityThread(() =>
            {
                if ( !_isDisposed )
                {
                    MatchFound?.Invoke(assignment);
                }
            });
        }

        private Task OnConnectionClosed(Exception exception)
        {
            if ( _isDisposed )
            {
                return Task.CompletedTask;
            }

            _isStarted = false;
            _isQueueing = false;

            GAME_SESSION_CONNECTION_STATE_ENUM connectionState =
                exception == null
                    ? GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED
                    : GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED;

            SetConnectionState(connectionState);

            return Task.CompletedTask;
        }

        private async Task DisposeConnection_async()
        {
            _matchFoundSubscription?.Dispose();
            _matchFoundSubscription = null;

            HubConnection connection = _connection;
            _connection = null;

            if ( connection == null )
            {
                return;
            }

            connection.Closed -= OnConnectionClosed;

            try
            {
                await connection.DisposeAsync();
            }
            catch
            {
                // 종료 중 발생한 통신 오류는 게임 상태를 변경하지 않습니다.
            }
        }

        private void SetConnectionState(
            GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            PostToUnityThread(() =>
            {
                if ( _isDisposed || ConnectionState == connectionState )
                {
                    return;
                }

                ConnectionState = connectionState;
                ConnectionStateChanged?.Invoke(connectionState);
            });
        }

        private void PostToUnityThread(Action action)
        {
            if ( _unitySynchronizationContext == null ||
                SynchronizationContext.Current == _unitySynchronizationContext )
            {
                action.Invoke();
                return;
            }

            _unitySynchronizationContext.Post(_ => action.Invoke() , null);
        }

        private static bool IsValidAssignment(
            MatchAssignment assignment)
        {
            return assignment != null &&
                   assignment.MatchId != Guid.Empty &&
                   assignment.LocalPlayerIndex != PLAYER_INDEX_ENUM.NONE &&
                   assignment.OpponentUserId != Guid.Empty;
        }

        private void ThrowIfNotStarted()
        {
            bool isConnected =
                _connection != null &&
                _connection.State == HubConnectionState.Connected &&
                ConnectionState ==
                GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED;

            if ( !_isStarted || !isConnected )
            {
                throw new InvalidOperationException(
                    "온라인 Session이 서버에 연결되지 않았습니다.");
            }
        }

        private void ThrowIfDisposed()
        {
            if ( _isDisposed )
            {
                throw new ObjectDisposedException(
                    nameof(SignalROnlineSession));
            }
        }
    }
}
