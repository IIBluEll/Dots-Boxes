# Google Play Games 서버 인증 구현 인계

## 구현된 흐름

Unity `GooglePlayLogin.Login_async` → Google 서버 인증 코드 → HTTPS `POST /auth/google-play`
→ Google 토큰 교환 → 서버 설정의 PGS ApplicationId로 `applications.verify`
→ 검증된 Player ID의 프로필 조회 → `IExternalAccountService` → 게임 JWT 응답.

게임 서버는 .NET 9, 계정 UserId는 Guid를 유지한다. DB 스키마 변경은 없다.
클라이언트가 보낸 Player ID, 닉네임, application ID는 인증 근거로 사용하지 않는다.
프로필 HTTP 조회 실패 시 검증된 ID와 null 닉네임을 계정 서비스에 전달한다.
Google 통신 실패/잘못된 응답은 로그인 실패이며, 클라이언트 재시도는 새 인증 코드를 요청한다.

## 이미 확인한 공개 설정

Unity `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset`에서 확인한 값을
서버 `appsettings.json`의 GooglePlay 설정에 반영했다.

| 키 | 값 |
|---|---|
| GooglePlay:ApplicationId | 300296646661 |
| GooglePlay:ClientId | 300296646661-rckpkqd6l9so7579e6knt2v2f0dgd3gs.apps.googleusercontent.com |
| GameJwt:Issuer | DotsAndBoxes.Server |
| GameJwt:Audience | DotsAndBoxes.Game |
| GameJwt:LifetimeMinutes | 60 |

기존 Lobby 서버 주소 `https://game.hmlee4135.cloud`를 로그인에도 사용한다.
Google Client Secret은 검색/출력/소스에 삽입하지 않았다.

## 서버 컴퓨터에서 주입할 비밀 설정

현재 개발 PC 프로세스에는 다음 환경변수가 없다. 서버 컴퓨터의 기존 비밀 파일 값은
이 PC에서 확인하지 않았으므로, 서버 담당자가 등록 여부를 확인한 후 누락된 값만 추가한다.

```text
GooglePlay__ClientSecret=<위 Web Client ID에 해당하는 기존 Client Secret>
GameJwt__SigningKey=<서버에서 새로 생성하거나 기존에 보관한 32바이트 이상 난수 키>
ConnectionStrings__GameDatabase=<기존 런타임 DB 연결 문자열>
```

- 기존 `runtime.env`의 DB 연결 정보는 보존한다. 새 DB나 역할을 만들지 않는다.
- JWT 키는 서버에서 암호학적 난수로 생성해 보관한다. 예: 32바이트 난수의 Base64 표현.
- JWT 키를 매 실행마다 새로 생성하지 않는다. 변경하면 기존 토큰이 모두 무효가 된다.
- 비밀값을 채팅, Git, Unity, 로그에 복사하지 않는다.
- 로컬 개발에만 .NET User Secrets 사용 가능. 프로젝트 UserSecretsId는 `DotsAndBoxes.Server.LocalSecrets`.
- Google 설정이나 JWT 키가 없으면 서버는 시작 시 실패한다. 비밀 설정 없이 새 이미지를 운영 반영하지 않는다.

## API 계약

```http
POST /auth/google-play
Content-Type: application/json

{"authCode":"새로 발급된 일회용 코드"}
```

성공 응답:

```json
{
  "userId": "GUID",
  "displayName": "플레이어 이름",
  "accessToken": "게임 서버 JWT",
  "expiresAtUtc": "UTC ISO-8601"
}
```

- 400: 비어 있거나 너무 긴 코드/잘못된 요청.
- 401: Google 인증 거부, 사용된 코드 등. 새 코드로 로그인한다.
- 429: 로그인 동시 처리 한도 초과. 잠시 후 새 코드로 재시도한다.
- 503: Google 통신/설정/저장소 문제. 응답에 코드나 내부 예외를 노출하지 않는다.
- 응답에 `Cache-Control: no-store` 적용.
- 로그인 API 동시 처리 16개, 요청 본문 제한 16KiB. Google HTTP 요청별 15초 제한.
- Google HTTP는 자동 재시도하지 않는다. 코드 교환을 맹목적으로 재실행하지 않는다.

## JWT와 SignalR

- HMAC SHA-256 서명, issuer/audience/만료/서명 검증, clock skew 0.
- `sub`는 내부 Guid UserId이며, 빈 Guid/잘못된 값은 인증 실패.
- `GameUserIdProvider`가 검증된 sub를 SignalR UserIdentifier로 변환한다.
- `/hubs/game`에 인증 필수. Unity 두 세션 모두 .NET SignalR `AccessTokenProvider`로 Bearer 토큰 전송.
- 기존 `?userId=...`는 인증으로 인정하지 않는다. Development 환경도 동일하다.
- 기존 DevelopmentUserSessionMiddleware 파일은 남아 있지만 파이프라인에 등록하지 않는다.
- 토큰 만료 시 기존 Hub 연결도 종료하도록 `CloseOnAuthenticationExpiration` 설정.
- 기존 경기 중 연결 종료 정책이 적용되므로 경기 중 토큰이 만료되면 기권 처리될 수 있다.
- Unity는 로비 진입/매칭 직전에 15분 이상 남은 토큰만 재사용하고, 아니면 Google 코드를 새로 요청한다.
  장시간 매칭 대기 중 만료되면 연결 종료 안내 후 다시 일반전을 눌러 로그인한다.
