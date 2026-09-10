namespace DotsAndBoxes.Shared
{
    public enum MATCH_COMMAND_ERROR_ENUM
    {
        NONE = 0,
        UNAUTHORIZED = 1,
        MATCH_NOT_FOUND = 2,
        NOT_A_MATCH_PLAYER = 3,
        MATCH_NOT_ACTIVE = 4,
        NOT_YOUR_TURN = 5,
        INVALID_EDGE = 6,
        EDGE_ALREADY_CONFIRMED = 7,
        REVISION_MISMATCH = 8,
        DUPLICATE_REQUEST_CONFLICT = 9,
        INTERNAL_ERROR = 10,
        INVALID_REQUEST = 11,
        RATE_LIMITED = 12
    }
}
