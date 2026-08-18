# Dots & Boxes 서버 아키텍처

작성 기준일: 2026-08-18

## 1. 문서 목적

이 문서는 Dots & Boxes의 Minimum Online Vertical Slice를 구현하기 위한 서버 책임, 프로젝트 구조, 통신 규격, Match 상태, 동시성 원칙과 개발 순서를 확정하는 기준입니다.

기획 전체를 다시 정의하지 않고 `Dots & Boxes 모바일 온라인 게임 기획서.md`에서 정한 정책을 실제 코드 구조로 옮기는 데 집중합니다.

### 이번 문서에서 확정하는 범위

- Unity Client와 서버의 권한 경계
- Shared Core 공유 방식
- ASP.NET Core 단일 서버의 논리 구조
- 개발용 User Session과 수동 Match 생성
- SignalR Hub와 MatchRoom의 책임
- Confirm, Snapshot, Revision 처리
- RequestId 멱등성
- 응답 유실 시 Client의 동일 RequestId 재전송
- RequestSync 상태 복구
- DB 없는 한 판의 종료 상태
- Match 단위 동시성 처리
- 단계별 구현 순서와 완료 조건

현재 완료 목표는 다음 한 흐름입니다.

```text
개발용 Player A와 B 준비
→ 수동 Match 생성
→ 두 Unity Client 참가
→ Server Confirm 판정
→ 전체 Snapshot 동기화
→ 4 × 4 한 판 완료
→ 양쪽 Client가 같은 결과 표시
```

Preview, Turn Timer, Docker, 인증, Matchmaking, PostgreSQL과 Rank는 후속 Phase의 방향만 기록하며 현재 Vertical Slice 완료 조건으로 사용하지 않습니다. Disconnect 기권은 현재 범위에 포함하고 Match 재접속은 Android 첫 출시 후로 미룹니다.

### 이번 문서에서 확정하지 않는 범위

- 최종 UI 디자인과 애니메이션
- Preview Rate Limit 최종값
- Turn Timer와 Post-Launch Grace Period 최종 정책
- 외부 인증 방식의 우선순위
- Matchmaking과 DB Schema의 최종 형태
- 시즌, 티어, Leaderboard 세부 정책
- 다중 Game Server와 무중단 Match 복구
- Redis 도입
- 친구 초대, 관전, Replay
- 결제와 Cosmetic
- 실제 운영 수치가 필요한 Rate Limit 및 Timeout 최종값

현재 Server Target Framework는 `net9.0`, Shared Target Framework는 `netstandard2.1`입니다. Runtime 또는 외부 패키지 업그레이드는 현재 Vertical Slice 완료와 분리해 검토합니다.

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
| 동시성 | MatchRoom별 `SemaphoreSlim`으로 명령 직렬화 |
| Match 생성 | 개발용 API에서 Player 2명과 Room을 수동 생성 |
| 결과 처리 | DB 없이 MatchRoom 메모리에 최종 결과 보관 |
| Preview | Phase 4에서 서버 경유 방식으로 추가 |
| Disconnect | 현재는 참가자당 연결 하나를 등록하고 연결 종료 시 기권 패배 |
| 재접속 | Android 첫 출시 후 별도 Post-Launch Reconnect Stage에서 추가 |
| Timer | Phase 5 Match Exit / Timer Stability에서 추가 |
| 외부 배포 | Phase 6에서 Game Server + Caddy로 시작 |
| 인증·Queue·DB | Phase 7에서 로그인 방식 하나, 일반전 Queue, PostgreSQL 추가 |
| Rank | Android 첫 출시 후 재접속 안정성과 사용자 규모를 확인하고 Phase 10에서 추가 |
| Redis와 다중 서버 | 실제 확장 필요성이 생기기 전까지 제외 |

---

## 3. 서버 권위 경계

온라인 Match에서 최종 상태를 결정하는 주체는 서버뿐입니다.

### Client가 할 수 있는 일

```text
Edge를 로컬 Preview로 표시
Edge Confirm 의도 전송
서버 Snapshot을 화면에 반영
Revision 이상 감지 시 동기화 요청
응답을 받지 못한 Confirm의 RequestId 보존 및 재전송
```

### Client가 결정하면 안 되는 일

```text
Edge 소유자 확정
Box 완성 및 소유자 결정
점수 변경
추가 턴 결정
게임 종료와 승패 결정
```

### Server가 담당하는 일

```text
Match 참가자와 현재 상태 검증
ExpectedRevision 검증
RequestId 중복 검사
DotsRule을 통한 Edge 확정
Revision 증가
전체 Snapshot 생성과 전송
게임 종료와 메모리 결과 확정
개별 Disconnect 시 기권자 상대의 승리 확정
```

