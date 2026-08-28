using DotsAndBoxes.Shared;
using NUnit.Framework;
using System;

namespace DotsAndBoxes.Gameplay.Tests
{
    [TestFixture]
    public sealed class GameBoardModelSnapshotTests
    {
        [Test]
        public void ApplySnapshot_WithNewSnapshot_UsesAuthoritativeStateAndClearsPreview()
        {
            GameBoard_Model model = new GameBoard_Model();
            MatchSnapshot snapshot = CreateSnapshot(Guid.NewGuid(), 3, Guid.NewGuid(), Guid.NewGuid());

            model.TrySetPreviewEdge(7);
            snapshot.CurrentPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_TWO;
            SetOwnedBox(snapshot , 0 , PLAYER_INDEX_ENUM.PLAYER_ONE);

            bool isApplied = model.ApplySnapshot(snapshot);
            BoxEdgeIds edgeIds = BoardTopology.GetBoxEdgeIDs(0);

            Assert.That(isApplied , Is.True);
            Assert.That(model.HasServerSnapshot , Is.True);
            Assert.That(model.HasPreview , Is.False);
            Assert.That(model.Revision , Is.EqualTo(3));
            Assert.That(model.CurrentPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_TWO));
            Assert.That(model.PlayerOneScore , Is.EqualTo(1));
            Assert.That(model.PlayerTwoScore , Is.EqualTo(0));
            Assert.That(model.GetEdgeOwner(edgeIds.TopEdgeId) , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
            Assert.That(model.GetBoxOwner(0) , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
        }

        [Test]
        public void ApplySnapshot_WithSameOrOlderRevision_PreservesCurrentStateAndPreview()
        {
            GameBoard_Model model = new GameBoard_Model();
            Guid matchId = Guid.NewGuid();
            Guid playerOneUserId = Guid.NewGuid();
            Guid playerTwoUserId = Guid.NewGuid();

            MatchSnapshot revisionTwo = CreateSnapshot(matchId, 2, playerOneUserId, playerTwoUserId);
            model.ApplySnapshot(revisionTwo);
            model.TrySetPreviewEdge(3);

            MatchSnapshot sameRevision = CreateSnapshot(matchId, 2, playerOneUserId, playerTwoUserId);
            sameRevision.EdgeOwners[ 0 ] = PLAYER_INDEX_ENUM.PLAYER_ONE;

            MatchSnapshot olderRevision = CreateSnapshot(matchId, 1, playerOneUserId, playerTwoUserId);

            bool isSameApplied = model.ApplySnapshot(sameRevision);
            bool isOlderApplied = model.ApplySnapshot(olderRevision);

            Assert.That(isSameApplied , Is.False);
            Assert.That(isOlderApplied , Is.False);
            Assert.That(model.Revision , Is.EqualTo(2));
            Assert.That(model.HasPreview , Is.True);
            Assert.That(model.PreviewEdgeId , Is.EqualTo(3));
            Assert.That(model.GetEdgeOwner(0) , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
        }

        [Test]
        public void TryConfirmPreview_WithServerSnapshot_DoesNotChangeLocalBoard()
        {
            GameBoard_Model model = new GameBoard_Model();
            MatchSnapshot snapshot = CreateSnapshot(Guid.NewGuid(), 0, Guid.NewGuid(), Guid.NewGuid());

            model.ApplySnapshot(snapshot);
            model.TrySetPreviewEdge(0);

            bool isConfirmed = model.TryConfirmPreview(out MoveResult moveResult);

            Assert.That(isConfirmed , Is.False);
            Assert.That(moveResult , Is.Null);
            Assert.That(model.HasPreview , Is.True);
            Assert.That(model.Board.ConfirmedEdgeCount , Is.EqualTo(0));
            Assert.That(model.GetEdgeOwner(0) , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
        }

        [Test]
        public void TrySetPreviewEdge_WithConfirmedSnapshotEdge_ReturnsFalse()
        {
            GameBoard_Model model = new GameBoard_Model();
            MatchSnapshot snapshot = CreateSnapshot(Guid.NewGuid(), 0, Guid.NewGuid(), Guid.NewGuid());

            snapshot.EdgeOwners[ 8 ] = PLAYER_INDEX_ENUM.PLAYER_TWO;
            model.ApplySnapshot(snapshot);

            bool isPreviewChanged = model.TrySetPreviewEdge(8);

            Assert.That(isPreviewChanged , Is.False);
            Assert.That(model.HasPreview , Is.False);
            Assert.That(model.GetEdgeOwner(8) , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_TWO));
        }

        [Test]
        public void TryApplyOpponentPreview_WithValidUpdate_AppliesOpponentPreview()
        {
            GameBoard_Model model = new GameBoard_Model();
            Guid matchId = Guid.NewGuid();
            MatchSnapshot snapshot = CreateSnapshot(matchId , 4 , Guid.NewGuid() , Guid.NewGuid());
            model.ApplySnapshot(snapshot);

            OpponentPreviewUpdate update = CreateOpponentPreviewUpdate(
                matchId ,
                snapshot.Revision ,
                1 ,
                6);

            bool isApplied = model.TryApplyOpponentPreview(update);

            Assert.That(isApplied , Is.True);
            Assert.That(model.HasOpponentPreview , Is.True);
            Assert.That(model.OpponentPreviewEdgeId , Is.EqualTo(6));
            Assert.That(model.OpponentPreviewPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
        }

        [Test]
        public void TryApplyOpponentPreview_WithOlderSequence_PreservesLatestPreview()
        {
            GameBoard_Model model = new GameBoard_Model();
            Guid matchId = Guid.NewGuid();
            MatchSnapshot snapshot = CreateSnapshot(matchId , 4 , Guid.NewGuid() , Guid.NewGuid());
            model.ApplySnapshot(snapshot);

            bool isLatestApplied = model.TryApplyOpponentPreview(
                CreateOpponentPreviewUpdate(matchId , snapshot.Revision , 2 , 6));
            bool isOlderApplied = model.TryApplyOpponentPreview(
                CreateOpponentPreviewUpdate(matchId , snapshot.Revision , 1 , 7));

            Assert.That(isLatestApplied , Is.True);
            Assert.That(isOlderApplied , Is.False);
            Assert.That(model.OpponentPreviewEdgeId , Is.EqualTo(6));
        }

        [Test]
        public void TryApplyOpponentPreview_WithClearUpdate_ClearsOpponentPreview()
        {
            GameBoard_Model model = new GameBoard_Model();
            Guid matchId = Guid.NewGuid();
            MatchSnapshot snapshot = CreateSnapshot(matchId , 4 , Guid.NewGuid() , Guid.NewGuid());
            model.ApplySnapshot(snapshot);
            model.TryApplyOpponentPreview(
                CreateOpponentPreviewUpdate(matchId , snapshot.Revision , 1 , 6));

            OpponentPreviewUpdate clearUpdate = CreateOpponentPreviewUpdate(
                matchId ,
                snapshot.Revision ,
                2 ,
                OpponentPreviewUpdate.NO_PREVIEW_EDGE_ID);
            clearUpdate.HasPreview = false;

            bool isApplied = model.TryApplyOpponentPreview(clearUpdate);

            Assert.That(isApplied , Is.True);
            Assert.That(model.HasOpponentPreview , Is.False);
            Assert.That(model.OpponentPreviewPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
        }

        [Test]
        public void ApplySnapshot_WithNewRevision_ClearsOpponentPreview()
        {
            GameBoard_Model model = new GameBoard_Model();
            Guid matchId = Guid.NewGuid();
            Guid playerOneUserId = Guid.NewGuid();
            Guid playerTwoUserId = Guid.NewGuid();
            MatchSnapshot revisionFour = CreateSnapshot(matchId , 4 , playerOneUserId , playerTwoUserId);
            model.ApplySnapshot(revisionFour);
            model.TryApplyOpponentPreview(
                CreateOpponentPreviewUpdate(matchId , revisionFour.Revision , 1 , 6));

            MatchSnapshot revisionFive = CreateSnapshot(matchId , 5 , playerOneUserId , playerTwoUserId);
            bool isApplied = model.ApplySnapshot(revisionFive);

            Assert.That(isApplied , Is.True);
            Assert.That(model.HasOpponentPreview , Is.False);
        }

        [Test]
        public void GetStartCountdownNumber_WithStartingSnapshot_ReturnsCeilingSeconds()
        {
            GameBoard_Model model = new GameBoard_Model();
            DateTimeOffset initialUtc = new DateTimeOffset(
                2026 ,
                8 ,
                19 ,
                0 ,
                0 ,
                0 ,
                TimeSpan.Zero);

            MatchSnapshot snapshot = CreateSnapshot(
                Guid.NewGuid() ,
                0 ,
                Guid.NewGuid() ,
                Guid.NewGuid());

            snapshot.MatchState = SERVER_MATCH_STATE_ENUM.STARTING;
            snapshot.MatchStartUtc = initialUtc.AddSeconds(3);
            model.ApplySnapshot(snapshot);

            Assert.That(model.GetStartCountdownNumber(initialUtc) , Is.EqualTo(3));
            Assert.That(model.GetStartCountdownNumber(initialUtc.AddSeconds(1.1)) , Is.EqualTo(2));
            Assert.That(model.GetStartCountdownNumber(initialUtc.AddSeconds(2.1)) , Is.EqualTo(1));
            Assert.That(model.GetStartCountdownNumber(initialUtc.AddSeconds(3)) , Is.EqualTo(0));
        }

        private static MatchSnapshot CreateSnapshot(Guid matchId , long revision , Guid playerOneUserId , Guid playerTwoUserId)
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
                GameResult = GAME_RESULT_ENUM.IN_PROGRESS
            };
        }

        private static void SetOwnedBox(MatchSnapshot snapshot , int boxId , PLAYER_INDEX_ENUM ownerPlayerIndex)
        {
            BoxEdgeIds edgeIds = BoardTopology.GetBoxEdgeIDs(boxId);

            snapshot.EdgeOwners[ edgeIds.TopEdgeId ] = ownerPlayerIndex;
            snapshot.EdgeOwners[ edgeIds.BottomEdgeId ] = ownerPlayerIndex;
            snapshot.EdgeOwners[ edgeIds.LeftEdgeId ] = ownerPlayerIndex;
            snapshot.EdgeOwners[ edgeIds.RightEdgeId ] = ownerPlayerIndex;
            snapshot.BoxOwners[ boxId ] = ownerPlayerIndex;

            if ( ownerPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE )
            {
                snapshot.PlayerOneScore++;
            }
            else
            {
                snapshot.PlayerTwoScore++;
            }
        }

        private static OpponentPreviewUpdate CreateOpponentPreviewUpdate(
            Guid matchId ,
            long revision ,
            long previewSequence ,
            int edgeId)
        {
            return new OpponentPreviewUpdate
            {
                MatchId = matchId ,
                PlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE ,
                HasPreview = true ,
                EdgeId = edgeId ,
                Revision = revision ,
                PreviewSequence = previewSequence
            };
        }

        private static PLAYER_INDEX_ENUM[] CreateEmptyOwners(int length)
        {
            PLAYER_INDEX_ENUM[] owners = new PLAYER_INDEX_ENUM[length];

            for ( int index = 0; index < owners.Length; index++ )
            {
                owners[ index ] = PLAYER_INDEX_ENUM.NONE;
            }

            return owners;
        }
    }
}
