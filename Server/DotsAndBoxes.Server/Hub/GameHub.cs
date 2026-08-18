using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Server.Matchmaking;
using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR;

namespace DotsAndBoxes.Server.Hubs
{
    public sealed class GameHub : Hub<IGameClient>
    {
        private const string SIMULATE_RESPONSE_LOSS_QUERY_KEY = "simulateConfirmResponseLossOnce";
        private const string RESPONSE_LOSS_SIMULATED_ITEM_KEY = "confirm-response-loss-simulated";

        private readonly MatchRoomProvider MATCH_ROOM_PROVIDER;
        private readonly MatchConnectionRegistry MATCH_CONNECTION_REGISTRY;
        private readonly MatchRoomLifecycleService MATCH_ROOM_LIFECYCLE_SERVICE;
        private readonly MatchRoomFactory MATCH_ROOM_FACTORY;
        private readonly MatchmakingQueue MATCHMAKING_QUEUE;
        private readonly IHostApplicationLifetime APPLICATION_LIFETIME;
        private readonly IHostEnvironment HOST_ENVIRONMENT;
        private readonly ILogger<GameHub> LOGGER;

        public GameHub(
            MatchRoomProvider matchRoomProvider ,
            MatchConnectionRegistry matchConnectionRegistry ,
            MatchRoomLifecycleService matchRoomLifecycleService ,
            MatchRoomFactory matchRoomFactory ,
            MatchmakingQueue matchmakingQueue ,
            IHostApplicationLifetime applicationLifetime ,
            IHostEnvironment hostEnvironment ,
            ILogger<GameHub> logger)
        {
            MATCH_ROOM_PROVIDER = matchRoomProvider ??
                throw new ArgumentNullException(nameof(matchRoomProvider));

            MATCH_CONNECTION_REGISTRY = matchConnectionRegistry ??
                throw new ArgumentNullException(nameof(matchConnectionRegistry));

            MATCH_ROOM_LIFECYCLE_SERVICE = matchRoomLifecycleService ??
                throw new ArgumentNullException(nameof(matchRoomLifecycleService));

            MATCH_ROOM_FACTORY = matchRoomFactory ??
                throw new ArgumentNullException(nameof(matchRoomFactory));

            MATCHMAKING_QUEUE = matchmakingQueue ??
                throw new ArgumentNullException(nameof(matchmakingQueue));

            APPLICATION_LIFETIME = applicationLifetime ??
                throw new ArgumentNullException(nameof(applicationLifetime));

            HOST_ENVIRONMENT = hostEnvironment ??
                throw new ArgumentNullException(nameof(hostEnvironment));

            LOGGER = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MatchSnapshot> JoinMatch(Guid matchId)
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            MatchRoom matchRoom = GetParticipantRoom(matchId, userId);
            MatchSnapshot currentSnapshot = await matchRoom.CreateSnapshot_async(
                Context.ConnectionAborted);

            if ( currentSnapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED ||
                 currentSnapshot.MatchState == SERVER_MATCH_STATE_ENUM.CANCELLED )
            {
                throw new HubException("이미 종료된 매치에는 참가할 수 없습니다.");
            }

            if ( !MATCH_CONNECTION_REGISTRY.TryRegister(
                Context.ConnectionId ,
                matchId ,
                userId) )
            {
                throw new HubException("이미 활성화된 Match 연결이 있습니다.");
            }

            try
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId ,
                    CreateMatchGroupName(matchId) ,
                    Context.ConnectionAborted);

                MatchRoomUpdateResult joinResult =
                    await matchRoom.MarkPlayerJoined_async(
                        userId ,
                        Context.ConnectionAborted);

                if ( joinResult.HasStateChanged )
                {
                    await Clients
                        .Group(CreateMatchGroupName(matchId))
                        .MatchStateChanged(joinResult.Snapshot);
                }

                LOGGER.LogInformation(
                    "User joined match. MatchId={MatchId}, UserId={UserId}, ConnectionId={ConnectionId}",
                    matchId ,
                    userId ,
                    Context.ConnectionId);

