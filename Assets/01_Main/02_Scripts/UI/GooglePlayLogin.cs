using TMPro;
using UnityEngine;

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

        public bool IsAuthenticated { get; private set; }
        public string Nickname { get; private set; } = string.Empty;

        private void Start()
        {
            IsAuthenticated = false;
            Nickname = string.Empty;

            if ( _nicknameTxt != null )
            {
                _nicknameTxt.text = string.Empty;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            ShowStatus("Google 로그인 확인 중...");

            // 플러그인이 시도한 자동 로그인의 결과를 받습니다.
            PlayGamesPlatform.Instance.Authenticate(
                OnAuthenticationCompleted);
#else
            ShowStatus("Android 기기에서 로그인해 주세요.");
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void OnAuthenticationCompleted(SignInStatus status)
        {
            // 결과를 기다리는 동안 씬이 바뀌었을 수 있습니다.
            if ( this == null )
            {
                return;
            }

            if ( status != SignInStatus.Success )
            {
                IsAuthenticated = false;
                Nickname = string.Empty;

                ShowStatus($"Google 로그인 실패: {status}");
                return;
            }

            IsAuthenticated = true;
            Nickname = PlayGamesPlatform.Instance.GetUserDisplayName();

            if ( _nicknameTxt != null )
            {
                _nicknameTxt.text = Nickname;
            }

            ShowStatus("Google 로그인 성공");

            // 로그인에 성공한 다음 서버 인증 코드를 요청합니다.
            RequestServerAuthCode();
        }

        private void RequestServerAuthCode()
        {
            ShowStatus("서버 인증 코드 요청 중...");
        
            PlayGamesPlatform.Instance.RequestServerSideAccess(
                false ,
                OnServerAuthCodeReceived);
        }
        
        private void OnServerAuthCodeReceived(string authCode)
        {
            if ( this == null )
            {
                return;
            }
        
            if ( string.IsNullOrWhiteSpace(authCode) )
            {
                ShowStatus("서버 인증 코드 발급 실패");
                return;
            }
        
            // 인증 코드 원문은 화면이나 로그에 출력하지 않습니다.
            ShowStatus("서버 인증 코드 발급 성공");
        
            // 다음 단계에서 authCode를 우리 서버로 전송합니다.
        }
#endif

        private void ShowStatus(string message)
        {
            if ( _statusTxt != null )
            {
                _statusTxt.text = message;
            }

            Debug.Log($"[GooglePlayLogin] {message}" , this);
        }
    }
}