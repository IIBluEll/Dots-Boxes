using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using Microsoft.AspNetCore.SignalR;

namespace DotsAndBoxes.Server.Hubs
{
    public sealed class GameHub : Hub<IGameClient>
    {
        private readonly MatchRoomProvider MATCH_ROOM_PROVIDER;
        private readonly MatchConnectionRegistry MATCH_CONNECTION_REGISTRY;
        private readonly IHostApplicationLifetime APPLICATION_LIFETIME;

        public GameHub(
            MatchRoomProvider matchRoomProvider ,
            MatchConnectionRegistry matchConnectionRegistry ,
            IHostApplicationLifetime applicationLifetime)
        {
            MATCH_ROOM_PROVIDER = matchRoomProvider ??
                throw new ArgumentNullException(nameof(matchRoomProvider));

            MATCH_CONNECTION_REGISTRY = matchConnectionRegistry ??
                throw new ArgumentNullException(nameof(matchConnectionRegistry));

            APPLICATION_LIFETIME = applicationLifetime ??
                throw new ArgumentNullException(nameof(applicationLifetime));
        }

        public async Task<MatchSnapshot> JoinMatch(Guid matchId)
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            MatchRoom matchRoom = GetParticipantRoom(matchId, userId);

            if ( !MATCH_CONNECTION_REGISTRY.TryRegister(
                Context.ConnectionId ,
                matchId ,
                userId) )
            {
                throw new HubException("이미 활성화된 Match 연결이 있습니다.");
            }

            try
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId ,
                    CreateMatchGroupName(matchId) ,
                    Context.ConnectionAborted);

                return await matchRoom.CreateSnapshot_async(
                    Context.ConnectionAborted);
            }
            catch
            {
                MATCH_CONNECTION_REGISTRY.TryRemove(
                    Context.ConnectionId ,
                    out _ ,
                    out _);

                throw;
            }
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                if ( MATCH_CONNECTION_REGISTRY.TryRemove(
                        Context.ConnectionId ,
                        out Guid matchId ,
                        out Guid userId) &&
                     !APPLICATION_LIFETIME.ApplicationStopping.IsCancellationRequested &&
                     MATCH_ROOM_PROVIDER.TryGet(matchId , out MatchRoom? matchRoom) &&
                     matchRoom != null )
                {
                    MatchSnapshot? finalSnapshot =
                        await matchRoom.TryForfeit_async(userId);

                    if ( finalSnapshot != null )
                    {
                        await Clients
                            .Group(CreateMatchGroupName(matchId))
                            .MatchStateChanged(finalSnapshot);
                    }
                }
            }
            finally
            {
                await base.OnDisconnectedAsync(exception);
            }
        }

        public async Task<ConfirmEdgeResponse> ConfirmEdge(
            ConfirmEdgeRequest request)
        {
            if ( request == null )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    Guid.Empty ,
                    MATCH_COMMAND_ERROR_ENUM.INVALID_REQUEST ,
                    false);
            }

            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.UNAUTHORIZED ,
                    false);
            }

            if ( !MATCH_ROOM_PROVIDER.TryGet(
                    request.MatchId ,
                    out MatchRoom? matchRoom) ||
                matchRoom == null )
            {
                return ConfirmEdgeResponseFactory.CreateFailure(
                    request.RequestId ,
                    MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_FOUND ,
                    false);
            }

            ConfirmEdgeResponse response =
                await matchRoom.ConfirmEdge_async(
                    userId,
                    request,
                    Context.ConnectionAborted);

            if ( response.IsAccepted && response.Snapshot != null )
            {
                await Clients
                    .Group(CreateMatchGroupName(request.MatchId))
                    .MatchStateChanged(response.Snapshot);
            }

            return response;
        }

        public async Task<MatchSnapshot> RequestSync(Guid matchId)
        {
            if ( !TryGetAuthenticatedUserId(out Guid userId) )
            {
                throw new HubException("인증되지 않은 연결입니다.");
            }

            MatchRoom matchRoom = GetParticipantRoom(matchId, userId);

            return await matchRoom.CreateSnapshot_async(
                Context.ConnectionAborted);
        }

        private MatchRoom GetParticipantRoom(Guid matchId , Guid userId)
        {
            if ( !MATCH_ROOM_PROVIDER.TryGet(
                    matchId ,
                    out MatchRoom? matchRoom) ||
                matchRoom == null )
            {
                throw new HubException("매치를 찾을 수 없습니다.");
            }

            if ( !matchRoom.TryGetPlayerIndex(userId , out _) )
            {
                throw new HubException("매치 참가자가 아닙니다.");
            }

            return matchRoom;
        }

        private bool TryGetAuthenticatedUserId(out Guid userId)
        {
            return Guid.TryParse(Context.UserIdentifier , out userId) &&
                   userId != Guid.Empty;
        }

        private static string CreateMatchGroupName(Guid matchId)
        {
            return $"match-{matchId:N}";
        }
    }
}
