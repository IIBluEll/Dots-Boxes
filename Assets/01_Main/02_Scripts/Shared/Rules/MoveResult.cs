// 한 번의 Edge 확정 요청으로 발생한 성공 여부와 Box 획득 결과를 보관합니다.
using System;
using System.Collections.Generic;

namespace DotsAndBoxes.Shared
{
    public sealed class MoveResult
    {
        private static readonly int[] EMPTY_BOX_IDS = Array.Empty<int>();

        private readonly int[] _completeBoxIds;

        public bool IsValid { get; }
        public int EdgeId { get; }
        public MOVE_ERROR_ENUM Error { get; }

        public IReadOnlyList<int> CompletedBoxIds => _completeBoxIds;
        public bool HasExtraTurn => _completeBoxIds.Length > 0;

        public bool IsGameFinished { get; }

        internal MoveResult(bool isValid, MOVE_ERROR_ENUM error, int edgeId, int[] completeBoxIds, bool isGameFinished)
        {
            IsValid = isValid;
            Error = error;
            EdgeId = edgeId;

            _completeBoxIds = completeBoxIds ?? EMPTY_BOX_IDS;

            IsGameFinished = isGameFinished;
        }

        internal static MoveResult CreateFailure(MOVE_ERROR_ENUM error , int edgeId , bool isGameFinished = false)
        {
            return new MoveResult(false , error , edgeId , EMPTY_BOX_IDS , isGameFinished);
        }

        internal static MoveResult CreateSuccess(int edgeId, int[] completeBoxIds, bool isGameFinished)
        {
            return new MoveResult(true , MOVE_ERROR_ENUM.NONE , edgeId , completeBoxIds , isGameFinished);
        }
    }
}
