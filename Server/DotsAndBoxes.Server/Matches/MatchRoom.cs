using DotsAndBoxes.Shared;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchRoom
    {
        private readonly DotsBoard BOARD;
        private readonly SemaphoreSlim COMMAND_LOCK = new SemaphoreSlim(1, 1);
        private readonly Dictionary<Guid, ProcessedConfirmRequest> PROCESSED_CONFIRM_REQUESTS = new Dictionary<Guid, ProcessedConfirmRequest>();
        private readonly TimeSpan TURN_DURATION;
        private readonly int MAX_TIMEOUTS_PER_PLAYER;
        private readonly Func<DateTimeOffset> UTC_NOW_PROVIDER;
        private readonly Random RANDOM;
        private readonly TimeSpan JOIN_TIMEOUT;
        private readonly TimeSpan READY_TIMEOUT;
        private readonly TimeSpan START_COUNTDOWN;

        private GAME_RESULT_ENUM _gameResult;
        private bool _isPlayerOneJoined;
        private bool _isPlayerTwoJoined;

        public Guid MatchId { get; }
        public MatchPlayer PlayerOne { get; }
        public MatchPlayer PlayerTwo { get; }

        public long Revision { get; private set; }
        public SERVER_MATCH_STATE_ENUM MatchState { get; private set; }
        public DateTimeOffset? TurnDeadlineUtc { get; private set; }
        public int PlayerOneTimeoutCount { get; private set; }
        public int PlayerTwoTimeoutCount { get; private set; }
        public bool PlayerOneReady { get; private set; }
        public bool PlayerTwoReady { get; private set; }
        public DateTimeOffset? JoinDeadlineUtc { get; private set; }
        public DateTimeOffset? ReadyDeadlineUtc { get; private set; }
        public DateTimeOffset? MatchStartUtc { get; private set; }

        public PLAYER_INDEX_ENUM CurrentPlayerIndex => BOARD.CurrentPlayerIndex;
        public GAME_RESULT_ENUM GameResult => _gameResult;

        public MatchRoom(
            Guid matchId ,
            MatchPlayer playerOne ,
            MatchPlayer playerTwo ,
            PLAYER_INDEX_ENUM startingPlayerIndex ,
            TimeSpan? turnDuration = null ,
            int maxTimeoutsPerPlayer = 3 ,
            Func<DateTimeOffset>? utcNowProvider = null ,
            Random? random = null ,
            TimeSpan? joinTimeout = null ,
            TimeSpan? readyTimeout = null ,
            TimeSpan? startCountdown = null)
        {
            if ( matchId == Guid.Empty )
            {
                throw new ArgumentException("MatchId는 Guid.Empty일 수 없습니다." , nameof(matchId));
            }

            PlayerOne = playerOne ?? throw new ArgumentNullException(nameof(playerOne));
            PlayerTwo = playerTwo ?? throw new ArgumentNullException(nameof(playerTwo));

            if ( playerOne.UserId == playerTwo.UserId )
            {
                throw new ArgumentException("같은 사용자를 두 Player Slot에 배치할 수 없습니다.");
            }

            if ( startingPlayerIndex != PLAYER_INDEX_ENUM.PLAYER_ONE && startingPlayerIndex != PLAYER_INDEX_ENUM.PLAYER_TWO )
            {
                throw new ArgumentOutOfRangeException(nameof(startingPlayerIndex));
            }

            TimeSpan resolvedTurnDuration = turnDuration ?? TimeSpan.FromSeconds(20);
            TimeSpan resolvedJoinTimeout = joinTimeout ?? TimeSpan.FromSeconds(10);
            TimeSpan resolvedReadyTimeout = readyTimeout ?? TimeSpan.FromSeconds(15);
            TimeSpan resolvedStartCountdown = startCountdown ?? TimeSpan.FromSeconds(3);

            if ( resolvedTurnDuration <= TimeSpan.Zero )
            {
                throw new ArgumentOutOfRangeException(nameof(turnDuration));
            }

            if ( maxTimeoutsPerPlayer <= 0 )
            {
                throw new ArgumentOutOfRangeException(nameof(maxTimeoutsPerPlayer));
            }

            if ( resolvedJoinTimeout <= TimeSpan.Zero )
            {
                throw new ArgumentOutOfRangeException(nameof(joinTimeout));
            }

            if ( resolvedReadyTimeout <= TimeSpan.Zero )
            {
                throw new ArgumentOutOfRangeException(nameof(readyTimeout));
            }

            if ( resolvedStartCountdown <= TimeSpan.Zero )
            {
                throw new ArgumentOutOfRangeException(nameof(startCountdown));
            }

            MatchId = matchId;
            BOARD = new DotsBoard(startingPlayerIndex);
            TURN_DURATION = resolvedTurnDuration;
            MAX_TIMEOUTS_PER_PLAYER = maxTimeoutsPerPlayer;
            UTC_NOW_PROVIDER = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
            RANDOM = random ?? new Random();
            JOIN_TIMEOUT = resolvedJoinTimeout;
            READY_TIMEOUT = resolvedReadyTimeout;
            START_COUNTDOWN = resolvedStartCountdown;

            Revision = 0;
            MatchState = SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS;
            _gameResult = GAME_RESULT_ENUM.IN_PROGRESS;
            JoinDeadlineUtc = UTC_NOW_PROVIDER() + JOIN_TIMEOUT;
        }

        public bool TryGetPlayerIndex(Guid userId , out PLAYER_INDEX_ENUM playerIndex)
        {
            if ( PlayerOne.UserId == userId )
            {
                playerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE;
                return true;
            }

            if ( PlayerTwo.UserId == userId )
            {
                playerIndex = PLAYER_INDEX_ENUM.PLAYER_TWO;
                return true;
            }

            playerIndex = PLAYER_INDEX_ENUM.NONE;
            return false;
        }

        public async Task<MatchRoomUpdateResult> MarkPlayerJoined_async(
            Guid userId ,
            CancellationToken cancellationToken = default)
        {
            await COMMAND_LOCK.WaitAsync(cancellationToken);

            try
            {
                if ( !TryGetPlayerIndex(userId , out PLAYER_INDEX_ENUM playerIndex) )
                {
                    throw new InvalidOperationException("Match 참가자가 아닙니다.");
                }

                if ( IsTerminalState(MatchState) )
                {
                    throw new InvalidOperationException("이미 종료된 Match에는 참가할 수 없습니다.");
                }

                bool wasAlreadyJoined = playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                    ? _isPlayerOneJoined
                    : _isPlayerTwoJoined;

                if ( wasAlreadyJoined )
                {
                    return new MatchRoomUpdateResult(false , CreateSnapshotLocked());
                }

                if ( playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE )
                {
                    _isPlayerOneJoined = true;
                }
                else
                {
                    _isPlayerTwoJoined = true;
                }

                bool hasStateChanged = false;

                if ( _isPlayerOneJoined &&
                     _isPlayerTwoJoined &&
                     MatchState == SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS )
                {
                    MatchState = SERVER_MATCH_STATE_ENUM.WAITING_FOR_READY;
                    JoinDeadlineUtc = null;
                    ReadyDeadlineUtc = UTC_NOW_PROVIDER() + READY_TIMEOUT;
                    Revision++;
                    hasStateChanged = true;
                }

                return new MatchRoomUpdateResult(
                    hasStateChanged ,
                    CreateSnapshotLocked());
            }
            finally
            {
                COMMAND_LOCK.Release();
            }
        }

        public async Task<MatchRoomUpdateResult> MarkPlayerReady_async(
            Guid userId ,
            CancellationToken cancellationToken = default)
        {
            await COMMAND_LOCK.WaitAsync(cancellationToken);

            try
            {
                if ( !TryGetPlayerIndex(userId , out PLAYER_INDEX_ENUM playerIndex) )
                {
                    throw new InvalidOperationException("Match 참가자가 아닙니다.");
                }

                bool isJoined = playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                    ? _isPlayerOneJoined
                    : _isPlayerTwoJoined;

                if ( !isJoined )
                {
                    throw new InvalidOperationException("JoinMatch가 완료되지 않은 참가자입니다.");
                }

                bool wasAlreadyReady = playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                    ? PlayerOneReady
                    : PlayerTwoReady;

                if ( wasAlreadyReady )
                {
                    return new MatchRoomUpdateResult(false , CreateSnapshotLocked());
                }

                if ( MatchState != SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS &&
                     MatchState != SERVER_MATCH_STATE_ENUM.WAITING_FOR_READY )
                {
                    throw new InvalidOperationException("현재 Match 상태에서는 Ready를 처리할 수 없습니다.");
                }

                if ( playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE )
                {
                    PlayerOneReady = true;
                }
                else
                {
                    PlayerTwoReady = true;
                }

                if ( _isPlayerOneJoined &&
                     _isPlayerTwoJoined &&
                     PlayerOneReady &&
                     PlayerTwoReady )
                {
                    MatchState = SERVER_MATCH_STATE_ENUM.STARTING;
                    JoinDeadlineUtc = null;
                    ReadyDeadlineUtc = null;
                    MatchStartUtc = UTC_NOW_PROVIDER() + START_COUNTDOWN;
                }

                Revision++;

                return new MatchRoomUpdateResult(true , CreateSnapshotLocked());
            }
            finally
            {
                COMMAND_LOCK.Release();
            }
        }

        public async Task<ConfirmEdgeResponse> ConfirmEdge_async(
    Guid userId ,
    ConfirmEdgeRequest request ,
    CancellationToken cancellationToken = default)
        {
            if ( request == null )
            {
                throw new ArgumentNullException(nameof(request));
            }

            await COMMAND_LOCK.WaitAsync(cancellationToken);

            try
            {
                return ConfirmEdgeLocked(userId , request);
            }
            finally
            {
                COMMAND_LOCK.Release();
            }
        }

        private ConfirmEdgeResponse ConfirmEdgeLocked(Guid userId , ConfirmEdgeRequest request)
        {
            if ( request.MatchId != MatchId )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_FOUND ,
                    false);
            }

            if ( !TryGetPlayerIndex(userId , out PLAYER_INDEX_ENUM playerIndex) )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.NOT_A_MATCH_PLAYER ,
                    false);
            }

            if ( request.RequestId == Guid.Empty )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.INVALID_REQUEST ,
                    false ,
                    CreateSnapshotLocked());
            }

            if ( PROCESSED_CONFIRM_REQUESTS.TryGetValue(
                request.RequestId ,
                out ProcessedConfirmRequest? processedRequest) )
            {
                if ( processedRequest.Matches(userId , request) )
                {
                    return processedRequest.Response;
                }

                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.DUPLICATE_REQUEST_CONFLICT ,
                    false ,
                    CreateSnapshotLocked());
            }

            if ( MatchState != SERVER_MATCH_STATE_ENUM.ACTIVE )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE ,
                    false ,
                    CreateSnapshotLocked());
            }

            if ( request.ExpectedRevision != Revision )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.REVISION_MISMATCH ,
                    true ,
                    CreateSnapshotLocked());
            }

            MoveResult moveResult = DotsRule.TryConfirmEdge(BOARD, playerIndex, request.EdgeId);

            if ( !moveResult.IsValid )
            {
                MATCH_COMMAND_ERROR_ENUM commandError = MatchCommandErrorMapper.ToMatchCommandError(moveResult.Error);

                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    commandError ,
                    false ,
                    CreateSnapshotLocked());
            }

            Revision++;

            if ( moveResult.IsGameFinished )
            {
                MatchState = SERVER_MATCH_STATE_ENUM.FINISHED;
                _gameResult = BOARD.GameResult;
                TurnDeadlineUtc = null;
            }
            else
            {
                TurnDeadlineUtc = UTC_NOW_PROVIDER() + TURN_DURATION;
            }

            ConfirmEdgeResponse response = ConfirmEdgeResponseFactory.CreateSuccess(request.RequestId, CreateSnapshotLocked());

            return CacheResponse(userId , request , response);
        }

        private ConfirmEdgeResponse CacheResponse(Guid userId , ConfirmEdgeRequest request , ConfirmEdgeResponse response)
        {
            PROCESSED_CONFIRM_REQUESTS.Add(request.RequestId, new ProcessedConfirmRequest(userId , request , response));

            return response;
        }

        private MatchSnapshot CreateSnapshotLocked()
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
                Revision = Revision ,
                MatchState = MatchState ,

                PlayerOneUserId = PlayerOne.UserId ,
                PlayerTwoUserId = PlayerTwo.UserId ,
                CurrentPlayerIndex = BOARD.CurrentPlayerIndex ,

                PlayerOneReady = PlayerOneReady ,
                PlayerTwoReady = PlayerTwoReady ,
                JoinDeadlineUtc = JoinDeadlineUtc ,
                ReadyDeadlineUtc = ReadyDeadlineUtc ,
                MatchStartUtc = MatchStartUtc ,

                EdgeOwners = edgeOwners ,
                BoxOwners = boxOwners ,

                PlayerOneScore = BOARD.PlayerOneScore ,
                PlayerTwoScore = BOARD.PlayerTwoScore ,

                TurnDeadlineUtc = TurnDeadlineUtc ,
                PlayerOneTimeoutCount = PlayerOneTimeoutCount ,
                PlayerTwoTimeoutCount = PlayerTwoTimeoutCount ,

                GameResult = _gameResult
            };
        }

        public async Task<MatchSnapshot> CreateSnapshot_async(CancellationToken cancellationToken = default)
        {
            await COMMAND_LOCK.WaitAsync(cancellationToken);

            try
            {
                return CreateSnapshotLocked();
            }
            finally
            {
                COMMAND_LOCK.Release();
            }
        }

        public async Task<MatchSnapshot?> TryHandlePlayerExit_async(
            Guid userId ,
            CancellationToken cancellationToken = default)
        {
            await COMMAND_LOCK.WaitAsync(cancellationToken);

            try
            {
                if ( IsTerminalState(MatchState) ||
                     !TryGetPlayerIndex(userId , out PLAYER_INDEX_ENUM playerIndex) )
                {
                    return null;
                }

                if ( MatchState == SERVER_MATCH_STATE_ENUM.ACTIVE )
                {
                    MatchState = SERVER_MATCH_STATE_ENUM.FINISHED;
                    _gameResult = playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                        ? GAME_RESULT_ENUM.PLAYER_TWO_WIN
                        : GAME_RESULT_ENUM.PLAYER_ONE_WIN;
                }
                else
                {
                    MatchState = SERVER_MATCH_STATE_ENUM.CANCELLED;
                    _gameResult = GAME_RESULT_ENUM.IN_PROGRESS;
                    MatchStartUtc = null;
                }

                JoinDeadlineUtc = null;
                ReadyDeadlineUtc = null;
                TurnDeadlineUtc = null;
                Revision++;
                return CreateSnapshotLocked();
            }
            finally
            {
                COMMAND_LOCK.Release();
            }
        }

        public async Task<MatchSnapshot?> TryAdvanceClock_async(
            DateTimeOffset utcNow ,
            CancellationToken cancellationToken = default)
        {
            await COMMAND_LOCK.WaitAsync(cancellationToken);

            try
            {
                if ( MatchState == SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS )
                {
                    if ( !JoinDeadlineUtc.HasValue || utcNow < JoinDeadlineUtc.Value )
                    {
                        return null;
                    }

                    return CancelMatchLocked();
                }

                if ( MatchState == SERVER_MATCH_STATE_ENUM.WAITING_FOR_READY )
                {
                    if ( !ReadyDeadlineUtc.HasValue || utcNow < ReadyDeadlineUtc.Value )
                    {
                        return null;
                    }

                    return CancelMatchLocked();
                }

                if ( MatchState == SERVER_MATCH_STATE_ENUM.STARTING )
                {
                    if ( !MatchStartUtc.HasValue || utcNow < MatchStartUtc.Value )
                    {
                        return null;
                    }

                    MatchState = SERVER_MATCH_STATE_ENUM.ACTIVE;
                    TurnDeadlineUtc = MatchStartUtc.Value + TURN_DURATION;
                    Revision++;

                    return CreateSnapshotLocked();
                }

                if ( MatchState != SERVER_MATCH_STATE_ENUM.ACTIVE ||
                     !TurnDeadlineUtc.HasValue ||
                     utcNow < TurnDeadlineUtc.Value )
                {
                    return null;
                }

                PLAYER_INDEX_ENUM timedOutPlayerIndex = BOARD.CurrentPlayerIndex;
                int timeoutCount = IncrementTimeoutCount(timedOutPlayerIndex);

                if ( timeoutCount >= MAX_TIMEOUTS_PER_PLAYER )
                {
                    MatchState = SERVER_MATCH_STATE_ENUM.FINISHED;
                    _gameResult = timedOutPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                        ? GAME_RESULT_ENUM.PLAYER_TWO_WIN
                        : GAME_RESULT_ENUM.PLAYER_ONE_WIN;
                    TurnDeadlineUtc = null;
                    Revision++;

                    return CreateSnapshotLocked();
                }

                int edgeId = SelectRandomUnconfirmedEdgeId();
                MoveResult moveResult = DotsRule.TryConfirmEdge(
                    BOARD ,
                    timedOutPlayerIndex ,
                    edgeId);

                if ( !moveResult.IsValid )
                {
                    throw new InvalidOperationException(
                        $"서버가 선택한 Edge를 확정하지 못했습니다. EdgeId={edgeId}, Error={moveResult.Error}");
                }

                Revision++;

                if ( moveResult.IsGameFinished )
                {
                    MatchState = SERVER_MATCH_STATE_ENUM.FINISHED;
                    _gameResult = BOARD.GameResult;
                    TurnDeadlineUtc = null;
                }
                else
                {
                    TurnDeadlineUtc = utcNow + TURN_DURATION;
                }

                return CreateSnapshotLocked();
            }
            finally
            {
                COMMAND_LOCK.Release();
            }
        }

        private int IncrementTimeoutCount(PLAYER_INDEX_ENUM playerIndex)
        {
            if ( playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE )
            {
                PlayerOneTimeoutCount++;
                return PlayerOneTimeoutCount;
            }

            PlayerTwoTimeoutCount++;
            return PlayerTwoTimeoutCount;
        }

        private int SelectRandomUnconfirmedEdgeId()
        {
            List<int> unconfirmedEdgeIds = new List<int>();

            for ( int edgeId = 0; edgeId < BoardTopology.EDGE_COUNT; edgeId++ )
            {
                if ( BOARD.GetEdge(edgeId).OwnerPlayerIndex == PLAYER_INDEX_ENUM.NONE )
                {
                    unconfirmedEdgeIds.Add(edgeId);
                }
            }

            if ( unconfirmedEdgeIds.Count == 0 )
            {
                throw new InvalidOperationException("진행 중인 Match에 선택 가능한 Edge가 없습니다.");
            }

            return unconfirmedEdgeIds[ RANDOM.Next(unconfirmedEdgeIds.Count) ];
        }

        private MatchSnapshot CancelMatchLocked()
        {
            MatchState = SERVER_MATCH_STATE_ENUM.CANCELLED;
            _gameResult = GAME_RESULT_ENUM.IN_PROGRESS;
            JoinDeadlineUtc = null;
            ReadyDeadlineUtc = null;
            MatchStartUtc = null;
            TurnDeadlineUtc = null;
            Revision++;

            return CreateSnapshotLocked();
        }

        private static bool IsTerminalState(SERVER_MATCH_STATE_ENUM matchState)
        {
            return matchState == SERVER_MATCH_STATE_ENUM.FINISHED ||
                   matchState == SERVER_MATCH_STATE_ENUM.CANCELLED;
        }
    }
}
