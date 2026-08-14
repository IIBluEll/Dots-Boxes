using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Matches
{
    public static class MatchCommandErrorMapper
    {
        public static MATCH_COMMAND_ERROR_ENUM ToMatchCommandError(MOVE_ERROR_ENUM moveError)
        {
            return moveError switch
            {
                MOVE_ERROR_ENUM.NONE => MATCH_COMMAND_ERROR_ENUM.NONE,
                MOVE_ERROR_ENUM.INVALID_PLAYER => MATCH_COMMAND_ERROR_ENUM.NOT_A_MATCH_PLAYER,
                MOVE_ERROR_ENUM.NOT_YOUR_TURN => MATCH_COMMAND_ERROR_ENUM.NOT_YOUR_TURN,
                MOVE_ERROR_ENUM.INVALID_EDGE => MATCH_COMMAND_ERROR_ENUM.INVALID_EDGE,
                MOVE_ERROR_ENUM.EDGE_ALREADY_CONFIRMED => MATCH_COMMAND_ERROR_ENUM.EDGE_ALREADY_CONFIRMED,
                MOVE_ERROR_ENUM.GAME_ALREADY_FINISHED => MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE,
                _ => MATCH_COMMAND_ERROR_ENUM.INTERNAL_ERROR
            };
        }
    }
}