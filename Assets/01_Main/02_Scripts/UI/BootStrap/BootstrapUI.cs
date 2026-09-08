using Cysharp.Threading.Tasks;
using DotsAndBoxes.Gameplay;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsAndBoxes.UI
{
    [DisallowMultipleComponent]
    public sealed class BootstrapUI : MonoBehaviour
    {
        private const string LOBBY_SCENE_NAME = "Lobby";
        private const float MINIMUM_DISPLAY_SECONDS = 1f;
        private const float INITIAL_PROGRESS = 5f;
        private const float SCENE_LOADING_START_PROGRESS = 10f;
        private const float SCENE_LOADING_END_PROGRESS = 60f;
        private const float LOGIN_WAIT_PROGRESS = 70f;
        private const float LOGIN_COMPLETE_PROGRESS = 90f;
        private const float COMPLETE_PROGRESS = 100f;
        private const float LOADING_STEP_DELAY_SECONDS = 0.5f;
        private const float FINAL_SCENE_DELAY_SECONDS = 1.5f;

        [Header("References")]
        [SerializeField] private Bootstrap_view _view;
        [SerializeField] private GooglePlayLogin _googlePlayLogin;

        [Space(5f), Header("Server")]
        [SerializeField] private string _serverUrl = "https://game.hmlee4135.cloud";

        private BootStrap_model _model;
        private Bootstrap_presenter _presenter;
        private CancellationTokenSource _destroyCancellationTokenSource;
        private AsyncOperation _lobbyLoadOperation;

        private bool _isRunning;
        private bool _isDestroying;

        private void Awake()
        {
            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            if ( !_view.Initialize() )
            {
                enabled = false;
                return;
            }

            _destroyCancellationTokenSource = new CancellationTokenSource();

            CreatePresenter();
        }

        private void Start()
        {
            _presenter.Open();
            StartBootstrap_async().Forget();
        }

        private void OnDestroy()
        {
            _isDestroying = true;
            _destroyCancellationTokenSource?.Cancel();

            if ( _presenter != null )
            {
                _presenter.RetryRequested -= OnRetryRequestedActioned;
                _presenter.Dispose();
                _presenter = null;
            }

            _destroyCancellationTokenSource?.Dispose();
            _destroyCancellationTokenSource = null;
            _model = null;
        }

        private void CreatePresenter()
        {
            _model = new BootStrap_model();
            _presenter = new Bootstrap_presenter(_model , _view);
            _presenter.RetryRequested += OnRetryRequestedActioned;
        }

        private void OnRetryRequestedActioned()
        {
            StartBootstrap_async().Forget();
        }

        private async UniTask StartBootstrap_async()
        {
            if ( _isRunning || _isDestroying || _destroyCancellationTokenSource == null )
            {
                return;
            }

            _isRunning = true;

            CancellationToken cancellationToken = _destroyCancellationTokenSource.Token;

            try
            {
                _presenter.BeginLoading();
                _presenter.UpdateLoading(INITIAL_PROGRESS , "게임을 초기화하고 있습니다...");

                await Wait_async(LOADING_STEP_DELAY_SECONDS , cancellationToken);

                Task<Guid> loginTask = _googlePlayLogin.Login_async(_serverUrl);

                PrepareLobbyScene();
                await WaitForLobbySceneLoad_async(cancellationToken);

                _presenter.UpdateLoading(SCENE_LOADING_END_PROGRESS , "로비 준비가 완료되었습니다.");

                await Wait_async(LOADING_STEP_DELAY_SECONDS , cancellationToken);

                _presenter.UpdateLoading(LOGIN_WAIT_PROGRESS , "Google Play Games에 로그인하고 있습니다...");

                await Wait_async(LOADING_STEP_DELAY_SECONDS , cancellationToken);

                Guid userId = await loginTask.AsUniTask().AttachExternalCancellation(cancellationToken);

                if ( userId == Guid.Empty )
                {
                    throw new InvalidOperationException("로그인한 사용자 정보가 올바르지 않습니다.");
                }

                string nickname = string.IsNullOrWhiteSpace(_googlePlayLogin.Nickname) ? "플레이어" : _googlePlayLogin.Nickname;

                _presenter.UpdateLoading(LOGIN_COMPLETE_PROGRESS , $"{nickname}님, 환영합니다.");

                await Wait_async(LOADING_STEP_DELAY_SECONDS , cancellationToken);

                _presenter.Complete("로딩이 완료되었습니다.");

                await Wait_async(FINAL_SCENE_DELAY_SECONDS , cancellationToken);

                ActivateLobbyScene();
            }
            catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested )
            {
                // Bootstrap 씬 종료에 따른 정상 취소입니다.
            }
            catch ( Exception exception )
            {
                GameAccountSession.Clear();

                if ( !_isDestroying && _presenter != null )
                {
                    Debug.LogException(exception , this);
                    _presenter.Fail(GetUserErrorMessage(exception));
                }
            }
            finally
            {
                _isRunning = false;
            }
        }

        private void PrepareLobbyScene()
        {
            if ( _lobbyLoadOperation != null )
            {
                return;
            }

            _presenter.UpdateLoading(SCENE_LOADING_START_PROGRESS , "로비를 준비하고 있습니다...");

            _lobbyLoadOperation = SceneManager.LoadSceneAsync(LOBBY_SCENE_NAME);

            if ( _lobbyLoadOperation == null )
            {
                throw new InvalidOperationException("Lobby 씬을 불러오지 못했습니다.");
            }

            _lobbyLoadOperation.allowSceneActivation = false;
        }

        private static UniTask Wait_async(float delaySeconds , CancellationToken cancellationToken)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(delaySeconds) , ignoreTimeScale: true , cancellationToken: cancellationToken);
        }

        private async UniTask WaitForLobbySceneLoad_async(CancellationToken cancellationToken)
        {
            while ( _lobbyLoadOperation.progress < 0.9f )
            {
                cancellationToken.ThrowIfCancellationRequested();

                float normalizedProgress = Mathf.Clamp01(_lobbyLoadOperation.progress / 0.9f);
                float displayedProgress = Mathf.Lerp(SCENE_LOADING_START_PROGRESS , SCENE_LOADING_END_PROGRESS , normalizedProgress);

                _presenter.UpdateLoading(displayedProgress , "로비를 준비하고 있습니다...");

                await UniTask.Yield(PlayerLoopTiming.Update , cancellationToken);
            }

            _presenter.UpdateLoading(SCENE_LOADING_END_PROGRESS , "로비 준비가 완료되었습니다.");
        }

        private static async UniTask WaitForMinimumDisplayTime_async(float startedTime , CancellationToken cancellationToken)
        {
            float elapsedTime = Time.realtimeSinceStartup - startedTime;
            float remainingTime = MINIMUM_DISPLAY_SECONDS - elapsedTime;

            if ( remainingTime <= 0f )
            {
                return;
            }

            await UniTask.Delay(TimeSpan.FromSeconds(remainingTime) , ignoreTimeScale: true , cancellationToken: cancellationToken);
        }

        private void ActivateLobbyScene()
        {
            if ( _lobbyLoadOperation == null )
            {
                throw new InvalidOperationException("Lobby 씬 로딩 정보가 없습니다.");
            }

            _lobbyLoadOperation.allowSceneActivation = true;
        }

        private static string GetUserErrorMessage(Exception exception)
        {
            if ( exception is InvalidOperationException && !string.IsNullOrWhiteSpace(exception.Message) )
            {
                return exception.Message;
            }

            return "자동 로그인에 실패했습니다.\n네트워크 상태를 확인한 후 다시 시도해 주세요.";
        }

        private bool ValidateReferences()
        {
            bool isServerUrlValid = Uri.TryCreate(_serverUrl , UriKind.Absolute , out Uri uri) && uri.Scheme == Uri.UriSchemeHttps;
            bool isValid = _view != null && _googlePlayLogin != null && isServerUrlValid;

            if ( !isValid )
            {
                Debug.LogError("BootstrapUI의 View, GooglePlayLogin 또는 HTTPS Server URL 설정이 올바르지 않습니다." , this);
            }

            return isValid;
        }
    }
}