Preview 전달, TurnDeadlineUtc, Match 재접속, 결과 영속화와 Rating은 해당 후속 Phase에서 Server 책임에 추가합니다.

로컬 2인 모드에서는 Unity의 `GameBoard_Model`이 `DotsRule`을 직접 호출할 수 있습니다. 온라인 모드에서는 Client가 `DotsRule` 결과를 권위 상태로 사용하지 않고 서버 Snapshot만 확정 상태로 반영합니다.

---

## 4. 전체 시스템 구조

Minimum Online Vertical Slice:

```text
Unity Client A                         Unity Client B
      │                                      │
      └────────────── HTTP / SignalR ─────────┘
                             │
                             ▼
                   ASP.NET Core Game Server
                  ┌──────────────────────────┐
                  │ Development User Session │
                  │ SignalR GameHub          │
                  │ MatchRoomProvider        │
                  │ MatchRoom                │
                  │ DotsAndBoxes.Shared      │
                  └──────────────────────────┘
```

Phase 6 외부 배포에서는 Game Server 앞에 Caddy와 TLS를 추가합니다. Phase 7에서 계정 또는 MatchHistory 저장이 필요할 때 Authentication, MatchmakingQueue, ResultPersistence와 PostgreSQL을 추가합니다.

초기에는 Game Server 인스턴스를 하나만 사용합니다. MatchRoom이 서버 메모리에 있으므로 Load Balancer, Redis Backplane, 분산 Lock은 사용하지 않습니다.

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
│     │  └─ Contracts
│     └─ GamePlay/Session
│
├─ Server
│  ├─ DotsAndBoxes.Server
│  │  ├─ Authentication
│  │  ├─ Hub
│  │  ├─ Matches
│  │
│  ├─ DotsAndBoxes.Shared
│  ├─ DotsAndBoxes.Server.Tests
│  └─ DotsAndBoxes.SignalRTestClient
│
├─ Server/DotsAndBoxes.Server.sln
├─ Dots-Boxes.sln
└─ SERVER_ARCHITECTURE.md
```

`Deploy`, `Matchmaking`, `Persistence`, 운영 전용 폴더는 관련 Phase에서 실제 파일이 필요할 때 추가합니다.

초기 Vertical Slice에서는 별도의 Application, Domain, Infrastructure 프로젝트를 만들지 않습니다. 현재의 Server, Shared Core 빌드, Server Test, SignalR Test Client 구성을 유지합니다.

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
└─ Server → DotsAndBoxes.Shared.csproj
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
- MatchRoomProvider 또는 MatchRoom 호출
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

> **적용 단계: Phase 7 — Account / Matchmaking / Persistence.** Minimum Online Vertical Slice에서는 개발용 API로 Match를 수동 생성합니다.

도입 시 초기 Queue는 서버 메모리에서 관리합니다.

담당 역할:

- Queue 진입과 취소
- 동일 사용자의 중복 Queue 진입 방지
- 자기 자신과 매칭 방지
- Match 후보 두 명을 원자적으로 Queue에서 제거
- MatchRoom 생성 요청

Phase 7의 첫 일반전은 단순 입장 순서로 매칭합니다. Rating 범위 검색은 Rank 단계에서 필요성이 확인된 뒤 추가합니다.

### 7.3 MatchRoomProvider

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
├─ 처리된 RequestId 결과 Cache
└─ 명령 직렬화 Lock
```

Phase 4~5에서 Preview, TurnDeadlineUtc와 Connection 상태를 필요한 순서대로 추가합니다.

MatchRoom만 다음 상태를 변경할 수 있습니다.

- DotsBoard
- Revision
- Match 상태
- 처리된 RequestId 결과

후속 Phase에서는 Preview, TurnDeadlineUtc와 플레이어 Connection 상태도 같은 Room 직렬화 경로에서 변경합니다.

### 7.5 ResultPersistenceService

> **적용 단계: Phase 7 이후.** Minimum Online Vertical Slice는 결과를 MatchRoom 메모리에만 보관합니다.

PostgreSQL 도입 후에는 일반전 MatchHistory를 Transaction으로 저장합니다. Rating은 Android 첫 출시 후 Rank Phase에서 활성화하며, 그때 Match 결과와 Rating을 하나의 Transaction으로 처리합니다.

Phase 7 일반전에서 Rating을 사용하지 않는 경우에도 `MatchHistory.MatchId`의 고유 제약으로 같은 결과가 두 번 저장되지 않게 합니다.

---

## 8. 식별자와 기본 타입

