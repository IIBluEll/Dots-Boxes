// Edge 확정부터 Box 획득과 턴 변경까지 핵심 게임 규칙을 처리합니다.
using System;

namespace DotsAndBoxes.Shared
{
    public static class DotsRule
    {
        public static MoveResult TryConfirmEdge(DotsBoard board , PLAYER_INDEX_ENUM playerIndex , int edgeId)
        {
            if(board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if(board.IsGameFinished)
            {
                return MoveResult.CreateFailure(MOVE_ERROR_ENUM.GAME_ALREADY_FINISHED , edgeId, true);
            }

            if(!IsValidPlayer(playerIndex))
            {
                return MoveResult.CreateFailure(MOVE_ERROR_ENUM.INVALID_PLAYER , edgeId);
            }

            if(board.CurrentPlayerIndex != playerIndex)
            {
                return MoveResult.CreateFailure(MOVE_ERROR_ENUM.NOT_YOUR_TURN , edgeId);
            }

            if(edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT)
            {
                return MoveResult.CreateFailure(MOVE_ERROR_ENUM.INVALID_EDGE , edgeId);
            }

            EdgeData edge = board.GetEdge(edgeId);

            if(edge.IsConfirmed)
            {
                return MoveResult.CreateFailure(MOVE_ERROR_ENUM.EDGE_ALREADY_CONFIRMED , edgeId);
            }

            edge.Confirm(playerIndex);

            int[] completedBoxIds = ClaimCompletedBoxes(board, playerIndex, edgeId);

            if(completedBoxIds.Length == 0)
            {
                board.SwitchTurn();
            }

            return MoveResult.CreateSuccess(edgeId , completedBoxIds , board.IsGameFinished);
        }

        private static int[] ClaimCompletedBoxes(DotsBoard board, PLAYER_INDEX_ENUM playerIndex, int edgeId)
        {
            AdjacentBoxIDs adjacentBoxIds = BoardTopology.GetAdjacentBoxIDs(edgeId);

            int[] temporaryBoxIds = new int[2];
            int completedBoxCount = 0;

            TryClaimBox(board , playerIndex , adjacentBoxIds.FirstBoxId , temporaryBoxIds , ref completedBoxCount);

            if(adjacentBoxIds.Count == 2)
            {
                TryClaimBox(board,playerIndex, adjacentBoxIds.SecondBoxId, temporaryBoxIds , ref completedBoxCount);
            }

            if(completedBoxCount == 0)
            {
                return Array.Empty<int>();
            }

            int[] completedBoxIds = new int[completedBoxCount];

            Array.Copy(temporaryBoxIds , completedBoxIds , completedBoxCount);

            return completedBoxIds;
        }

        private static void TryClaimBox(DotsBoard board, PLAYER_INDEX_ENUM playerIndex, int boxId, int[] completedBoxIds, ref int completedBoxCount)
        {
            BoxData box = board.GetBox(boxId);

            if ( box.IsOwned || !IsBoxComplete(board , box))
            {
                return;
            }

            box.Claim(playerIndex);
            completedBoxIds[ completedBoxCount ] = boxId;
            completedBoxCount++;
        }

        private static bool IsBoxComplete(DotsBoard board, BoxData box)
        {
            return board.GetEdge(box.TopEdgeId).IsConfirmed &&
                board.GetEdge(box.BottomEdgeId).IsConfirmed &&
                board.GetEdge(box.LeftEdgeId).IsConfirmed &&
                board.GetEdge(box.RightEdgeId).IsConfirmed;
        }

        private static bool IsValidPlayer(PLAYER_INDEX_ENUM playerIndex)
        {
            return playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE || playerIndex == PLAYER_INDEX_ENUM.PLAYER_TWO;
        }
    }
}
