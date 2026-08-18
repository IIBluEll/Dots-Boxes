using DotsAndBoxes.Server.Hubs;
using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchTimerService : BackgroundService
    {
        private readonly MatchRoomProvider MATCH_ROOM_PROVIDER;
        private readonly MatchRoomLifecycleService MATCH_ROOM_LIFECYCLE_SERVICE;
        private readonly IHubContext<GameHub, IGameClient> HUB_CONTEXT;
        private readonly ILogger<MatchTimerService> LOGGER;
        private readonly TimeSpan SWEEP_INTERVAL;

        public MatchTimerService(
            MatchRoomProvider matchRoomProvider ,
            MatchRoomLifecycleService matchRoomLifecycleService ,
            IHubContext<GameHub, IGameClient> hubContext ,
            IOptions<MatchTimingOptions> options ,
            ILogger<MatchTimerService> logger)
        {
            MATCH_ROOM_PROVIDER = matchRoomProvider ??
                throw new ArgumentNullException(nameof(matchRoomProvider));
            MATCH_ROOM_LIFECYCLE_SERVICE = matchRoomLifecycleService ??
                throw new ArgumentNullException(nameof(matchRoomLifecycleService));
            HUB_CONTEXT = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            LOGGER = logger ?? throw new ArgumentNullException(nameof(logger));

            MatchTimingOptions timingOptions = options?.Value ??
                throw new ArgumentNullException(nameof(options));

            if ( timingOptions.SweepIntervalMilliseconds <= 0 )
            {
                throw new InvalidOperationException("MatchTiming:SweepIntervalMilliseconds는 1 이상이어야 합니다.");
            }

            SWEEP_INTERVAL = TimeSpan.FromMilliseconds(timingOptions.SweepIntervalMilliseconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using PeriodicTimer timer = new PeriodicTimer(SWEEP_INTERVAL);

            while ( await timer.WaitForNextTickAsync(stoppingToken) )
            {
                await SweepExpiredTurns_async(stoppingToken);
            }
        }

        private async Task SweepExpiredTurns_async(CancellationToken cancellationToken)
        {
            IReadOnlyList<MatchRoom> matchRooms = MATCH_ROOM_PROVIDER.GetRoomsSnapshot();
            DateTimeOffset utcNow = DateTimeOffset.UtcNow;

            foreach ( MatchRoom matchRoom in matchRooms )
            {
                try
                {
                    MatchSnapshot? snapshot = await matchRoom.TryAdvanceClock_async(
                        utcNow ,
                        cancellationToken);

                    if ( snapshot == null )
                    {
                        continue;
                    }

                    LOGGER.LogInformation(
                        "Match clock transition handled. MatchId={MatchId}, Revision={Revision}, CurrentPlayer={CurrentPlayer}, PlayerOneTimeouts={PlayerOneTimeouts}, PlayerTwoTimeouts={PlayerTwoTimeouts}, MatchState={MatchState}",
                        snapshot.MatchId ,
                        snapshot.Revision ,
                        snapshot.CurrentPlayerIndex ,
                        snapshot.PlayerOneTimeoutCount ,
                        snapshot.PlayerTwoTimeoutCount ,
                        snapshot.MatchState);

                    await HUB_CONTEXT.Clients
                        .Group(GameHub.CreateMatchGroupName(snapshot.MatchId))
                        .MatchStateChanged(snapshot);

                    if ( snapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED ||
                         snapshot.MatchState == SERVER_MATCH_STATE_ENUM.CANCELLED )
                    {
                        await MATCH_ROOM_LIFECYCLE_SERVICE
                            .TryRemoveTerminalWithoutConnections_async(
                                snapshot.MatchId ,
                                cancellationToken);
                    }
                }
                catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested )
                {
                    throw;
                }
                catch ( Exception exception )
                {
                    LOGGER.LogError(
                        exception ,
                        "Match clock handling failed. MatchId={MatchId}",
                        matchRoom.MatchId);
                }
            }
        }
    }
}
