using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay
{
    public sealed class SignalRGameSession : IGameSession
    {
        private static readonly TimeSpan[] RECONNECT_DELAYS =
        {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(8)
        };

        private readonly GameSessionSnapshotStore SNAPSHOT_STORE = new GameSessionSnapshotStore();
        private readonly string SERVER_URL;
        private readonly Guid USER_ID;

        private HubConnection _connection;
        private IDisposable _matchStateChangedSubscription;
        private SynchronizationContext _unitySynchronizationContext;
        private bool _isStarted;
        private bool _isDisposed;

        public event Action<MatchSnapshot> SnapshotChanged;
        public event Action<GAME_SESSION_CONNECTION_STATE_ENUM> ConnectionStateChanged;

        public Guid MatchId { get; }
        public GAME_SESSION_CONNECTION_STATE_ENUM ConnectionState { get; private set; } = GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED;

        public PLAYER_INDEX_ENUM LocalPlayerIndex
        {
            get
            {
                if ( !HasSnapshot )
                {
                    return PLAYER_INDEX_ENUM.NONE;
                }

                MatchSnapshot snapshot = CurrentSnapshot;

                if ( snapshot.PlayerOneUserId == USER_ID )
                {
                    return PLAYER_INDEX_ENUM.PLAYER_ONE;
                }

                if ( snapshot.PlayerTwoUserId == USER_ID )
                {
                    return PLAYER_INDEX_ENUM.PLAYER_TWO;
                }

                return PLAYER_INDEX_ENUM.NONE;
            }
        }

        public bool CanConfirmCurrentTurn
        {
            get
            {
                return ConnectionState == GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED &&
                       _isStarted &&
                       _connection != null &&
                       _connection.State == HubConnectionState.Connected &&
                       HasSnapshot &&
                       CurrentSnapshot.MatchState == SERVER_MATCH_STATE_ENUM.ACTIVE &&
                       CurrentSnapshot.CurrentPlayerIndex == LocalPlayerIndex;
            }
        }

        public bool HasSnapshot => SNAPSHOT_STORE.HasSnapshot;
        public MatchSnapshot CurrentSnapshot => SNAPSHOT_STORE.CurrentSnapshot;

        public SignalRGameSession(string serverUrl , Guid matchId , Guid userId)
        {
            if ( !Uri.TryCreate(serverUrl , UriKind.Absolute , out _) )
            {
                throw new ArgumentException("Server URL이 올바르지 않습니다." , nameof(serverUrl));
            }

            if ( matchId == Guid.Empty )
            {
                throw new ArgumentException("MatchId가 비어 있습니다." , nameof(matchId));
            }

            if ( userId == Guid.Empty )
            {
                throw new ArgumentException("UserId가 비어 있습니다." , nameof(userId));
            }

            SERVER_URL = serverUrl.TrimEnd('/');
            MatchId = matchId;
            USER_ID = userId;
        }

        public async Task Start_async(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if ( _isStarted )
            {
                return;
            }

            _unitySynchronizationContext = SynchronizationContext.Current;

            if ( _unitySynchronizationContext == null )
            {
                throw new InvalidOperationException("SignalRGameSession은 Unity 메인 스레드에서 시작해야 합니다.");
            }

            SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTING);

            string hubUrl = $"{SERVER_URL}/hubs/game?userId={USER_ID:D}";

            _connection = new HubConnectionBuilder().WithUrl(hubUrl).WithAutomaticReconnect(RECONNECT_DELAYS).Build();
            _connection.Reconnecting += OnConnectionReconnecting;
            _connection.Reconnected += OnConnectionReconnected_async;
            _connection.Closed += OnConnectionClosed;

            _matchStateChangedSubscription = _connection.On<MatchSnapshot>("MatchStateChanged" , OnMatchStateChanged);

            try
            {
                await _connection.StartAsync(cancellationToken);

                MatchSnapshot initialSnapshot = await _connection.InvokeAsync<MatchSnapshot>(
                    "JoinMatch",
                    MatchId,
                    cancellationToken);

                _isStarted = true;
                ApplyAndPublishSnapshot(initialSnapshot);
                SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED);
            }
            catch
            {
                _isStarted = false;
                SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED);

                await DisposeConnection_async();
                throw;
            }
        }

        public async Task<ConfirmEdgeResponse> ConfirmEdge_async(
            int edgeId ,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            MatchSnapshot currentSnapshot = CurrentSnapshot;

            ConfirmEdgeRequest request = new ConfirmEdgeRequest
            {
                MatchId = MatchId,
                EdgeId = edgeId,
                ExpectedRevision = currentSnapshot.Revision,
                RequestId = Guid.NewGuid()
            };

            ConfirmEdgeResponse response = await _connection.InvokeAsync<ConfirmEdgeResponse>(
                "ConfirmEdge",
                request,
                cancellationToken);

            if ( response.Snapshot != null )
            {
                ReceiveSnapshot(response.Snapshot);
            }

            if ( response.ShouldRequestSync )
            {
                await RequestSync_async(cancellationToken);
            }

            return response;
        }

        public async Task<MatchSnapshot> RequestSync_async(
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            MatchSnapshot snapshot = await _connection.InvokeAsync<MatchSnapshot>(
                "RequestSync",
                MatchId,
                cancellationToken);

            ReceiveSnapshot(snapshot);
            return snapshot;
        }

        public void Dispose()
        {
            if ( _isDisposed )
            {
                return;
            }

            _isStarted = false;
            SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED);
            _isDisposed = true;

            SnapshotChanged = null;
            ConnectionStateChanged = null;

            _ = DisposeConnection_async();
        }

        private void OnMatchStateChanged(MatchSnapshot snapshot)
        {
            ReceiveSnapshot(snapshot);
        }

        private Task OnConnectionReconnecting(Exception exception)
        {
            if ( !_isDisposed )
            {
                SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.RECONNECTING);
            }

            return Task.CompletedTask;
        }

        private async Task OnConnectionReconnected_async(string connectionId)
        {
            if ( _isDisposed )
            {
                return;
            }

            try
            {
                MatchSnapshot snapshot = await _connection.InvokeAsync<MatchSnapshot>("JoinMatch" , MatchId);

                if ( _isDisposed )
                {
                    return;
                }

                _isStarted = true;
                ReceiveSnapshot(snapshot);
                SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED);
            }
            catch
            {
                if ( !_isDisposed )
                {
                    _isStarted = false;
                    SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED);
                }
            }
        }

        private Task OnConnectionClosed(Exception exception)
        {
            if ( _isDisposed )
            {
                return Task.CompletedTask;
            }

            _isStarted = false;

            GAME_SESSION_CONNECTION_STATE_ENUM connectionState = exception == null
                ? GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED
                : GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED;

            SetConnectionState(connectionState);
            return Task.CompletedTask;
        }

        private void ReceiveSnapshot(MatchSnapshot snapshot)
        {
            if ( _isDisposed )
            {
                return;
            }

            if ( SynchronizationContext.Current == _unitySynchronizationContext )
            {
                ApplyAndPublishSnapshot(snapshot);
                return;
            }

            _unitySynchronizationContext.Post(_ =>
            {
                if ( !_isDisposed )
                {
                    ApplyAndPublishSnapshot(snapshot);
                }
            } , null);
        }

        private void ApplyAndPublishSnapshot(MatchSnapshot snapshot)
        {
            if ( snapshot.MatchId != MatchId )
            {
                throw new InvalidOperationException("현재 Session과 다른 Match의 Snapshot입니다.");
            }

            if ( snapshot.PlayerOneUserId != USER_ID && snapshot.PlayerTwoUserId != USER_ID )
            {
                throw new InvalidOperationException("현재 사용자가 Snapshot의 Match 참가자가 아닙니다.");
            }

            if ( !SNAPSHOT_STORE.TryApply(snapshot) )
            {
                return;
            }

            SnapshotChanged?.Invoke(CurrentSnapshot);
        }

        private async Task DisposeConnection_async()
        {
            _matchStateChangedSubscription?.Dispose();
            _matchStateChangedSubscription = null;

            HubConnection connection = _connection;
            _connection = null;

            if ( connection == null )
            {
                return;
            }

            connection.Reconnecting -= OnConnectionReconnecting;
            connection.Reconnected -= OnConnectionReconnected_async;
            connection.Closed -= OnConnectionClosed;

            try
            {
                await connection.DisposeAsync();
            }
            catch
            {
                // Dispose 과정의 통신 오류는 게임 상태를 변경하지 않으므로 무시합니다.
            }
        }

        private void SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            if ( _unitySynchronizationContext == null || SynchronizationContext.Current == _unitySynchronizationContext )
            {
                ApplyConnectionState(connectionState);
                return;
            }

            _unitySynchronizationContext.Post(_ =>
            {
                if ( !_isDisposed )
                {
                    ApplyConnectionState(connectionState);
                }
            } , null);
        }

        private void ApplyConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            if ( ConnectionState == connectionState )
            {
                return;
            }

            ConnectionState = connectionState;
            ConnectionStateChanged?.Invoke(connectionState);
        }

        private void ThrowIfNotStarted()
        {
            if ( !_isStarted || _connection == null || ConnectionState != GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED )
            {
                throw new InvalidOperationException("SignalRGameSession이 연결되지 않았습니다.");
            }
        }

        private void ThrowIfDisposed()
        {
            if ( _isDisposed )
            {
                throw new ObjectDisposedException(nameof(SignalRGameSession));
            }
        }
    }
}
