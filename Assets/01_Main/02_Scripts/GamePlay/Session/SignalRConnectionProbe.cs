using DotsAndBoxes.Shared;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    public sealed class SignalRConnectionProbe : MonoBehaviour
    {
        private readonly ConcurrentQueue<string> RECEIVED_LOGS = new ConcurrentQueue<string>();

        [Header("Server")]
        [SerializeField] private string _serverUrl = "http://localhost:5049";

        [Header("Development Match")]
        [SerializeField] private string _matchId;
        [SerializeField] private string _userId;
        [SerializeField, Min(0)] private int _edgeId = 1;

        private IGameSession _session;
        private CancellationTokenSource _destroyCancellationTokenSource;
        private bool _isConfirming;
        private int _snapshotChangedCount;

        private async void Start()
        {
            _destroyCancellationTokenSource = new CancellationTokenSource();

            try
            {
                if ( !Guid.TryParse(_matchId , out Guid matchId) || matchId == Guid.Empty )
                {
                    throw new InvalidOperationException("MatchId가 올바르지 않습니다.");
                }

                if ( !Guid.TryParse(_userId , out Guid userId) || userId == Guid.Empty )
                {
                    throw new InvalidOperationException("UserId가 올바르지 않습니다.");
                }

                _session = new SignalRGameSession(_serverUrl , matchId , userId);
                _session.SnapshotChanged += OnSnapshotChanged;

                await _session.Start_async(_destroyCancellationTokenSource.Token);

                RECEIVED_LOGS.Enqueue(
                    $"[Second Player] 연결 완료 | LocalPlayer={_session.LocalPlayerIndex} | " +
                    $"CanConfirm={_session.CanConfirmCurrentTurn}");
            }
            catch ( OperationCanceledException )
            {
                RECEIVED_LOGS.Enqueue("[Second Player] 작업이 취소됐습니다.");
            }
            catch ( Exception exception )
            {
                RECEIVED_LOGS.Enqueue($"[Second Player] 연결 오류: {exception}");
            }
        }

        private void Update()
        {
            while ( RECEIVED_LOGS.TryDequeue(out string message) )
            {
                Debug.Log(message , this);
            }
        }

        private void OnDestroy()
        {
            _destroyCancellationTokenSource?.Cancel();

            if ( _session != null )
            {
                _session.SnapshotChanged -= OnSnapshotChanged;
                _session.Dispose();
            }

            _destroyCancellationTokenSource?.Dispose();
        }

        [ContextMenu("Confirm Configured Edge")]
        private void ConfirmConfiguredEdge()
        {
            if ( !Application.isPlaying )
            {
                Debug.LogWarning("Play Mode에서만 Confirm할 수 있습니다." , this);
                return;
            }

            _ = ConfirmConfiguredEdge_async();
        }

        [ContextMenu("Request Sync")]
        private void RequestSync()
        {
            if ( !Application.isPlaying )
            {
                Debug.LogWarning("Play Mode에서만 Sync를 요청할 수 있습니다." , this);
                return;
            }

            _ = RequestSync_async();
        }

        private async Task ConfirmConfiguredEdge_async()
        {
            if ( _isConfirming )
            {
                RECEIVED_LOGS.Enqueue("[Second Player] 이미 Confirm 요청을 처리 중입니다.");
                return;
            }

            if ( _session == null || !_session.HasSnapshot )
            {
                RECEIVED_LOGS.Enqueue("[Second Player] Session이 아직 준비되지 않았습니다.");
                return;
            }

            if ( !_session.CanConfirmCurrentTurn )
            {
                RECEIVED_LOGS.Enqueue(
                    $"[Second Player] 현재 Confirm할 수 없습니다. " +
                    $"LocalPlayer={_session.LocalPlayerIndex} | " +
                    $"CurrentTurn={_session.CurrentSnapshot.CurrentPlayerIndex}");
                return;
            }

            _isConfirming = true;

            try
            {
                ConfirmEdgeResponse response = await _session.ConfirmEdge_async(
                    _edgeId,
                    _destroyCancellationTokenSource.Token);

                RECEIVED_LOGS.Enqueue(
                    $"[Second Player] Confirm 응답 | EdgeId={_edgeId} | " +
                    $"Accepted={response.IsAccepted} | Error={response.Error} | " +
                    $"Revision={response.Snapshot?.Revision ?? -1}");
            }
            catch ( OperationCanceledException )
            {
                RECEIVED_LOGS.Enqueue("[Second Player] Confirm 요청이 취소됐습니다.");
            }
            catch ( Exception exception )
            {
                RECEIVED_LOGS.Enqueue($"[Second Player] Confirm 오류: {exception}");
            }
            finally
            {
                _isConfirming = false;
            }
        }

        private async Task RequestSync_async()
        {
            if ( _session == null || !_session.HasSnapshot )
            {
                RECEIVED_LOGS.Enqueue("[Second Player] Session이 아직 준비되지 않았습니다.");
                return;
            }

            try
            {
                MatchSnapshot snapshot = await _session.RequestSync_async(
                    _destroyCancellationTokenSource.Token);

                RECEIVED_LOGS.Enqueue(
                    $"[Second Player] RequestSync 완료 | Revision={snapshot.Revision}");
            }
            catch ( Exception exception )
            {
                RECEIVED_LOGS.Enqueue($"[Second Player] RequestSync 오류: {exception}");
            }
        }

        private void OnSnapshotChanged(MatchSnapshot snapshot)
        {
            _snapshotChangedCount++;

            string edgeOwner = snapshot.EdgeOwners != null &&
                               _edgeId >= 0 &&
                               _edgeId < snapshot.EdgeOwners.Length
                ? snapshot.EdgeOwners[_edgeId].ToString()
                : "INVALID_EDGE_ID";

            RECEIVED_LOGS.Enqueue(
                $"[Second Player] Snapshot #{_snapshotChangedCount} | " +
                $"Revision={snapshot.Revision} | Turn={snapshot.CurrentPlayerIndex} | " +
                $"Edge[{_edgeId}]={edgeOwner} | " +
                $"CanConfirm={_session.CanConfirmCurrentTurn}");
        }
    }
}