using DotsAndBoxes.Shared;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchRoomFactory
    {
        private readonly TimeSpan TURN_DURATION;
        private readonly int MAX_TIMEOUTS_PER_PLAYER;

        public MatchRoomFactory(IOptions<MatchTimingOptions> options)
        {
            MatchTimingOptions timingOptions = options?.Value ??
                throw new ArgumentNullException(nameof(options));

            if ( timingOptions.TurnDurationSeconds <= 0 )
            {
                throw new InvalidOperationException("MatchTiming:TurnDurationSeconds는 1 이상이어야 합니다.");
            }

            if ( timingOptions.MaxTimeoutsPerPlayer <= 0 )
            {
                throw new InvalidOperationException("MatchTiming:MaxTimeoutsPerPlayer는 1 이상이어야 합니다.");
            }

            TURN_DURATION = TimeSpan.FromSeconds(timingOptions.TurnDurationSeconds);
            MAX_TIMEOUTS_PER_PLAYER = timingOptions.MaxTimeoutsPerPlayer;
        }

        public MatchRoom Create(
            MatchPlayer playerOne ,
            MatchPlayer playerTwo ,
            PLAYER_INDEX_ENUM? startingPlayerIndex = null)
        {
            PLAYER_INDEX_ENUM resolvedStartingPlayerIndex = startingPlayerIndex ??
                (RandomNumberGenerator.GetInt32(2) == 0
                    ? PLAYER_INDEX_ENUM.PLAYER_ONE
                    : PLAYER_INDEX_ENUM.PLAYER_TWO);

            return new MatchRoom(
                Guid.NewGuid() ,
                playerOne ,
                playerTwo ,
                resolvedStartingPlayerIndex ,
                TURN_DURATION ,
                MAX_TIMEOUTS_PER_PLAYER);
        }
    }
}
