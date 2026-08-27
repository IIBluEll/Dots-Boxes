using DotsAndBoxes.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay
{
    public interface IOnlineSession : IDisposable
    {
        event Action<MatchAssignment> MatchFound;
        event Action<GAME_SESSION_CONNECTION_STATE_ENUM> ConnectionStateChanged;

        GAME_SESSION_CONNECTION_STATE_ENUM ConnectionState { get; }
        bool IsQueueing { get; }
        bool HasMatchAssignment { get; }
        MatchAssignment CurrentAssignment { get; }

        Task Start_async(CancellationToken cancellationToken = default);
        Task EnterMatchmaking_async(CancellationToken cancellationToken = default);
        Task<bool> CancelMatchmaking_async(CancellationToken cancellationToken = default);
    }
}
