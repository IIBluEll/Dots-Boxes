namespace DotsAndBoxes.Shared
{
    public enum MATCH_FINISH_REASON_ENUM
    {
        NONE = 0,
        BOARD_COMPLETED = 1,
        FORFEIT = 2,
        TIMEOUT_FORFEIT = 3,
        DISCONNECT_TIMEOUT = 4,
        SERVER_ABORTED = 5
    }
}