# Dots & Boxes 서버 아키텍처

작성 기준일: 2026-08-14

## 1. 문서 목적

이 문서는 Dots & Boxes 온라인 Vertical Slice를 구현하기 전에 서버의 책임, 프로젝트 구조, 통신 규격, Match 상태, 동시성 원칙과 개발 순서를 확정하기 위한 기준입니다.

기획 전체를 다시 정의하지 않고 `Dots & Boxes 모바일 온라인 게임 기획서.md`에서 정한 정책을 실제 코드 구조로 옮기는 데 집중합니다.

### 이번 문서에서 확정하는 범위

- Unity Client와 서버의 권한 경계
- Shared Core 공유 방식
- ASP.NET Core 서버의 논리 구조
- SignalR Hub와 MatchRoom의 책임
- Preview, Confirm, Snapshot, Revision 처리
- RequestId 멱등성
- Match 상태 전이
- Match 단위 동시성 처리
- 재접속과 Turn Timer의 기본 방향
- DB 저장 경계와 초기 배포 구조
- 단계별 구현 순서와 완료 조건

### 이번 문서에서 확정하지 않는 범위

- 최종 UI 디자인과 애니메이션
- 시즌, 티어, Leaderboard 세부 정책
- 다중 Game Server와 무중단 Match 복구
- Redis 도입
- 친구 초대, 관전, Replay
- 결제와 Cosmetic
- 정확한 서버 Runtime 버전
- 실제 운영 수치가 필요한 Rate Limit 및 Timeout 최종값

Runtime 버전과 외부 패키지 버전은 서버 프로젝트를 생성하는 시점에 공식 지원 상태와 로컬 개발 환경을 확인한 뒤 고정합니다.

---

## 2. 핵심 결정 요약

| 항목 | 결정 |
|---|---|
| 서버 방식 | ASP.NET Core 단일 Game Server |
| 실시간 통신 | SignalR |
| 게임 권한 | 서버 권위형 |
| 규칙 실행 | 서버가 `DotsRule.TryConfirmEdge()` 실행 |
| 진행 중 Match | 서버 메모리의 `MatchRoom`에서 관리 |
| 동기화 | Confirm 성공 시 전체 `MatchSnapshot` 전송 |
| 버전 관리 | 권위 Snapshot 변경마다 단조 증가하는 `Revision` 사용 |
| 중복 요청 | Client `RequestId` 기반 멱등 처리 |
| Preview | 서버 경유, 비권위 임시 상태, Revision 미증가 |
| 동시성 | MatchRoom별 `SemaphoreSlim`으로 명령 직렬화 |
| 결과 저장 | Match 종료 시 PostgreSQL Transaction |
| 초기 배포 | Docker Compose: Game Server + PostgreSQL + Caddy |
| Redis | 초기 범위에서 제외 |
| 장애 시 진행 Match | `ABORTED`, 결과와 Rating 미반영 |

---

## 3. 서버 권위 경계

온라인 Match에서 최종 상태를 결정하는 주체는 서버뿐입니다.

### Client가 할 수 있는 일

```text
Edge를 로컬 Preview로 표시
Preview 변경 요청 전송
Edge Confirm 의도 전송
서버 Snapshot을 화면에 반영
Revision 이상 감지 시 동기화 요청
TurnDeadlineUtc를 기준으로 남은 시간 표시
```

### Client가 결정하면 안 되는 일

```text
Edge 소유자 확정
Box 완성 및 소유자 결정
점수 변경
추가 턴 결정
게임 종료와 승패 결정
Turn Timer 만료 판정
Rating 변경
```

### Server가 담당하는 일

```text
Match 참가자와 현재 상태 검증
Preview 전달 정책과 Rate Limit 적용
ExpectedRevision 검증
RequestId 중복 검사
DotsRule을 통한 Edge 확정
Revision 증가
전체 Snapshot 생성과 전송
TurnDeadlineUtc 관리
Disconnect와 재접속 처리
게임 종료 및 결과 저장
```

로컬 2인 모드에서는 Unity의 `GameBoard_Model`이 `DotsRule`을 직접 호출할 수 있습니다. 온라인 모드에서는 Client가 `DotsRule` 결과를 권위 상태로 사용하지 않고 서버 Snapshot만 확정 상태로 반영합니다.

---

## 4. 전체 시스템 구조

```text
Unity Client A                         Unity Client B
      │                                      │
      └────────────── HTTPS / SignalR ───────┘
                             │
                             ▼
                         Caddy
                             │
                             ▼
                   ASP.NET Core Game Server
                  ┌──────────────────────────┐
                  │ Authentication Boundary  │
                  │ SignalR GameHub          │
                  │ Matchmaking Queue        │
                  │ MatchRoomManager         │
                  │ MatchRoom                │
                  │ DotsAndBoxes.Shared      │
                  │ Result Persistence       │
                  └──────────────────────────┘
                             │
                             ▼
                         PostgreSQL
```

