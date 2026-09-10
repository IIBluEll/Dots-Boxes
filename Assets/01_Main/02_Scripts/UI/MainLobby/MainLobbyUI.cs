using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsAndBoxes.UI
{
    public sealed class MainLobbyUI : MonoBehaviour
    {
        private const string LOADING_SCENE_NAME = "Loading";

        [Header("References")]
        [SerializeField] private MainLobby_view _view;

        [Space(5f), Header("Server")]
        [SerializeField] private string _serverUrl = "http://localhost:5049";

        private MainLobby_model _model;
        private MainLobby_presenter _presenter;
        private CancellationTokenSource _cancellationTokenSource;
        private GooglePlayLogin _googleLogin;

        private void Awake()
        {
            if(!ValidateReferences())
            {
                enabled = false;
                return;
            }

            _cancellationTokenSource = new CancellationTokenSource();
        }

        private void Start()
        {
            OnlineSessionProvider sessionProvider = OnlineSessionProvider.Instance;

            if(sessionProvider == null)
            {
                Debug.LogError("OnlineSessionProvider를 찾을 수 없습니다." , this);
                enabled = false;
                return;
            }

            try
            {
                _googleLogin = FindFirstObjectByType<GooglePlayLogin>();
                if (_googleLogin == null) _googleLogin = gameObject.AddComponent<GooglePlayLogin>();
                CreatePresenter(null);
                _ = LoginOnEntry_async();
            }
            catch ( Exception ex )
            {
                Debug.LogException(ex , this);
                enabled = false;
            }
        }

        private void OnDestroy()
        {
            _cancellationTokenSource?.Cancel();

            if(_presenter != null)
            {
                _presenter.MatchFound -= OnMatchFoundActioned;
                _presenter.LocalMatchRequested -= OnLocalMatchActioned;
                _presenter.MatchMakingFailed -= OnMatchMakingFailedActioned;
                _presenter.Dispose();
                _presenter = null;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _model = null;
        }

        private void CreatePresenter(IOnlineSession session)
        {
            _model = new MainLobby_model();
            _presenter = new MainLobby_presenter(_model , _view , session , _cancellationTokenSource.Token, PrepareOnlineSession_async);

            _presenter.MatchFound += OnMatchFoundActioned;
            _presenter.LocalMatchRequested += OnLocalMatchActioned;
            _presenter.MatchMakingFailed += OnMatchMakingFailedActioned;
            _presenter.Open();
        }

        private void OnMatchFoundActioned(MatchAssignment assignment)
        {
            OnlineSessionProvider sessionProvider = OnlineSessionProvider.Instance;

            if ( sessionProvider == null )
            {
                Debug.LogError("OnlineSessionProvider를 찾을 수 없습니다." , this);
                return;
            }

            try
            {
                sessionProvider.PrepareGameSession(assignment);
                SceneManager.LoadScene(LOADING_SCENE_NAME);
            }
            catch ( Exception exception )
            {
                Debug.LogException(exception , this);
            }
        }

        private void OnLocalMatchActioned()
        {
            OnlineSessionProvider sessionProvider = OnlineSessionProvider.Instance;

            if ( sessionProvider == null )
            {
                Debug.LogError("OnlineSessionProvider를 찾을 수 없습니다." , this);
                return;
            }

            try
            {
                sessionProvider.PrepareLocalGameSession();
                SceneManager.LoadScene(LOADING_SCENE_NAME);
            }
            catch ( Exception exception )
            {
                Debug.LogException(exception , this);
            }
        }

        private void OnMatchMakingFailedActioned(Exception exception)
        {
            GameAccountSession.Clear();
            Debug.LogException(exception , this);
            _view.ShowMatchMakingError("서버에 연결할 수 없습니다.\n잠시 후 다시 시도해 주세요.");
        }

        private async Task LoginOnEntry_async()
        {
            try
            {
                await _googleLogin.Login_async(_serverUrl);
            }
            catch (OperationCanceledException) { }
            catch (Exception)
            {
                // Login component shows a retryable status. Local play remains available.
            }
        }

        private async Task<IOnlineSession> PrepareOnlineSession_async()
        {
            Guid userId = await _googleLogin.Login_async(_serverUrl);
            if (this == null) throw new OperationCanceledException();
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();
            OnlineSessionProvider provider = OnlineSessionProvider.Instance;
            if (provider == null) throw new InvalidOperationException("OnlineSessionProvider를 찾을 수 없습니다.");
            provider.ResetSession();
            provider.Initialize(_serverUrl, userId);
            return provider.MatchmakingSession;
        }

        private bool ValidateReferences()
        {
            bool isValid = _view != null && Uri.TryCreate(_serverUrl, UriKind.Absolute, out _);

            if ( !isValid )
            {
                Debug.LogError("MainLobbyUI의 View 또는 Server URL이 올바르지 않습니다." , this);
            }

            return isValid;
        }
    }
}
