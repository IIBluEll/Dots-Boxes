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

        private GAME_RESULT_ENUM _gameResult;

        public Guid MatchId { get; }
        public MatchPlayer PlayerOne { get; }
        public MatchPlayer PlayerTwo { get; }

        public long Revision { get; private set; }
        public SERVER_MATCH_STATE_ENUM MatchState { get; private set; }

        public PLAYER_INDEX_ENUM CurrentPlayerIndex => BOARD.CurrentPlayerIndex;
        public GAME_RESULT_ENUM GameResult => _gameResult;

        public MatchRoom(Guid matchId , MatchPlayer playerOne , MatchPlayer playerTwo , PLAYER_INDEX_ENUM startingPlayerIndex)
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

            MatchId = matchId;
            BOARD = new DotsBoard(startingPlayerIndex);

            Revision = 0;
            MatchState = SERVER_MATCH_STATE_ENUM.ACTIVE;
            _gameResult = GAME_RESULT_ENUM.IN_PROGRESS;
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

                EdgeOwners = edgeOwners ,
                BoxOwners = boxOwners ,

                PlayerOneScore = BOARD.PlayerOneScore ,
                PlayerTwoScore = BOARD.PlayerTwoScore ,

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

        public async Task<MatchSnapshot?> TryForfeit_async(
            Guid userId ,
            CancellationToken cancellationToken = default)
        {
            await COMMAND_LOCK.WaitAsync(cancellationToken);

            try
            {
                if ( MatchState != SERVER_MATCH_STATE_ENUM.ACTIVE ||
                     !TryGetPlayerIndex(userId , out PLAYER_INDEX_ENUM playerIndex) )
                {
                    return null;
                }

                MatchState = SERVER_MATCH_STATE_ENUM.FINISHED;
                _gameResult = playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                    ? GAME_RESULT_ENUM.PLAYER_TWO_WIN
                    : GAME_RESULT_ENUM.PLAYER_ONE_WIN;

                Revision++;
                return CreateSnapshotLocked();
            }
            finally
            {
                COMMAND_LOCK.Release();
            }
        }
    }
}