초기에는 Game Server 인스턴스를 하나만 운영합니다. MatchRoom이 서버 메모리에 있으므로 Load Balancer, Redis Backplane, 분산 Lock은 사용하지 않습니다.

---

## 5. Repository와 프로젝트 구조

Unity 프로젝트와 서버를 같은 Repository에서 관리하는 모노레포를 사용합니다.

```text
Dots-Boxes
├─ Assets
│  └─ 01_Main/02_Scripts
│     ├─ Shared
│     │  ├─ Board
│     │  ├─ Rules
│     │  └─ Contracts             추후 추가
│     └─ GamePlay
│
├─ Server
│  ├─ DotsAndBoxes.Server
│  │  ├─ Hubs
│  │  ├─ Matchmaking
│  │  ├─ Matches
│  │  ├─ Persistence
│  │  ├─ Authentication
│  │  └─ Observability
│  │
│  ├─ DotsAndBoxes.Shared.Server
│  │  └─ DotsAndBoxes.Shared.Server.csproj
│  │
│  └─ DotsAndBoxes.Server.Tests
│
├─ Deploy
│  ├─ docker-compose.yml
│  ├─ Caddyfile
│  └─ environment example
│
├─ Dots-Boxes.sln
└─ SERVER_ARCHITECTURE.md
```

초기 Vertical Slice에서는 별도의 Application, Domain, Infrastructure 프로젝트를 과도하게 만들지 않습니다. `DotsAndBoxes.Server`, Shared Core 빌드 프로젝트, Server Test 프로젝트 세 개로 시작합니다.

기능과 의존성이 실제로 커진 뒤에만 프로젝트 분리를 검토합니다.

---

## 6. Shared Core 공유 방식

### 원칙

Unity와 Server가 같은 규칙 코드를 각각 복사해서 보관하지 않습니다.

규칙의 단일 원본은 현재 위치를 유지합니다.

```text
Assets/01_Main/02_Scripts/Shared
```

Unity는 기존 `DotsAndBoxes.Shared.asmdef`로 컴파일합니다.

Server 쪽에는 기본 Compile 항목을 사용하지 않는 전용 `.csproj`를 만들고 위 Shared 소스를 Link하여 컴파일합니다.

개념적인 구조는 다음과 같습니다.

```text
동일한 Shared C# 파일
├─ Unity → DotsAndBoxes.Shared.asmdef
└─ Server → DotsAndBoxes.Shared.Server.csproj
```

### 이 방식을 선택하는 이유

- 현재 Unity 파일과 `.meta`를 이동하지 않아도 됩니다.
- Core 코드가 실제로 하나만 존재합니다.
- Unity와 Server가 같은 `DotsRule`을 컴파일합니다.
- 초기 모노레포에서 별도 NuGet 배포 과정이 필요하지 않습니다.

### 제한

- Shared 코드는 계속 `UnityEngine`을 참조하면 안 됩니다.
- Unity 전용 Attribute나 직렬화 타입을 Shared에 넣으면 안 됩니다.
- Server 전용 DB, SignalR, ASP.NET 타입을 Shared에 넣으면 안 됩니다.
- 두 빌드 환경에서 사용할 수 있는 순수 C# API만 사용합니다.

Repository를 분리할 필요가 생기면 Shared를 별도 Package 또는 NuGet으로 추출하는 방식을 다시 검토합니다. 초기 단계에서는 적용하지 않습니다.

---

## 7. 서버 내부 구성요소

### 7.1 GameHub

`GameHub`는 SignalR 전송 계층입니다.

담당 역할:

- 인증된 연결에서 UserId 확인
- Request DTO 수신
- Application Service 또는 MatchRoomManager 호출
- SignalR Group 참가 및 이탈
- Client에게 Response 또는 Event 전송

담당하지 않는 역할:

- Edge 유효성 규칙 직접 판정
- Box 완성 계산
- Revision 직접 증가
- MatchRoom 내부 상태 직접 수정
- DB Transaction 직접 실행

Hub는 가능한 얇게 유지합니다.

### 7.2 MatchmakingQueue

초기 Queue는 서버 메모리에서 관리합니다.

담당 역할:

- Queue 진입과 취소
- 동일 사용자의 중복 Queue 진입 방지
- 자기 자신과 매칭 방지
- Match 후보 두 명을 원자적으로 Queue에서 제거
- MatchRoom 생성 요청

초기 일반전은 단순 입장 순서로 매칭합니다. Rating 범위 검색은 Account/Rank 단계에서 추가합니다.

### 7.3 MatchRoomManager

진행 중 MatchRoom을 보관하고 검색합니다.

```text
Dictionary<Guid, MatchRoom>
```

담당 역할:

- MatchRoom 생성 및 조회
- User가 속한 활성 Match 검색
- 종료된 Room 제거 정책
- 서버 종료 시 활성 Match 목록 확인
- 진행 중 Match 수 관측값 제공

Dictionary의 동시 접근에는 `ConcurrentDictionary` 또는 Manager 전용 Lock을 사용합니다. Room 내부 상태 변경 Lock과 Manager Collection Lock은 분리합니다.

