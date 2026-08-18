namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchPlayer
    {
        public Guid UserId { get; }

        public MatchPlayer(Guid userId)
        {
            if ( userId == Guid.Empty )
            {
                throw new ArgumentException("UserId는 Guid.Empty일 수 없습니다." , nameof(userId));
            }

            UserId = userId;
        }
    }
}
