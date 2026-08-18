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
            MatchRoom room = CreateRoom(maxTimeoutsPerPlayer: 3);

            MatchSnapshot? snapshot = await room.TryHandleTurnTimeout_async(
                INITIAL_UTC.AddSeconds(19));

            Assert.Multiple(() =>
            {
                Assert.That(snapshot , Is.Null);
                Assert.That(room.Revision , Is.Zero);
                Assert.That(room.PlayerOneTimeoutCount , Is.Zero);
            });
        }

        [Test]
        public async Task TryHandleTurnTimeout_FirstTimeout_SelectsEdgeAndAdvancesTurn()
        {
            MatchRoom room = CreateRoom(maxTimeoutsPerPlayer: 3);
            DateTimeOffset timeoutUtc = INITIAL_UTC.AddSeconds(20);

            MatchSnapshot? snapshot = await room.TryHandleTurnTimeout_async(timeoutUtc);

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.Revision , Is.EqualTo(1));
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
            MatchRoom room = CreateRoom(maxTimeoutsPerPlayer: 1);

            MatchSnapshot? snapshot = await room.TryHandleTurnTimeout_async(
                INITIAL_UTC.AddSeconds(20));

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.Revision , Is.EqualTo(1));
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
            MatchRoom room = CreateRoom(
                maxTimeoutsPerPlayer: 3 ,
                utcNowProvider: () => currentUtc);

            currentUtc = currentUtc.AddSeconds(7);

            ConfirmEdgeResponse response = await room.ConfirmEdge_async(
                room.PlayerOne.UserId ,
                new ConfirmEdgeRequest
                {
                    MatchId = room.MatchId ,
                    EdgeId = 0 ,
                    ExpectedRevision = 0 ,
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

        private static MatchRoom CreateRoom(
            int maxTimeoutsPerPlayer ,
            Func<DateTimeOffset>? utcNowProvider = null)
        {
            return new MatchRoom(
                Guid.NewGuid() ,
                new MatchPlayer(Guid.NewGuid()) ,
                new MatchPlayer(Guid.NewGuid()) ,
                PLAYER_INDEX_ENUM.PLAYER_ONE ,
                TimeSpan.FromSeconds(20) ,
                maxTimeoutsPerPlayer ,
                utcNowProvider ?? (() => INITIAL_UTC) ,
                new Random(1234));
        }
    }
}