### 7.4 MatchRoom

MatchRoom은 한 게임의 권위 상태와 명령 처리 단위입니다.

```text
MatchRoom
├─ MatchId
├─ PlayerOne
├─ PlayerTwo
├─ DotsBoard
├─ Revision
├─ MatchState
├─ TurnDeadlineUtc
├─ Preview 상태
├─ Connection 상태
├─ 처리된 RequestId 결과 Cache
└─ 명령 직렬화 Lock
```

MatchRoom만 다음 상태를 변경할 수 있습니다.

- DotsBoard
- Revision
- TurnDeadlineUtc
- Preview
- Match 상태
- 플레이어 Connection 상태

### 7.5 ResultPersistenceService

게임 종료 시 Match 결과와 Rating을 하나의 DB Transaction으로 저장합니다.

초기 일반전에서 Rating을 사용하지 않는 경우에도 `MatchHistory.MatchId`의 고유 제약으로 같은 결과가 두 번 저장되지 않게 합니다.

---

## 8. 식별자와 기본 타입

| 항목 | 권장 타입 | 이유 |
|---|---|---|
| `MatchId` | `Guid` | 서버 간 또는 재시작 후에도 충돌 가능성이 낮음 |
| `RequestId` | `Guid` | Client가 Confirm마다 생성하는 멱등성 키 |
| `UserId` | `Guid` | 내부 계정 식별자 |
| `Revision` | `long` | Match 동안 단조 증가하는 버전 |
| `PreviewSequence` | `long` | 연결 중 Preview 순서를 비교 |
| Deadline | `DateTimeOffset` UTC | 서버와 Client 시간대 분리 |
| `EdgeId` | `int` | 현재 Core ID와 동일 |
| `PlayerIndex` | `PLAYER_INDEX_ENUM` | Shared 규칙과 동일 |

Client가 전송한 UserId를 신뢰하지 않습니다. UserId는 인증 Claim 또는 서버 Connection Session에서 가져옵니다.

시간은 서버에서 UTC로 생성합니다. Client가 보낸 현재 시간은 Turn 판정에 사용하지 않습니다.

---

## 9. 공유 Contract

Contract는 Unity와 Server가 함께 컴파일할 수 있는 순수 C# 데이터 타입으로 작성합니다.

ASP.NET, SignalR, Entity Framework Core 타입이나 Unity 타입을 포함하지 않습니다.

### 9.1 PreviewEdgeRequest

```text
MatchId              Guid
EdgeId               int
PreviewSequence      long
```

UserId는 Request Body에 포함하지 않습니다.

### 9.2 ConfirmEdgeRequest

```text
MatchId              Guid
EdgeId               int
ExpectedRevision     long
RequestId            Guid
```

### 9.3 ConfirmEdgeResponse

```text
RequestId            Guid
IsAccepted           bool
Error                MATCH_COMMAND_ERROR_ENUM
ShouldRequestSync    bool
Snapshot             MatchSnapshot 또는 null
```

보드 크기가 작으므로 성공 시 전체 Snapshot을 반환합니다. Revision 불일치와 상태 복구가 필요한 오류에도 최신 Snapshot을 함께 반환합니다. 인증 실패나 존재하지 않는 Match처럼 Server가 Snapshot을 제공할 수 없는 오류에서는 `null`입니다.

### 9.4 MatchSnapshot

```text
SchemaVersion        int
MatchId              Guid
Revision             long
MatchState           SERVER_MATCH_STATE_ENUM

PlayerOneUserId      Guid
PlayerTwoUserId      Guid
CurrentPlayerIndex   PLAYER_INDEX_ENUM

EdgeOwners[40]       PLAYER_INDEX_ENUM[]
BoxOwners[16]        PLAYER_INDEX_ENUM[]

PlayerOneScore       int
PlayerTwoScore       int

TurnDeadlineUtc      DateTimeOffset 또는 null
GameResult           GAME_RESULT_ENUM
FinishReason         MATCH_FINISH_REASON_ENUM
```

`PlayerOneScore`, `PlayerTwoScore`는 Edge와 Box 상태에서 계산할 수 있지만 Client 표시와 검증 편의를 위해 Snapshot에 포함합니다. Server는 항상 DotsBoard에서 계산한 점수를 넣습니다.

보드가 정상 완료되면 `GameResult`도 DotsBoard에서 가져옵니다. 기권, Disconnect Timeout처럼 보드가 모두 채워지기 전에 끝나는 경우에는 MatchRoom이 종료 원인과 상대 Player를 기준으로 최종 `GameResult`를 보관합니다.

`TurnDeadlineUtc`는 `ACTIVE` 상태에서만 값이 있으며 종료 상태에서는 `null`입니다.

`SchemaVersion`은 저장 상태 Revision과 다른 Contract 형식 버전입니다. Contract 필드의 의미가 바뀔 때만 증가합니다.

### 9.5 Match Event

