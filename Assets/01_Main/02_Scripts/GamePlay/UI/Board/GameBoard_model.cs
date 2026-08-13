using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameBoard_model
    {
        public const int NO_PREVIEW_EDGE_ID = -1;

        public DotsBoard Board { get; }

        public int PreviewEdgeId { get; private set; }

        public bool HasPreview => PreviewEdgeId != NO_PREVIEW_EDGE_ID;

        public GameBoard_model(PLAYER_INDEX_ENUM startingPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE)
        {
            Board = new DotsBoard(startingPlayerIndex);
            PreviewEdgeId = NO_PREVIEW_EDGE_ID;
        }

        public bool TrySetPreviewEdge(int edgeId)
        {
            if(Board.IsGameFinished)
            {
                return false;
            }

            if ( edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT)
            {
                return false;
            }

            if(Board.GetEdge(edgeId).IsConfirmed)
            {
                return false;
            }

            PreviewEdgeId = edgeId;
            return true;
        }

        public void ClearPreview()
        {
            PreviewEdgeId = NO_PREVIEW_EDGE_ID;
        }
    }
}