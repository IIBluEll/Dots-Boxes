namespace DotsAndBoxes.Shared
{
    public enum SERVER_MATCH_STATE_ENUM
    {
        NONE = 0,
        WAITING_FOR_PLAYERS = 1,
        WAITING_FOR_READY = 2,
        STARTING = 3,
        ACTIVE = 4,
        FINISHED = 5,
        CANCELLED = 6
    }
}