Server에서 Client로 보내는 주요 Event는 다음과 같습니다.

```text
MatchFound
MatchStarted
OpponentPreviewChanged
MatchStateChanged
OpponentDisconnected
OpponentReconnected
GameFinished
```

상태가 변경되는 Event에는 가능한 한 전체 Snapshot을 포함합니다. `TurnChanged`처럼 Snapshot과 같은 사실을 중복 전달하는 Event는 초기 구현에서 만들지 않고, 필요성이 확인될 때 추가합니다.

---

## 10. Enum 경계

### SERVER_MATCH_STATE_ENUM

```text
CREATED
WAITING_FOR_PLAYERS
ACTIVE
FINISHING
FINISHED
ABORTED
```

### MATCH_COMMAND_ERROR_ENUM

```text
NONE
UNAUTHORIZED
MATCH_NOT_FOUND
NOT_A_MATCH_PLAYER
MATCH_NOT_ACTIVE
NOT_YOUR_TURN
INVALID_EDGE
EDGE_ALREADY_CONFIRMED
REVISION_MISMATCH
DUPLICATE_REQUEST_CONFLICT
RATE_LIMITED
RECONNECT_REQUIRED
INTERNAL_ERROR
```

Core의 `MOVE_ERROR_ENUM`과 Server의 `MATCH_COMMAND_ERROR_ENUM`은 책임이 다릅니다.

```text
MOVE_ERROR_ENUM
└─ 순수 보드 규칙 실패

MATCH_COMMAND_ERROR_ENUM
└─ 인증, Match, Revision, Rate Limit을 포함한 서버 명령 실패
```

Server는 Core 실패 값을 Server 명령 오류로 변환합니다.

### MATCH_FINISH_REASON_ENUM

```text
NONE
BOARD_COMPLETED
FORFEIT
TIMEOUT_FORFEIT
DISCONNECT_TIMEOUT
SERVER_ABORTED
```

---

## 11. Match 상태 전이

```text
CREATED
   │
   ▼
WAITING_FOR_PLAYERS
   │ 두 플레이어 준비
   ▼
ACTIVE
   │ 정상 게임 종료 또는 기권
   ▼
FINISHING
   │ 결과 DB Transaction 성공
   ▼
FINISHED

CREATED / WAITING_FOR_PLAYERS / ACTIVE / FINISHING
   └─ 정상 완료 불가 → ABORTED
```

### 상태별 허용 명령

| 상태 | 허용 명령 |
|---|---|
| `CREATED` | 내부 초기화만 허용 |
| `WAITING_FOR_PLAYERS` | 참가, 준비, 취소 |
| `ACTIVE` | Preview, Confirm, Sync, Forfeit, Reconnect |
| `FINISHING` | Sync만 허용, 추가 Move 거부 |
| `FINISHED` | 최종 결과 조회 |
| `ABORTED` | 무효 사유 조회 |

Disconnect가 발생해도 Grace Period 동안 Match는 `ACTIVE`를 유지합니다. Turn Timer도 계속 진행합니다.

---

## 12. MatchRoom 동시성 원칙

하나의 MatchRoom에서 상태를 변경하는 명령은 동시에 실행하지 않습니다.

초기 구현은 Room마다 다음 Lock을 하나씩 사용합니다.

```text
SemaphoreSlim(1, 1)
```

### Lock 안에서 처리하는 작업

- Match 상태 검증
- 참가자 검증
- RequestId 검사
- Revision 검사
- `DotsRule.TryConfirmEdge()` 실행
- Revision 증가
- Preview 제거
- TurnDeadlineUtc 갱신
- Snapshot 생성
- Match 종료 상태 전이
- 처리 결과 Cache 기록

### Lock 밖에서 처리 가능한 작업

- 이미 생성한 Snapshot 직렬화
- SignalR Broadcast
- 구조화 로그 전송
- 상태를 변경하지 않는 Metric 기록

Broadcast는 Room 상태 Lock을 오래 잡지 않도록 Snapshot 생성 이후 실행합니다.

Match 종료 DB Transaction은 먼저 Room을 `FINISHING`으로 전환하여 추가 Move를 막은 다음 실행합니다. Transaction 완료 후 같은 직렬화 경로로 `FINISHED` 또는 `ABORTED`를 확정합니다.

처리량 측정에서 Room Lock이 실제 병목으로 확인되기 전에는 Channel 기반 Actor 구조로 변경하지 않습니다.

---

## 13. Confirm 처리 순서

```text
Client ConfirmEdge 전송
        │
        ▼
GameHub가 인증 UserId 확인
        │
        ▼
MatchRoomManager에서 Room 조회
        │
        ▼
MatchRoom Lock 획득
        │
        ├─ ACTIVE 상태인가?
        ├─ 해당 Match 참가자인가?
        ├─ 이미 처리한 RequestId인가?
        ├─ ExpectedRevision이 일치하는가?
        ├─ 현재 해당 플레이어 Turn인가?
        └─ Edge 요청이 유효한가?
        │
        ▼
DotsRule.TryConfirmEdge()
        │
        ├─ 실패 → 상태 변경 없이 오류 Response
        │
        └─ 성공
            ├─ Revision + 1
            ├─ Preview 제거
            ├─ 새 TurnDeadlineUtc 설정
            ├─ MatchSnapshot 생성
            ├─ RequestId 결과 Cache 저장
            └─ 게임 종료 시 FINISHING 전환
        │
        ▼
Room Lock 해제
        │
        ▼
양쪽 Client에 Snapshot Broadcast
```

