using System.Collections.Concurrent;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchRoomProvider
    {
        private readonly ConcurrentDictionary<Guid, MatchRoom> MATCH_ROOMS = new ConcurrentDictionary<Guid, MatchRoom>();

        public int Count => MATCH_ROOMS.Count;
        public int WaitingCount => MATCH_ROOMS.Values.Count(room =>
            room.MatchState == DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS ||
            room.MatchState == DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.WAITING_FOR_READY);
        public int StartingCount => MATCH_ROOMS.Values.Count(room => room.MatchState == DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.STARTING);
        public int ActiveCount => MATCH_ROOMS.Values.Count(room => room.MatchState == DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.ACTIVE);
        public int FinishedCount => MATCH_ROOMS.Values.Count(room => room.MatchState == DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.FINISHED);
        public int CancelledCount => MATCH_ROOMS.Values.Count(room => room.MatchState == DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.CANCELLED);

        public bool TryAdd(MatchRoom matchRoom)
        {
            if ( matchRoom == null )
            {
                throw new ArgumentNullException(nameof(matchRoom));
            }

            return MATCH_ROOMS.TryAdd(matchRoom.MatchId , matchRoom);
        }

        public bool TryGet(Guid matchId , out MatchRoom? matchRoom)
        {
            return MATCH_ROOMS.TryGetValue(matchId , out matchRoom);
        }

        public bool TryRemove(Guid matchId , out MatchRoom? matchRoom)
        {
            return MATCH_ROOMS.TryRemove(matchId , out matchRoom);
        }

        public IReadOnlyList<MatchRoom> GetRoomsSnapshot()
        {
            return MATCH_ROOMS.Values.ToArray();
        }

        public bool ContainsUserInOpenMatch(Guid userId)
        {
            if ( userId == Guid.Empty )
            {
                return false;
            }

            foreach ( MatchRoom matchRoom in MATCH_ROOMS.Values )
            {
                if ( matchRoom.MatchState != DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.FINISHED &&
                     matchRoom.MatchState != DotsAndBoxes.Shared.SERVER_MATCH_STATE_ENUM.CANCELLED &&
                     matchRoom.TryGetPlayerIndex(userId , out _) )
                {
                    return true;
                }
            }

            return false;
        }
    }
}
