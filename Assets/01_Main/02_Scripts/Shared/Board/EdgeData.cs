// 보드 위 Edge 하나의 ID와 확정 여부 및 소유 플레이어를 관리합니다.
using System;

namespace DotsAndBoxes.Shared
{
    public sealed class EdgeData
    {
        public int EdgeId { get; }

        public PLAYER_INDEX_ENUM OwnerPlayerIndex { get; private set; }

        public bool IsConfirmed => OwnerPlayerIndex != PLAYER_INDEX_ENUM.NONE;

        public EdgeData(int edgeId)
        {
            if(edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeId), edgeId , $"Edge ID는 0부터 {BoardTopology.EDGE_COUNT - 1} 사이여야 합니다.");
            }

            EdgeId = edgeId;
            OwnerPlayerIndex = PLAYER_INDEX_ENUM.NONE;
        }

        internal void Confirm(PLAYER_INDEX_ENUM playerIndex)
        {
            if ( IsConfirmed )
            {
                throw new InvalidOperationException($"Edge {EdgeId}은 이미 확정됐습니다.");
            }

            ValidatePlayerIndex(playerIndex);

            OwnerPlayerIndex = playerIndex;
        }

        private static void ValidatePlayerIndex(PLAYER_INDEX_ENUM playerIndex)
        {
            if(playerIndex != PLAYER_INDEX_ENUM.PLAYER_ONE && playerIndex != PLAYER_INDEX_ENUM.PLAYER_TWO)
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex) , playerIndex , "유효한 플레이어만 Edge를 확정할 수 있습니다.");
            }
        }
    }
}