Server 판정이 오기 전까지 Client는 Edge를 확정색으로 표시하지 않습니다. 선택한 Edge는 Local Preview 상태로만 유지합니다.

---

## 14. Revision 규칙

Revision 초기값은 `0`입니다.

Revision은 Move 횟수가 아니라 Client에 전송하는 권위 `MatchSnapshot`의 버전입니다. Snapshot에 포함된 권위 값이 변경되면 1 증가합니다.

다음 경우에 1 증가합니다.

- 유효한 Confirm으로 DotsBoard 상태가 변경됨
- Turn Timer 자동 선택으로 유효한 Edge가 확정됨
- Forfeit 또는 Disconnect Timeout으로 최종 결과가 결정됨
- `FINISHING`에서 `FINISHED` 또는 `ABORTED`로 상태가 변경됨
- 그 밖에 Snapshot에 포함된 권위 상태가 변경됨

다음 경우에는 증가하지 않습니다.

- Preview 변경
- 잘못된 Confirm 요청
- 동일 RequestId 재전송
- Sync 요청
- Ping/Pong
- Connection 상태 알림
- 남은 시간 표시 갱신

게임을 끝내는 마지막 Confirm은 Edge, Box, 점수와 `FINISHING` 상태를 하나의 원자적 변경으로 처리하여 Revision을 한 번 증가시킵니다. 결과 저장이 끝나 `FINISHED`가 되면 다시 한 번 증가합니다.

기존 기획서의 “Confirm으로 상태가 변경될 때만 증가”라는 표현은 구현 시 같은 Revision의 서로 다른 Snapshot을 만들 수 있습니다. 이 문서에서는 Revision을 Snapshot 버전으로 명확히 하며, 이후 기획서의 Revision 항목도 같은 기준으로 맞춥니다.

### Client Snapshot 적용 규칙

```text
Snapshot.Revision < LocalRevision
→ 오래된 Snapshot이므로 무시

Snapshot.Revision == LocalRevision
→ 동일 Snapshot의 재전송으로 취급하고 무시

Snapshot.Revision == LocalRevision + 1
→ 정상 적용

Snapshot.Revision > LocalRevision + 1
→ 전체 Snapshot이면 적용 가능하지만 누락 로그 기록
→ Event만 받은 경우 RequestSync
```

전체 Snapshot을 적용할 때 Client의 Local Preview와 진행 중인 임시 연출 상태를 서버 상태에 맞게 정리합니다.

---

## 15. RequestId 멱등성

Client는 Confirm 버튼을 누를 때마다 새로운 `Guid RequestId`를 생성합니다.

Server는 MatchRoom 안에 다음 Cache를 둡니다.

```text
RequestId → ConfirmEdgeResponse
```

### 동일 RequestId 재수신

- 요청 내용이 처음 요청과 같으면 저장된 Response를 반환합니다.
- EdgeId 또는 MatchId 등 내용이 다르면 `DUPLICATE_REQUEST_CONFLICT`를 반환하고 상태를 변경하지 않습니다.
- 같은 RequestId로 `DotsRule`을 두 번 실행하지 않습니다.

정상 Match의 Confirm 성공 횟수는 최대 40회지만 악의적 요청으로 Cache가 무한히 커지지 않도록 요청 개수와 전송 빈도에 제한을 둡니다.

초기 구현에서는 Match가 끝날 때까지 처리 결과를 유지하고 MatchRoom 제거 시 함께 삭제합니다.

---

## 16. Preview 처리

Preview는 권위 상태가 아닌 임시 상태입니다.

### 검증 조건

- Match가 `ACTIVE`인가?
- 요청 User가 Match 참가자인가?
- 현재 요청 User의 Turn인가?
- EdgeId가 유효하고 아직 확정되지 않았는가?
- `PreviewSequence`가 이전 값보다 큰가?
- Rate Limit을 초과하지 않았는가?

### 전송 정책

- Edge가 실제로 바뀐 경우에만 상대방에게 전달합니다.
- 자기 자신에게 다시 Broadcast하지 않습니다.
- Player당 초기 최대 초당 5회로 제한합니다.
- Confirm, Turn 변경, Disconnect, Match 종료 시 제거합니다.
- Preview 변경은 Revision을 증가시키지 않습니다.
- Preview 유실은 Sync 대상이 아닙니다.

Rate Limit의 최종 수치는 모바일 사용성 및 트래픽 측정 후 조정합니다.

---

## 17. Snapshot 생성과 검증