- 토큰은 메모리에만 보관한다. 서버 주소/UserId가 다르면 토큰을 전달하지 않는다.
- 로컬 플레이는 로그인 실패나 네트워크 단절 시에도 진입할 수 있다.

## Unity 연결

- 기존 GooglePlayLogin 컴포넌트와 텍스트 참조는 유지한다.
- GooglePlayLogin의 자체 Start 대신 MainLobbyUI가 기존 서버 URL로 로그인 시작.
- 동시 자동 로그인/버튼 요청은 같은 로그인 Task를 공유한다.
- 일반전 요청은 서버 로그인 완료를 기다린 뒤 OnlineSessionProvider를 서버 UserId로 초기화한다.
- 기존 PlayerPrefs 임의 UserId 생성 로직은 로비에서 제거했다. 저장돼 있던 값은 읽지 않는다.
- 실패 후 일반전을 다시 누르면 새 로그인 시도. Google 자체 인증이 실패했던 경우 수동 인증 시도.
- 씬 종료 시 진행 중 HTTP 요청 취소, 로그인 대기 Task 취소.
- 개발 Editor에서는 실제 PGS 로그인이 불가능하므로 로컬 모드만 사용한다. 우회 인증은 추가하지 않았다.
- InGame_Test와 기존 CLI의 `--user-id`만으로는 더 이상 온라인 인증할 수 없다.
- 상대 닉네임을 MatchAssignment에 추가하는 기능은 이번 7개 요청 범위 밖이므로 구현하지 않았다.

## 검증 및 남은 실제 연동 확인

인증 통합 테스트는 ASP.NET 테스트 호스트와 실제 JWT/SignalR 파이프라인을 사용한다.
Google HTTP와 계정 서비스는 대체 구현을 사용하므로 실제 Google/운영 DB 연동 검증과 구분한다.

```powershell
dotnet test Server/DotsAndBoxes.Server.Tests/DotsAndBoxes.Server.Tests.csproj
```

기존 AccountDatabaseTests 11개는 전용 테스트 DB 환경변수가 없으면 skip한다.
이번 로컬 실행 결과: 전체 98개 중 87개 통과, DB 11개 skip, 실패 0개.
Unity 전체 C# 프로젝트는 기본 생성 설정의 DOTween/.NET 참조 충돌로 실패했고,
프로젝트 파일 수정 없이 빌드 명령의 TargetFrameworkVersion=v4.7.2를 지정한 컴파일은 오류 0개였다.
추가로 GooglePlayLogin의 Android 전용 분기를 임시 컴파일 프로젝트로 확인했으며 오류 0개,
Inspector가 할당하는 TMP 필드 경고 2개였다. Android APK 빌드/실기기 연동 검증을 뜻하지 않는다.
서버 컴퓨터에서는 `DATABASE_SETUP.md`의 test.env와 테스트 DB 연결로 실행한다.
기존 `DotsAndBoxes.SignalRTestClient`는 개발용 query ID를 사용하는 이전 도구로, 새 서버에서 그대로 실행하면 인증 실패한다.
이번 JWT Hub 테스트는 Server.Tests/Authentication에 있다. 이전 도구를 통과시키기 위해 개발 인증을 다시 켜지 않는다.

실제 검증은 별도의 테스트 인스턴스와 HTTPS 접속 경로에서 진행한다. 기존 공개 컨테이너는 재시작하지 않는다.

1. 서버 비밀 설정을 주입하고 전용 테스트 인스턴스를 시작한다.
2. Google과 기존 테스트 DB에 접근 가능한지 확인한다. DB 포트를 외부 공개하지 않는다.
3. 테스트용 Unity 서버 URL을 해당 HTTPS 주소로 설정한다.
4. Android에서 `게임 서버 로그인 성공`, 일반전 Hub 접속 확인.
5. 동일 계정 앱 재실행 시 동일 UserId, 다른 계정에서는 다른 UserId 확인.
6. 두 기기 매칭 → 경기 → 결과 → 로비 복귀, 로컬 모드 진입 확인.
7. 만료 시 재로그인과 새 토큰 접속 확인. 비밀값이나 인증 코드 원문을 로그에 넣지 않는다.

현재 작업에서는 서비스 배포·재시작, 기존 DB 데이터 변경, 실제 Google 인증 요청을 실행하지 않았다.
DATABASE_SETUP.md의 '공개 서버 미반영' 기록과 사용자의 '배포 완료' 설명이 다르므로,
서버 담당자가 현재 이미지/설정 반영 상태를 확인해야 한다. 이 문서는 배포 실행 지시가 아니다.

참고:
- https://developer.android.com/games/pgs/android/server-access
- https://developer.android.com/games/services/web/api/rest/v1/applications/verify
- https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-9.0
