using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Shared;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DotsAndBoxes.UI
{
    public sealed class MainLobbyUI : MonoBehaviour
    {
        private const string USER_ID_KEY = "DotsAndBoxes.UserId";
        private const string LOADING_SCENE_NAME = "Loading";

        [Header("References")]
        [SerializeField] private MainLobby_view _view;

        [Space(5f), Header("Server")]
        [SerializeField] private string _serverUrl = "http://localhost:5049";

        private MainLobby_model _model;
        private MainLobby_presenter _presenter;
        private CancellationTokenSource _cancellationTokenSource;

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
                Guid userId = GetOrCreateUserId();

                sessionProvider.Initialize(_serverUrl , userId);
                CreatePresenter(sessionProvider.MatchmakingSession);
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
            _presenter = new MainLobby_presenter(_model , _view , session , _cancellationTokenSource.Token);

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
            Debug.LogException(exception , this);
            _view.ShowMatchMakingError("서버에 연결할 수 없습니다.\n잠시 후 다시 시도해 주세요.");
        }

        private Guid GetOrCreateUserId()
        {
            string savedUserId = PlayerPrefs.GetString(USER_ID_KEY, string.Empty);

            if ( Guid.TryParse(savedUserId , out Guid userId) && userId != Guid.Empty )
            {
                return userId;
            }

            Guid newUserId = Guid.NewGuid();

            PlayerPrefs.SetString(USER_ID_KEY , newUserId.ToString("D"));
            PlayerPrefs.Save();

            return newUserId;
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
