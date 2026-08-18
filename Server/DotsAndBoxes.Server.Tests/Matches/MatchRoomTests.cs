using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomTests
    {
        [Test]
        public async Task NewRoom_CreatesActiveInitialSnapshot()
        {
            Guid matchId = Guid.NewGuid();
            MatchPlayer playerOne = new MatchPlayer(Guid.NewGuid());
            MatchPlayer playerTwo = new MatchPlayer(Guid.NewGuid());

            MatchRoom room = new MatchRoom(
                matchId,
                playerOne,
                playerTwo,
                PLAYER_INDEX_ENUM.PLAYER_ONE);

            MatchSnapshot snapshot = await room.CreateSnapshot_async();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.SchemaVersion , Is.EqualTo(MatchSnapshot.CURRENT_SCHEMA_VERSION));
                Assert.That(snapshot.MatchId , Is.EqualTo(matchId));
                Assert.That(snapshot.Revision , Is.EqualTo(0));
                Assert.That(snapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.ACTIVE));

                Assert.That(snapshot.PlayerOneUserId , Is.EqualTo(playerOne.UserId));
                Assert.That(snapshot.PlayerTwoUserId , Is.EqualTo(playerTwo.UserId));
                Assert.That(snapshot.CurrentPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));

                Assert.That(snapshot.EdgeOwners , Has.Length.EqualTo(BoardTopology.EDGE_COUNT));
                Assert.That(snapshot.BoxOwners , Has.Length.EqualTo(BoardTopology.BOX_COUNT));
                Assert.That(snapshot.EdgeOwners , Is.All.EqualTo(PLAYER_INDEX_ENUM.NONE));
                Assert.That(snapshot.BoxOwners , Is.All.EqualTo(PLAYER_INDEX_ENUM.NONE));

                Assert.That(snapshot.PlayerOneScore , Is.EqualTo(0));
                Assert.That(snapshot.PlayerTwoScore , Is.EqualTo(0));
                Assert.That(snapshot.GameResult , Is.EqualTo(GAME_RESULT_ENUM.IN_PROGRESS));
            });
        }

        [TestCase(PLAYER_INDEX_ENUM.PLAYER_ONE)]
        [TestCase(PLAYER_INDEX_ENUM.PLAYER_TWO)]
        public async Task NewRoom_UsesRequestedStartingPlayer(PLAYER_INDEX_ENUM startingPlayerIndex)
        {
            MatchRoom room = CreateRoom(startingPlayerIndex);

            MatchSnapshot snapshot = await room.CreateSnapshot_async();

            Assert.That(snapshot.CurrentPlayerIndex , Is.EqualTo(startingPlayerIndex));
        }

        [Test]
        public void TryGetPlayerIndex_ReturnsPlayerSlot()
        {
            MatchPlayer playerOne = new MatchPlayer(Guid.NewGuid());
            MatchPlayer playerTwo = new MatchPlayer(Guid.NewGuid());

            MatchRoom room = new MatchRoom(
                Guid.NewGuid(),
                playerOne,
                playerTwo,
                PLAYER_INDEX_ENUM.PLAYER_ONE);

            bool foundPlayerOne = room.TryGetPlayerIndex(playerOne.UserId, out PLAYER_INDEX_ENUM playerOneIndex);
            bool foundPlayerTwo = room.TryGetPlayerIndex(playerTwo.UserId, out PLAYER_INDEX_ENUM playerTwoIndex);
            bool foundUnknown = room.TryGetPlayerIndex(Guid.NewGuid(), out PLAYER_INDEX_ENUM unknownIndex);

            Assert.Multiple(() =>
            {
                Assert.That(foundPlayerOne , Is.True);
                Assert.That(playerOneIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));

                Assert.That(foundPlayerTwo , Is.True);
                Assert.That(playerTwoIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_TWO));

                Assert.That(foundUnknown , Is.False);
                Assert.That(unknownIndex , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
            });
        }

        [Test]
        public void NewRoom_WithSameUserId_Throws()
        {
            Guid duplicatedUserId = Guid.NewGuid();

            MatchPlayer playerOne = new MatchPlayer(duplicatedUserId);
            MatchPlayer playerTwo = new MatchPlayer(duplicatedUserId);

            Assert.Throws<ArgumentException>(() =>
            {
                _ = new MatchRoom(
                    Guid.NewGuid() ,
                    playerOne ,
                    playerTwo ,
                    PLAYER_INDEX_ENUM.PLAYER_ONE);
            });
        }

        [Test]
        public async Task CreateSnapshot_ReturnsIndependentArrays()
        {
            MatchRoom room = CreateRoom(PLAYER_INDEX_ENUM.PLAYER_ONE);

            MatchSnapshot firstSnapshot = await room.CreateSnapshot_async(); ;
            MatchSnapshot secondSnapshot = await room.CreateSnapshot_async(); ;

            firstSnapshot.EdgeOwners[ 0 ] = PLAYER_INDEX_ENUM.PLAYER_ONE;
            firstSnapshot.BoxOwners[ 0 ] = PLAYER_INDEX_ENUM.PLAYER_ONE;

            Assert.Multiple(() =>
            {
                Assert.That(secondSnapshot.EdgeOwners[ 0 ] , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
                Assert.That(secondSnapshot.BoxOwners[ 0 ] , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
                Assert.That(firstSnapshot.EdgeOwners , Is.Not.SameAs(secondSnapshot.EdgeOwners));
                Assert.That(firstSnapshot.BoxOwners , Is.Not.SameAs(secondSnapshot.BoxOwners));
            });
        }

        private static MatchRoom CreateRoom(PLAYER_INDEX_ENUM startingPlayerIndex)
        {
            MatchPlayer playerOne = new MatchPlayer(Guid.NewGuid());
            MatchPlayer playerTwo = new MatchPlayer(Guid.NewGuid());

            return new MatchRoom(
                Guid.NewGuid() ,
                playerOne ,
                playerTwo ,
                startingPlayerIndex);
        }
    }
}