Snapshot은 MatchRoom의 현재 상태에서 새 객체로 생성합니다. 내부 `DotsBoard`, 배열, Player Session 객체를 그대로 외부에 노출하지 않습니다.

### 생성 시 확인할 불변 조건

```text
EdgeOwners.Length = 40
BoxOwners.Length = 16
PlayerOneScore + PlayerTwoScore = OwnedBoxCount
Revision은 감소하지 않음
CurrentPlayerIndex는 실제 플레이어 값
FINISHED이면 GameResult는 IN_PROGRESS가 아님
ACTIVE이면 FinishReason은 NONE
```

Snapshot 배열은 전송 후 MatchRoom 상태 변경의 영향을 받지 않도록 복사합니다.

---

## 18. Unity Client 적용 방향

현재 로컬 GameBoard는 다음 흐름을 사용합니다.

```text
GameBoard_Model
→ DotsRule 직접 호출
→ MoveResult로 View 갱신
```

온라인에서는 다음 흐름으로 바뀝니다.

```text
GameBoard_Model Local Preview 변경
→ GameBoard_Presenter Confirm 요청
→ Network Session이 Server에 전송
→ MatchSnapshot 수신
→ GameBoard_Model.ApplySnapshot()
→ GameBoard_Presenter가 View 전체 동기화
```

`GameBoard_View`는 로컬과 온라인에서 재사용합니다.

온라인 적용 시 필요한 Client 계층은 다음 구현 단계에서 별도로 설계합니다.

```text
IGameSession
├─ LocalGameSession
└─ OnlineGameSession
```

이 추상화 이름과 정확한 책임은 Client 리팩터링 전에 다시 검토합니다. 현재는 방향만 확정하며 기존 로컬 코드를 먼저 변경하지 않습니다.

---

## 19. Turn Timer

Timer의 권위 시간은 서버의 `TurnDeadlineUtc`입니다.

Client는 자신의 로컬 시계를 기준으로 남은 시간을 표시하지만 시간 만료를 확정하지 않습니다.

### 초기 정책

```text
Turn 제한 시간: 20초 초기값
시간 초과: 남은 유효 Edge 중 서버가 균등 무작위 자동 선택
자동 선택 성공: 일반 Move와 동일하게 Revision 증가
연속 2회 또는 누적 3회 초과: 기권 패배
Reconnect Grace Period 중 Timer 계속 진행
```

자동 선택한 Edge와 원인 `TIMEOUT_AUTO_MOVE`를 서버 기록에 남깁니다.

Timer는 MatchRoom마다 별도 Thread를 만들지 않습니다. 초기 구현에서는 중앙 BackgroundService가 만료 예정 Room을 확인하거나 만료 예약 작업을 관리합니다. 정확한 방식은 MatchRoom 기본 Confirm 흐름이 완성된 뒤 결정합니다.

---

## 20. Disconnect와 재접속

SignalR `ConnectionId`를 User 식별자로 사용하지 않습니다.

재접속 검증 값:

```text
AuthenticatedUserId
MatchId
ReconnectToken
```

### 정책

- Match 시작 시 Player별 짧은 수명의 ReconnectToken을 발급합니다.
- Server에는 원문 대신 Token Hash 저장을 우선 검토합니다.
- Token은 해당 User와 Match에만 사용할 수 있습니다.
- Grace Period 초기값은 15초입니다.
- 재접속 성공 시 이전 Connection을 무효화합니다.
- 재접속한 Client에 전체 Snapshot을 보냅니다.
- Grace Period를 넘으면 `DISCONNECT_TIMEOUT` 기권 패배로 처리합니다.
- Disconnect 중에도 Turn Timer는 멈추지 않습니다.

Token 형식, Hash 방식과 만료 서명 방식은 인증 구현 단계에서 확정합니다.

---

## 21. 게임 종료와 DB Transaction

정상적인 보드 완료 또는 기권이 발생하면 MatchRoom을 먼저 `FINISHING`으로 전환합니다.

```text
ACTIVE
→ 종료 조건 확인
→ FINISHING
→ 승패 계산
→ DB Transaction 시작
   ├─ MatchHistory 저장
   └─ Rating 변경
→ Transaction 성공
→ FINISHED
→ GameFinished 전송
```

### 무결성 원칙

- `MatchHistory.MatchId`는 고유 키입니다.
- MatchHistory와 Rating 변경은 하나의 Transaction으로 처리합니다.
- 같은 Match 결과를 두 번 적용하지 않습니다.
- Transaction 실패 시 Rating이 일부만 반영되면 안 됩니다.
- 최종 저장 실패 시 성공 결과를 먼저 확정하지 않습니다.

일반전에서 Rating을 변경하지 않는 경우에도 MatchHistory 저장의 멱등성은 유지합니다.

DB 없이 진행하는 초기 MatchRoom 단계에서는 결과를 메모리에만 남기되, 해당 단계가 운영 완료 상태가 아님을 명확히 구분합니다.

---

## 22. 장애 정책

