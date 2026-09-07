using Cysharp.Threading.Tasks;
using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Shared;
using System;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsAndBoxes.UI
{
    [DisallowMultipleComponent]
    public sealed class LoadingSceneUI : MonoBehaviour
    {
        private const string IN_GAME_SCENE_NAME = "InGame";
        private const string LOBBY_SCENE_NAME = "Lobby";
        private const double RETURN_DELAY_SECONDS = 1.2d;

        [Header("References")]
        [SerializeField] private TMP_Text _statusTxt;

        [Header("Player Names")]
        [SerializeField] private TMP_Text _localPlayerNameTxt;
        [SerializeField] private TMP_Text _opponentNameTxt;

        private IGameSession _gameSession;
        private CancellationTokenSource _destroyCancellationTokenSource;

        private bool _isChangingScene;

        private void Awake()
        {
            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _destroyCancellationTokenSource =
                new CancellationTokenSource();

            // 닉네임의 태그 형태 문자열을 서식으로 해석하지 않습니다.
            if ( _localPlayerNameTxt != null )
            {
                _localPlayerNameTxt.richText = false;
            }

            if ( _opponentNameTxt != null )
            {
                _opponentNameTxt.richText = false;
            }

            ShowStatus("게임을 준비하고 있습니다...");
        }

        private void Start()
        {
            StartLoading_async().Forget();
        }

        private void OnDestroy()
        {
            _destroyCancellationTokenSource?.Cancel();

            if ( _gameSession != null )
            {
                _gameSession.SnapshotChanged -= OnSnapshotChangedActioned;
                _gameSession = null;
            }

            _destroyCancellationTokenSource?.Dispose();
            _destroyCancellationTokenSource = null;
        }

        private async UniTask StartLoading_async()
        {
            OnlineSessionProvider sessionProvider =
                OnlineSessionProvider.Instance;

            if ( sessionProvider == null || !sessionProvider.HasGameSession )
            {
                Debug.LogError(
                    "준비된 GameSession을 찾을 수 없습니다." ,
                    this);

                BeginReturnToLobby("게임 정보를 찾을 수 없습니다.");
                return;
            }

            ShowPlayerNames(
                sessionProvider.LocalDisplayName ,
                sessionProvider.OpponentDisplayName);

            _gameSession = sessionProvider.GameSession;
            _gameSession.SnapshotChanged += OnSnapshotChangedActioned;

            CancellationToken cancellationToken =
                _destroyCancellationTokenSource.Token;

            try
            {
                ShowStatus("서버에 연결하고 있습니다...");
                await _gameSession.Start_async(cancellationToken);

                if ( _isChangingScene )
                {
                    return;
                }

                ShowStatus("게임 화면을 준비하고 있습니다...");
                await PrepareLocalGame_async(cancellationToken);

                if ( _isChangingScene )
                {
                    return;
                }

                ShowStatus("상대 플레이어를 기다리고 있습니다...");
                await _gameSession.Ready_async(cancellationToken);
            }
            catch ( OperationCanceledException )
                when ( cancellationToken.IsCancellationRequested )
            {
                // 씬 종료에 따른 정상 취소입니다.
            }
            catch ( Exception exception )
            {
                if ( _isChangingScene )
                {
                    return;
                }

                Debug.LogException(exception , this);
                BeginReturnToLobby("게임 연결에 실패했습니다.");
            }
        }

        private async UniTask PrepareLocalGame_async(
            CancellationToken cancellationToken)
        {
            await UniTask.Yield(
                PlayerLoopTiming.Update ,
                cancellationToken);
        }

        private void ShowPlayerNames(
            string localDisplayName ,
            string opponentDisplayName)
        {
            if ( _localPlayerNameTxt != null )
            {
                _localPlayerNameTxt.text = localDisplayName;
            }

            if ( _opponentNameTxt != null )
            {
                _opponentNameTxt.text = opponentDisplayName;
            }
        }

        private void OnSnapshotChangedActioned(MatchSnapshot snapshot)
        {
            if ( snapshot == null || _isChangingScene )
            {
                return;
            }

            switch ( snapshot.MatchState )
            {
                case SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS:
                    ShowStatus("상대 플레이어의 접속을 기다리고 있습니다...");
                    break;

                case SERVER_MATCH_STATE_ENUM.WAITING_FOR_READY:
                    ShowStatus("상대 플레이어의 준비를 기다리고 있습니다...");
                    break;

                case SERVER_MATCH_STATE_ENUM.STARTING:
                    ShowStatus("곧 게임이 시작됩니다...");
                    break;

                case SERVER_MATCH_STATE_ENUM.ACTIVE:
                    OpenInGameScene();
                    break;

                case SERVER_MATCH_STATE_ENUM.CANCELLED:
                    BeginReturnToLobby("매칭이 취소되었습니다.");
                    break;

                case SERVER_MATCH_STATE_ENUM.FINISHED:
                    BeginReturnToLobby("이미 종료된 게임입니다.");
                    break;
            }
        }

        private void OpenInGameScene()
        {
            if ( _isChangingScene )
            {
                return;
            }

            _isChangingScene = true;
            SceneManager.LoadScene(IN_GAME_SCENE_NAME);
        }

        private void BeginReturnToLobby(string message)
        {
            if ( _isChangingScene )
            {
                return;
            }

            _isChangingScene = true;
            ShowStatus(message);
            ReturnToLobby_async().Forget();
        }

        private async UniTask ReturnToLobby_async()
        {
            CancellationToken cancellationToken =
                _destroyCancellationTokenSource.Token;

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(RETURN_DELAY_SECONDS) ,
                    cancellationToken: cancellationToken);
            }
            catch ( OperationCanceledException )
            {
                return;
            }

            OnlineSessionProvider sessionProvider =
                OnlineSessionProvider.Instance;

            if ( sessionProvider != null &&
                ReferenceEquals(sessionProvider.GameSession , _gameSession) )
            {
                sessionProvider.ResetGameSession();
            }

            SceneManager.LoadScene(LOBBY_SCENE_NAME);
        }

        private void ShowStatus(string status)
        {
            _statusTxt.text = status;
        }

        private bool ValidateReferences()
        {
            if ( _statusTxt == null )
            {
                Debug.LogError(
                    "LoadingSceneUI의 Status Text 참조가 설정되지 않았습니다." ,
                    this);

                return false;
            }

            // 이름 텍스트 누락으로 게임 시작 자체가 막히지는 않게 합니다.
            if ( _localPlayerNameTxt == null || _opponentNameTxt == null )
            {
                Debug.LogWarning(
                    "LoadingSceneUI의 플레이어 이름 Text 참조를 연결해 주세요." ,
                    this);
            }

            return true;
        }
    }
}