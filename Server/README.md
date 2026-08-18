# Dots & Boxes Server

ASP.NET Core와 SignalR로 구현한 단일 프로세스 서버입니다. 게임 판정은 `DotsAndBoxes.Shared`의 순수 C# 규칙을 사용하며, Match 상태는 현재 메모리에만 보관합니다.

## 현재 구현 범위

- 개발용 User Session과 수동 Match 생성
- `JoinMatch` 이후 양쪽 `ReadyMatch`를 기다리는 시작 준비 절차
- 서버 기준 10초 Join 제한, 15초 Ready 제한과 3초 공통 시작 Countdown
- 서버 권위형 Confirm, 전체 Snapshot, Revision과 RequestId 멱등성
- RequestSync와 SignalR Broadcast
- 게임 시작 전 나가기·Disconnect 시 Match 취소, 게임 시작 후에는 즉시 기권
- 서버 종료 중 Disconnect 기권 방지
- 20초 Turn Timer, 자동 Edge와 플레이어별 누적 3회 AFK 패배
- FINISHED 또는 CANCELLED이며 연결이 없는 MatchRoom 제거
- 인메모리 FIFO 일반전 Matchmaking과 선공 무작위 결정
- 구조화 로그와 Live/Ready/Core/Development Status Endpoint

실제 인증, PostgreSQL, MatchHistory, Docker 외부 배포, 재접속, Rank와 분산 서버는 아직 범위에 포함하지 않습니다.

## 실행

```powershell
dotnet run --project Server/DotsAndBoxes.Server/DotsAndBoxes.Server.csproj --urls http://localhost:5049
```

Development 환경에서 사용할 수 있는 Endpoint는 다음과 같습니다.

```text
POST /development/matches
GET  /development/status
GET  /health/live
GET  /health/ready
GET  /health/core
Hub  /hubs/game
```

`/development/matches`는 Unity 수동 연결 검증용입니다. 실제 일반전 흐름은 Hub의 `EnterMatchmaking` → `MatchFound` → `JoinMatch` → `ReadyMatch` → `STARTING` → `ACTIVE` 순서입니다. `JoinMatch`는 SignalR Group 입장 완료, `ReadyMatch`는 게임 Scene·보드·UI·이벤트 구독 준비 완료를 뜻합니다. 양쪽 Ready가 확인되면 서버가 하나의 `MatchStartUtc`를 생성하며, 3초 뒤 `ACTIVE`로 전환합니다.

현재 Unity Client에는 `ReadyMatch` 호출과 시작 대기 UI가 아직 연결되지 않았습니다. 따라서 서버 통합 클라이언트로는 전체 흐름을 검증할 수 있지만, Unity 온라인 Match는 후속 Client 작업 전까지 `ACTIVE`로 전환되지 않습니다. 대기 Queue 취소는 `CancelMatchmaking`, Match 입장 후 나가기는 `LeaveMatch`를 호출합니다.

## 검증

```powershell
dotnet test Server/DotsAndBoxes.Server.Tests/DotsAndBoxes.Server.Tests.csproj
dotnet run --project Server/DotsAndBoxes.SignalRTestClient/DotsAndBoxes.SignalRTestClient.csproj
```

SignalR 통합 클라이언트를 실행하기 전에 서버가 `http://localhost:5049`에서 실행 중이어야 합니다.

## Match 시간 설정

`DotsAndBoxes.Server/appsettings.json`의 `MatchTiming`에서 조정합니다.

```json
{
  "JoinTimeoutSeconds": 10,
  "ReadyTimeoutSeconds": 15,
  "StartCountdownSeconds": 3,
  "TurnDurationSeconds": 20,
  "MaxTimeoutsPerPlayer": 3,
  "SweepIntervalMilliseconds": 500
}
```

Client 시간은 판정에 사용하지 않습니다. 서버가 `JoinDeadlineUtc`, `ReadyDeadlineUtc`, `MatchStartUtc`, `TurnDeadlineUtc`를 만들고 `MatchTimerService`가 상태 전이와 만료를 확인합니다. Client의 시작 Countdown은 `MatchStartUtc - 현재 UTC`를 표시할 뿐이며, 초마다 서버 메시지를 보내지 않습니다.

## Unity Client 후속 작업

서버가 구현된 뒤 Client에서 다음 순서로 연결합니다.

1. `MatchFound` 후 게임 Scene으로 이동하고 `JoinMatch`를 호출합니다.
2. 초기 Snapshot 적용과 `MatchStateChanged` 구독을 먼저 완료합니다.
3. 보드, UI와 입력 처리기가 모두 준비된 뒤 `ReadyMatch`를 한 번 호출합니다.
4. `WAITING_FOR_PLAYERS`와 `WAITING_FOR_READY`에서는 대기 화면을 표시하고 게임 입력을 막습니다.
5. `STARTING`에서는 `MatchStartUtc` 기준으로 3초 Countdown을 표시하고 입력을 계속 막습니다.
6. `ACTIVE` Snapshot을 받은 뒤에만 Edge 입력과 Turn Timer UI를 활성화합니다.
7. `CANCELLED`를 받으면 승패를 표시하지 않고 Lobby로 돌아가 다시 매칭하도록 안내합니다.