| 항목 | 권장 타입 | 이유 |
|---|---|---|
| `MatchId` | `Guid` | 서버 간 또는 재시작 후에도 충돌 가능성이 낮음 |
| `RequestId` | `Guid` | Client가 Confirm마다 생성하는 멱등성 키 |
| `UserId` | `Guid` | 내부 계정 식별자 |
| `Revision` | `long` | Match 동안 단조 증가하는 버전 |
| `PreviewSequence` | `long` | Phase 4 Preview 순서를 비교 |
| Deadline | `DateTimeOffset` UTC | Phase 5 Timer의 서버 권위 시간 |
| `EdgeId` | `int` | 현재 Core ID와 동일 |
| `PlayerIndex` | `PLAYER_INDEX_ENUM` | Shared 규칙과 동일 |

Client가 전송한 UserId를 신뢰하지 않습니다. UserId는 인증 Claim 또는 서버 Connection Session에서 가져옵니다.

시간은 서버에서 UTC로 생성합니다. Client가 보낸 현재 시간은 Turn 판정에 사용하지 않습니다.

---

## 9. 공유 Contract

Contract는 Unity와 Server가 함께 컴파일할 수 있는 순수 C# 데이터 타입으로 작성합니다.

ASP.NET, SignalR, Entity Framework Core 타입이나 Unity 타입을 포함하지 않습니다.

### 9.1 PreviewEdgeRequest

> **적용 단계: Phase 4 — Online Flow Completion.** 현재 코드에서는 제거했으며 Preview 구현 시 다시 추가합니다.

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

GameResult           GAME_RESULT_ENUM
```

`PlayerOneScore`, `PlayerTwoScore`는 Edge와 Box 상태에서 계산할 수 있지만 Client 표시와 검증 편의를 위해 Snapshot에 포함합니다. Server는 항상 DotsBoard에서 계산한 점수를 넣습니다.

보드가 정상 완료되면 `GameResult`도 DotsBoard에서 가져옵니다.

Phase 5에서 Timer를 구현할 때 `TurnDeadlineUtc`를 추가합니다. 현재 Disconnect 기권은 `MatchState`, `GameResult`와 `Revision`만 변경합니다. 정상 완료와 기권을 UI 또는 MatchHistory에서 구분해야 할 때 최소 종료 사유 Contract를 추가합니다.

`SchemaVersion`은 저장 상태 Revision과 다른 Contract 형식 버전입니다. Contract 필드의 의미가 바뀔 때만 증가합니다.

### 9.5 Match Event

Minimum Online Vertical Slice에서 사용하는 상태 Event:

```text
MatchStateChanged
```

`ConfirmEdgeResponse`와 `RequestSync`는 Hub 호출의 반환값으로 Snapshot을 제공합니다.

후속 Phase에서 필요한 시점에 다음 Event를 추가합니다.

```text
MatchFound
OpponentPreviewChanged
GameFinished

Post-Launch:
OpponentDisconnected
OpponentReconnected
```

상태가 변경되는 Event에는 가능한 한 전체 Snapshot을 포함합니다. `TurnChanged`처럼 Snapshot과 같은 사실을 중복 전달하는 Event는 초기 구현에서 만들지 않고, 필요성이 확인될 때 추가합니다.

---

## 10. Enum 경계

### SERVER_MATCH_STATE_ENUM

```text
NONE
ACTIVE
FINISHED
```

`NONE`은 초기화되지 않은 Snapshot을 구분하기 위한 값입니다. 개발용 API가 참가자 두 명이 준비된 Room을 바로 생성하므로 현재 Match 흐름에는 `ACTIVE`와 `FINISHED`만 사용합니다.

`CREATED`, `WAITING_FOR_PLAYERS`, `FINISHING`, `ABORTED`는 현재 Enum에서 제거합니다. Matchmaking, 결과 저장과 장애 처리를 실제로 구현할 때 필요한 상태만 다시 추가합니다.

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
INTERNAL_ERROR
INVALID_REQUEST
```

`RATE_LIMITED`와 `RECONNECT_REQUIRED`는 현재 Enum에서 제거합니다. Preview Rate Limit과 재접속 정책을 구현할 때 실제 오류 전달 방식이 필요하면 다시 추가합니다.

Core의 `MOVE_ERROR_ENUM`과 Server의 `MATCH_COMMAND_ERROR_ENUM`은 책임이 다릅니다.

```text
MOVE_ERROR_ENUM
└─ 순수 보드 규칙 실패

MATCH_COMMAND_ERROR_ENUM
└─ 인증, Match와 Revision을 포함한 현재 서버 명령 실패
```

Server는 Core 실패 값을 Server 명령 오류로 변환합니다.

### 후속 종료 사유

현재는 정상 보드 완료와 Disconnect 기권을 `MatchState`와 `GameResult`로 처리합니다. 출시판 UI는 최종 승패만 표시하므로 `MATCH_FINISH_REASON_ENUM`을 다시 추가하지 않습니다.

