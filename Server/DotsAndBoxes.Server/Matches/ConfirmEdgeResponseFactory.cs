using DotsAndBoxes.Shared;
using System;

namespace DotsAndBoxes.Server.Matches
{
    public static class ConfirmEdgeResponseFactory
    {
        public static ConfirmEdgeResponse CreateSuccess(Guid requestId , MatchSnapshot snapshot)
        {
            if ( snapshot == null )
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new ConfirmEdgeResponse
            {
                RequestId = requestId ,
                IsAccepted = true ,
                Error = MATCH_COMMAND_ERROR_ENUM.NONE ,
                ShouldRequestSync = false ,
                Snapshot = snapshot
            };
        }

        public static ConfirmEdgeResponse CreateFailure( Guid requestId , MATCH_COMMAND_ERROR_ENUM error , bool shouldRequestSync , MatchSnapshot? snapshot = null)
        {
            if ( error == MATCH_COMMAND_ERROR_ENUM.NONE )
            {
                throw new ArgumentException("실패 Response에는 오류가 필요합니다." , nameof(error));
            }

            return new ConfirmEdgeResponse
            {
                RequestId = requestId ,
                IsAccepted = false ,
                Error = error ,
                ShouldRequestSync = shouldRequestSync ,
                Snapshot = snapshot
            };
        }
    }
}