# Dots & Boxes Server

ASP.NET Core와 SignalR로 구현한 단일 프로세스 서버입니다. 게임 판정은 `DotsAndBoxes.Shared`의 순수 C# 규칙을 사용하며, Match 상태는 현재 메모리에만 보관합니다.

## 현재 구현 범위

- 개발용 User Session과 수동 Match 생성
- 서버 권위형 Confirm, 전체 Snapshot, Revision과 RequestId 멱등성
- RequestSync와 SignalR Broadcast
- 명시적 나가기 및 Disconnect 즉시 기권
- 서버 종료 중 Disconnect 기권 방지
- 20초 Turn Timer, 자동 Edge와 플레이어별 누적 3회 AFK 패배
- FINISHED이며 연결이 없는 MatchRoom 제거
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

`/development/matches`는 Unity 수동 연결 검증용입니다. 실제 일반전 흐름은 Hub의 `EnterMatchmaking` → `MatchFound` → `JoinMatch` 순서입니다. 대기 취소는 `CancelMatchmaking`, 진행 중 명시적 나가기는 `LeaveMatch`를 호출합니다.

## 검증

```powershell
dotnet test Server/DotsAndBoxes.Server.Tests/DotsAndBoxes.Server.Tests.csproj
dotnet run --project Server/DotsAndBoxes.SignalRTestClient/DotsAndBoxes.SignalRTestClient.csproj
```

SignalR 통합 클라이언트를 실행하기 전에 서버가 `http://localhost:5049`에서 실행 중이어야 합니다.

## Turn Timer 설정

`DotsAndBoxes.Server/appsettings.json`의 `MatchTiming`에서 조정합니다.

```json
{
  "TurnDurationSeconds": 20,
  "MaxTimeoutsPerPlayer": 3,
  "SweepIntervalMilliseconds": 500
}
```

Client 시간은 판정에 사용하지 않습니다. 서버가 `TurnDeadlineUtc`를 만들고 `MatchTimerService`가 만료된 Room을 확인합니다.