초기 서버는 진행 중 Match를 메모리에만 보관합니다.

서버가 비정상 종료되면 진행 중 Match를 복구할 수 없습니다.

```text
Match 상태 → ABORTED
Rating 변경 없음
승패 기록 없음 또는 무효 Match 기록
운영 로그에 마지막 MatchId와 Revision 기록
Client에 서버 장애 무효 사유 표시
```

정상 배포 시에는 다음 순서를 사용합니다.

```text
신규 Queue 진입 중단
→ 진행 중 Match 종료 대기
→ Game Server 종료
→ 새 Container 실행
→ Health/Readiness 확인
→ Queue 재개
```

---

## 23. 인증과 보안 경계

초기 MatchRoom 기능 검증에서는 개발용 User Session을 사용할 수 있지만 외부 공개 환경에서는 인증되지 않은 Hub 명령을 허용하지 않습니다.

### 기본 원칙

- UserId는 인증 Claim에서 가져옵니다.
- Client가 보낸 PlayerIndex를 신뢰하지 않습니다.
- MatchRoom의 참가자 정보로 PlayerIndex를 결정합니다.
- Hub 요청 크기와 호출 빈도를 제한합니다.
- 모든 Match 명령은 해당 User의 Match 참가 여부를 검사합니다.
- Password 원문을 저장하거나 로그에 기록하지 않습니다.
- Access Token, ReconnectToken, DB 비밀번호를 로그에 기록하지 않습니다.
- Caddy부터 Client까지 TLS를 사용합니다.

Google 계정, 자체 계정과 Steam 연동의 세부 구현은 Account 단계에서 별도 문서로 분리합니다.

---

## 24. 로그와 관측

구조화 로그에 가능한 경우 다음 값을 포함합니다.

```text
TimestampUtc
LogLevel
EventName
UserId
MatchId
RequestId
Revision
ConnectionId
DurationMs
```

주요 EventName:

```text
MATCH_CREATED
PLAYER_JOINED
MATCH_STARTED
PREVIEW_CHANGED
EDGE_CONFIRMED
COMMAND_REJECTED
REVISION_MISMATCH
PLAYER_DISCONNECTED
PLAYER_RECONNECTED
MATCH_FINISHING
MATCH_FINISHED
MATCH_ABORTED
RATING_UPDATED
```

초기 관측값:

- SignalR 연결 수
- Queue 대기 인원
- 진행 중 Match 수
- Confirm 요청 성공/실패 수
- Revision 불일치 수
- 평균 및 p95 Confirm 처리시간
- 재접속 성공률
- ABORTED Match 수
- DB Transaction 실패 수

---

## 25. Docker 배포 구조

```text
docker-compose
├─ game-server
│  └─ ASP.NET Core
├─ postgres
└─ caddy
```

### Container 책임

| Container | 책임 |
|---|---|
| `game-server` | REST, SignalR, MatchRoom, Queue, 결과 처리 |
| `postgres` | 계정, MatchHistory, Rating 저장 |
| `caddy` | TLS 종료와 Reverse Proxy |

환경 변수 파일에는 실제 비밀번호를 Commit하지 않습니다. Repository에는 필요한 Key 이름만 있는 예제 파일을 둡니다.

Redis는 다음 조건이 실제로 생길 때 검토합니다.

- Game Server 인스턴스를 두 개 이상 운영
- SignalR Backplane 필요
- MatchRoom 외부 저장 필요
- 분산 Queue 필요

---

## 26. 구현 단계

### Stage 1 — Shared Core Server Build

- Server 폴더와 Solution 구조 생성
- Shared 전용 `.csproj` 생성
- 현재 Shared Core를 Link Compile
- Server 환경에서 Core 빌드 확인

완료 조건:

```text
Unity와 Server가 같은 Shared 소스를 컴파일한다.
Shared 소스 복사본이 없다.
Server에서 DotsBoard와 DotsRule을 실행할 수 있다.
```

### Stage 2 — MatchRoom 단독 구현

- 두 Player Slot
- Match 상태
- Revision
- ConfirmEdge 명령
- RequestId Cache
- Snapshot 생성
- Match별 직렬화 Lock

이 단계에서는 SignalR과 DB를 연결하지 않습니다.

완료 조건:

```text
서버 코드만으로 한 Match를 끝까지 진행할 수 있다.
잘못된 Turn, Edge, Revision을 거부한다.
동일 RequestId가 Move를 두 번 실행하지 않는다.
매 성공 Move마다 Revision이 정확히 1 증가한다.
```

### Stage 3 — Server Test

- 정상 한 판
- Turn 오류
- Edge 중복
- Revision 불일치
- RequestId 재전송
- 두 Confirm 동시 요청
- 마지막 Move와 게임 종료
- Snapshot 불변 조건

### Stage 4 — SignalR 연결

- GameHub
- 개발용 User Session
- Match Group
- Confirm Request/Response
- Snapshot Broadcast
- Sync 요청

완료 조건:

