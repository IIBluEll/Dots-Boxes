using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchRoomLifecycleService
    {
        private readonly MatchRoomProvider MATCH_ROOM_PROVIDER;
        private readonly MatchConnectionRegistry MATCH_CONNECTION_REGISTRY;
        private readonly ILogger<MatchRoomLifecycleService> LOGGER;

        public MatchRoomLifecycleService(
            MatchRoomProvider matchRoomProvider ,
            MatchConnectionRegistry matchConnectionRegistry ,
            ILogger<MatchRoomLifecycleService> logger)
        {
            MATCH_ROOM_PROVIDER = matchRoomProvider ??
                throw new ArgumentNullException(nameof(matchRoomProvider));

            MATCH_CONNECTION_REGISTRY = matchConnectionRegistry ??
                throw new ArgumentNullException(nameof(matchConnectionRegistry));

            LOGGER = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> TryRemoveFinishedWithoutConnections_async(
            Guid matchId ,
            CancellationToken cancellationToken = default)
        {
            if ( matchId == Guid.Empty ||
                 MATCH_CONNECTION_REGISTRY.GetMatchConnectionCount(matchId) != 0 ||
                 !MATCH_ROOM_PROVIDER.TryGet(matchId , out MatchRoom? matchRoom) ||
                 matchRoom == null )
            {
                return false;
            }

            MatchSnapshot snapshot =
                await matchRoom.CreateSnapshot_async(cancellationToken);

            if ( snapshot.MatchState != SERVER_MATCH_STATE_ENUM.FINISHED ||
                 MATCH_CONNECTION_REGISTRY.GetMatchConnectionCount(matchId) != 0 )
            {
                return false;
            }

            bool removed = MATCH_ROOM_PROVIDER.TryRemove(matchId , out _);

            if ( removed )
            {
                LOGGER.LogInformation(
                    "MatchRoom removed. MatchId={MatchId} Revision={Revision} Result={GameResult}" ,
                    matchId ,
                    snapshot.Revision ,
                    snapshot.GameResult);
            }

            return removed;
        }
    }
}
