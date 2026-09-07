using System;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using DotsAndBoxes.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_ANDROID && !UNITY_EDITOR
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif

namespace DotsAndBoxes.UI
{
    [DisallowMultipleComponent]
    public sealed class GooglePlayLogin : MonoBehaviour
    {
        [Header("Texts")]
        [SerializeField] private TMP_Text _statusTxt;
        [SerializeField] private TMP_Text _nicknameTxt;

        private TaskCompletionSource<Guid> _loginCompletion;
        private UnityWebRequest _request;
        private string _serverUrl;
        private bool _hasAttemptedGoogleLogin;
        public bool IsAuthenticated { get; private set; }
        public string Nickname { get; private set; } = string.Empty;

        // MainLobbyUI supplies its existing server URL. Concurrent callers share one login.
        public Task<Guid> Login_async(string serverUrl)
        {
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
                return Task.FromException<Guid>(new InvalidOperationException("로그인 서버는 HTTPS 주소여야 합니다."));

            if (_loginCompletion != null && !_loginCompletion.Task.IsCompleted)
                return _serverUrl == serverUrl.TrimEnd('/') ? _loginCompletion.Task :
                    Task.FromException<Guid>(new InvalidOperationException("다른 서버로 로그인 중입니다."));

            _serverUrl = serverUrl.TrimEnd('/');
            if (GameAccountSession.TryGet(_serverUrl, out Guid cachedId, out string cachedName))
            {
                IsAuthenticated = true;
                Nickname = cachedName;
                ShowProfile();
                ShowStatus("게임 서버 로그인 성공");
                return Task.FromResult(cachedId);
            }

            _loginCompletion = new TaskCompletionSource<Guid>();
            IsAuthenticated = false;
            GameAccountSession.Clear();
            ShowStatus("Google 로그인 확인 중...");
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_hasAttemptedGoogleLogin && !PlayGamesPlatform.Instance.IsAuthenticated())
                PlayGamesPlatform.Instance.ManuallyAuthenticate(OnAuthenticationCompleted);
            else
                PlayGamesPlatform.Instance.Authenticate(OnAuthenticationCompleted);
            _hasAttemptedGoogleLogin = true;
#else
            Fail("Google 로그인은 Android 기기에서 확인해 주세요.");
#endif
            return _loginCompletion.Task;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnAuthenticationCompleted(SignInStatus status)
        {
            if (this == null) return;
            if (status != SignInStatus.Success)
            {
                Fail("Google 로그인 실패. 다시 시도해 주세요.");
                return;
            }
            Nickname = PlayGamesPlatform.Instance.GetUserDisplayName();
            ShowProfile();
            ShowStatus("서버 인증 코드 요청 중...");
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, OnServerAuthCodeReceived);
        }

        private void OnServerAuthCodeReceived(string authCode)
        {
            if (this == null) return;
            if (string.IsNullOrWhiteSpace(authCode))
            {
                Fail("서버 인증 코드를 받지 못했습니다. 다시 시도해 주세요.");
                return;
            }
            StartCoroutine(LoginServer_cor(authCode));
        }
#endif

        private IEnumerator LoginServer_cor(string authCode)
        {
            ShowStatus("게임 서버 로그인 중...");
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new LoginRequest { authCode = authCode }));
            using (UnityWebRequest request = new UnityWebRequest(_serverUrl + "/auth/google-play", "POST"))
            {
                _request = request;
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 50;
                request.redirectLimit = 0;
                yield return request.SendWebRequest();
                _request = null;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Fail(request.responseCode == 401
                        ? "Google 인증이 거부되었습니다. 다시 로그인해 주세요."
                        : "게임 서버 로그인에 실패했습니다. 잠시 후 다시 시도해 주세요.");
                    yield break;
                }

                try
                {
                    LoginResponse response = JsonUtility.FromJson<LoginResponse>(request.downloadHandler.text);
                    if (response == null || !Guid.TryParse(response.userId, out Guid userId) ||
                        !DateTimeOffset.TryParse(response.expiresAtUtc, CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal, out DateTimeOffset expires))
                        throw new InvalidOperationException();
                    GameAccountSession.Set(_serverUrl, userId, response.displayName, response.accessToken, expires);
                    IsAuthenticated = true;
                    Nickname = response.displayName;
                    ShowProfile();
                    ShowStatus("게임 서버 로그인 성공");
                    _loginCompletion.TrySetResult(userId);
                }
                catch (Exception)
                {
                    Fail("서버 로그인 응답이 올바르지 않습니다.");
                }
            }
        }

        private void Fail(string message)
        {
            IsAuthenticated = false;
            ShowStatus(message);
            _loginCompletion?.TrySetException(new InvalidOperationException(message));
        }

        private void ShowProfile()
        {
            if (_nicknameTxt != null) _nicknameTxt.text = Nickname;
        }

        private void ShowStatus(string message)
        {
            if (_statusTxt != null) _statusTxt.text = message;
            Debug.Log($"[GooglePlayLogin] {message}", this);
        }

        private void OnDestroy()
        {
            _request?.Abort();
            _loginCompletion?.TrySetCanceled();
        }

        [Serializable]
        private sealed class LoginRequest { public string authCode; }

        [Serializable]
        private sealed class LoginResponse
        {
            public string userId = string.Empty;
            public string displayName = string.Empty;
            public string accessToken = string.Empty;
            public string expiresAtUtc = string.Empty;
        }
    }
}