```text
두 Test Client가 같은 Match Snapshot을 유지한다.
응답 유실 후 같은 RequestId 재전송이 안전하다.
Revision 이상 시 전체 동기화가 가능하다.
```

### Stage 5 — Unity Online 적용

- Network Session
- Snapshot 적용
- Local Preview와 Server Confirm 분리
- 연결 상태 UI
- 상대 Preview

### Stage 6 — Matchmaking

- Queue 진입과 취소
- 중복 Queue 방지
- MatchRoom 자동 생성
- 선공 무작위 결정

### Stage 7 — Timer와 재접속

- TurnDeadlineUtc
- 자동 Edge 선택
- Timeout 횟수
- Disconnect 감지
- Grace Period
- ReconnectToken

### Stage 8 — PostgreSQL과 결과 저장

- DB Schema
- MatchHistory 고유 키
- Result Transaction
- 일반전 결과 저장
- Rating은 Rank 단계에서 활성화

### Stage 9 — Docker와 운영 검증

- Docker Image
- Docker Compose
- Caddy TLS
- Health Check
- Readiness Check
- 구조화 로그
- 서버 재시작 및 장애 시나리오 검증

---

## 27. 첫 번째 Online Vertical Slice

첫 번째 네트워크 목표는 다음 한 흐름만 완성하는 것입니다.

```text
개발용 Player A와 B 준비
→ MatchRoom 생성
→ 선공 결정
→ 두 Client가 Snapshot 수신
→ Preview 전달
→ Confirm 서버 판정
→ Snapshot 동기화
→ 게임 종료
→ 결과 표시
```

첫 Vertical Slice에서 제외할 항목:

- Google 및 자체 계정 로그인
- Rank와 Rating
- 시즌
- Redis
- 다중 서버
- 무중단 진행 Match 복구
- 최종 UI 디자인과 애니메이션

기능이 단순해도 서버 권위 판정, Revision, RequestId, 전체 Snapshot은 첫 Vertical Slice부터 포함합니다. 이 세 항목을 나중에 추가하면 Client와 Server의 명령 흐름을 다시 설계해야 하기 때문입니다.

---

## 28. 미확정 항목

다음 항목은 구현 직전에 확인하거나 측정 결과로 결정합니다.

| 항목 | 현재 방향 | 확정 시점 |
|---|---|---|
| Server Runtime 버전 | 당시 공식 지원 LTS 우선 | Server 프로젝트 생성 전 |
| JSON 옵션 | 명시적 필드명과 Enum 표현 검토 | Contract 작성 시 |
| Preview Rate Limit | Player당 초당 5회 초기값 | 모바일 실험 후 |
| Turn 제한 시간 | 20초 초기값 | 플레이테스트 후 |
| Grace Period | 15초 초기값 | 네트워크 장애 테스트 후 |
| ReconnectToken 형식 | 짧은 수명, Match/User 귀속 | 인증 구현 시 |
| Timer Scheduler | 중앙 BackgroundService 우선 검토 | Timer 구현 시 |
| 일반전 MatchHistory 보존 기간 | 미확정 | DB 정책 수립 시 |
| Rating 동시성 방식 | Transaction + 행 잠금 또는 낙관적 동시성 | Rank 구현 시 |

미확정 항목은 추측으로 코드에 고정하지 않습니다.

---

## 29. 구현 전 확인 목록

- Shared Core가 Unity API를 참조하지 않는가?
- Unity와 Server가 같은 Core 소스를 컴파일하는가?
- Hub에 게임 규칙이 들어가지 않았는가?
- Client가 보낸 UserId와 PlayerIndex를 신뢰하지 않는가?
- 모든 Match 변경 명령이 Room 직렬화 경로를 통과하는가?
- 성공 Confirm에서만 Revision이 증가하는가?
- 같은 RequestId가 Move를 두 번 실행하지 않는가?
- Snapshot 배열이 MatchRoom 내부 배열을 직접 노출하지 않는가?
- Preview가 Revision과 점수를 바꾸지 않는가?
- GameFinished 전 DB Transaction 경계가 명확한가?
- 서버 장애 Match가 Rating에 반영되지 않는가?
- Token과 비밀번호가 로그에 남지 않는가?

---

## 30. 기준 문서

이 문서는 다음 기획서 항목을 구현 기준으로 구체화합니다.

- `3.3 입력 및 판정 예외 규칙`
- `12. 턴 제한 시간`
- `13. 네트워크 구조`
- `15. 실시간 통신`
- `16. 서버 권위형 구조`
- `17. 게임 상태 동기화`
- `18. Revision`
- `19. 재접속`
- `20. 서버 MatchRoom`
- `21. 데이터베이스`
- `22. 게임 종료 처리`
- `23. 게임 데이터 구조`
- `40. 상태 머신`
- `42. 테스트 전략`
- `44. 운영 및 관측`

기획 정책이 변경되면 구현 코드보다 먼저 이 문서의 결정 표, Contract, 상태 전이와 구현 단계를 갱신합니다.
