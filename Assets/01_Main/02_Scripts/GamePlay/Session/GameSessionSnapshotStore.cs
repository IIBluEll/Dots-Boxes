using DotsAndBoxes.Shared;
using System;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameSessionSnapshotStore
    {
        private MatchSnapshot _currentSnapshot;

        public bool HasSnapshot => _currentSnapshot != null;

        public MatchSnapshot CurrentSnapshot
        {
            get
            {
                if ( _currentSnapshot == null )
                {
                    throw new InvalidOperationException("적용된 Snapshot이 없습니다.");
                }

                return CloneSnapshot(_currentSnapshot);
            }
        }

        public bool TryApply(MatchSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);

            if ( _currentSnapshot != null )
            {
                if ( _currentSnapshot.MatchId != snapshot.MatchId )
                {
                    throw new InvalidOperationException("다른 Match snapshot은 적용할 수 없습니다.");
                }

                if ( _currentSnapshot.PlayerOneUserId != snapshot.PlayerOneUserId || _currentSnapshot.PlayerTwoUserId != snapshot.PlayerTwoUserId )
                {
                    throw new InvalidOperationException("같은 Match에서 Player 정보는 변경될 수 없습니다.");
                }

                if ( snapshot.Revision <= _currentSnapshot.Revision )
                {
                    return false;
                }
            }

            _currentSnapshot = CloneSnapshot(snapshot);
            return true;
        }

        private static void ValidateSnapshot(MatchSnapshot snapshot)
        {
            if ( snapshot == null )
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if ( snapshot.SchemaVersion != MatchSnapshot.CURRENT_SCHEMA_VERSION )
            {
                throw new InvalidOperationException($"지원하지 않는 Snapshot Schema입니다: {snapshot.SchemaVersion}");
            }

            if ( snapshot.MatchId == Guid.Empty )
            {
                throw new InvalidOperationException("Snapshot MatchId가 비어 있습니다.");
            }

            if ( snapshot.Revision < 0 )
            {
                throw new InvalidOperationException("Snapshot Revision은 음수일 수 없습니다.");
            }

            if ( snapshot.PlayerOneUserId == Guid.Empty ||
                snapshot.PlayerTwoUserId == Guid.Empty ||
                snapshot.PlayerOneUserId == snapshot.PlayerTwoUserId )
            {
                throw new InvalidOperationException("Snapshot Player 정보가 올바르지 않습니다.");
            }

            if ( snapshot.EdgeOwners == null ||
                snapshot.EdgeOwners.Length != BoardTopology.EDGE_COUNT )
            {
                throw new InvalidOperationException(
                    $"EdgeOwners 길이는 {BoardTopology.EDGE_COUNT}이어야 합니다.");
            }

            if ( snapshot.BoxOwners == null ||
                snapshot.BoxOwners.Length != BoardTopology.BOX_COUNT )
            {
                throw new InvalidOperationException(
                    $"BoxOwners 길이는 {BoardTopology.BOX_COUNT}이어야 합니다.");
            }

            if ( !IsPlayer(snapshot.CurrentPlayerIndex) )
            {
                throw new InvalidOperationException("현재 플레이어 정보가 올바르지 않습니다.");
            }

            ValidateOwners(snapshot.EdgeOwners , "EdgeOwners");

            int playerOneBoxCount = 0;
            int playerTwoBoxCount = 0;

            for ( int boxId = 0; boxId < snapshot.BoxOwners.Length; boxId++ )
            {
                PLAYER_INDEX_ENUM ownerPlayerIndex = snapshot.BoxOwners[boxId];

                if ( !IsOwner(ownerPlayerIndex) )
                {
                    throw new InvalidOperationException(
                        $"BoxOwners[{boxId}] 값이 올바르지 않습니다.");
                }

                if ( ownerPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE )
                {
                    playerOneBoxCount++;
                }
                else if ( ownerPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_TWO )
                {
                    playerTwoBoxCount++;
                }
            }

            if ( snapshot.PlayerOneScore != playerOneBoxCount ||
                snapshot.PlayerTwoScore != playerTwoBoxCount )
            {
                throw new InvalidOperationException(
                    "Snapshot 점수와 Box 소유권이 일치하지 않습니다.");
            }

            if ( snapshot.MatchState == SERVER_MATCH_STATE_ENUM.ACTIVE &&
                snapshot.GameResult != GAME_RESULT_ENUM.IN_PROGRESS )
            {
                throw new InvalidOperationException(
                    "ACTIVE Match의 결과는 IN_PROGRESS여야 합니다.");
            }

            if ( snapshot.MatchState == SERVER_MATCH_STATE_ENUM.FINISHED &&
                snapshot.GameResult == GAME_RESULT_ENUM.IN_PROGRESS )
            {
                throw new InvalidOperationException(
                    "FINISHED Match의 결과가 IN_PROGRESS일 수 없습니다.");
            }
        }

        private static void ValidateOwners(PLAYER_INDEX_ENUM[] owners , string fieldName)
        {
            for ( int index = 0; index < owners.Length; index++ )
            {
                if ( !IsOwner(owners[ index ]) )
                {
                    throw new InvalidOperationException(
                        $"{fieldName}[{index}] 값이 올바르지 않습니다.");
                }
            }
        }

        private static bool IsPlayer(PLAYER_INDEX_ENUM playerIndex)
        {
            return playerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE || playerIndex == PLAYER_INDEX_ENUM.PLAYER_TWO;
        }

        private static bool IsOwner(PLAYER_INDEX_ENUM playerIndex)
        {
            return playerIndex == PLAYER_INDEX_ENUM.NONE || IsPlayer(playerIndex);
        }

        private static MatchSnapshot CloneSnapshot(MatchSnapshot snapshot)
        {
            return new MatchSnapshot
            {
                SchemaVersion = snapshot.SchemaVersion ,
                MatchId = snapshot.MatchId ,
                Revision = snapshot.Revision ,
                MatchState = snapshot.MatchState ,
                PlayerOneUserId = snapshot.PlayerOneUserId ,
                PlayerTwoUserId = snapshot.PlayerTwoUserId ,
                CurrentPlayerIndex = snapshot.CurrentPlayerIndex ,
                EdgeOwners = (PLAYER_INDEX_ENUM[])snapshot.EdgeOwners.Clone() ,
                BoxOwners = (PLAYER_INDEX_ENUM[])snapshot.BoxOwners.Clone() ,
                PlayerOneScore = snapshot.PlayerOneScore ,
                PlayerTwoScore = snapshot.PlayerTwoScore ,
                GameResult = snapshot.GameResult
            };
        }
    }
}
