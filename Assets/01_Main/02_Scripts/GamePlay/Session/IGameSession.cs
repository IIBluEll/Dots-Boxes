using DotsAndBoxes.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay
{
    public interface IGameSession : IDisposable
    {
        event Action<MatchSnapshot> SnapshotChanged;
        event Action<GAME_SESSION_CONNECTION_STATE_ENUM> ConnectionStateChanged;

        Guid MatchId { get; }
        PLAYER_INDEX_ENUM LocalPlayerIndex { get; }
        GAME_SESSION_CONNECTION_STATE_ENUM ConnectionState { get; }
        bool CanConfirmCurrentTurn { get; }
        bool HasPendingConfirm { get; }
        int PendingConfirmEdgeId { get; }

        bool HasSnapshot { get; }
        MatchSnapshot CurrentSnapshot { get; }

        Task Start_async(CancellationToken cancellationToken = default);
        Task<ConfirmEdgeResponse> ConfirmEdge_async(int edgeId , CancellationToken cancellationToken = default);
        Task<MatchSnapshot> RequestSync_async(CancellationToken cancellationToken = default);
    }
}
