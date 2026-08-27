using Cysharp.Threading.Tasks;
using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Shared;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsAndBoxes.UI
{
    public class LoadingSceneUI : MonoBehaviour
    {
        private const string IN_GAME_SCENE_NAME = "InGame_Test";

        private IGameSession _gameSession;
        private CancellationTokenSource _destoryCancellTokenSource;

        private bool _isChangingScene;

        private void Awake()
        {
            _destoryCancellTokenSource = new CancellationTokenSource();
        }

        private void Start()
        {
            StartLoading_async().Forget();
        }

        private void OnDestroy()
        {
            _destoryCancellTokenSource?.Cancel();

            if ( _gameSession != null )
            {
                _gameSession.SnapshotChanged -= OnSnapshotChangedActioned;
                _gameSession = null;
            }

            _destoryCancellTokenSource?.Dispose();
            _destoryCancellTokenSource = null;
        }

        private async UniTask StartLoading_async()
        {
            OnlineSessionProvider sessionProvider = OnlineSessionProvider.Instance;

            if ( sessionProvider == null || !sessionProvider.HasGameSession )
            {
                Debug.LogError("준비된 GameSession을 찾을 수 없습니다." , this);
                return;
            }

            _gameSession = sessionProvider.GameSession;
            _gameSession.SnapshotChanged += OnSnapshotChangedActioned;

            try
            {
                await _gameSession.Start_async(_destoryCancellTokenSource.Token);
                await PrepareLocalGame_async(_destoryCancellTokenSource.Token);
                await _gameSession.Ready_async(_destoryCancellTokenSource.Token);

                Debug.Log("로컬 로딩 완료 및 Ready 전송 완료" , this);
            }
            catch ( OperationCanceledException ) when ( _destoryCancellTokenSource.IsCancellationRequested )
            {
                // Scene이 종료되면서 취소된 경우
            }
            catch ( Exception exception )
            {
                Debug.LogException(exception , this);
            }
        }

        private async UniTask PrepareLocalGame_async(CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.Update , cancellationToken);
        }

        private void OpenInGameScene()
        {
            if( _isChangingScene )
            {
                return;
            }

            _isChangingScene = true;
            SceneManager.LoadScene(IN_GAME_SCENE_NAME);
        }

        private void OnSnapshotChangedActioned(MatchSnapshot snapshot)
        {
            switch ( snapshot.MatchState )
            {
                case SERVER_MATCH_STATE_ENUM.STARTING:
                    Debug.Log("모든 플레이어 준비 완료: 게임 시작 카운트다운" , this);
                    break;

                case SERVER_MATCH_STATE_ENUM.ACTIVE:
                    OpenInGameScene();
                    break;

                case SERVER_MATCH_STATE_ENUM.CANCELLED:
                    Debug.LogError("상대 플레이어가 제한시간 내에 준비하지 않았습니다." , this);
                    break;
            }
        }
    }
}


