using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomReadyFlowTests
    {
        private static readonly DateTimeOffset INITIAL_UTC =
            new DateTimeOffset(2026 , 8 , 18 , 0 , 0 , 0 , TimeSpan.Zero);

        [Test]
        public async Task JoinAndReady_BothPlayers_StartsSharedCountdownThenActivatesMatch()
        {
            DateTimeOffset currentUtc = INITIAL_UTC;
            MatchRoom room = CreateRoom(() => currentUtc);

            MatchRoomUpdateResult firstJoin =
                await room.MarkPlayerJoined_async(room.PlayerOne.UserId);

            MatchRoomUpdateResult secondJoin =
                await room.MarkPlayerJoined_async(room.PlayerTwo.UserId);

            MatchRoomUpdateResult firstReady =
                await room.MarkPlayerReady_async(room.PlayerOne.UserId);

            MatchRoomUpdateResult secondReady =
                await room.MarkPlayerReady_async(room.PlayerTwo.UserId);

            DateTimeOffset expectedStartUtc = INITIAL_UTC.AddSeconds(3);

            MatchSnapshot? beforeStart = await room.TryAdvanceClock_async(
                expectedStartUtc.AddMilliseconds(-1));

            MatchSnapshot? activeSnapshot = await room.TryAdvanceClock_async(
                expectedStartUtc);

            Assert.That(activeSnapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(firstJoin.HasStateChanged , Is.False);
                Assert.That(firstJoin.Snapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS));

                Assert.That(secondJoin.HasStateChanged , Is.True);
                Assert.That(secondJoin.Snapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.WAITING_FOR_READY));
                Assert.That(secondJoin.Snapshot.Revision , Is.EqualTo(1));
                Assert.That(secondJoin.Snapshot.JoinDeadlineUtc , Is.Null);
                Assert.That(secondJoin.Snapshot.ReadyDeadlineUtc , Is.EqualTo(INITIAL_UTC.AddSeconds(15)));

                Assert.That(firstReady.Snapshot.PlayerOneReady , Is.True);
                Assert.That(firstReady.Snapshot.PlayerTwoReady , Is.False);
                Assert.That(firstReady.Snapshot.Revision , Is.EqualTo(2));

                Assert.That(secondReady.Snapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.STARTING));
                Assert.That(secondReady.Snapshot.PlayerOneReady , Is.True);
                Assert.That(secondReady.Snapshot.PlayerTwoReady , Is.True);
                Assert.That(secondReady.Snapshot.MatchStartUtc , Is.EqualTo(expectedStartUtc));
                Assert.That(secondReady.Snapshot.TurnDeadlineUtc , Is.Null);
                Assert.That(secondReady.Snapshot.Revision , Is.EqualTo(3));

                Assert.That(beforeStart , Is.Null);
                Assert.That(activeSnapshot!.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.ACTIVE));
                Assert.That(activeSnapshot.Revision , Is.EqualTo(4));
                Assert.That(activeSnapshot.TurnDeadlineUtc , Is.EqualTo(expectedStartUtc.AddSeconds(20)));
            });
        }

        [Test]
        public void ReadyMatch_BeforeJoin_Throws()
        {
            MatchRoom room = CreateRoom(() => INITIAL_UTC);

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await room.MarkPlayerReady_async(room.PlayerOne.UserId);
            });
        }

        [Test]
        public async Task ReadyMatch_DuplicateCall_DoesNotIncreaseRevisionAgain()
        {
            MatchRoom room = CreateRoom(() => INITIAL_UTC);
            await room.MarkPlayerJoined_async(room.PlayerOne.UserId);

            MatchRoomUpdateResult firstReady =
                await room.MarkPlayerReady_async(room.PlayerOne.UserId);

            MatchRoomUpdateResult duplicateReady =
                await room.MarkPlayerReady_async(room.PlayerOne.UserId);

            Assert.Multiple(() =>
            {
                Assert.That(firstReady.HasStateChanged , Is.True);
                Assert.That(duplicateReady.HasStateChanged , Is.False);
                Assert.That(duplicateReady.Snapshot.Revision , Is.EqualTo(firstReady.Snapshot.Revision));
            });
        }

        [Test]
        public async Task MatchClock_JoinTimeout_CancelsWithoutWinner()
        {
            MatchRoom room = CreateRoom(() => INITIAL_UTC);

            MatchSnapshot? snapshot = await room.TryAdvanceClock_async(
                INITIAL_UTC.AddSeconds(10));

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.CANCELLED));
                Assert.That(snapshot.GameResult , Is.EqualTo(GAME_RESULT_ENUM.IN_PROGRESS));
                Assert.That(snapshot.Revision , Is.EqualTo(1));
                Assert.That(snapshot.JoinDeadlineUtc , Is.Null);
                Assert.That(snapshot.TurnDeadlineUtc , Is.Null);
            });
        }

        [Test]
        public async Task MatchClock_ReadyTimeout_CancelsWithoutWinner()
        {
            MatchRoom room = CreateRoom(() => INITIAL_UTC);
            await room.MarkPlayerJoined_async(room.PlayerOne.UserId);
            MatchRoomUpdateResult secondJoin =
                await room.MarkPlayerJoined_async(room.PlayerTwo.UserId);

            MatchSnapshot? snapshot = await room.TryAdvanceClock_async(
                secondJoin.Snapshot.ReadyDeadlineUtc!.Value);

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.CANCELLED));
                Assert.That(snapshot.GameResult , Is.EqualTo(GAME_RESULT_ENUM.IN_PROGRESS));
                Assert.That(snapshot.Revision , Is.EqualTo(2));
                Assert.That(snapshot.ReadyDeadlineUtc , Is.Null);
            });
        }

        [Test]
        public async Task PlayerExit_BeforeActive_CancelsWithoutWinner()
        {
            MatchRoom room = CreateRoom(() => INITIAL_UTC);
            await room.MarkPlayerJoined_async(room.PlayerOne.UserId);

            MatchSnapshot? snapshot = await room.TryHandlePlayerExit_async(
                room.PlayerOne.UserId);

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.CANCELLED));
                Assert.That(snapshot.GameResult , Is.EqualTo(GAME_RESULT_ENUM.IN_PROGRESS));
                Assert.That(snapshot.Revision , Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ConfirmEdge_BeforeActive_ReturnsMatchNotActive()
        {
            MatchRoom room = CreateRoom(() => INITIAL_UTC);

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
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE));
                Assert.That(room.Revision , Is.Zero);
            });
        }

        private static MatchRoom CreateRoom(Func<DateTimeOffset> utcNowProvider)
        {
            return new MatchRoom(
                Guid.NewGuid() ,
                new MatchPlayer(Guid.NewGuid()) ,
                new MatchPlayer(Guid.NewGuid()) ,
                PLAYER_INDEX_ENUM.PLAYER_ONE ,
                TimeSpan.FromSeconds(20) ,
                3 ,
                utcNowProvider ,
                new Random(1234) ,
                TimeSpan.FromSeconds(10) ,
                TimeSpan.FromSeconds(15) ,
                TimeSpan.FromSeconds(3));
        }
    }
}
