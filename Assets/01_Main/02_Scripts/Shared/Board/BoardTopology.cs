// 4×4 보드의 Edge·Box ID 배치와 서로의 인접 관계를 계산합니다.
using System;

namespace DotsAndBoxes.Shared
{
    public readonly struct BoxEdgeIds
    {
        public int TopEdgeId { get; }
        public int BottomEdgeId { get; }
        public int LeftEdgeId { get; }
        public int RightEdgeId { get; }

        public BoxEdgeIds(int  topEdgeId, int bottomEdgeId, int leftEdgeId, int rightEdgeId)
        {
            TopEdgeId = topEdgeId;
            BottomEdgeId = bottomEdgeId;
            LeftEdgeId = leftEdgeId;
            RightEdgeId = rightEdgeId;
        }
    }

    public readonly struct AdjacentBoxIDs
    {
        public int FirstBoxId { get; }
        public int SecondBoxId { get; }
        public int Count { get; }

        internal AdjacentBoxIDs(int firstBoxId, int secondBoxId, int count)
        {
            FirstBoxId = firstBoxId;
            SecondBoxId = secondBoxId;
            Count = count;
        }
    }

    public static class BoardTopology
    {
        public const int BOX_ROWS = 4;
        public const int BOX_COLUMNS = 4;

        public const int DOT_ROWS = BOX_ROWS + 1;
        public const int DOT_COLUMNS = BOX_COLUMNS + 1;

        public const int HORIZONTAL_EDGE_COUNT = DOT_ROWS * BOX_COLUMNS;
        public const int VERTICAL_EDGE_COUNT = DOT_COLUMNS * BOX_ROWS;

        public const int EDGE_COUNT = HORIZONTAL_EDGE_COUNT + VERTICAL_EDGE_COUNT;

        public const int BOX_COUNT = BOX_ROWS * BOX_COLUMNS;

        public static int GetHorizontalEdgeID(int row, int column)
        {
            ValidateRange(row , 0 , DOT_ROWS - 1 , nameof(row));
            ValidateRange(column, 0, BOX_COLUMNS - 1, nameof(column));

            return row * BOX_COLUMNS + column;
        }

        public static int GetVerticalEdgeID(int row, int column)
        {
            ValidateRange(row , 0 , BOX_ROWS - 1 , nameof(row));
            ValidateRange(column , 0 , DOT_COLUMNS - 1 , nameof(column));

            return HORIZONTAL_EDGE_COUNT + row * DOT_COLUMNS + column;
        }

        public static int GetBoxID(int row, int column)
        {
            ValidateRange(row , 0 , BOX_ROWS - 1 , nameof(row));
            ValidateRange(column , 0 , BOX_COLUMNS - 1 , nameof(column));

            return row * BOX_COLUMNS + column;
        }

        public static BoxEdgeIds GetBoxEdgeIDs(int boxId)
        {
            ValidateRange(boxId, 0, BOX_COUNT - 1, nameof(boxId));

            int row = boxId / BOX_COLUMNS;
            int column = boxId % BOX_COLUMNS;

            int topEdgeId = GetHorizontalEdgeID(row, column);
            int bottomEdgeId = GetHorizontalEdgeID(row + 1, column);
            int leftEdgeId = GetVerticalEdgeID(row, column);
            int rightEdgeId = GetVerticalEdgeID(row, column + 1);

            return new BoxEdgeIds(topEdgeId , bottomEdgeId , leftEdgeId , rightEdgeId);
        }

        public static AdjacentBoxIDs GetAdjacentBoxIDs(int edgeId)
        {
            ValidateRange(edgeId, 0, EDGE_COUNT - 1, nameof(edgeId));

            if(edgeId < HORIZONTAL_EDGE_COUNT)
            {
                return GetHorizontalAdjacentBoxIDs(edgeId);
            }

            return GetVerticalAdjacentBoxIDs(edgeId);
        }

        private static AdjacentBoxIDs GetHorizontalAdjacentBoxIDs(int edgeId)
        {
            int row = edgeId / BOX_COLUMNS;
            int column = edgeId % BOX_COLUMNS;

            if(row == 0)
            {
                int boxId = GetBoxID(row,column);
                return new AdjacentBoxIDs(boxId , -1 , 1);
            }

            if(row == BOX_ROWS)
            {
                int boxId = GetBoxID(row - 1, column);
                return new AdjacentBoxIDs(boxId , -1 , 1);
            }

            int upperBoxId = GetBoxID(row - 1, column);
            int lowerBoxId = GetBoxID(row, column);

            return new AdjacentBoxIDs(upperBoxId, lowerBoxId , 2);
        }

        private static AdjacentBoxIDs GetVerticalAdjacentBoxIDs(int edgeId)
        {
            int localEdgeId = edgeId - HORIZONTAL_EDGE_COUNT;
            int row = localEdgeId / DOT_COLUMNS;
            int column = localEdgeId % DOT_COLUMNS;

            if(column == 0)
            {
                int boxId = GetBoxID(row, column);
                return new AdjacentBoxIDs(boxId , -1 , 1);
            }

            if ( column == BOX_COLUMNS )
            {
                int boxId = GetBoxID(row, column - 1);
                return new AdjacentBoxIDs(boxId , -1 , 1);
            }

            int leftBoxId = GetBoxID(row , column - 1);
            int rightBoxId = GetBoxID(row, column);

            return new AdjacentBoxIDs(leftBoxId , rightBoxId , 2);
        }

        private static void ValidateRange(int value, int minimum, int maximum, string name)
        {
            if(value < minimum ||  value > maximum)
            {
                throw new ArgumentOutOfRangeException(name , value , $"값은 {minimum}부터 {maximum} 사이여야 합니다.");
            }
        }
    }
}
