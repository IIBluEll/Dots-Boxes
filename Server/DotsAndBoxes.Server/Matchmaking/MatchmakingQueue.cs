namespace DotsAndBoxes.Server.Matchmaking
{
    public sealed class MatchmakingQueue
    {
        private readonly object QUEUE_LOCK = new object();
        private readonly Queue<Guid> WAITING_USERS = new Queue<Guid>();
        private readonly HashSet<Guid> WAITING_USER_IDS = new HashSet<Guid>();

        public int Count
        {
            get
            {
                lock ( QUEUE_LOCK )
                {
                    return WAITING_USER_IDS.Count;
                }
            }
        }

        public MatchmakingEnqueueResult Enqueue(Guid userId)
        {
            if ( userId == Guid.Empty )
            {
                throw new ArgumentException("UserId가 비어 있습니다." , nameof(userId));
            }

            lock ( QUEUE_LOCK )
            {
                if ( WAITING_USER_IDS.Contains(userId) )
                {
                    return MatchmakingEnqueueResult.AlreadyQueued();
                }

                while ( WAITING_USERS.Count > 0 )
                {
                    Guid opponentUserId = WAITING_USERS.Dequeue();

                    if ( WAITING_USER_IDS.Remove(opponentUserId) )
                    {
                        return MatchmakingEnqueueResult.Matched(opponentUserId);
                    }
                }

                WAITING_USERS.Enqueue(userId);
                WAITING_USER_IDS.Add(userId);
                return MatchmakingEnqueueResult.Queued();
            }
        }

        public bool TryCancel(Guid userId)
        {
            if ( userId == Guid.Empty )
            {
                return false;
            }

            lock ( QUEUE_LOCK )
            {
                return WAITING_USER_IDS.Remove(userId);
            }
        }
    }

    public sealed class MatchmakingEnqueueResult
    {
        public MATCHMAKING_ENQUEUE_STATE_ENUM State { get; }
        public Guid OpponentUserId { get; }

        private MatchmakingEnqueueResult(
            MATCHMAKING_ENQUEUE_STATE_ENUM state ,
            Guid opponentUserId)
        {
            State = state;
            OpponentUserId = opponentUserId;
        }

        public static MatchmakingEnqueueResult Queued()
        {
            return new MatchmakingEnqueueResult(
                MATCHMAKING_ENQUEUE_STATE_ENUM.QUEUED ,
                Guid.Empty);
        }

        public static MatchmakingEnqueueResult Matched(Guid opponentUserId)
        {
            return new MatchmakingEnqueueResult(
                MATCHMAKING_ENQUEUE_STATE_ENUM.MATCHED ,
                opponentUserId);
        }

        public static MatchmakingEnqueueResult AlreadyQueued()
        {
            return new MatchmakingEnqueueResult(
                MATCHMAKING_ENQUEUE_STATE_ENUM.ALREADY_QUEUED ,
                Guid.Empty);
        }
    }

    public enum MATCHMAKING_ENQUEUE_STATE_ENUM
    {
        QUEUED = 0 ,
        MATCHED = 1 ,
        ALREADY_QUEUED = 2
    }
}
