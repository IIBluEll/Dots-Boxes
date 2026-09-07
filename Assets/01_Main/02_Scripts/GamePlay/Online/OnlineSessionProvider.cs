using DotsAndBoxes.Shared;
using System;
using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class OnlineSessionProvider : MonoBehaviour
    {
        private IOnlineSession _matchmakingSession;
        private IGameSession _gameSession;

        public static OnlineSessionProvider Instance { get; private set; }

        public IOnlineSession MatchmakingSession => _matchmakingSession;
        public IGameSession GameSession => _gameSession;

        public bool IsInitialized => !string.IsNullOrWhiteSpace(ServerUrl) && UserId != Guid.Empty;
        public bool HasGameSession => _gameSession != null;

        public string ServerUrl { get; private set; }
        public Guid UserId { get; private set; }

        private void Awake()
        {
            if ( Instance != null && Instance != this )
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if ( Instance != this )
            {
                return;
            }

            ResetSession();
            Instance = null;
        }

        public void Initialize(string serverUrl , Guid userId)
        {
            if ( !Uri.TryCreate(serverUrl , UriKind.Absolute , out _) )
            {
                throw new ArgumentException("Server Url이 올바르지 않음" , nameof(serverUrl));
            }

            if ( userId == Guid.Empty )
            {
                throw new ArgumentException("UserID가 비어있음" , nameof(userId));
            }

            string normalizedServerUrl = serverUrl.TrimEnd('/');

            if ( IsInitialized )
            {
                bool isSameConfiguration = ServerUrl == normalizedServerUrl && UserId == userId;

                if ( !isSameConfiguration )
                {
                    throw new InvalidOperationException("다른 사용자로 이미 초기화됨");
                }

                if(_matchmakingSession == null && _gameSession == null)
                {
                    _matchmakingSession = new SignalROnlineSession(ServerUrl , UserId);
                }

                return;
            }

            ServerUrl = normalizedServerUrl;
            UserId = userId;
            _matchmakingSession = new SignalROnlineSession( ServerUrl, UserId );
        }

        public void PrepareGameSession(MatchAssignment assignment)
        {
            if ( !IsInitialized )
            {
                throw new InvalidOperationException("OnlineSessionProvider가 초기화 안됨");
            }

            if ( !IsValidAssignment(assignment) )
            {
                throw new ArgumentException("MatchAssignment가 올바르지 않습니다." , nameof(assignment));
            }

            if ( _gameSession != null )
            {
                if ( _gameSession.MatchId == assignment.MatchId )
                {
                    return;
                }

                throw new InvalidOperationException("다른 게임 Session이 이미 준비되어 있습니다.");
            }

            _gameSession = new SignalRGameSession(ServerUrl , assignment.MatchId , UserId);

            _matchmakingSession?.Dispose();
            _matchmakingSession = null;
        }

        public void PrepareLocalGameSession()
        {
            if ( _gameSession != null )
            {
                throw new InvalidOperationException("다른 게임 Session이 이미 준비되어 있습니다.");
            }

            _gameSession = new LocalGameSession();

            _matchmakingSession?.Dispose();
            _matchmakingSession = null;
        }

        public void ResetGameSession()
        {
            _gameSession?.Dispose();
            _gameSession= null;
        }

        public void ResetSession()
        {
            _matchmakingSession?.Dispose();
            _gameSession?.Dispose();

            _matchmakingSession = null;
            _gameSession = null;

            ServerUrl = null;
            UserId = Guid.Empty;
        }

        private static bool IsValidAssignment(MatchAssignment assignment)
        {
            return assignment != null &&
                   assignment.MatchId != Guid.Empty &&
                   assignment.LocalPlayerIndex != PLAYER_INDEX_ENUM.NONE &&
                   assignment.OpponentUserId != Guid.Empty;
        }
    }
}