Timeout, Post-Launch Grace Period 만료와 서버 무효 종료를 구현할 때 실제 UI와 저장 요구사항을 확인한 뒤 종료 사유 Contract를 추가합니다.

---

## 11. Match 상태 전이

Minimum Online Vertical Slice:

```text
ACTIVE
   │ 모든 Box 완료
   ▼
FINISHED
```

DB가 없으므로 마지막 Confirm에서 보드 상태, 점수, GameResult와 `FINISHED`를 하나의 원자적 변경으로 확정합니다.

Phase 7 결과 저장 도입 후:

```text
ACTIVE
→ FINISHING
→ MatchHistory Transaction
→ FINISHED 또는 ABORTED
```

이 상태들은 현재 Enum에 미리 넣지 않고 해당 기능 구현 시 추가합니다.

### 상태별 허용 명령

| 상태 | 허용 명령 |
|---|---|
| `ACTIVE` | Confirm, Sync, Disconnect 기권 전환 |
| `FINISHED` | 최종 결과 조회 |

Matchmaking과 DB가 추가되면 그때 `CREATED`, `WAITING_FOR_PLAYERS`, `FINISHING`, `ABORTED`의 필요성을 다시 검토합니다.

Preview와 Reconnect는 각 기능이 구현된 뒤 `ACTIVE` 허용 명령에 추가합니다. 현재 Disconnect 기권은 Hub 연결 생명주기에서 MatchRoom의 직렬화 경로로 진입합니다.

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
- Snapshot 생성
- Match 종료 상태 전이
- 처리 결과 Cache 기록

### Lock 밖에서 처리 가능한 작업

- 이미 생성한 Snapshot 직렬화
- SignalR Broadcast
- 구조화 로그 전송
- 상태를 변경하지 않는 Metric 기록

Broadcast는 Room 상태 Lock을 오래 잡지 않도록 Snapshot 생성 이후 실행합니다.

Preview 제거와 TurnDeadlineUtc 갱신은 해당 기능이 추가된 뒤 Lock 안의 작업으로 확장합니다. DB Transaction은 Phase 7에서 Room을 `FINISHING`으로 전환한 뒤 Lock 밖의 Persistence Service가 수행하고, 완료 상태 전이만 다시 Room 직렬화 경로를 통과합니다.

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
MatchRoomProvider에서 Room 조회
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
            ├─ MatchSnapshot 생성
            ├─ RequestId 결과 Cache 저장
            └─ 게임 종료 시 FINISHED 전환
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

마지막 Confirm으로 게임이 끝나면 Edge, Box, 점수, GameResult와 `FINISHED`를 같은 Snapshot 변경으로 처리하므로 Revision은 한 번만 증가합니다.

현재 다음 권위 상태 변경도 Revision을 증가시킵니다.

- 개별 Disconnect로 상대 승리와 `FINISHED`가 확정됨

후속 Phase에서는 다음 권위 상태 변경도 Revision을 증가시킵니다.

- Turn Timer 자동 선택으로 유효한 Edge가 확정됨
- Turn Timeout 또는 Post-Launch Grace Period 만료로 최종 결과가 결정됨
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

DB 저장이 추가되면 마지막 Confirm의 `FINISHING` 전환에서 한 번, 결과 저장 후 `FINISHED` 또는 `ABORTED` 전환에서 한 번 증가합니다. Revision은 Move 번호가 아니라 권위 Snapshot 버전이라는 원칙을 유지합니다.

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

Client는 사용자가 Confirm을 한 번 시작할 때 새로운 `Guid RequestId`를 생성합니다. Response 또는 해당 Move가 반영된 Snapshot을 확인할 때까지 Pending Confirm에 같은 RequestId와 요청 내용을 보존합니다.

Server는 MatchRoom 안에 다음 Cache를 둡니다.

```text
RequestId → ConfirmEdgeResponse
```

### 동일 RequestId 재수신

- 요청 내용이 처음 요청과 같으면 저장된 Response를 반환합니다.
- EdgeId 또는 MatchId 등 내용이 다르면 `DUPLICATE_REQUEST_CONFLICT`를 반환하고 상태를 변경하지 않습니다.
- 같은 RequestId로 `DotsRule`을 두 번 실행하지 않습니다.
- 응답 유실 후 Client가 재전송할 때 새 RequestId를 만들지 않습니다.

정상 Match의 Confirm 성공 횟수는 최대 40회입니다. Cache는 Match가 끝날 때까지 성공 Confirm과 재전송에 필요한 결과를 유지하고 MatchRoom 제거 시 함께 삭제합니다.

