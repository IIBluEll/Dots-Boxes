using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Shared;
using NUnit.Framework;
using System;

namespace DotsAndBoxes.Gameplay.Tests
{
    [TestFixture]
    public sealed class GameSessionSnapshotStoreTests
    {
        [Test]
        public void TryApply_WithValidSnapshot_StoresIndependentCopy()
        {
            GameSessionSnapshotStore store =
                new GameSessionSnapshotStore();

            MatchSnapshot snapshot = CreateSnapshot(
                Guid.NewGuid(),
                0,
                Guid.NewGuid(),
                Guid.NewGuid());

            bool applied = store.TryApply(snapshot);

            snapshot.EdgeOwners[ 0 ] =
                PLAYER_INDEX_ENUM.PLAYER_ONE;

            MatchSnapshot firstRead = store.CurrentSnapshot;

            firstRead.BoxOwners[ 0 ] =
                PLAYER_INDEX_ENUM.PLAYER_ONE;

            MatchSnapshot secondRead = store.CurrentSnapshot;


            Assert.That(applied , Is.True);
            Assert.That(
                secondRead.EdgeOwners[ 0 ] ,
                Is.EqualTo(PLAYER_INDEX_ENUM.NONE));

            Assert.That(
                secondRead.BoxOwners[ 0 ] ,
                Is.EqualTo(PLAYER_INDEX_ENUM.NONE));

            Assert.That(firstRead.EdgeOwners , Is.Not.SameAs(secondRead.EdgeOwners));
            Assert.That(firstRead.BoxOwners , Is.Not.SameAs(secondRead.BoxOwners));

        }

        [Test]
        public void TryApply_WithSameOrOlderRevision_IgnoresSnapshot()
        {
            GameSessionSnapshotStore store =
                new GameSessionSnapshotStore();

            Guid matchId = Guid.NewGuid();
            Guid playerOneUserId = Guid.NewGuid();
            Guid playerTwoUserId = Guid.NewGuid();

            MatchSnapshot revisionTwo = CreateSnapshot(
                matchId,
                2,
                playerOneUserId,
                playerTwoUserId);

            MatchSnapshot sameRevision = CreateSnapshot(
                matchId,
                2,
                playerOneUserId,
                playerTwoUserId);

            MatchSnapshot olderRevision = CreateSnapshot(
                matchId,
                1,
                playerOneUserId,
                playerTwoUserId);

            bool firstApplied = store.TryApply(revisionTwo);
            bool sameApplied = store.TryApply(sameRevision);
            bool olderApplied = store.TryApply(olderRevision);

            Assert.That(firstApplied , Is.True);
            Assert.That(sameApplied , Is.False);
            Assert.That(olderApplied , Is.False);
            Assert.That(store.CurrentSnapshot.Revision , Is.EqualTo(2));
        }

        [Test]
        public void TryApply_WithDifferentMatch_Throws()
        {
            GameSessionSnapshotStore store =
                new GameSessionSnapshotStore();

            Guid playerOneUserId = Guid.NewGuid();
            Guid playerTwoUserId = Guid.NewGuid();

            store.TryApply(CreateSnapshot(
                Guid.NewGuid() ,
                0 ,
                playerOneUserId ,
                playerTwoUserId));

            MatchSnapshot differentMatch = CreateSnapshot(
                Guid.NewGuid(),
                1,
                playerOneUserId,
                playerTwoUserId);

            Assert.Throws<InvalidOperationException>(() =>
            {
                store.TryApply(differentMatch);
            });
        }

        [Test]
        public void TryApply_WithChangedPlayers_Throws()
        {
            GameSessionSnapshotStore store =
                new GameSessionSnapshotStore();

            Guid matchId = Guid.NewGuid();
            Guid playerOneUserId = Guid.NewGuid();
            Guid playerTwoUserId = Guid.NewGuid();

            store.TryApply(CreateSnapshot(
                matchId ,
                0 ,
                playerOneUserId ,
                playerTwoUserId));

            MatchSnapshot changedPlayers = CreateSnapshot(
                matchId,
                1,
                Guid.NewGuid(),
                playerTwoUserId);

            Assert.Throws<InvalidOperationException>(() =>
            {
                store.TryApply(changedPlayers);
            });
        }

        [Test]
        public void TryApply_WithInvalidOwnerArrayLength_Throws()
        {
            GameSessionSnapshotStore store =
                new GameSessionSnapshotStore();

            MatchSnapshot snapshot = CreateSnapshot(
                Guid.NewGuid(),
                0,
                Guid.NewGuid(),
                Guid.NewGuid());

            snapshot.EdgeOwners =
                new PLAYER_INDEX_ENUM[ BoardTopology.EDGE_COUNT - 1 ];

            Assert.Throws<InvalidOperationException>(() =>
            {
                store.TryApply(snapshot);
            });
        }

        private static MatchSnapshot CreateSnapshot(
            Guid matchId ,
            long revision ,
            Guid playerOneUserId ,
            Guid playerTwoUserId)
        {
            return new MatchSnapshot
            {
                MatchId = matchId ,
                Revision = revision ,
                MatchState = SERVER_MATCH_STATE_ENUM.ACTIVE ,
                PlayerOneUserId = playerOneUserId ,
                PlayerTwoUserId = playerTwoUserId ,
                CurrentPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE ,
                EdgeOwners = CreateEmptyOwners(BoardTopology.EDGE_COUNT) ,
                BoxOwners = CreateEmptyOwners(BoardTopology.BOX_COUNT) ,
                PlayerOneScore = 0 ,
                PlayerTwoScore = 0 ,
                TurnDeadlineUtc = DateTimeOffset.UtcNow.AddSeconds(20) ,
                GameResult = GAME_RESULT_ENUM.IN_PROGRESS ,
                FinishReason = MATCH_FINISH_REASON_ENUM.NONE
            };
        }

        private static PLAYER_INDEX_ENUM[] CreateEmptyOwners(int length)
        {
            PLAYER_INDEX_ENUM[] owners = new PLAYER_INDEX_ENUM[ length ];

            for ( int index = 0; index < owners.Length; index++ )
            {
                owners[ index ] = PLAYER_INDEX_ENUM.NONE;
            }

            return owners;
        }
    }
}
