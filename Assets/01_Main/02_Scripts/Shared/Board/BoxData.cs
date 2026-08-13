// Box 하나를 구성하는 네 Edge와 Box 소유 플레이어를 관리합니다.
using System;

namespace DotsAndBoxes.Shared
{
    public sealed class BoxData
    {
        public int BoxId { get; }

        public int TopEdgeId { get; }
        public int BottomEdgeId { get; }
        public int LeftEdgeId { get; }
        public int RightEdgeId { get; }

        public PLAYER_INDEX_ENUM OwnerPlayerIndex { get; private set; }

        public bool IsOwned => OwnerPlayerIndex != PLAYER_INDEX_ENUM.NONE;

        public BoxData(int boxId)
        {
            if(boxId < 0 || boxId >= BoardTopology.BOX_COUNT)
            {
                throw new ArgumentOutOfRangeException(nameof(boxId), boxId, $"Box ID는 0부터 {BoardTopology.BOX_COUNT - 1} 사이여야 합니다.");
            }

            BoxEdgeIds edgeIds = BoardTopology.GetBoxEdgeIDs(boxId);

            BoxId = boxId;
            TopEdgeId = edgeIds.TopEdgeId;
            BottomEdgeId = edgeIds.BottomEdgeId;
            LeftEdgeId = edgeIds.LeftEdgeId;
            RightEdgeId = edgeIds.RightEdgeId;

            OwnerPlayerIndex = PLAYER_INDEX_ENUM.NONE;
        }

        internal void Claim(PLAYER_INDEX_ENUM playerIndex)
        {
            if(IsOwned)
            {
                throw new InvalidOperationException($"Box {BoxId}은 이미 소유자가 있습니다.");
            }

            ValidatePlayerIndex(playerIndex);

            OwnerPlayerIndex = playerIndex;
        }

        private static void ValidatePlayerIndex(PLAYER_INDEX_ENUM playerIndex)
        {
            if(playerIndex != PLAYER_INDEX_ENUM.PLAYER_ONE && playerIndex != PLAYER_INDEX_ENUM.PLAYER_TWO)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex) , playerIndex , "유효한 플레이어만 Box를 획득할 수 있습니다.");
            }
        }
    }
}
