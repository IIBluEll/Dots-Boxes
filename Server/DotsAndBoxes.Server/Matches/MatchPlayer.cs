namespace DotsAndBoxes.Server.Matches
{
    public class MatchPlayer
    {
        public Guid UserId { get; }
      
        public string? ConnectionId { get; private set; }

        public bool IsConnected => !string.IsNullOrWhiteSpace(ConnectionId);

        public MatchPlayer(Guid userId, string connectionId)
        {
            if(userId == Guid.Empty)
            {
                throw new ArgumentException("UserId는 Guid.Empty일 수 없습니다." , nameof(userId));
            }

            ValidateConnectionID(connectionId);

            UserId = userId;
            ConnectionId = connectionId;
        }

        public void Connect(string connectionId)
        {
            ValidateConnectionID(connectionId);
            ConnectionId = connectionId;
        }

        public void Disconnect()
        {
            ConnectionId = null;
        }

        private static void ValidateConnectionID(string connectionId)
        {
            if(string.IsNullOrWhiteSpace(connectionId))
            {
                throw new ArgumentException("ConnectionId를 지정해야 합니다." , nameof(connectionId));
            }
        }
    }
}
