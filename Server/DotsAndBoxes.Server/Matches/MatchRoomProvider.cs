using System.Collections.Concurrent;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchRoomProvider
    {
        private readonly ConcurrentDictionary<Guid, MatchRoom> MATCH_ROOMS = new ConcurrentDictionary<Guid, MatchRoom>();

        public int Count => MATCH_ROOMS.Count;

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
    }
}