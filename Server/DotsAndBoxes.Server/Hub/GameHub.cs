using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR;

namespace DotsAndBoxes.Server.Hubs
{
    public sealed class GameHub : Hub
    {
        private readonly MatchRoomProvider MATCH_ROOM_PROVIDER;

        public GameHub(MatchRoomProvider matchRoomProvider)
        {
            MATCH_ROOM_PROVIDER = matchRoomProvider ??
                throw new ArgumentNullException(nameof(matchRoomProvider));
        }

        public Task<ConfirmEdgeResponse> ConfirmEdge(
            ConfirmEdgeRequest request)
        {
            if ( request == null )
            {
                ConfirmEdgeResponse invalidResponse =
                    ConfirmEdgeResponseFactory.CreateFailure(
                        Guid.Empty,
                        MATCH_COMMAND_ERROR_ENUM.INVALID_REQUEST,
                        false);

                return Task.FromResult(invalidResponse);
            }

            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                ConfirmEdgeResponse unauthorizedResponse =
                    ConfirmEdgeResponseFactory.CreateFailure(
                        request.RequestId,
                        MATCH_COMMAND_ERROR_ENUM.UNAUTHORIZED,
                        false);

                return Task.FromResult(unauthorizedResponse);
            }

            if ( !MATCH_ROOM_PROVIDER.TryGet(
                    request.MatchId ,
                    out MatchRoom? matchRoom) ||
                matchRoom == null )
            {
                ConfirmEdgeResponse notFoundResponse =
                    ConfirmEdgeResponseFactory.CreateFailure(
                        request.RequestId,
                        MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_FOUND,
                        false);

                return Task.FromResult(notFoundResponse);
            }

            return matchRoom.ConfirmEdge_async(
                userId ,
                request ,
                Context.ConnectionAborted);
        }

        private bool TryGetAuthenticatedUserId(out Guid userId)
        {
            return Guid.TryParse(Context.UserIdentifier , out userId) &&
                   userId != Guid.Empty;
        }
    }
}