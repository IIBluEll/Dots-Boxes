using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchRoomPreviewResult
    {
        public bool IsAccepted { get; }
        public MATCH_COMMAND_ERROR_ENUM Error { get; }
        public OpponentPreviewUpdate? Update { get; }

        private MatchRoomPreviewResult(
            bool isAccepted ,
            MATCH_COMMAND_ERROR_ENUM error ,
            OpponentPreviewUpdate? update)
        {
            IsAccepted = isAccepted;
            Error = error;
            Update = update;
        }

        public static MatchRoomPreviewResult CreateAccepted(OpponentPreviewUpdate update)
        {
            return new MatchRoomPreviewResult(
                true ,
                MATCH_COMMAND_ERROR_ENUM.NONE ,
                update ?? throw new ArgumentNullException(nameof(update)));
        }

        public static MatchRoomPreviewResult CreateRejected(MATCH_COMMAND_ERROR_ENUM error)
        {
            if ( error == MATCH_COMMAND_ERROR_ENUM.NONE )
            {
                throw new ArgumentException("거절 결과에는 오류 코드가 필요합니다." , nameof(error));
            }

            return new MatchRoomPreviewResult(false , error , null);
        }
    }
}
