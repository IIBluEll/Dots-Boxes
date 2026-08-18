using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomTimeoutTests
    {
        private static readonly DateTimeOffset INITIAL_UTC =
            new DateTimeOffset(2026 , 8 , 18 , 0 , 0 , 0 , TimeSpan.Zero);

        [Test]
        public async Task TryHandleTurnTimeout_BeforeDeadline_DoesNotChangeMatch()
        {
            MatchRoom room = await CreateRoom_async(maxTimeoutsPerPlayer: 3);
            long initialRevision = room.Revision;

            MatchSnapshot? snapshot = await room.TryAdvanceClock_async(
                room.TurnDeadlineUtc!.Value.AddMilliseconds(-1));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot , Is.Null);
                Assert.That(room.Revision , Is.EqualTo(initialRevision));
                Assert.That(room.PlayerOneTimeoutCount , Is.Zero);
            });
        }

        [Test]
        public async Task TryHandleTurnTimeout_FirstTimeout_SelectsEdgeAndAdvancesTurn()
        {
            MatchRoom room = await CreateRoom_async(maxTimeoutsPerPlayer: 3);
            long initialRevision = room.Revision;
            DateTimeOffset timeoutUtc = room.TurnDeadlineUtc!.Value;

            MatchSnapshot? snapshot = await room.TryAdvanceClock_async(timeoutUtc);

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.Revision , Is.EqualTo(initialRevision + 1));
                Assert.That(snapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.ACTIVE));
                Assert.That(snapshot.PlayerOneTimeoutCount , Is.EqualTo(1));
                Assert.That(snapshot.PlayerTwoTimeoutCount , Is.Zero);
                Assert.That(
                    snapshot.EdgeOwners.Count(owner => owner == PLAYER_INDEX_ENUM.PLAYER_ONE) ,
                    Is.EqualTo(1));
                Assert.That(snapshot.CurrentPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_TWO));
                Assert.That(snapshot.TurnDeadlineUtc , Is.EqualTo(timeoutUtc.AddSeconds(20)));
            });
        }

        [Test]
        public async Task TryHandleTurnTimeout_ReachingLimit_FinishesWithOpponentWin()
        {
            MatchRoom room = await CreateRoom_async(maxTimeoutsPerPlayer: 1);
            long initialRevision = room.Revision;

            MatchSnapshot? snapshot = await room.TryAdvanceClock_async(
                room.TurnDeadlineUtc!.Value);

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.Revision , Is.EqualTo(initialRevision + 1));
                Assert.That(snapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.FINISHED));
                Assert.That(snapshot.GameResult , Is.EqualTo(GAME_RESULT_ENUM.PLAYER_TWO_WIN));
                Assert.That(snapshot.PlayerOneTimeoutCount , Is.EqualTo(1));
                Assert.That(snapshot.EdgeOwners , Is.All.EqualTo(PLAYER_INDEX_ENUM.NONE));
                Assert.That(snapshot.TurnDeadlineUtc , Is.Null);
            });
        }

        [Test]
        public async Task ConfirmEdge_AcceptedMove_ResetsDeadlineFromCurrentServerTime()
        {
            DateTimeOffset currentUtc = INITIAL_UTC;
            MatchRoom room = await CreateRoom_async(
                maxTimeoutsPerPlayer: 3 ,
                utcNowProvider: () => currentUtc);

            currentUtc = currentUtc.AddSeconds(7);

            ConfirmEdgeResponse response = await room.ConfirmEdge_async(
                room.PlayerOne.UserId ,
                new ConfirmEdgeRequest
                {
                    MatchId = room.MatchId ,
                    EdgeId = 0 ,
                    ExpectedRevision = room.Revision ,
                    RequestId = Guid.NewGuid()
                });

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.True);
                Assert.That(response.Snapshot , Is.Not.Null);
                Assert.That(
                    response.Snapshot!.TurnDeadlineUtc ,
                    Is.EqualTo(currentUtc.AddSeconds(20)));
            });
        }

        private static Task<MatchRoom> CreateRoom_async(
            int maxTimeoutsPerPlayer ,
            Func<DateTimeOffset>? utcNowProvider = null)
        {
            return ActiveMatchRoomTestFactory.Create_async(
                turnDuration: TimeSpan.FromSeconds(20) ,
                maxTimeoutsPerPlayer: maxTimeoutsPerPlayer ,
                utcNowProvider: utcNowProvider ?? (() => INITIAL_UTC) ,
                random: new Random(1234));
        }
    }
}
