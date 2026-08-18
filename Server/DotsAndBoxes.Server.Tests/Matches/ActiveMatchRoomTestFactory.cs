using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Tests.Matches
{
    internal static class ActiveMatchRoomTestFactory
    {
        public static async Task<MatchRoom> Create_async(
            PLAYER_INDEX_ENUM startingPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE ,
            TimeSpan? turnDuration = null ,
            int maxTimeoutsPerPlayer = 3 ,
            Func<DateTimeOffset>? utcNowProvider = null ,
            Random? random = null)
        {
            Func<DateTimeOffset> resolvedUtcNowProvider = utcNowProvider ??
                (() => DateTimeOffset.UtcNow);

            MatchRoom room = new MatchRoom(
                Guid.NewGuid() ,
                new MatchPlayer(Guid.NewGuid()) ,
                new MatchPlayer(Guid.NewGuid()) ,
                startingPlayerIndex ,
                turnDuration ,
                maxTimeoutsPerPlayer ,
                resolvedUtcNowProvider ,
                random);

            await room.MarkPlayerJoined_async(room.PlayerOne.UserId);
            await room.MarkPlayerJoined_async(room.PlayerTwo.UserId);
            await room.MarkPlayerReady_async(room.PlayerOne.UserId);

            MatchRoomUpdateResult startingResult =
                await room.MarkPlayerReady_async(room.PlayerTwo.UserId);

            DateTimeOffset matchStartUtc = startingResult.Snapshot.MatchStartUtc ??
                throw new InvalidOperationException("MatchStartUtc가 생성되지 않았습니다.");

            MatchSnapshot? activeSnapshot =
                await room.TryAdvanceClock_async(matchStartUtc);

            if ( activeSnapshot?.MatchState != SERVER_MATCH_STATE_ENUM.ACTIVE )
            {
                throw new InvalidOperationException("테스트용 MatchRoom을 ACTIVE로 전환하지 못했습니다.");
            }

            return room;
        }
    }
}