잘못된 요청을 반복해 Cache 상한을 채운 뒤 정상 Confirm까지 영구 거부하는 구조는 허용하지 않습니다. 실패 결과 보관 범위, Cache 상한과 호출 빈도 제한은 정상 게임 진행을 막지 않도록 별도로 검증합니다.

---

## 16. Preview 처리

> **적용 단계: Phase 4 — Online Flow Completion.** Minimum Online Vertical Slice 완료 조건에는 포함하지 않습니다.

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
ACTIVE이면 GameResult는 IN_PROGRESS
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
→ SignalRGameSession이 RequestId를 보존하고 Server에 전송
→ MatchSnapshot 수신
→ GameBoard_Model.ApplySnapshot()
→ GameBoard_Presenter가 View 전체 동기화
```

`GameBoard_View`는 로컬과 온라인에서 재사용합니다.

온라인 적용 시 필요한 Client 계층은 다음 구현 단계에서 별도로 설계합니다.

```text
IGameSession
├─ LocalGameSession
└─ SignalRGameSession
```

Minimum Online Vertical Slice에서는 Client가 다음을 실제로 검증해야 합니다.

- 응답 유실 시 Pending Confirm의 RequestId 보존
- 동일 요청 재전송
- 오래된 Snapshot 무시
- Revision 불일치 후 RequestSync
- 마지막 Snapshot을 통한 결과 표시

---

## 19. Turn Timer

> **적용 단계: Phase 5 — Match Exit / Timer Stability.** Minimum Online Vertical Slice에서는 `TurnDeadlineUtc`를 사용하지 않아도 됩니다.

Timer의 권위 시간은 서버의 `TurnDeadlineUtc`입니다.

Client는 자신의 로컬 시계를 기준으로 남은 시간을 표시하지만 시간 만료를 확정하지 않습니다.

### 초기 정책

```text
Turn 제한 시간: 20초 초기값
시간 초과: 남은 유효 Edge 중 서버가 균등 무작위 자동 선택
자동 선택 성공: 일반 Move와 동일하게 Revision 증가
연속 2회 또는 누적 3회 초과: 기권 패배
Post-Launch Reconnect의 Grace Period 중 Timer 계속 진행
```

자동 선택한 Edge와 원인 `TIMEOUT_AUTO_MOVE`를 서버 기록에 남깁니다.

Timer는 MatchRoom마다 별도 Thread를 만들지 않습니다. 도입 시 중앙 BackgroundService가 만료 예정 Room을 확인하거나 만료 예약 작업을 관리합니다. 정확한 방식은 Minimum Online Vertical Slice가 완료된 뒤 결정합니다.

---

## 20. Disconnect와 출시 후 재접속

### 20.1 출시판 Disconnect 기권

현재 서버는 `MatchConnectionRegistry`에 다음 최소 정보만 메모리로 보관합니다.

```text
ConnectionId → MatchId, UserId
(MatchId, UserId) → ConnectionId
```

- `JoinMatch` 성공 과정에서 참가자당 활성 연결 하나를 등록합니다.
- 같은 참가자의 두 번째 연결은 거부합니다.
- `ConnectionId`는 연결 추적에만 사용하고 User 신원은 인증 Claim의 UserId로 확인합니다.
- `OnDisconnectedAsync`가 등록된 개별 연결 종료를 확정하면 `MatchRoom.TryForfeit_async()`를 호출합니다.
- 기권 처리는 MatchRoom의 기존 `SemaphoreSlim` 경로에서 직렬화합니다.
- `ACTIVE`인 Match만 상대 승리, `FINISHED`, Revision 증가로 한 번 전환합니다.
- 연결된 상대에게 기존 `MatchStateChanged` 전체 Snapshot을 전송합니다.
- 이미 종료된 Match와 등록되지 않은 연결 종료는 상태를 변경하지 않습니다.
- Server `ApplicationStopping` 중 발생한 Disconnect는 Player 기권으로 처리하지 않습니다.
- Server Process 장애로 메모리 Room이 유실되면 승패와 전적을 확정하지 않습니다.

출시판에는 ReconnectToken, Grace Period, 연결 상태를 저장하는 `MatchPlayer` 필드와 자동 재접속을 추가하지 않습니다.

### 20.2 Post-Launch Reconnect

Android 첫 출시 후 실제 Disconnect 비율과 사용자 이탈을 측정한 뒤 다음 항목을 별도 Stage에서 구현합니다.

```text
AuthenticatedUserId
MatchId
ReconnectToken
Grace Period
전체 Snapshot 복구
```

- SignalR `ConnectionId` 자체를 User 식별자로 사용하지 않습니다.
- Token은 Match와 User에 귀속하고 짧은 수명으로 발급합니다.
- 원문 대신 Token Hash 저장을 우선 검토합니다.
- Grace Period와 Timer 정책은 모바일 실측 후 확정합니다.
- 재접속 성공 시 전체 Snapshot을 전송합니다.
- Grace Period를 넘으면 기권 패배로 확정합니다.

---

## 21. 게임 종료와 DB Transaction

> **적용 단계: Phase 7 — Account / Matchmaking / Persistence.** Minimum Online Vertical Slice는 마지막 Confirm에서 결과를 메모리에 확정하고 `FINISHED`로 전환합니다.

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

일반전에서 Rating을 변경하지 않는 경우에도 MatchHistory 저장의 멱등성은 유지합니다. Rating Transaction은 Android 첫 출시 후 Rank Phase에서 추가합니다.

---

## 22. 장애 정책

초기 서버는 진행 중 Match를 메모리에만 보관합니다.

서버가 비정상 종료되면 진행 중 Match를 복구할 수 없습니다. Process 메모리가 사라진 뒤에는 기존 Room을 `ABORTED`로 바꿀 수 없으므로, Minimum Online Vertical Slice에서는 Client에 연결 실패를 표시하고 해당 Match를 재개 불가로 처리합니다.

```text
연결 실패 표시
Match 재개 불가
Rating과 전적 저장 없음
```

Phase 5 이후 정상적으로 감지한 장애에서는 `ABORTED` Snapshot과 무효 사유를 전송할 수 있습니다.

Phase 6의 개발용 수동 Match 배포에서는 다음 순서를 사용합니다.

```text
신규 개발용 Match 생성 중단
→ 진행 중 Match 종료 대기 또는 테스트 종료 공지
→ Game Server 종료
→ 새 Container 실행
→ Health 확인
→ 개발용 Match 생성 재개
```

Phase 7에서 Queue와 PostgreSQL이 추가되면 Queue 중단, Readiness와 DB Migration 확인 절차를 별도로 확장합니다.

---

## 23. 인증과 보안 경계

Minimum Online Vertical Slice에서는 개발용 User Session만 사용합니다. 외부 공개 환경에서는 인증되지 않은 Hub 명령을 허용하지 않으며 Phase 7에서 로그인 방식 하나를 선택해 실제 인증으로 교체합니다.

### 기본 원칙

- UserId는 인증 Claim에서 가져옵니다.
- Client가 보낸 PlayerIndex를 신뢰하지 않습니다.
- MatchRoom의 참가자 정보로 PlayerIndex를 결정합니다.
- Hub 요청 크기와 호출 빈도를 제한합니다.
- 모든 Match 명령은 해당 User의 Match 참가 여부를 검사합니다.
- 개발용 User Session은 Development 환경에서만 활성화합니다.
- Password, Access Token, ReconnectToken과 DB 비밀번호는 관련 기능 도입 후에도 로그에 기록하지 않습니다.
- 외부 배포에서는 Caddy부터 Client까지 TLS를 사용합니다.

Google 계정, 자체 계정과 Steam 연동의 세부 구현은 Account 단계에서 별도 문서로 분리합니다.

---

## 24. 로그와 관측

Minimum Online Vertical Slice의 구조화 로그에는 다음 값을 포함합니다.

```text
TimestampUtc
LogLevel
EventName
UserId
MatchId
RequestId
Revision
DurationMs
```

주요 EventName:

```text
MATCH_CREATED
PLAYER_JOINED
EDGE_CONFIRMED
COMMAND_REJECTED
REVISION_MISMATCH
MATCH_FINISHED
```

초기 관측값:

- SignalR 연결 수
- 진행 중 Match 수
- Confirm 요청 성공/실패 수
- Revision 불일치 수
- 평균 및 p95 Confirm 처리시간

ConnectionId와 Disconnect 기권 수는 현재 연결 추적 로그에 포함할 수 있습니다. Queue 대기 인원, 재접속 성공률, ABORTED Match와 DB Transaction 실패 수는 관련 Phase에서 추가합니다.

---

## 25. Docker 배포 구조

> **적용 단계: Phase 6 — Remote Deployment.** 로컬 Minimum Online Vertical Slice의 완료 조건에는 포함하지 않습니다.

```text
docker-compose
├─ game-server
│  └─ ASP.NET Core
└─ caddy
```

### Container 책임

| Container | 책임 |
|---|---|
| `game-server` | REST, SignalR, MatchRoom, 메모리 결과 처리 |
| `caddy` | TLS 종료와 Reverse Proxy |

환경 변수 파일에는 실제 비밀번호를 Commit하지 않습니다. Repository에는 필요한 Key 이름만 있는 예제 파일을 둡니다.

Phase 7에서 계정 또는 MatchHistory 저장을 구현할 때 `postgres` Container를 추가합니다. 첫 출시 후 Rank 기능을 구현할 때 Rating 저장을 같은 DB에 확장합니다.

Redis는 다음 조건이 실제로 생길 때 검토합니다.

- Game Server 인스턴스를 두 개 이상 운영
- SignalR Backplane 필요
- MatchRoom 외부 저장 필요
- 분산 Queue 필요

---

## 26. 구현 단계

아래 Stage 1~5가 기획서의 `Phase 3 — Minimum Online Vertical Slice`를 구성합니다. 이후 Stage는 기획서의 후속 Phase와 같은 순서로 진행합니다.

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

### Stage 5 — Unity Minimum Online 적용

- `SignalRGameSession`
- 전체 Snapshot 적용
- Local Preview와 Server Confirm 분리
- Pending Confirm에 RequestId와 요청 내용 보존
- 응답 유실 후 동일 요청 재전송
- Revision 불일치 후 RequestSync
- 마지막 Snapshot 결과 표시

완료 조건:

```text
개발용 수동 Match에 두 Unity Client가 참가한다.
4 × 4 한 판을 시작부터 결과 표시까지 완료한다.
응답 유실 후 같은 RequestId를 재전송해도 Move가 한 번만 적용된다.
양쪽 Client와 Server의 최종 Snapshot이 일치한다.
```

여기까지가 Minimum Online Vertical Slice입니다.

### Stage 6 — Online Flow Completion

- 상대 Preview 전달
- PreviewSequence
- Preview Rate Limit
- 연결 상태와 기본 오류 UI
- 실제 Android 기기 두 대 검증

### Stage 7 — Match Exit / Timer Stability

- 직접 나가기와 Disconnect 패배 UX
- Server 종료 시 기권 미적용 검증
- TurnDeadlineUtc
- 자동 Edge 선택
- Timeout 횟수
- AFK 기권

### Stage 8 — Remote Deployment

- Game Server Docker Image
- Caddy TLS와 Reverse Proxy
- Health Check
- N100 Mini PC 배포
- 외부 네트워크 Android 기기 검증
- 재배포와 이전 Image 복귀 절차

이 단계에서는 계정이나 MatchHistory가 없다면 PostgreSQL을 추가하지 않습니다.

### Stage 9 — Account / Matchmaking / Persistence

- Android 로그인 방식 하나 선택
- 실제 인증과 내부 UserId
- Queue 진입과 취소
- 중복 Queue 방지
- MatchRoom 자동 생성
- 선공 무작위 결정
- PostgreSQL Schema
- MatchHistory 고유 키와 일반전 결과 저장

### Stage 10 — Release Server Readiness

- Android 내부 테스트용 외부 서버 운영
- 배포와 이전 Image 복귀 절차 확인
- 비밀 정보 로그 미기록 확인
- 일반전 MatchHistory 저장 확인
- 알려진 서버 제한과 미구현 범위 문서화

**목표:** Rank 없이 일반전 중심 Android 첫 버전을 배포할 수 있는 서버 상태 완성.

### Stage 11 — Post-Launch Reconnect

- Disconnect 비율과 이탈 데이터 수집
- ReconnectToken
- Grace Period
- SignalR 재연결 후 전체 Snapshot 복구
- 중복 연결과 만료 Token 거부
- Wi-Fi/LTE 전환과 백그라운드 복귀 검증

Rank Match에 적용하기 전에 재접속 안정성을 먼저 검증합니다.

### Stage 12 — Post-Launch Rank

- Elo Rating
- MatchHistory와 Rating Transaction
- 중복 결과 적용 방지
- Rank와 Leaderboard

Android 첫 출시 후 일반전 운영, 재접속 안정성과 사용자 규모가 확인되기 전에는 진행하지 않습니다.

Easy / Normal / Hard AI 확장은 Shared Core와 Unity Gameplay의 출시 후 작업이며 Server Architecture 범위에는 포함하지 않습니다.

---

## 27. 첫 번째 Online Vertical Slice

첫 번째 네트워크 목표는 다음 한 흐름만 완성하는 것입니다.

```text
개발용 Player A와 B 준비
→ 개발용 API로 MatchRoom 수동 생성
→ 선공 결정
→ 두 Unity Client가 초기 Snapshot 수신
→ Confirm 서버 판정
→ Snapshot 동기화
→ 응답 유실 시 동일 RequestId 재전송
→ RequestSync 복구
→ Player 1 Disconnect
→ Player 2 승리 Snapshot
→ 4 × 4 게임 종료
→ 양쪽 Client가 같은 결과 표시
```

첫 Vertical Slice에서 제외할 항목:

- Google 및 자체 계정 로그인
- 자동 Matchmaking
- 상대 Preview
- Turn Timer와 AFK 처리
- 재접속과 Grace Period
- Docker와 Caddy
- PostgreSQL과 MatchHistory
- Rank와 Rating
- 시즌
- Normal/Hard AI
- Redis
- 다중 서버
- 무중단 진행 Match 복구
- 최종 UI 디자인과 애니메이션

기능이 단순해도 서버 권위 판정, Revision, RequestId, 전체 Snapshot과 RequestSync는 첫 Vertical Slice부터 포함합니다. 또한 RequestId는 Server Cache만 존재하는 것으로 완료하지 않고 Unity Client의 Pending Confirm 보존과 재전송까지 검증합니다.

---

## 28. 미확정 항목

다음 항목은 구현 직전에 확인하거나 측정 결과로 결정합니다.

| 항목 | 현재 방향 | 확정 시점 |
|---|---|---|
| Pending Confirm 보존 범위 | RequestId, MatchId, EdgeId, ExpectedRevision | Stage 5 Unity 적용 시 |
| RequestId Cache 상한 | 정상 진행을 막지 않는 제한 필요 | Stage 5 재전송 검증 시 |
| Preview Rate Limit | Player당 초당 5회 초기값 | Stage 6 모바일 실험 후 |
| Turn 제한 시간 | 20초 초기값 | Stage 7 플레이테스트 후 |
| Grace Period | 모바일 실측 후 결정 | Stage 11 Post-Launch Reconnect 시작 시 |
| ReconnectToken 형식 | 짧은 수명, Match/User 귀속 | Stage 11 인증 경계 검토 시 |
| Timer Scheduler | 중앙 BackgroundService 우선 검토 | Stage 7 Timer 구현 시 |
| 최초 로그인 방식 | Google 또는 아이디·비밀번호 중 하나 | Stage 9 시작 전 |
| 일반전 MatchHistory 보존 기간 | 미확정 | Stage 9 DB 정책 수립 시 |
| Rating 동시성 방식 | Transaction + 행 잠금 또는 낙관적 동시성 | Stage 12 Post-Launch Rank 구현 시 |

미확정 항목은 추측으로 코드에 고정하지 않습니다.

---

## 29. 구현 전 확인 목록

Minimum Online Vertical Slice:

- Shared Core가 Unity API를 참조하지 않는가?
- Unity와 Server가 같은 Core 소스를 컴파일하는가?
- Hub에 게임 규칙이 들어가지 않았는가?
- Client가 보낸 UserId와 PlayerIndex를 신뢰하지 않는가?
- 모든 Match 변경 명령이 Room 직렬화 경로를 통과하는가?
- 성공 Confirm에서만 Revision이 증가하는가?
- 같은 RequestId가 Move를 두 번 실행하지 않는가?
- Client가 응답 유실 시 같은 RequestId를 보존하는가?
- RequestId Cache 제한이 정상 Confirm을 영구 차단하지 않는가?
- Snapshot 배열이 MatchRoom 내부 배열을 직접 노출하지 않는가?
- 마지막 Confirm에서 FINISHED 결과가 Snapshot에 포함되는가?
- 개별 Disconnect가 상대 승리와 Revision 증가로 한 번만 적용되는가?
- Server 종료 중 Disconnect가 Player 기권을 만들지 않는가?
- 두 Unity Client와 Server의 최종 Snapshot이 일치하는가?

후속 Phase에서 추가할 확인 항목:

- Preview가 Revision과 점수를 바꾸지 않는가?
- Post-Launch Reconnect Token과 만료 정책이 검증되는가?
- GameFinished 전 DB Transaction 경계가 명확한가?
- 서버 장애 Match가 Rating에 반영되지 않는가?
- Token과 비밀번호가 로그에 남지 않는가?

---

## 30. 기준 문서

Minimum Online Vertical Slice에서 직접 사용하는 기획서 항목:

- `3.3 입력 및 판정 예외 규칙`
- `15. 실시간 통신`
- `16. 서버 권위형 구조`
- `17. 게임 상태 동기화`
- `18. Revision`
- `20. 서버 MatchRoom`
- `22. 게임 종료 처리`
- `23. 게임 데이터 구조`
- `40. 상태 머신`
- `42. 테스트 전략`

후속 Phase에서 적용하는 기획서 항목:

- `12. 턴 제한 시간`
- `13. 네트워크 구조`
- `19. Disconnect와 출시 후 재접속`
- `21. 데이터베이스`
- `44. 운영 및 관측`

기획 정책이 변경되면 구현 코드보다 먼저 이 문서의 결정 표, Contract, 상태 전이와 구현 단계를 갱신합니다.
