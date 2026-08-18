using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameBoard_Model
    {
        public const int NO_PREVIEW_EDGE_ID = -1;
        public const long NO_REVISION = -1;

        private readonly GameSessionSnapshotStore SNAPSHOT_STORE = new GameSessionSnapshotStore();

        private MatchSnapshot _currentSnapshot;

        public DotsBoard Board { get; }
        public int PreviewEdgeId { get; private set; }

        public bool HasPreview => PreviewEdgeId != NO_PREVIEW_EDGE_ID;
        public bool HasServerSnapshot => _currentSnapshot != null;
        public long Revision => HasServerSnapshot ? _currentSnapshot.Revision : NO_REVISION;

        public SERVER_MATCH_STATE_ENUM MatchState
        {
            get
            {
                if ( HasServerSnapshot )
                {
                    return _currentSnapshot.MatchState;
                }

                return Board.IsGameFinished ? SERVER_MATCH_STATE_ENUM.FINISHED : SERVER_MATCH_STATE_ENUM.ACTIVE;
            }
        }

        public PLAYER_INDEX_ENUM CurrentPlayerIndex
        {
            get
            {
                return HasServerSnapshot ? _currentSnapshot.CurrentPlayerIndex : Board.CurrentPlayerIndex;
            }
        }

        public int PlayerOneScore
        {
            get
            {
                return HasServerSnapshot ? _currentSnapshot.PlayerOneScore : Board.PlayerOneScore;
            }
        }

        public int PlayerTwoScore
        {
            get
            {
                return HasServerSnapshot ? _currentSnapshot.PlayerTwoScore : Board.PlayerTwoScore;
            }
        }

        public GAME_RESULT_ENUM GameResult
        {
            get
            {
                return HasServerSnapshot ? _currentSnapshot.GameResult : Board.GameResult;
            }
        }

        public bool CanSelectEdge => MatchState == SERVER_MATCH_STATE_ENUM.ACTIVE;
        public bool IsGameFinished => MatchState == SERVER_MATCH_STATE_ENUM.FINISHED;

        public GameBoard_Model(PLAYER_INDEX_ENUM startingPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE)
        {
            Board = new DotsBoard(startingPlayerIndex);
            PreviewEdgeId = NO_PREVIEW_EDGE_ID;
        }

        public bool ApplySnapshot(MatchSnapshot snapshot)
        {
            bool isApplied = SNAPSHOT_STORE.TryApply(snapshot);

            if ( !isApplied )
            {
                return false;
            }

            _currentSnapshot = SNAPSHOT_STORE.CurrentSnapshot;
            ClearPreview();
            return true;
        }

        public PLAYER_INDEX_ENUM GetEdgeOwner(int edgeId)
        {
            if ( edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT )
            {
                throw new System.ArgumentOutOfRangeException(nameof(edgeId));
            }

            if ( HasServerSnapshot )
            {
                return _currentSnapshot.EdgeOwners[ edgeId ];
            }

            return Board.GetEdge(edgeId).OwnerPlayerIndex;
        }

        public PLAYER_INDEX_ENUM GetBoxOwner(int boxId)
        {
            if ( boxId < 0 || boxId >= BoardTopology.BOX_COUNT )
            {
                throw new System.ArgumentOutOfRangeException(nameof(boxId));
            }

            if ( HasServerSnapshot )
            {
                return _currentSnapshot.BoxOwners[ boxId ];
            }

            return Board.GetBox(boxId).OwnerPlayerIndex;
        }

        public bool IsEdgeConfirmed(int edgeId)
        {
            return GetEdgeOwner(edgeId) != PLAYER_INDEX_ENUM.NONE;
        }

        public bool IsBoxOwned(int boxId)
        {
            return GetBoxOwner(boxId) != PLAYER_INDEX_ENUM.NONE;
        }

        public bool TrySetPreviewEdge(int edgeId)
        {
            if ( !CanSelectEdge )
            {
                return false;
            }

            if ( edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT )
            {
                return false;
            }

            if ( IsEdgeConfirmed(edgeId) )
            {
                return false;
            }

            PreviewEdgeId = edgeId;
            return true;
        }

        public bool TryConfirmPreview(out MoveResult moveResult)
        {
            moveResult = null;

            if ( HasServerSnapshot || !HasPreview )
            {
                return false;
            }

            moveResult = DotsRule.TryConfirmEdge(Board , Board.CurrentPlayerIndex , PreviewEdgeId);

            if ( !moveResult.IsValid )
            {
                return false;
            }

            ClearPreview();
            return true;
        }

        public void ClearPreview()
        {
            PreviewEdgeId = NO_PREVIEW_EDGE_ID;
        }
    }
}

