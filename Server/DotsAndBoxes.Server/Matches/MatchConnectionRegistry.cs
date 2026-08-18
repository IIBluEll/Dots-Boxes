namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchConnectionRegistry
    {
        private readonly object CONNECTION_LOCK = new object();
        private readonly Dictionary<string, (Guid MatchId, Guid UserId)> CONNECTIONS =
            new Dictionary<string, (Guid MatchId, Guid UserId)>();
        private readonly Dictionary<(Guid MatchId, Guid UserId), string> PARTICIPANT_CONNECTIONS =
            new Dictionary<(Guid MatchId, Guid UserId), string>();

        public bool TryRegister(string connectionId , Guid matchId , Guid userId)
        {
            if ( string.IsNullOrWhiteSpace(connectionId) )
            {
                throw new ArgumentException("ConnectionId가 비어 있습니다." , nameof(connectionId));
            }

            if ( matchId == Guid.Empty )
            {
                throw new ArgumentException("MatchId가 비어 있습니다." , nameof(matchId));
            }

            if ( userId == Guid.Empty )
            {
                throw new ArgumentException("UserId가 비어 있습니다." , nameof(userId));
            }

            lock ( CONNECTION_LOCK )
            {
                if ( CONNECTIONS.TryGetValue(
                    connectionId ,
                    out (Guid MatchId, Guid UserId) registeredParticipant) )
                {
                    return registeredParticipant.MatchId == matchId &&
                           registeredParticipant.UserId == userId;
                }

                (Guid MatchId, Guid UserId) participant = (matchId, userId);

                if ( PARTICIPANT_CONNECTIONS.ContainsKey(participant) )
                {
                    return false;
                }

                CONNECTIONS.Add(connectionId , participant);
                PARTICIPANT_CONNECTIONS.Add(participant , connectionId);
                return true;
            }
        }

        public bool TryRemove(string connectionId , out Guid matchId , out Guid userId)
        {
            if ( string.IsNullOrWhiteSpace(connectionId) )
            {
                matchId = Guid.Empty;
                userId = Guid.Empty;
                return false;
            }

            lock ( CONNECTION_LOCK )
            {
                if ( !CONNECTIONS.Remove(
                    connectionId ,
                    out (Guid MatchId, Guid UserId) participant) )
                {
                    matchId = Guid.Empty;
                    userId = Guid.Empty;
                    return false;
                }

                PARTICIPANT_CONNECTIONS.Remove(participant);
                matchId = participant.MatchId;
                userId = participant.UserId;
                return true;
            }
        }
    }
}
