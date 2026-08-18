using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomForfeitTests
    {
        [TestCase(PLAYER_INDEX_ENUM.PLAYER_ONE , GAME_RESULT_ENUM.PLAYER_TWO_WIN)]
        [TestCase(PLAYER_INDEX_ENUM.PLAYER_TWO , GAME_RESULT_ENUM.PLAYER_ONE_WIN)]
        public async Task TryForfeit_ActivePlayer_FinishesMatchWithOpponentWin(
            PLAYER_INDEX_ENUM forfeitingPlayerIndex ,
            GAME_RESULT_ENUM expectedGameResult)
        {
            MatchRoom room = CreateRoom();

            Guid forfeitingUserId = forfeitingPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                ? room.PlayerOne.UserId
                : room.PlayerTwo.UserId;

            MatchSnapshot? snapshot =
                await room.TryForfeit_async(forfeitingUserId);

            Assert.That(snapshot , Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(snapshot!.Revision , Is.EqualTo(1));
                Assert.That(snapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.FINISHED));
                Assert.That(snapshot.GameResult , Is.EqualTo(expectedGameResult));
                Assert.That(snapshot.EdgeOwners.All(owner => owner == PLAYER_INDEX_ENUM.NONE) , Is.True);
                Assert.That(snapshot.BoxOwners.All(owner => owner == PLAYER_INDEX_ENUM.NONE) , Is.True);
            });
        }

        [Test]
        public async Task TryForfeit_AfterMatchFinished_DoesNotChangeResultAgain()
        {
            MatchRoom room = CreateRoom();

            MatchSnapshot? firstSnapshot =
                await room.TryForfeit_async(room.PlayerOne.UserId);

            MatchSnapshot? secondSnapshot =
                await room.TryForfeit_async(room.PlayerTwo.UserId);

            MatchSnapshot finalSnapshot =
                await room.CreateSnapshot_async();

            Assert.Multiple(() =>
            {
                Assert.That(firstSnapshot , Is.Not.Null);
                Assert.That(secondSnapshot , Is.Null);
                Assert.That(finalSnapshot.Revision , Is.EqualTo(1));
                Assert.That(finalSnapshot.GameResult , Is.EqualTo(GAME_RESULT_ENUM.PLAYER_TWO_WIN));
            });
        }

        [Test]
        public async Task TryForfeit_UnknownUser_DoesNotChangeMatch()
        {
            MatchRoom room = CreateRoom();

            MatchSnapshot? snapshot =
                await room.TryForfeit_async(Guid.NewGuid());

            Assert.Multiple(() =>
            {
                Assert.That(snapshot , Is.Null);
                Assert.That(room.Revision , Is.Zero);
                Assert.That(room.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.ACTIVE));
                Assert.That(room.GameResult , Is.EqualTo(GAME_RESULT_ENUM.IN_PROGRESS));
            });
        }

        private static MatchRoom CreateRoom()
        {
            return new MatchRoom(
                Guid.NewGuid() ,
                new MatchPlayer(Guid.NewGuid()) ,
                new MatchPlayer(Guid.NewGuid()) ,
                PLAYER_INDEX_ENUM.PLAYER_ONE);
        }
    }
}