                return joinResult.Snapshot;
            }
            catch
            {
                MATCH_CONNECTION_REGISTRY.TryRemove(
                    Context.ConnectionId ,
                    out _ ,
                    out _);

                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId ,
                    CreateMatchGroupName(matchId));

                throw;
            }
        }

        public async Task<MatchSnapshot> ReadyMatch(Guid matchId)
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            if ( !MATCH_CONNECTION_REGISTRY.TryGetParticipant(
                    Context.ConnectionId ,
                    out Guid registeredMatchId ,
                    out Guid registeredUserId) ||
                 registeredMatchId != matchId ||
                 registeredUserId != userId )
            {
                throw new HubException("JoinMatch가 완료되지 않은 연결입니다.");
            }

            MatchRoom matchRoom = GetParticipantRoom(matchId , userId);
            MatchRoomUpdateResult readyResult;

            try
            {
                readyResult = await matchRoom.MarkPlayerReady_async(
                    userId ,
                    Context.ConnectionAborted);
            }
            catch ( InvalidOperationException exception )
            {
                throw new HubException(exception.Message);
            }

            if ( readyResult.HasStateChanged )
            {
                await Clients
                    .Group(CreateMatchGroupName(matchId))
                    .MatchStateChanged(readyResult.Snapshot);
            }

            LOGGER.LogInformation(
                "Player ready state confirmed. MatchId={MatchId}, UserId={UserId}, MatchState={MatchState}, Revision={Revision}",
                matchId ,
                userId ,
                readyResult.Snapshot.MatchState ,
                readyResult.Snapshot.Revision);

            return readyResult.Snapshot;
        }

        public async Task<MatchAssignment?> EnterMatchmaking()
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            if ( MATCH_CONNECTION_REGISTRY.ContainsUser(userId) ||
                 MATCH_ROOM_PROVIDER.ContainsUserInOpenMatch(userId) )
            {
                throw new HubException("이미 진행 중인 매치가 있습니다.");
            }

            MatchmakingEnqueueResult enqueueResult = MATCHMAKING_QUEUE.Enqueue(userId);

            if ( enqueueResult.State == MATCHMAKING_ENQUEUE_STATE_ENUM.ALREADY_QUEUED )
            {
                throw new HubException("이미 매칭 대기열에 있습니다.");
            }

            if ( enqueueResult.State == MATCHMAKING_ENQUEUE_STATE_ENUM.QUEUED )
            {
                LOGGER.LogInformation(
                    "User entered matchmaking queue. UserId={UserId}, QueueCount={QueueCount}",
                    userId ,
                    MATCHMAKING_QUEUE.Count);

                return null;
            }

            Guid playerOneUserId = enqueueResult.OpponentUserId;
            Guid playerTwoUserId = userId;
            MatchRoom matchRoom = CreateAndRegisterMatchRoom(
                playerOneUserId ,
                playerTwoUserId);

            MatchAssignment playerOneAssignment = new MatchAssignment
            {
                MatchId = matchRoom.MatchId ,
                LocalPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE ,
                OpponentUserId = playerTwoUserId
            };

            MatchAssignment playerTwoAssignment = new MatchAssignment
            {
                MatchId = matchRoom.MatchId ,
                LocalPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_TWO ,
                OpponentUserId = playerOneUserId
            };

            await Task.WhenAll(
                Clients.User(playerOneUserId.ToString()).MatchFound(playerOneAssignment) ,
                Clients.User(playerTwoUserId.ToString()).MatchFound(playerTwoAssignment));

            LOGGER.LogInformation(
                "Matchmaking completed. MatchId={MatchId}, PlayerOneUserId={PlayerOneUserId}, PlayerTwoUserId={PlayerTwoUserId}, StartingPlayer={StartingPlayer}",
                matchRoom.MatchId ,
                playerOneUserId ,
                playerTwoUserId ,
                matchRoom.CurrentPlayerIndex);

            return playerTwoAssignment;
        }

        public bool CancelMatchmaking()
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            bool wasCancelled = MATCHMAKING_QUEUE.TryCancel(userId);

            if ( wasCancelled )
            {
                LOGGER.LogInformation(
                    "User cancelled matchmaking. UserId={UserId}, QueueCount={QueueCount}",
                    userId ,
                    MATCHMAKING_QUEUE.Count);
            }

            return wasCancelled;
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                if ( TryGetAuthenticatedUserId(out Guid disconnectedUserId) )
                {
                    if ( MATCHMAKING_QUEUE.TryCancel(disconnectedUserId) )
                    {
                        LOGGER.LogInformation(
                            "Disconnected user removed from matchmaking queue. UserId={UserId}",
                            disconnectedUserId);
                    }
                }

                if ( MATCH_CONNECTION_REGISTRY.TryRemove(
                        Context.ConnectionId ,
                        out Guid matchId ,
                        out Guid userId) &&
                     !APPLICATION_LIFETIME.ApplicationStopping.IsCancellationRequested &&
                     MATCH_ROOM_PROVIDER.TryGet(matchId , out MatchRoom? matchRoom) &&
                     matchRoom != null )
                {
                    MatchSnapshot? finalSnapshot =
                        await matchRoom.TryHandlePlayerExit_async(userId);

                    if ( finalSnapshot != null )
                    {
                        LOGGER.LogInformation(
                            "Disconnected player changed match state. MatchId={MatchId}, UserId={UserId}, Revision={Revision}, MatchState={MatchState}",
                            matchId ,
                            userId ,
                            finalSnapshot.Revision ,
                            finalSnapshot.MatchState);

                        await Clients
                            .Group(CreateMatchGroupName(matchId))
                            .MatchStateChanged(finalSnapshot);
                    }

                    await MATCH_ROOM_LIFECYCLE_SERVICE
                        .TryRemoveTerminalWithoutConnections_async(matchId);
                }
            }
            finally
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        public async Task<ConfirmEdgeResponse> ConfirmEdge(
            ConfirmEdgeRequest request)
        {
            if ( request == null )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    Guid.Empty ,
                    MATCH_COMMAND_ERROR_ENUM.INVALID_REQUEST ,
                    false);
            }

            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.UNAUTHORIZED ,
                    false);
            }

            if ( !MATCH_ROOM_PROVIDER.TryGet(
                    request.MatchId ,
                    out MatchRoom? matchRoom) ||
                matchRoom == null )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_FOUND ,
                    false);
            }

            ConfirmEdgeResponse response =
                await matchRoom.ConfirmEdge_async(
                    userId,
                    request,
                    Context.ConnectionAborted);

            if ( response.IsAccepted && response.Snapshot != null )
            {
                LOGGER.LogInformation(
                    "ConfirmEdge accepted. MatchId={MatchId}, UserId={UserId}, RequestId={RequestId}, Revision={Revision}, EdgeId={EdgeId}",
                    request.MatchId ,
                    userId ,
                    request.RequestId ,
                    response.Snapshot.Revision ,
                    request.EdgeId);

                string matchGroupName = CreateMatchGroupName(request.MatchId);

                if ( TryConsumeConfirmResponseLossSimulation() )
                {
                    await Clients
                        .OthersInGroup(matchGroupName)
                        .MatchStateChanged(response.Snapshot);

                    return response;
                }

                await Clients
                    .Group(matchGroupName)
                    .MatchStateChanged(response.Snapshot);
            }
            else
            {
                LOGGER.LogWarning(
                    "ConfirmEdge rejected. MatchId={MatchId}, UserId={UserId}, RequestId={RequestId}, ExpectedRevision={ExpectedRevision}, EdgeId={EdgeId}, Error={Error}",
                    request.MatchId ,
                    userId ,
                    request.RequestId ,
                    request.ExpectedRevision ,
                    request.EdgeId ,
                    response.Error);
            }

            return response;
        }

        public async Task<MatchSnapshot> RequestSync(Guid matchId)
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            MatchRoom matchRoom = GetParticipantRoom(matchId, userId);

            return await matchRoom.CreateSnapshot_async(
                Context.ConnectionAborted);
        }

        public async Task<MatchSnapshot> LeaveMatch(Guid matchId)
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            if ( !MATCH_CONNECTION_REGISTRY.TryGetParticipant(
                    Context.ConnectionId ,
                    out Guid registeredMatchId ,
                    out Guid registeredUserId) ||
                 registeredMatchId != matchId ||
                 registeredUserId != userId )
            {
                throw new HubException("현재 연결은 해당 매치에 참가하고 있지 않습니다.");
            }

            MatchRoom matchRoom = GetParticipantRoom(matchId , userId);
            MatchSnapshot? exitSnapshot =
                await matchRoom.TryHandlePlayerExit_async(
                    userId ,
                    Context.ConnectionAborted);

            MatchSnapshot finalSnapshot = exitSnapshot ??
                await matchRoom.CreateSnapshot_async(Context.ConnectionAborted);

            if ( exitSnapshot != null )
            {
                await Clients
                    .Group(CreateMatchGroupName(matchId))
                    .MatchStateChanged(finalSnapshot);
            }

            MATCH_CONNECTION_REGISTRY.TryRemove(
                Context.ConnectionId ,
                out _ ,
                out _);

            try
            {
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId ,
                    CreateMatchGroupName(matchId));
            }
            finally
            {
                await MATCH_ROOM_LIFECYCLE_SERVICE
                    .TryRemoveTerminalWithoutConnections_async(matchId);
            }

            LOGGER.LogInformation(
                "User left match. MatchId={MatchId}, UserId={UserId}, Revision={Revision}, MatchState={MatchState}",
                matchId ,
                userId ,
                finalSnapshot.Revision ,
                finalSnapshot.MatchState);

            return finalSnapshot;
        }

        private MatchRoom GetParticipantRoom(Guid matchId , Guid userId)
        {
            if ( !MATCH_ROOM_PROVIDER.TryGet(
                    matchId ,
                    out MatchRoom? matchRoom) ||
                matchRoom == null )
            {
                throw new HubException("매치를 찾을 수 없습니다.");
            }

            if ( !matchRoom.TryGetPlayerIndex(userId , out _) )
            {
                throw new HubException("매치 참가자가 아닙니다.");
            }

            return matchRoom;
        }

        private MatchRoom CreateAndRegisterMatchRoom(
            Guid playerOneUserId ,
            Guid playerTwoUserId)
        {
            const int MAX_CREATE_ATTEMPTS = 3;

            for ( int attempt = 0; attempt < MAX_CREATE_ATTEMPTS; attempt++ )
            {
                MatchRoom matchRoom = MATCH_ROOM_FACTORY.Create(
                    new MatchPlayer(playerOneUserId) ,
                    new MatchPlayer(playerTwoUserId));

                if ( MATCH_ROOM_PROVIDER.TryAdd(matchRoom) )
                {
                    return matchRoom;
                }
            }

            throw new HubException("매치 방을 생성하지 못했습니다. 잠시 후 다시 시도해 주세요.");
        }

        private bool TryGetAuthenticatedUserId(out Guid userId)
        {
            return Guid.TryParse(Context.UserIdentifier , out userId) &&
                   userId != Guid.Empty;
        }

        private bool TryConsumeConfirmResponseLossSimulation()
        {
            if ( !HOST_ENVIRONMENT.IsDevelopment() ||
                 Context.Items.ContainsKey(RESPONSE_LOSS_SIMULATED_ITEM_KEY) )
            {
                return false;
            }

            string? requestedValue = Context
                .GetHttpContext()?
                .Request
                .Query[ SIMULATE_RESPONSE_LOSS_QUERY_KEY ]
                .ToString();

            if ( !bool.TryParse(requestedValue , out bool shouldSimulate) || !shouldSimulate )
            {
                return false;
            }

            Context.Items[ RESPONSE_LOSS_SIMULATED_ITEM_KEY ] = true;
            return true;
        }

        internal static string CreateMatchGroupName(Guid matchId)
        {
            return $"match-{matchId:N}";
        }
    }
}
