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
        private const string GOOGLE_LOGIN_PATH =
            "/auth/google-play";

        private const string TEST_LOGIN_PATH =
            "/auth/test-login";

        [Header("Texts")]
        [SerializeField] private TMP_Text _statusTxt;
        [SerializeField] private TMP_Text _nicknameTxt;

        private TaskCompletionSource<Guid> _loginCompletion;
        private UnityWebRequest _request;
        private string _serverUrl;
        private bool _hasAttemptedGoogleLogin;

        public bool IsAuthenticated { get; private set; }

        public string Nickname { get; private set; }
            = string.Empty;

        public Task<Guid> Login_async(string serverUrl)
        {
            if ( !Uri.TryCreate(
                    serverUrl ,
                    UriKind.Absolute ,
                    out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps )
            {
                return Task.FromException<Guid>(
                    new InvalidOperationException(
                        "로그인 서버는 HTTPS 주소여야 합니다."));
            }

            string normalizedServerUrl =
                serverUrl.TrimEnd('/');

            if ( _loginCompletion != null &&
                !_loginCompletion.Task.IsCompleted )
            {
                return _serverUrl == normalizedServerUrl
                    ? _loginCompletion.Task
                    : Task.FromException<Guid>(
                        new InvalidOperationException(
                            "다른 서버로 로그인 중입니다."));
            }

            _serverUrl = normalizedServerUrl;

            if ( GameAccountSession.TryGet(
                    _serverUrl ,
                    out Guid cachedUserId ,
                    out string cachedDisplayName) )
            {
                IsAuthenticated = true;
                Nickname = cachedDisplayName;

                ShowProfile();
                ShowStatus("게임 서버 로그인 성공");

                return Task.FromResult(cachedUserId);
            }

            _loginCompletion =
                new TaskCompletionSource<Guid>();

            IsAuthenticated = false;
            GameAccountSession.Clear();

#if UNITY_ANDROID && !UNITY_EDITOR
            BeginGoogleLogin();
#elif UNITY_EDITOR || UNITY_STANDALONE
            BeginTestLogin();
#else
            Fail("현재 플랫폼에서는 로그인을 지원하지 않습니다.");
#endif

            return _loginCompletion.Task;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void BeginGoogleLogin()
        {
            ShowStatus("Google 로그인 확인 중...");

            bool isAlreadyAuthenticated =
                PlayGamesPlatform.Instance.IsAuthenticated();

            if (_hasAttemptedGoogleLogin &&
                !isAlreadyAuthenticated)
            {
                PlayGamesPlatform.Instance
                    .ManuallyAuthenticate(
                        OnAuthenticationCompleted);
            }
            else
            {
                PlayGamesPlatform.Instance
                    .Authenticate(
                        OnAuthenticationCompleted);
            }

            _hasAttemptedGoogleLogin = true;
        }

        private void OnAuthenticationCompleted(
            SignInStatus status)
        {
            if (this == null)
            {
                return;
            }

            if (status != SignInStatus.Success)
            {
                Fail(
                    "Google 로그인 실패. 다시 시도해 주세요.");

                return;
            }

            Nickname =
                PlayGamesPlatform.Instance
                    .GetUserDisplayName();

            ShowProfile();
            ShowStatus("서버 인증 코드 요청 중...");

            PlayGamesPlatform.Instance
                .RequestServerSideAccess(
                    false,
                    OnServerAuthCodeReceived);
        }

        private void OnServerAuthCodeReceived(
            string authCode)
        {
            if (this == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(authCode))
            {
                Fail(
                    "서버 인증 코드를 받지 못했습니다. " +
                    "다시 시도해 주세요.");

                return;
            }

            string requestJson =
                JsonUtility.ToJson(
                    new GoogleLoginRequest
                    {
                        authCode = authCode
                    });

            StartCoroutine(
                LoginServer_cor(
                    GOOGLE_LOGIN_PATH,
                    requestJson,
                    "게임 서버 로그인 중..."));
        }
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
        private void BeginTestLogin()
        {
            StartCoroutine(
                LoginServer_cor(
                    TEST_LOGIN_PATH ,
                    "{}" ,
                    "PC 테스트 계정 로그인 중..."));
        }
#endif

        private IEnumerator LoginServer_cor(
            string requestPath ,
            string requestJson ,
            string statusMessage)
        {
            ShowStatus(statusMessage);

            byte[] requestBody =
                Encoding.UTF8.GetBytes(requestJson);

            using UnityWebRequest request =
                new UnityWebRequest(
                    _serverUrl + requestPath,
                    UnityWebRequest.kHttpVerbPOST);

            _request = request;

            request.uploadHandler =
                new UploadHandlerRaw(requestBody);

            request.downloadHandler =
                new DownloadHandlerBuffer();

            request.SetRequestHeader(
                "Content-Type" ,
                "application/json");

            request.timeout = 50;
            request.redirectLimit = 0;

            yield return request.SendWebRequest();

            _request = null;

            if ( request.result !=
                UnityWebRequest.Result.Success )
            {
                HandleLoginRequestFailure(
                    request ,
                    requestPath);

                yield break;
            }

            try
            {
                LoginResponse response =
                    JsonUtility.FromJson<LoginResponse>(
                        request.downloadHandler.text);

                bool hasValidUserId =
                    response != null &&
                    Guid.TryParse(
                        response.userId,
                        out Guid userId) &&
                    userId != Guid.Empty;

                bool hasValidExpiry =
                    response != null &&
                    DateTimeOffset.TryParse(
                        response.expiresAtUtc,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal,
                        out DateTimeOffset expiresAtUtc);

                if ( !hasValidUserId ||
                    !hasValidExpiry ||
                    string.IsNullOrWhiteSpace(
                        response.accessToken) )
                {
                    throw new InvalidOperationException();
                }

                GameAccountSession.Set(
                    _serverUrl ,
                    userId ,
                    response.displayName ,
                    response.accessToken ,
                    expiresAtUtc);

                IsAuthenticated = true;
                Nickname = response.displayName;

                ShowProfile();

#if UNITY_ANDROID && !UNITY_EDITOR
                ShowStatus("게임 서버 로그인 성공");
#else
                ShowStatus("PC 테스트 계정 로그인 성공");
#endif

                _loginCompletion.TrySetResult(userId);
            }
            catch ( Exception )
            {
                Fail("서버 로그인 응답이 올바르지 않습니다.");
            }
        }

        private void HandleLoginRequestFailure(
            UnityWebRequest request ,
            string requestPath)
        {
            if ( request.responseCode == 401 )
            {
                Fail(
                    "Google 인증이 거부되었습니다. " +
                    "다시 로그인해 주세요.");

                return;
            }

            if ( requestPath == TEST_LOGIN_PATH &&
                request.responseCode == 404 )
            {
                Fail(
                    "서버의 PC 테스트 로그인이 꺼져 있습니다.");

                return;
            }

            if ( request.responseCode == 429 )
            {
                Fail(
                    "로그인 요청이 너무 많습니다. " +
                    "잠시 후 다시 시도해 주세요.");

                return;
            }

            Fail(
                "게임 서버 로그인에 실패했습니다. " +
                "잠시 후 다시 시도해 주세요.");
        }

        private void Fail(string message)
        {
            IsAuthenticated = false;

            ShowStatus(message);

            _loginCompletion?
                .TrySetException(
                    new InvalidOperationException(message));
        }

        private void ShowProfile()
        {
            if ( _nicknameTxt != null )
            {
                _nicknameTxt.text = Nickname;
            }
        }

        private void ShowStatus(string message)
        {
            if ( _statusTxt != null )
            {
                _statusTxt.text = message;
            }

            Debug.Log(
                $"[GooglePlayLogin] {message}" ,
                this);
        }

        private void OnDestroy()
        {
            _request?.Abort();
            _loginCompletion?.TrySetCanceled();
        }

        [Serializable]
        private sealed class GoogleLoginRequest
        {
            public string authCode;
        }

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