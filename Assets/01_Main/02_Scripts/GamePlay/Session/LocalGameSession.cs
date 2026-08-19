using DotsAndBoxes.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay
{
    public sealed class LocalGameSession : IGameSession
    {
        private readonly DotsBoard BOARD;
        private readonly GameSessionSnapshotStore SNAPSHOT_STORE;
        private readonly Guid PLAYER_ONE_USER_ID;
        private readonly Guid PLAYER_TWO_USER_ID;

        private long _revision;
        private bool _isStarted;
        private bool _isDisposed;

        public event Action<MatchSnapshot> SnapshotChanged;
        public event Action<GAME_SESSION_CONNECTION_STATE_ENUM> ConnectionStateChanged;

        public Guid MatchId { get; }
        public PLAYER_INDEX_ENUM LocalPlayerIndex => PLAYER_INDEX_ENUM.NONE;
        public GAME_SESSION_CONNECTION_STATE_ENUM ConnectionState { get; private set; } = GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED;
        public bool CanConfirmCurrentTurn => ConnectionState == GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED && _isStarted && !_isDisposed && !BOARD.IsGameFinished;
        public bool HasPendingConfirm => false;
        public int PendingConfirmEdgeId => PendingConfirmRequestStore.NO_PENDING_EDGE_ID;

        public bool HasSnapshot => SNAPSHOT_STORE.HasSnapshot;
        public MatchSnapshot CurrentSnapshot => SNAPSHOT_STORE.CurrentSnapshot;

        public LocalGameSession(PLAYER_INDEX_ENUM startingPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE)
        {
            BOARD = new DotsBoard(startingPlayerIndex);
            SNAPSHOT_STORE = new GameSessionSnapshotStore();

            MatchId = Guid.NewGuid();
            PLAYER_ONE_USER_ID = Guid.NewGuid();
            PLAYER_TWO_USER_ID = Guid.NewGuid();
        }

        public Task Start_async(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if ( cancellationToken.IsCancellationRequested )
            {
                return Task.FromCanceled(cancellationToken);
            }

            if ( _isStarted )
            {
                return Task.CompletedTask;
            }

            SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTING);

            _isStarted = true;
            ApplyAndPublishSnapshot();
            SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED);
            return Task.CompletedTask;
        }

        public Task Ready_async(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if ( cancellationToken.IsCancellationRequested )
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (!_isStarted)
            {
                InvalidOperationException exception = new InvalidOperationException("시작되지 않은 Session은 Ready 할 수 없음");

                return Task.FromException(exception);
            }

            return Task.CompletedTask;
        }

        public Task<ConfirmEdgeResponse> ConfirmEdge_async(int edgeId , CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if ( cancellationToken.IsCancellationRequested )
            {
                return Task.FromCanceled<ConfirmEdgeResponse>(cancellationToken);
            }

            Guid requestId = Guid.NewGuid();

            if ( !_isStarted )
            {
                ConfirmEdgeResponse notStartedResponse = CreateFailureResponse(requestId, MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE, null);
                return Task.FromResult(notStartedResponse);
            }

            MoveResult moveResult = DotsRule.TryConfirmEdge(BOARD, BOARD.CurrentPlayerIndex, edgeId);

            if ( !moveResult.IsValid )
            {
                MATCH_COMMAND_ERROR_ENUM error = MapMoveError(moveResult.Error);
                ConfirmEdgeResponse failureResponse = CreateFailureResponse(requestId, error, CurrentSnapshot);
                return Task.FromResult(failureResponse);
            }

            _revision++;

            MatchSnapshot snapshot = ApplyAndPublishSnapshot();
            ConfirmEdgeResponse successResponse = new ConfirmEdgeResponse
            {
                RequestId = requestId,
                IsAccepted = true,
                Error = MATCH_COMMAND_ERROR_ENUM.NONE,
                ShouldRequestSync = false,
                Snapshot = snapshot
            };

            return Task.FromResult(successResponse);
        }

        public Task<MatchSnapshot> RequestSync_async(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if ( cancellationToken.IsCancellationRequested )
            {
                return Task.FromCanceled<MatchSnapshot>(cancellationToken);
            }

            if ( !_isStarted )
            {
                InvalidOperationException exception = new InvalidOperationException("시작되지 않은 LocalGameSession은 동기화할 수 없습니다.");
                return Task.FromException<MatchSnapshot>(exception);
            }

            return Task.FromResult(CurrentSnapshot);
        }

        public void Dispose()
        {
            if ( _isDisposed )
            {
                return;
            }

            SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED);

            SnapshotChanged = null;
            ConnectionStateChanged = null;
            _isDisposed = true;
        }

        private MatchSnapshot ApplyAndPublishSnapshot()
        {
            MatchSnapshot snapshot = CreateSnapshot();
            bool isApplied = SNAPSHOT_STORE.TryApply(snapshot);

            if ( !isApplied )
            {
                throw new InvalidOperationException("새로운 Local Snapshot을 적용하지 못했습니다.");
            }

            MatchSnapshot publishedSnapshot = SNAPSHOT_STORE.CurrentSnapshot;
            SnapshotChanged?.Invoke(publishedSnapshot);
            return SNAPSHOT_STORE.CurrentSnapshot;
        }

        private MatchSnapshot CreateSnapshot()
        {
            PLAYER_INDEX_ENUM[] edgeOwners = new PLAYER_INDEX_ENUM[BoardTopology.EDGE_COUNT];
            PLAYER_INDEX_ENUM[] boxOwners = new PLAYER_INDEX_ENUM[BoardTopology.BOX_COUNT];

            for ( int edgeId = 0; edgeId < edgeOwners.Length; edgeId++ )
            {
                edgeOwners[ edgeId ] = BOARD.GetEdge(edgeId).OwnerPlayerIndex;
            }

            for ( int boxId = 0; boxId < boxOwners.Length; boxId++ )
            {
                boxOwners[ boxId ] = BOARD.GetBox(boxId).OwnerPlayerIndex;
            }

            return new MatchSnapshot
            {
                MatchId = MatchId ,
                Revision = _revision ,
                MatchState = BOARD.IsGameFinished ? SERVER_MATCH_STATE_ENUM.FINISHED : SERVER_MATCH_STATE_ENUM.ACTIVE ,
                PlayerOneUserId = PLAYER_ONE_USER_ID ,
                PlayerTwoUserId = PLAYER_TWO_USER_ID ,
                CurrentPlayerIndex = BOARD.CurrentPlayerIndex ,
                EdgeOwners = edgeOwners ,
                BoxOwners = boxOwners ,
                PlayerOneScore = BOARD.PlayerOneScore ,
                PlayerTwoScore = BOARD.PlayerTwoScore ,
                GameResult = BOARD.GameResult
            };
        }

        private static ConfirmEdgeResponse CreateFailureResponse(Guid requestId , MATCH_COMMAND_ERROR_ENUM error , MatchSnapshot snapshot)
        {
            return new ConfirmEdgeResponse
            {
                RequestId = requestId ,
                IsAccepted = false ,
                Error = error ,
                ShouldRequestSync = false ,
                Snapshot = snapshot
            };
        }

        private static MATCH_COMMAND_ERROR_ENUM MapMoveError(MOVE_ERROR_ENUM moveError)
        {
            switch ( moveError )
            {
                case MOVE_ERROR_ENUM.NONE:
                    return MATCH_COMMAND_ERROR_ENUM.NONE;

                case MOVE_ERROR_ENUM.INVALID_PLAYER:
                    return MATCH_COMMAND_ERROR_ENUM.NOT_A_MATCH_PLAYER;

                case MOVE_ERROR_ENUM.NOT_YOUR_TURN:
                    return MATCH_COMMAND_ERROR_ENUM.NOT_YOUR_TURN;

                case MOVE_ERROR_ENUM.INVALID_EDGE:
                    return MATCH_COMMAND_ERROR_ENUM.INVALID_EDGE;

                case MOVE_ERROR_ENUM.EDGE_ALREADY_CONFIRMED:
                    return MATCH_COMMAND_ERROR_ENUM.EDGE_ALREADY_CONFIRMED;

                case MOVE_ERROR_ENUM.GAME_ALREADY_FINISHED:
                    return MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE;

                default:
                    return MATCH_COMMAND_ERROR_ENUM.INTERNAL_ERROR;
            }
        }

        private void ThrowIfDisposed()
        {
            if ( _isDisposed )
            {
                throw new ObjectDisposedException(nameof(LocalGameSession));
            }
        }

        private void SetConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            if ( ConnectionState == connectionState )
            {
                return;
            }

            ConnectionState = connectionState;
            ConnectionStateChanged?.Invoke(connectionState);
        }
    }
}
