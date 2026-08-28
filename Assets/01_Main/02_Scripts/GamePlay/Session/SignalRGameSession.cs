using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay
{
    public sealed class SignalRGameSession : IGameSession
    {
        private readonly GameSessionSnapshotStore SNAPSHOT_STORE = new GameSessionSnapshotStore();
        private readonly PendingConfirmRequestStore PENDING_CONFIRM_REQUEST_STORE = new PendingConfirmRequestStore();
        private readonly string SERVER_URL;
        private readonly Guid USER_ID;
        private readonly bool SIMULATE_CONFIRM_RESPONSE_LOSS_ONCE;

        private HubConnection _connection;
        private IDisposable _matchStateChangedSubscription;
        private IDisposable _opponentPreviewChangedSubscription;
        private SynchronizationContext _unitySynchronizationContext;

        private long _previewSequence;
        private long _lastOpponentPreviewRevision = -1;
        private long _lastOpponentPreviewSequence;
        private bool _isStarted;
        private bool _isReady;
        private bool _hasLeft;
        private bool _isDisposed;
        private bool _hasSimulatedConfirmResponseLoss;

        public event Action<MatchSnapshot> SnapshotChanged;
        public event Action<GAME_SESSION_CONNECTION_STATE_ENUM> ConnectionStateChanged;
        public event Action<OpponentPreviewUpdate> OpponentPreviewChanged;

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

        public bool HasPendingConfirm => PENDING_CONFIRM_REQUEST_STORE.HasPendingRequest;
        public int PendingConfirmEdgeId => PENDING_CONFIRM_REQUEST_STORE.PendingEdgeId;

        public bool HasSnapshot => SNAPSHOT_STORE.HasSnapshot;
        public MatchSnapshot CurrentSnapshot => SNAPSHOT_STORE.CurrentSnapshot;

        public SignalRGameSession(
            string serverUrl ,
            Guid matchId ,
            Guid userId ,
            bool simulateConfirmResponseLossOnce = false)
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
            SIMULATE_CONFIRM_RESPONSE_LOSS_ONCE = simulateConfirmResponseLossOnce;
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

            if ( SIMULATE_CONFIRM_RESPONSE_LOSS_ONCE )
            {
                hubUrl += "&simulateConfirmResponseLossOnce=true";
            }

            _connection = new HubConnectionBuilder().WithUrl(hubUrl).Build();
            _connection.Closed += OnConnectionClosed;

            _matchStateChangedSubscription = _connection.On<MatchSnapshot>("MatchStateChanged" , OnMatchStateChanged);
            _opponentPreviewChangedSubscription = _connection.On<OpponentPreviewUpdate>("OpponentPreviewChanged" , OnOpponentPreviewChanged);

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

        public async Task Ready_async(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            if(_isReady)
            {
                return;
            }

            MatchSnapshot snapshot = await _connection.InvokeAsync<MatchSnapshot>("ReadyMatch", MatchId, cancellationToken);

            if(snapshot == null)
            {
                throw new InvalidOperationException("Ready SnapShot을 받지 못함");
            }

            ReceiveSnapshot(snapshot);
            _isReady = true;
        }

        public async Task<MatchSnapshot> Leave_async(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            if ( _hasLeft )
            {
                return CurrentSnapshot;
            }

            MatchSnapshot snapshot = await _connection.InvokeAsync<MatchSnapshot>(
        "LeaveMatch",
        MatchId,
        cancellationToken);

            if ( snapshot == null )
            {
                throw new InvalidOperationException("LeaveMatch 응답 Snapshot이 없습니다.");
            }

            _hasLeft = true;
            _isReady = false;

            ReceiveSnapshot(snapshot);

            return snapshot;
        }

        public async Task<MATCH_COMMAND_ERROR_ENUM> SetPreviewEdge_async(
            int edgeId ,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            if ( !HasSnapshot )
            {
                return MATCH_COMMAND_ERROR_ENUM.INVALID_REQUEST;
            }

            MatchSnapshot snapshot = CurrentSnapshot;

            if ( snapshot.MatchState != SERVER_MATCH_STATE_ENUM.ACTIVE )
            {
                return MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE;
            }

            if ( snapshot.CurrentPlayerIndex != LocalPlayerIndex )
            {
                return MATCH_COMMAND_ERROR_ENUM.NOT_YOUR_TURN;
            }

            bool shouldClearPreview = edgeId == OpponentPreviewUpdate.NO_PREVIEW_EDGE_ID;

            if ( !shouldClearPreview && (edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT) )
            {
                return MATCH_COMMAND_ERROR_ENUM.INVALID_EDGE;
            }

            if ( !shouldClearPreview && snapshot.EdgeOwners[ edgeId ] != PLAYER_INDEX_ENUM.NONE )
            {
                return MATCH_COMMAND_ERROR_ENUM.EDGE_ALREADY_CONFIRMED;
            }

            long previewSequence = Interlocked.Increment(ref _previewSequence);

            PreviewEdgeRequest request = new PreviewEdgeRequest
            {
                MatchId = MatchId ,
                EdgeId = edgeId ,
                ExpectedRevision = snapshot.Revision ,
                PreviewSequence = previewSequence
            };

            return await _connection.InvokeAsync<MATCH_COMMAND_ERROR_ENUM>(
                "SetPreviewEdge" ,
                request ,
                cancellationToken);
        }

        public async Task<ConfirmEdgeResponse> ConfirmEdge_async(int edgeId , CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            ThrowIfNotStarted();

            MatchSnapshot currentSnapshot = CurrentSnapshot;

            ConfirmEdgeRequest request = PENDING_CONFIRM_REQUEST_STORE.GetOrCreate(MatchId , edgeId , currentSnapshot.Revision);

            ConfirmEdgeResponse response = await _connection.InvokeAsync<ConfirmEdgeResponse>("ConfirmEdge" , request , cancellationToken);

            if ( response == null )
            {
                throw new InvalidOperationException("서버에서 ConfirmEdgeResponse를 받지 못했습니다.");
            }

            if ( response.RequestId != request.RequestId )
            {
                throw new InvalidOperationException("Confirm 응답의 RequestId가 요청과 일치하지 않습니다.");
            }

            if ( SIMULATE_CONFIRM_RESPONSE_LOSS_ONCE &&
                 !_hasSimulatedConfirmResponseLoss &&
                 response.IsAccepted )
            {
                _hasSimulatedConfirmResponseLoss = true;
                throw new InvalidOperationException(
                    "개발용 Confirm 응답 유실을 재현했습니다. 같은 Preview를 다시 Confirm해야 합니다.");
            }

            PENDING_CONFIRM_REQUEST_STORE.TryComplete(response.RequestId);

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
            _isReady = false;
            _hasLeft = false;
            
            PENDING_CONFIRM_REQUEST_STORE.Clear();

            SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED);
            
            _isDisposed = true;

            SnapshotChanged = null;
            ConnectionStateChanged = null;
            OpponentPreviewChanged = null;

            _previewSequence = 0;
            _lastOpponentPreviewRevision = -1;
            _lastOpponentPreviewSequence = 0;

            _ = DisposeConnection_async();
        }

        private void OnMatchStateChanged(MatchSnapshot snapshot)
        {
            ReceiveSnapshot(snapshot);
        }

        private void OnOpponentPreviewChanged(OpponentPreviewUpdate update)
        {
            ReceiveOpponentPreview(update);
        }

        private Task OnConnectionClosed(Exception exception)
        {
            if ( _isDisposed )
            {
                return Task.CompletedTask;
            }

            _isStarted = false;
            _isReady = false;
            PENDING_CONFIRM_REQUEST_STORE.Clear();

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

        private void ReceiveOpponentPreview(OpponentPreviewUpdate update)
        {
            if ( _isDisposed || update == null )
            {
                return;
            }

            if ( SynchronizationContext.Current == _unitySynchronizationContext )
            {
                ApplyAndPublishOpponentPreview(update);
                return;
            }

            _unitySynchronizationContext.Post(_ =>
            {
                if ( !_isDisposed )
                {
                    ApplyAndPublishOpponentPreview(update);
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

            MatchSnapshot appliedSnapshot = SNAPSHOT_STORE.CurrentSnapshot;

            _lastOpponentPreviewRevision = appliedSnapshot.Revision;
            _lastOpponentPreviewSequence = 0;

            PENDING_CONFIRM_REQUEST_STORE.TryComplete(appliedSnapshot);
            SnapshotChanged?.Invoke(appliedSnapshot);
        }

        private void ApplyAndPublishOpponentPreview(OpponentPreviewUpdate update)
        {
            if ( update.MatchId != MatchId || !HasSnapshot )
            {
                return;
            }

            MatchSnapshot snapshot = CurrentSnapshot;

            if ( update.Revision != snapshot.Revision )
            {
                return;
            }

            PLAYER_INDEX_ENUM opponentPlayerIndex = LocalPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                ? PLAYER_INDEX_ENUM.PLAYER_TWO
                : PLAYER_INDEX_ENUM.PLAYER_ONE;

            if ( update.PlayerIndex != opponentPlayerIndex || update.PreviewSequence <= 0 )
            {
                return;
            }

            if ( update.HasPreview )
            {
                bool isValidEdge = update.EdgeId >= 0 && update.EdgeId < BoardTopology.EDGE_COUNT;

                if ( !isValidEdge || snapshot.EdgeOwners[ update.EdgeId ] != PLAYER_INDEX_ENUM.NONE )
                {
                    return;
                }
            }
            else if ( update.EdgeId != OpponentPreviewUpdate.NO_PREVIEW_EDGE_ID )
            {
                return;
            }

            bool isOlderUpdate =
                _lastOpponentPreviewRevision > update.Revision ||
                (_lastOpponentPreviewRevision == update.Revision &&
                 _lastOpponentPreviewSequence >= update.PreviewSequence);

            if ( isOlderUpdate )
            {
                return;
            }

            _lastOpponentPreviewRevision = update.Revision;
            _lastOpponentPreviewSequence = update.PreviewSequence;
            OpponentPreviewChanged?.Invoke(update);
        }

        private async Task DisposeConnection_async()
        {
            _matchStateChangedSubscription?.Dispose();
            _matchStateChangedSubscription = null;

            _opponentPreviewChangedSubscription?.Dispose();
            _opponentPreviewChangedSubscription = null;

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
