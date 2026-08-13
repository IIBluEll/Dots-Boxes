// 전체 Edge와 Box, 현재 턴, 점수 및 게임 종료 상태를 보관합니다.
using System;
using System.Collections.Generic;

namespace DotsAndBoxes.Shared
{
    public sealed class DotsBoard
    {
        private readonly EdgeData[] _edges;
        private readonly BoxData[] _boxes;

        public IReadOnlyList<EdgeData> Edges => _edges;
        public IReadOnlyList<BoxData> Boxes => _boxes;

        public PLAYER_INDEX_ENUM CurrentPlayerIndex { get; private set; }

        public int ConfirmedEdgeCount
        {
            get
            {
                int count = 0;
                for(int i = 0; i < _edges.Length; i++ )
                {
                    if ( _edges[i].IsConfirmed)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int OwnedBoxCount
        {
            get
            {
                int count = 0;

                for ( int i = 0; i < _boxes.Length; i++ )
                {
                    if ( _boxes[ i ].IsOwned )
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PlayerOneScore => GetScore(PLAYER_INDEX_ENUM.PLAYER_ONE);
        public int PlayerTwoScore => GetScore(PLAYER_INDEX_ENUM.PLAYER_TWO);

        public bool IsGameFinished => OwnedBoxCount == BoardTopology.BOX_COUNT;

        public GAME_RESULT_ENUM GameResult
        {
            get
            {
                if ( !IsGameFinished )
                {
                    return GAME_RESULT_ENUM.IN_PROGRESS;
                }

                if ( PlayerOneScore > PlayerTwoScore )
                {
                    return GAME_RESULT_ENUM.PLAYER_ONE_WIN;
                }

                if ( PlayerTwoScore > PlayerOneScore )
                {
                    return GAME_RESULT_ENUM.PLAYER_TWO_WIN;
                }

                return GAME_RESULT_ENUM.DRAW;
            }
        }

        public DotsBoard(PLAYER_INDEX_ENUM startingPlayerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE)
        {
            ValidatePlayerIndex(startingPlayerIndex);

            _edges = new EdgeData[ BoardTopology.EDGE_COUNT ];
            _boxes = new BoxData[ BoardTopology.BOX_COUNT ];

            for ( int edgeId = 0; edgeId < _edges.Length; edgeId++ )
            {
                _edges[ edgeId ] = new EdgeData(edgeId);
            }

            for ( int boxid = 0; boxid < _boxes.Length; boxid++ )
            {
                _boxes[ boxid ] = new BoxData(boxid);
            }

            CurrentPlayerIndex = startingPlayerIndex;
        }

        public EdgeData GetEdge(int edgeId)
        {
            if ( edgeId < 0 || edgeId >= _edges.Length )
            {
                throw new ArgumentOutOfRangeException(nameof(edgeId) , edgeId , $"Edge ID는 0부터 {_edges.Length - 1} 사이여야 합니다.");
            }

            return _edges[ edgeId ];
        }

        public BoxData GetBox(int boxId)
        {
            if(boxId < 0 || boxId >= _boxes.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(boxId) , boxId , $"Box ID는 0부터 {_boxes.Length - 1} 사이여야 합니다.");
            }

            return _boxes[boxId];
        }

        public int GetScore(PLAYER_INDEX_ENUM playerIndex)
        {
            ValidatePlayerIndex(playerIndex);

            int score = 0;

            for(int i = 0; i < _boxes.Length; i++ )
            {
                if ( _boxes[ i ].OwnerPlayerIndex == playerIndex )
                {
                    score++;
                }
            }

            return score;
        }

        internal void SwitchTurn()
        {
            CurrentPlayerIndex = CurrentPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE ? PLAYER_INDEX_ENUM.PLAYER_TWO : PLAYER_INDEX_ENUM.PLAYER_ONE;
        }

        private static void ValidatePlayerIndex(PLAYER_INDEX_ENUM playerIndex)
        {
            if ( playerIndex != PLAYER_INDEX_ENUM.PLAYER_ONE &&
                playerIndex != PLAYER_INDEX_ENUM.PLAYER_TWO )
            {
                throw new ArgumentOutOfRangeException(nameof(playerIndex) , playerIndex , "유효한 플레이어를 지정해야 합니다.");
            }
        }
    }
}
