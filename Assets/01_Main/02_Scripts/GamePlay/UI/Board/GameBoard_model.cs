using DotsAndBoxes.Shared;
using System;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameBoard_Model
    {
        public const int NO_PREVIEW_EDGE_ID = -1;
        public const long NO_REVISION = -1;

        private readonly GameSessionSnapshotStore SNAPSHOT_STORE = new GameSessionSnapshotStore();

        private MatchSnapshot _currentSnapshot;
        private long _lastOpponentPreviewSequence;

        public DotsBoard Board { get; }
        public int PreviewEdgeId { get; private set; }
        public int OpponentPreviewEdgeId { get; private set; }
        public PLAYER_INDEX_ENUM OpponentPreviewPlayerIndex { get; private set; }

        public bool HasPreview => PreviewEdgeId != NO_PREVIEW_EDGE_ID;
        public bool HasOpponentPreview => OpponentPreviewEdgeId != NO_PREVIEW_EDGE_ID;
        public bool HasServerSnapshot => _currentSnapshot != null;
        public long Revision => HasServerSnapshot ? _currentSnapshot.Revision : NO_REVISION;
        public DateTimeOffset? MatchStartUtc => HasServerSnapshot ? _currentSnapshot.MatchStartUtc : null;

        public DateTimeOffset? TurnDeadLineUtc => HasServerSnapshot ? _currentSnapshot.TurnDeadlineUtc : null;

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
            OpponentPreviewEdgeId = NO_PREVIEW_EDGE_ID;
            OpponentPreviewPlayerIndex = PLAYER_INDEX_ENUM.NONE;
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
            ClearOpponentPreview();
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

        public int GetStartCountdownNumber(DateTimeOffset utcNow)
        {
            if ( MatchState != SERVER_MATCH_STATE_ENUM.STARTING ||
                 !MatchStartUtc.HasValue )
            {
                return 0;
            }

            TimeSpan remainingTime = MatchStartUtc.Value - utcNow;

            if ( remainingTime <= TimeSpan.Zero )
            {
                return 0;
            }

            return (int)Math.Ceiling(remainingTime.TotalSeconds);
        }

        public int GetTurnCountdownNumber(DateTimeOffset utcNow)
        {
            if ( MatchState != SERVER_MATCH_STATE_ENUM.ACTIVE || !TurnDeadLineUtc.HasValue )
            {
                return 0;
            }

            TimeSpan remainingTime = TurnDeadLineUtc.Value - utcNow;

            if ( remainingTime <= TimeSpan.Zero )
            {
                return 0;
            }

            return (int)Math.Ceiling(remainingTime.TotalSeconds);
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

        public bool TryClearPreviewEdge(int edgeId)
        {
            if ( PreviewEdgeId != edgeId )
            {
                return false;
            }

            ClearPreview();
            return true;
        }

        public bool TryApplyOpponentPreview(OpponentPreviewUpdate update)
        {
            if ( update == null || !HasServerSnapshot )
            {
                return false;
            }

            if ( update.MatchId != _currentSnapshot.MatchId ||
                 update.Revision != Revision ||
                 update.PlayerIndex != CurrentPlayerIndex ||
                 update.PreviewSequence <= _lastOpponentPreviewSequence )
            {
                return false;
            }

            if ( !update.HasPreview )
            {
                if ( update.EdgeId != OpponentPreviewUpdate.NO_PREVIEW_EDGE_ID )
                {
                    return false;
                }

                _lastOpponentPreviewSequence = update.PreviewSequence;
                ClearOpponentPreview(false);
                return true;
            }

            if ( update.EdgeId < 0 ||
                 update.EdgeId >= BoardTopology.EDGE_COUNT ||
                 IsEdgeConfirmed(update.EdgeId) )
            {
                return false;
            }

            _lastOpponentPreviewSequence = update.PreviewSequence;
            OpponentPreviewEdgeId = update.EdgeId;
            OpponentPreviewPlayerIndex = update.PlayerIndex;
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

        public void ClearOpponentPreview(bool shouldResetSequence = true)
        {
            OpponentPreviewEdgeId = NO_PREVIEW_EDGE_ID;
            OpponentPreviewPlayerIndex = PLAYER_INDEX_ENUM.NONE;

            if ( shouldResetSequence )
            {
                _lastOpponentPreviewSequence = 0;
            }
        }
    }
}

