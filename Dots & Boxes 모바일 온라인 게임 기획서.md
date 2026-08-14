# Dots & Boxes 모바일 온라인 게임 기획서

# 1. 프로젝트 개요

## 1.1 프로젝트명

**가제: DOTS ARENA**

최종 명칭은 출시 전 별도 결정합니다.

## 1.2 장르

- 모바일 캐주얼 보드게임
- 1:1 턴제 전략
- 실시간 온라인 대전
- 랭크 경쟁

## 1.3 플랫폼

- **1차 개발 및 출시: Android 전용**
- Google Play Store 배포
- **차후 확장 검토: Windows PC**
- PC 버전은 Steam 배포 및 Steam 계정 인증을 전제로 함
- Unity 기반 개발

iOS는 현재 개발 및 출시 범위에 포함하지 않습니다.

게임 규칙과 서버는 플랫폼에 직접 의존하지 않도록 설계하지만, Android 버전이 완성되고 운영 안정성이 검증되기 전에는 PC 버전을 병행 개발하지 않습니다.

## 1.4 핵심 콘셉트

고전 보드게임 **Dots and Boxes**를 모바일 환경에 맞게 재구성한 1:1 전략 게임입니다.

플레이어는 점 사이의 선을 선택하고 확정하여 사각형을 완성합니다. 사각형을 완성한 플레이어는 해당 영역을 획득하며 추가 턴을 얻습니다.

단순히 번갈아 선을 긋는 게임이 아니라,

- 상대방이 현재 고민하고 있는 선을 실시간으로 확인
- 연속 사각형 획득을 노리는 Chain 전략
- 실시간 온라인 매칭
- MMR 기반 랭크전
- 시즌 랭킹
- AI 대전

을 핵심 콘텐츠로 합니다.

---

# 2. 핵심 목표

## 2.1 개발 목표

단순한 Dots and Boxes 복제보다는 다음 기술을 포함한 완성형 모바일 온라인 게임을 목표로 합니다.

- Unity 모바일 UI/UX
- 순수 C# 기반 게임 로직
- 서버 권위형 게임 처리
- ASP.NET Core 실시간 서버
- SignalR 기반 통신
- Docker 서버 배포
- PostgreSQL 데이터 저장
- 실시간 Matchmaking
- Elo/MMR 기반 Rank
- 네트워크 재접속
- AI 플레이

## 2.2 포트폴리오 목표

이 프로젝트는 기능 개수보다 다음 문제를 직접 설계하고 검증한 과정을 보여주는 것을 목표로 합니다.

- Unity View와 순수 C# 게임 규칙의 분리
- 서버 권위형 턴 처리와 클라이언트 예측 범위 결정
- 중복 요청, 메시지 순서 역전, 연결 끊김에서의 상태 일관성 유지
- 모바일 환경에서의 재접속과 게임 복구
- 제한된 개인 서버 환경에서의 배포, 운영, 로깅
- 단위 테스트, 통합 테스트, 장애 테스트를 통한 근거 제시

최종 포트폴리오에는 구현 결과뿐 아니라 설계 선택의 이유, 대안, 테스트 결과와 개선 과정을 함께 기록합니다.

## 2.3 타깃 플레이어와 플레이 경험

### 타깃 플레이어

- 규칙을 짧게 익히고 바로 경쟁하고 싶은 모바일 보드게임 이용자
- 한 판의 길이가 길지 않은 1:1 전략 게임을 원하는 이용자
- 운보다 수 읽기와 심리전을 선호하는 이용자

### 목표 플레이 경험

```text
간단한 규칙 이해
↓
안전한 Edge와 위험한 Edge 탐색
↓
상대 Preview를 통한 심리전
↓
Chain을 넘길 시점 결정
↓
연속 Box 획득의 쾌감
```

4 × 4 게임의 목표 플레이 시간은 **5~10분**으로 가정합니다.

이 수치는 확정값이 아니며 프로토타입 플레이테스트에서 다음 데이터를 수집해 조정합니다.

- 평균 게임 시간
- 평균 Turn 수
- Turn당 평균 고민 시간
- 중도 이탈률
- 선공/후공 승률

---

# 3. 게임 규칙

## 3.1 기본 보드

기본 랭크전 규격은 **4 × 4 Box**로 합니다.

```text
●──●──●──●──●
│  │  │  │  │
●──●──●──●──●
│  │  │  │  │
●──●──●──●──●
│  │  │  │  │
●──●──●──●──●
│  │  │  │  │
●──●──●──●──●
```

구성:

- Dot: 25개
- Box: 16개
- Horizontal Edge: 20개
- Vertical Edge: 20개
- 전체 Edge: 40개

일반전 또는 AI 대전에서는 추후 다음 규격도 제공할 수 있습니다.

- 3 × 3
- 4 × 4
- 5 × 5

랭크전에서는 밸런스와 MMR 일관성을 위해 하나의 크기로 고정합니다.

## 3.2 선공과 무승부

- 선공은 서버가 매 Match마다 무작위로 결정합니다.
- 16개의 Box를 8개씩 획득한 경우 무승부로 처리합니다.
- Elo 계산에서 무승부의 실제 점수는 양쪽 모두 `0.5`로 처리합니다.
- 선공/후공 승률은 MatchHistory를 통해 별도로 집계합니다.
- 선공 우위가 의미 있는 수준으로 확인되기 전에는 임의의 보정 규칙을 추가하지 않습니다.

선공 우위 판단 기준은 운영 전 플레이테스트를 통해 정합니다. 초기 검증에서는 최소 100판 이상의 AI 대 AI 및 사용자 테스트 결과를 함께 확인합니다.

## 3.3 입력 및 판정 예외 규칙

다음 요청은 서버에서 거부합니다.

- 존재하지 않는 EdgeId
- 이미 확정된 Edge
- 자신의 Turn이 아닐 때의 Preview 또는 Confirm
- 이미 종료된 Match에 대한 요청
- 현재 Match와 다른 MatchId를 포함한 요청
- 클라이언트 Revision이 서버의 기준과 맞지 않는 Confirm

거부된 요청은 게임 상태를 변경하지 않습니다. 클라이언트에는 오류 코드와 최신 Snapshot 또는 동기화 필요 여부를 전달합니다.

예시 오류 코드:

```text
NOT_YOUR_TURN
INVALID_EDGE
EDGE_ALREADY_CONFIRMED
REVISION_MISMATCH
MATCH_NOT_ACTIVE
RATE_LIMITED
```

---

# 4. 턴 규칙

플레이어는 자신의 턴마다 비어 있는 Edge 하나를 선택합니다.

## 4.1 Edge 선택

선택 즉시 실제 게임판에는 적용되지 않습니다.

```text
Edge 터치
    ↓
Preview 표시
    ↓
확정 버튼 활성화
```

다른 Edge를 선택하면 기존 Preview는 취소됩니다.

```text
Edge 12 선택
↓
Edge 12 Preview

Edge 17 선택
↓
Edge 12 Preview 제거
Edge 17 Preview 표시
```

---

## 4.2 상대방 Preview

온라인 대전에서는 상대 플레이어가 현재 선택하고 있는 Edge도 실시간으로 표시합니다.

```text
Player A
Edge 17 선택
    ↓
Server
    ↓
Player B
Edge 17 상대 Preview 표시
```

Preview는 게임 상태에는 영향을 주지 않습니다.

Preview 상태에서는:

- Box 획득 판정 없음
- Turn 변경 없음
- Score 변경 없음
- DB 저장 없음

상대방의 선택 과정을 보여줌으로써 실제 보드게임에서 상대방이 수를 고민하는 느낌과 심리전을 제공합니다.

---

# 5. Edge 확정

플레이어가 **확정 버튼**을 누르면 서버에 요청합니다.

```text
ConfirmEdge(EdgeId)
```

서버는 다음을 검증합니다.

```text
현재 해당 플레이어 턴인가?
        ↓
유효한 Edge인가?
        ↓
이미 사용된 Edge인가?
        ↓
Edge 확정
        ↓
인접 Box 검사
```

클라이언트가 직접 게임 결과를 결정하지 않습니다.

---

# 6. Box 획득

Edge를 확정한 결과 사각형의 네 변이 모두 완성됐다면 해당 플레이어가 Box를 획득합니다.

```text
□
↓
마지막 Edge 확정
↓
■
```

Box를 획득한 플레이어는 **추가 턴**을 얻습니다.

한 Edge를 통해 동시에 두 개의 Box가 완성될 수도 있습니다.

```text
□ │ □
   ↑

Edge 확정

■ │ ■
```

이 경우 두 Box를 모두 획득합니다.

---

# 7. 턴 변경

### Box를 획득하지 못한 경우

```text
Player A
Edge 확정
↓
Box 없음
↓
Player B Turn
```

### Box를 획득한 경우

```text
Player A
Edge 확정
↓
Box 획득
↓
Player A 추가 Turn
```

---

# 8. 게임 종료

모든 Box의 소유자가 결정되면 게임이 종료됩니다.

```text
Player A : 10
Player B : 6

Player A WIN
```

16 Box 기준 과반수인 9개 이상을 확보하면 사실상 승리지만, 기본적으로 모든 Edge가 완성될 때까지 게임을 진행합니다.

추후 게임 템포 개선이 필요하다면 승패가 수학적으로 확정된 시점에 조기 종료하는 기능을 고려할 수 있습니다.

---

# 9. 게임 모드

## 9.1 AI 대전

네트워크 없이 플레이합니다.

난이도:

- Easy
- Normal
- Hard

### Easy

- 랜덤 Edge 중심
- 즉시 획득 가능한 Box는 일정 확률로 획득

### Normal

- 완성 가능한 Box 우선
- 상대에게 즉시 Box를 제공하는 Edge 회피
- 기본적인 Chain 판단

### Hard

- Minimax
- Alpha-Beta Pruning
- Chain 분석
- 희생 전략
- Endgame 계산

### AI 공통 성능 원칙

AI는 Unity View가 아닌 `DotsAndBoxes.Shared`의 게임 상태를 사용합니다.

Hard AI가 게임 초반부터 모든 수를 완전 탐색하는 것은 상태 공간이 크므로 다음 방식으로 제한합니다.

- 즉시 획득 가능한 Box 우선 정렬
- 상대에게 Box를 제공하지 않는 안전한 Edge 우선 정렬
- Alpha-Beta Pruning을 위한 Move Ordering
- 동일 상태의 중복 계산을 줄이는 Transposition Table
- 초·중반은 제한 깊이 탐색과 평가 함수 사용
- 남은 Edge가 기준 이하가 되면 Endgame 완전 탐색 전환
- 한 Turn의 최대 연산 시간 제한

초기 성능 목표는 다음과 같습니다.

```text
Easy   : 100ms 이내
Normal : 300ms 이내
Hard   : 1,000ms 이내
```

수치는 목표값이며 실제 Android 기기 프로파일링 후 조정합니다. 난이도는 단순 지연시간이 아니라 승률과 의사결정 품질로 구분합니다.

AI 검증 항목:

- 동일 Seed 및 동일 상태에서 결정론적 결과 재현
- 유효하지 않은 Edge를 선택하지 않음
- 즉시 획득 가능한 Box 판단
- 안전한 Edge 판단
- Chain 시작 및 희생 판단
- 제한 시간 초과 시 현재까지 찾은 최선의 유효 수 반환

---

## 9.2 일반전

실시간 온라인 1:1 대전입니다.

- MMR 변동 없음
- 전적 기록 선택적
- 랭크 부담 없이 플레이 가능

---

## 9.3 랭크전

MMR 기반 실시간 1:1 매칭입니다.

특징:

- 공식 보드 크기 고정
- MMR 적용
- 시즌 Rank
- 승/패 기록
- Leaderboard 적용

---

# 10. Rank 시스템

## 10.1 Rating

기본 Rating:

```text
1000
```

초기 티어 예시:

| 티어 | Rating |
|---|---:|
| Bronze | 0 ~ 799 |
| Silver | 800 ~ 999 |
| Gold | 1000 ~ 1199 |
| Platinum | 1200 ~ 1399 |
| Diamond | 1400 ~ 1599 |
| Master | 1600 이상 |

수치는 운영 데이터를 기반으로 조정합니다.

---

## 10.2 Elo

Rating 차이를 고려하여 획득/감소량을 계산합니다.

예:

```text
1200 Player
VS
1500 Player

1200 Player 승리
→ 큰 Rating 획득

1500 Player 승리
→ 작은 Rating 획득
```

예상 밖의 결과일수록 Rating 변동을 크게 합니다.

## 10.3 초기 Rating 계산 규칙

초기 버전은 표준 Elo 계산식을 사용합니다.

```text
ExpectedA = 1 / (1 + 10 ^ ((RatingB - RatingA) / 400))

NewRatingA = RatingA + K × (ActualScoreA - ExpectedA)
```

실제 점수:

```text
승리 = 1.0
무승부 = 0.5
패배 = 0.0
```

초기 `K` 값은 `32`로 가정하며 플레이테스트와 시즌 데이터를 통해 조정합니다.

- 정상 종료된 랭크 Match만 Rating 반영
- 서버 장애로 `ABORTED`된 Match는 Rating 미반영
- 기권 및 Grace Period 초과는 패배로 반영
- 같은 MatchId의 Rating 계산은 한 번만 실행
- Rating은 정수로 저장하며 반올림 규칙을 서버에서 통일

시즌 초기화 방식과 배치 Match는 실제 사용자 수가 확보되기 전까지 구현 우선순위를 낮춥니다. 도입할 경우 기존 Rating을 완전히 삭제하지 않고 Soft Reset을 사용하며 계산식을 별도 명시합니다.

---

# 11. Matchmaking

랭크전 매칭은 Rating 범위를 기준으로 탐색합니다.

예:

```text
0 ~ 5초
MMR ±100

5 ~ 10초
MMR ±200

10 ~ 20초
MMR ±300

20초 이상
탐색 범위 추가 확장
```

정확한 범위와 시간은 실제 사용자 수를 기준으로 조정합니다.

## 11.1 Queue 처리 원칙

- 한 사용자는 동시에 하나의 Queue에만 참가할 수 있습니다.
- 이미 진행 중인 Match가 있는 사용자는 새 Queue에 참가할 수 없습니다.
- `JoinQueue`와 `CancelQueue`는 중복 호출되어도 최종 상태가 한 번만 변경됩니다.
- 매칭 후보가 정해진 순간 양쪽 사용자를 원자적으로 Queue에서 제거합니다.
- 자기 자신 또는 동일 계정의 다른 연결과 매칭하지 않습니다.
- Match 생성 실패 시 양쪽 사용자를 Queue로 복구하거나 명시적인 실패 응답을 전달합니다.
- 장시간 매칭되지 않으면 탐색 범위를 넓히되 예상 품질 저하를 UI에 표시합니다.

초기 사용자 수가 적은 단계에서는 낮은 동시 접속을 숨기기 위해 AI를 랭크 상대처럼 제공하지 않습니다. AI Match와 사용자 Match는 전적 및 Rating에서 명확히 구분합니다.

---

# 12. 턴 제한 시간

온라인 게임의 장시간 방치를 막기 위해 Turn Timer를 적용합니다.

예:

```text
Turn Time

20초
```

시간은 서버가 관리합니다.

클라이언트는 남은 시간만 표시합니다.

```text
Server
TurnDeadlineUtc
      ↓
Client
20
19
18
17...
```

## 12.1 시간 초과 정책

초기 정책은 다음과 같습니다.

1. 제한 시간이 끝나면 서버가 남은 유효 Edge 중 하나를 자동 확정합니다.
2. 자동 선택은 서버에서만 수행하며 일반 Move와 동일하게 Revision을 증가시킵니다.
3. 한 Match에서 연속 2회 또는 누적 3회 시간 초과 시 기권 패배 처리합니다.
4. 재접속 Grace Period 중에는 Turn Timer를 별도로 멈추지 않습니다.
5. 양쪽 클라이언트에 시간 초과 횟수와 자동 선택 여부를 전달합니다.

자동 선택은 서버에서 남은 유효 Edge를 대상으로 균등하게 수행합니다. 선택된 Edge와 `TIMEOUT_AUTO_MOVE` 원인을 Move 기록에 남기며, Replay에서는 난수를 다시 계산하지 않고 기록된 Edge를 사용합니다.

해당 정책은 초기값이며 플레이테스트에서 지나치게 가혹하거나 악용 가능성이 확인되면 조정합니다.

---

# 13. 네트워크 구조

## 13.1 서버

개인 N100 Mini PC를 서버로 사용합니다.

예상 환경:

```text
N100 Mini PC
│
└─ Linux
    │
    └─ Docker
        │
        ├─ ASP.NET Core Game Server
        ├─ PostgreSQL
        └─ Caddy
```

필요 시 Redis를 추후 추가합니다.

---

# 14. Docker 구조

```text
docker-compose

├─ game-server
│    └─ ASP.NET Core
│
├─ postgres
│
└─ caddy
```

초기에는 단일 서버 구조로 운영합니다.

Redis는 서버 수평 확장이 필요해지는 시점까지 사용하지 않습니다.

---

# 15. 실시간 통신

ASP.NET Core **SignalR**을 사용합니다.

## Client → Server

```text
JoinQueue
CancelQueue

PreviewEdge(MatchId, EdgeId, PreviewSequence)
ConfirmEdge(MatchId, EdgeId, ExpectedRevision, RequestId)

RequestSync

Forfeit
```

## Server → Client

```text
MatchFound
MatchStarted

OpponentPreviewChanged

MatchStateChanged
TurnChanged

OpponentDisconnected
OpponentReconnected

GameFinished
```

## 15.1 Preview 전송 정책

Preview는 게임 상태를 변경하지 않는 임시 정보지만 서버를 경유합니다.

- 자신의 Turn에만 전송할 수 있습니다.
- 클라이언트는 Edge가 실제로 변경됐을 때만 전송합니다.
- 서버는 플레이어당 초당 최대 5회의 Preview만 허용합니다.
- `PreviewSequence`가 이전 값보다 큰 메시지만 상대에게 전달합니다.
- Confirm, Turn 변경, Disconnect, Match 종료 시 Preview를 제거합니다.
- Preview가 유실되어도 Snapshot Revision에는 영향을 주지 않습니다.

Preview 제한 수치는 초기값이며 실제 사용성과 트래픽 측정 후 조정합니다.

## 15.2 Confirm 요청의 멱등성

`ConfirmEdge`에는 클라이언트가 생성한 고유 `RequestId`를 포함합니다.

- 서버는 동일 RequestId가 재전송되면 Move를 다시 실행하지 않습니다.
- 처리 완료된 요청이면 기존 처리 결과 또는 최신 Snapshot을 다시 반환합니다.
- `ExpectedRevision`이 서버 Revision과 다르면 Edge를 적용하지 않고 동기화를 요청합니다.
- 서버 판정이 완료되기 전에는 클라이언트가 해당 Edge를 확정된 상태로 표시하지 않습니다.

이를 통해 모바일 재전송, 버튼 중복 입력, 응답 유실로 같은 수가 두 번 처리되는 문제를 방지합니다.

---

# 16. 서버 권위형 구조

온라인 대전에서 서버를 게임 상태의 최종 권한자로 사용합니다.

클라이언트는:

```text
"17번 Edge를 선택하고 싶다."
```

라고 요청할 뿐입니다.

서버가 판단합니다.

```text
17번 Edge 사용 가능?
현재 플레이어 Turn?
이미 사용된 Edge?
Box 완성?
추가 Turn?
게임 종료?
```

서버 판정 결과를 양쪽 Client에 전달합니다.

---

# 17. 게임 상태 동기화

Dots and Boxes는 전체 데이터 크기가 매우 작기 때문에 Edge 확정 시 전체 Match Snapshot을 전달합니다.

```text
MatchSnapshot

MatchId

Revision

CurrentPlayer

Edges[40]
Boxes[16]

Player1Score
Player2Score

TurnDeadline
MatchState
```

Delta 기반 동기화보다 전체 Snapshot 방식으로 구현하여 상태 불일치를 최소화합니다.

---

# 18. Revision

모든 Match State에는 Revision을 사용합니다.

예:

```text
Revision 10
↓
Edge 확정
↓
Revision 11
```

클라이언트가 비정상적인 Revision을 받으면 서버 상태를 다시 요청합니다.

```text
Client Revision = 10

Server Message = 12

↓

RequestSync()
```

Revision은 Confirm으로 실제 Match 상태가 변경될 때만 증가합니다.

다음 항목은 Revision을 증가시키지 않습니다.

- Preview 변경
- 단순 Ping/Pong
- 남은 시간 표시 갱신
- 연결 상태 알림

클라이언트는 현재 Revision보다 낮은 Snapshot을 무시하고, 두 단계 이상 높은 이벤트를 받으면 `RequestSync()`를 호출합니다. 전체 Snapshot을 적용할 때는 진행 중인 로컬 Preview와 애니메이션 상태를 서버 상태에 맞게 정리합니다.

---

# 19. 재접속

모바일 환경에서는 네트워크 변경이 빈번하기 때문에 재접속 기능을 필수 기능으로 구현합니다.

예:

```text
게임 진행 중
↓
Wi-Fi 연결 해제
↓
SignalR Disconnect
↓
Server에서 Match 유지
↓
Grace Period
↓
Player 재접속
↓
Snapshot 수신
↓
게임 복구
```

재접속 유예 시간 예:

```text
15초
```

시간은 실제 테스트 후 변경합니다.

유예시간을 초과하면 기권 패배 처리합니다.

## 19.1 재접속 식별

재접속은 단순 ConnectionId가 아니라 다음 정보를 기준으로 검증합니다.

```text
AuthenticatedUserId
MatchId
ReconnectToken
```

- SignalR ConnectionId는 재연결 시 변경될 수 있으므로 사용자 식별자로 사용하지 않습니다.
- Match 시작 시 서버가 짧은 수명의 ReconnectToken을 발급합니다.
- 토큰은 해당 사용자와 Match에만 사용할 수 있습니다.
- 재접속 성공 시 이전 연결을 무효화하고 전체 Snapshot을 전달합니다.
- 동일 계정의 중복 접속 정책은 마지막으로 인증된 연결을 활성 연결로 사용합니다.

---

# 20. 서버 MatchRoom

현재 진행 중인 게임은 서버 메모리에 관리합니다.

```text
MatchRoom

MatchId

Player1
Player2

DotsBoard

CurrentTurn

Revision

TurnDeadline

ConnectionState
```

예:

```text
Dictionary<string, MatchRoom>
```

게임 도중 모든 상태를 DB에 지속적으로 기록하지 않습니다.

## 20.1 서버 종료 및 장애 정책

초기 단일 서버 버전에서는 진행 중 Match를 서버 메모리에만 유지합니다.

따라서 예기치 않은 서버 종료가 발생하면 해당 Match는 다음과 같이 처리합니다.

- 승패와 Rating을 반영하지 않는 무효 경기로 처리
- 서버 복구 후 양쪽 플레이어에게 Match 무효 사유 표시
- 운영 로그에 MatchId, 마지막 Revision, 종료 원인 기록
- 정상 배포 시 신규 Matchmaking을 먼저 중단하고 진행 중 Match가 끝난 뒤 서버 종료

이 정책은 초기 포트폴리오 및 소규모 운영 범위의 명시적인 한계입니다. 서버 수평 확장이나 무중단 복구가 필요해지면 Redis 또는 외부 상태 저장소에 Match Snapshot을 주기적으로 저장하는 방식을 별도 검토합니다.

---

# 21. 데이터베이스

PostgreSQL을 사용합니다.

## Users

```text
UserId
Nickname
CreatedAt
LastLoginAt
AccountState
```

## LocalAccounts

아이디와 비밀번호로 가입한 자체 계정 정보입니다.

```text
UserId
LoginId
PasswordHash
RecoveryEmail (선택 또는 출시 전 정책 확정)
EmailVerifiedAt
CreatedAt
```

`PasswordHash`는 ASP.NET Core Identity가 관리하며 원본 비밀번호는 저장하지 않습니다.

## ExternalIdentities

Google 또는 Steam과 같은 외부 계정을 내부 UserId에 연결합니다.

```text
ExternalIdentityId
UserId
Provider
ProviderSubject
CreatedAt
LastAuthenticatedAt
```

Provider 예시:

```text
GOOGLE
STEAM
```

`(Provider, ProviderSubject)` 조합은 고유해야 하며 하나의 외부 계정을 여러 UserId에 연결할 수 없습니다.

## PlayerRank

```text
UserId

Rating

WinCount
LoseCount
DrawCount

HighestRating
CurrentSeasonId
```

## MatchHistory

```text
MatchId

Player1Id
Player2Id

Player1Score
Player2Score

WinnerId (무승부 시 null)
IsDraw
FinishReason

Player1RatingBefore
Player1RatingAfter

Player2RatingBefore
Player2RatingAfter

StartedAt
FinishedAt
```

## Seasons

```text
SeasonId
Name
StartedAt
EndedAt
State
```

## 21.1 데이터 무결성

- `Users.Nickname`은 정책에 따라 중복 여부와 금칙어를 검증합니다.
- `PlayerRank`는 `(UserId, CurrentSeasonId)` 조합을 고유 키로 사용합니다.
- `MatchHistory.MatchId`는 고유 키로 사용하여 결과가 중복 저장되지 않도록 합니다.
- Rating 변경과 MatchHistory 저장은 하나의 DB Transaction에서 처리합니다.
- 낙관적 동시성 또는 행 잠금을 사용해 동일 사용자의 Rating이 동시에 덮어써지지 않도록 합니다.
- DB Transaction 실패 시 클라이언트에 성공 결과를 먼저 확정하지 않습니다.

## 21.2 인증 및 기본 보안

게임 서버는 플랫폼 계정을 게임 데이터의 기본 키로 직접 사용하지 않습니다. 모든 로그인 수단을 서버 내부의 `UserId`에 연결하고, 인증 성공 후 게임 서버 전용 Access Token을 발급합니다.

### Android 로그인

Android 버전은 다음 두 가지 게임 계정 로그인 방식을 제공합니다.

1. **Google 계정 로그인**
2. **아이디와 비밀번호를 사용하는 자체 회원가입 및 로그인**

Google Play Games Services의 플랫폼 인증과 게임 내부 계정 인증은 구분합니다.

- Google 계정 로그인은 게임 내부 계정의 외부 인증 수단으로 사용합니다.
- Google Play Games Services는 플랫폼 프로필, 업적, Google Play 기능 연동에 사용합니다.
- Play Games Services 플랫폼 인증은 게임 시작 시 백그라운드에서 시도하며, 실패하더라도 아이디·비밀번호 게임 계정 로그인은 차단하지 않습니다.
- Play Games Services Player ID를 검증 없이 게임 서버 UserId로 저장하지 않습니다.
- Client가 받은 Google 인증 정보를 서버로 전달하고 서버가 유효성을 검증한 뒤 내부 UserId와 연결합니다.

자체 계정은 ASP.NET Core Identity를 사용합니다.

- LoginId 중복 확인
- 비밀번호 길이 및 복잡도 정책
- 반복 로그인 실패 제한
- 안전한 Password Hash와 Salt 관리
- 비밀번호 원문 저장 및 로그 기록 금지
- 계정 복구를 제공할 경우 검증된 이메일을 통해서만 처리

### PC Steam 로그인

PC 버전은 Steam 출시 단계에서 Steam 계정 로그인을 사용합니다.

```text
Steam Client
↓
Steam Session Ticket 발급
↓
Game Server 전달
↓
Steamworks API로 Ticket과 소유권 검증
↓
SteamID를 내부 UserId와 연결
↓
게임 서버 Access Token 발급
```

- Client가 전달한 SteamID 문자열만 신뢰하지 않습니다.
- 서버가 Steam 인증 Ticket을 검증한 뒤 사용자와 게임 소유권을 확인합니다.
- Steam 인증 코드는 Android 1차 버전의 구현 범위에 포함하지 않습니다.

### 계정 연결

하나의 내부 UserId에 자체 계정, Google 계정, Steam 계정을 연결할 수 있는 구조를 사용합니다.

- 사용자가 기존 계정으로 로그인한 상태에서 새로운 인증 수단을 직접 연결
- 연결하려는 외부 계정도 다시 인증
- 닉네임이나 이메일이 같다는 이유로 계정을 자동 병합하지 않음
- 이미 다른 UserId에 연결된 외부 계정은 연결 거부
- 계정 연결 해제 후에도 최소 하나의 로그인 수단 유지
- 계정 병합 기능은 초기 Android 출시 범위에서 제외

PC판에서 Android 진행 상황을 이어서 사용하는 기능은 Steam 계정과 기존 Android 계정의 연결 기능이 완성된 뒤 제공합니다.

PC에서의 연결 절차:

```text
Steam 계정으로 PC판 로그인
↓
계정 연결 메뉴 선택
↓
기존 Android 계정 재인증
├─ Google 계정 인증
└─ 아이디·비밀번호 인증
↓
서버가 두 계정의 소유권 확인
↓
Steam ExternalIdentity를 기존 UserId에 연결
```

연결하지 않은 Steam 사용자는 새로운 내부 UserId로 시작합니다. 연결 전 양쪽 계정에 서로 다른 진행 상황이 존재하는 경우 자동 병합하지 않으며, 별도 계정 병합 정책이 마련되기 전에는 연결을 제한합니다.

### 공통 보안 원칙

- 모든 랭크 요청은 인증된 UserId를 기준으로 처리
- 클라이언트가 전달한 UserId, Rating, 승패 값은 신뢰하지 않음
- Access Token 만료 및 재발급 정책 적용
- 로그인, Queue, Preview, Confirm 요청별 Rate Limit 적용
- 서버 로그에 인증 토큰과 비밀번호를 기록하지 않음
- 운영 환경에서 TLS 적용
- DB 계정과 비밀 값은 저장소에 커밋하지 않고 환경 변수 또는 Secret으로 주입

초기 Android 버전은 Google 계정 또는 자체 계정 로그인을 요구합니다. 게스트 로그인은 필수 범위에서 제외합니다.

---

# 22. 게임 종료 처리

게임 종료 시 서버에서만 결과를 저장합니다.

```text
Game Finished
      ↓
승자 또는 무승부 계산
      ↓
Rating 계산
      ↓
DB Transaction
      ↓
MatchHistory 저장
      ↓
Player Rating 변경
      ↓
Client 결과 전달
```

클라이언트가 승패나 Rating을 직접 수정할 수 없도록 합니다.

---

# 23. 게임 데이터 구조

게임 규칙은 Unity와 분리된 순수 C# 프로젝트로 작성합니다.

```text
DotsAndBoxes.Shared

├─ DotsBoard
├─ EdgeData
├─ BoxData
├─ MatchState
├─ MoveResult
└─ DotsRule
```

UnityEngine에 의존하지 않습니다.

이를 통해 동일한 게임 규칙을:

```text
Unity Client
AI
ASP.NET Server
Unit Test
```

에서 공통으로 사용할 수 있도록 합니다.

---

# 24. Edge 구조

```text
EdgeData

EdgeId
OwnerPlayerIndex
IsConfirmed
```

Edge는 Unity상의 좌표나 Transform을 게임 데이터에 저장하지 않습니다.

UI와 게임 데이터를 분리합니다.

---

# 25. Box 구조

```text
BoxData

TopEdgeId
BottomEdgeId
LeftEdgeId
RightEdgeId

OwnerPlayerIndex
```

각 Edge는 자신과 연결된 Box를 최대 두 개까지 참조하도록 사전에 구성합니다.

이를 통해 Edge 확정 시 전체 Box를 검사하지 않고 인접 Box만 검사합니다.

---

# 26. Move 처리

```text
TryConfirmEdge
      ↓
Turn 검사
      ↓
Edge 검사
      ↓
Edge 확정
      ↓
인접 Box 검사
      ↓
Box 획득
      ↓
추가 Turn 판단
      ↓
Game End 검사
```

Move 결과:

```text
MoveResult

IsValid
EdgeId

CompletedBoxes

HasExtraTurn

IsGameFinished
```

---

# 27. 모바일 UI

## 메인 화면

```text
[게임 로고]

[랭크전]

[일반전]

[AI 대전]

[랭킹]

[프로필]

[설정]
```

---

# 28. 인게임 UI

기본 구성:

```text
┌────────────────────────┐

 Player A        Player B
    5               4

        Turn 18

       ●   ●   ●
       │
       ●   ●   ●

       [확정]

      남은 시간 14

└────────────────────────┘
```

화면에서 항상 확인 가능해야 하는 정보:

- 양쪽 Score
- 현재 Turn
- 선택한 Edge
- 상대 Preview
- Turn Timer
- 네트워크 연결 상태

---

# 29. Edge 터치

모바일 특성상 실제 표시 영역보다 터치 영역을 크게 설정합니다.

```text
Visible Line

──────

Touch Area

══════
```

Edge 선택 시:

- 선택 Edge 강조
- 다른 Edge 선택 가능
- 확정 버튼 활성화

---

# 30. 색상 표현

색상은 최종 디자인 단계에서 결정합니다.

논리적 구분:

```text
사용 가능 Edge

내 Preview Edge

상대 Preview Edge

Player 1 확정 Edge

Player 2 확정 Edge

Player 1 Box

Player 2 Box
```

색상 외에도 투명도, 애니메이션, 외곽선 등을 활용해 색약 사용자도 구분할 수 있도록 고려합니다.

---

# 31. 연출

게임 자체가 정적인 보드게임이므로 최소한의 시각 피드백을 제공합니다.

### Edge Preview

선이 부드럽게 나타남.

### Edge Confirm

Edge가 빠르게 채워지며 고정됩니다.

### Box Capture

```text
Edge 완성
↓
Box Flash
↓
Player 색상으로 채워짐
↓
Score +1
```

### 연속 획득

Chain이 발생한 경우 획득 템포를 빠르게 유지하여 연속 획득의 쾌감을 강조합니다.

---

# 32. 사운드

필수 효과음:

- Edge 선택
- Edge 선택 변경
- Edge 확정
- Box 획득
- 여러 Box 동시 획득
- Turn 변경
- Match Found
- Victory
- Defeat
- Timer 경고

BGM은 게임 집중을 방해하지 않는 가벼운 음악을 사용합니다.

---

# 33. UI MVP(Model-View-Presenter) 구조

UI는 MVP 패턴을 적용합니다.

```text
GameModel_model
        ↓
GamePresenter_presenter
        ↓
GameBoard_view
```

## GameModel_model

- 현재 Board
- Score
- Turn
- Preview 정보
- Match 정보

## GamePresenter_presenter

- 사용자 입력 처리
- Network 요청
- 서버 Snapshot 반영
- View 상태 제어

## GameBoard_view

- Board 표시
- Edge 표시
- Preview 표시
- Box 애니메이션
- Score UI
- Turn UI

---

# 34. 게임 진행 Flow

## 온라인

```text
게임 실행
↓
Google Play Games Services 플랫폼 인증 백그라운드 시도
↓ (성공 여부와 별도로 진행)
게임 계정 로그인 선택
├─ Google 계정 로그인
└─ 아이디·비밀번호 로그인 또는 회원가입
↓
게임 서버 Access Token 발급
↓
메인 메뉴
↓
랭크전 선택
↓
Matchmaking
↓
Match Found
↓
GameRoom 입장
↓
선공 결정
↓
게임 진행
↓
Game Finished
↓
Rating 변경
↓
Result
↓
메인 메뉴
```

---

# 35. 핵심 개발 우선순위

## Phase 1 — Core

- Board 생성
- Edge 데이터
- Box 데이터
- Edge 선택
- Edge 확정
- Box 판정
- 추가 Turn
- Score
- Game End

**목표:** 네트워크 없이 게임 한 판이 정상적으로 동작.

---

## Phase 2 — Unity Gameplay

- Board View
- 터치 Input
- Preview
- Confirm
- Box 획득 연출
- Score UI
- Turn UI
- 로컬 2인

**목표:** 모바일에서 완전한 로컬 게임 가능.

---

## Phase 3 — Online

- ASP.NET Core 서버
- SignalR
- Docker
- MatchRoom
- 게임 서버 검증
- 상대 Preview
- Snapshot
- Revision

**목표:** 인터넷을 통해 1:1 정상 플레이.

---

## Phase 4 — Network Stability

- 재접속
- 연결 끊김
- Grace Period
- 기권
- Turn Timer
- AFK 처리

**목표:** 실제 모바일 환경에서도 게임 유지.

---

## Phase 5 — Account / Rank

- Android Google 계정 로그인
- 아이디·비밀번호 회원가입 및 로그인
- 내부 UserId와 외부 계정 연결
- Access Token 및 인증 갱신
- PostgreSQL
- 전적
- Elo Rating
- Rank
- Matchmaking
- Leaderboard

**목표:** 실제 서비스 가능한 경쟁 구조 구현.

---

## Phase 6 — AI

- Easy
- Normal
- Hard
- Minimax
- Alpha-Beta
- Chain 판단

---

## Phase 7 — Release Polish

- 튜토리얼
- 설정
- 사운드
- 애니메이션
- 최종 UX
- Android Release AAB 빌드
- Google Play Console 내부 테스트
- Google Play Games Services 설정 및 테스트 계정 검증
- 서버 로그 및 관리 기능
- Crash 대응
- 개인정보처리방침 등 스토어 출시 준비

---

## Phase 8 — Future PC / Steam

Android 버전 출시와 운영 안정성 검증 이후에만 진행합니다.

- Windows PC UI와 마우스 입력 대응
- 해상도 및 화면 비율 대응
- Steamworks SDK 연동
- Steam Session Ticket 서버 검증
- Steam 계정과 내부 UserId 연결
- Steam 배포 빌드와 업적 연동 검토
- Android와 PC의 프로토콜 버전 호환성 검증
- Cross-play 및 계정 진행 상황 공유 여부 결정

**목표:** Android 서버 구조를 재사용하면서 Steam 계정 기반 PC 버전으로 확장 가능성을 검증.

---

# 36. 개발 완료 기준과 공개 범위

## 36.1 포트폴리오 Vertical Slice

전체 서비스 기능을 한 번에 구현하지 않고, 먼저 다음 범위를 완성해 핵심 기술을 검증합니다.

### 포함

- 4 × 4 핵심 게임 규칙
- 로컬 2인 대전
- Normal 수준의 기본 AI
- 서버 권위형 실시간 일반전
- Preview와 Confirm 분리
- Snapshot과 Revision 동기화
- Turn Timer
- 재접속과 Grace Period
- Docker 기반 단일 서버 배포
- 핵심 단위 테스트 및 서버 통합 테스트

### 제외

- 시즌과 티어
- Leaderboard
- Hard AI 완전 구현
- 친구 초대, 관전, Replay
- Cosmetic과 결제
- 다중 Game Server 및 무중단 Match 복구
- PC 및 Steam 연동

Vertical Slice의 목적은 실제 서비스 기능을 모두 갖추는 것이 아니라, 게임 한 판의 전체 흐름과 온라인 안정성을 증명하는 것입니다.

## 36.2 1차 서비스 공개 범위

Vertical Slice 검증이 끝난 뒤 **Android 전용 Google Play Store 버전**으로 다음 기능을 추가합니다.

### 필수

- 4 × 4 Dots and Boxes
- Google 계정 로그인
- 아이디·비밀번호 회원가입 및 로그인
- AI 대전
- 실시간 일반전
- 실시간 랭크전
- 상대 Edge Preview
- MMR
- 티어
- Leaderboard
- 전적
- Turn Timer
- 기권
- 재접속

### 후순위

- 친구 초대
- 이모티콘
- 3 × 3 / 5 × 5 보드
- 관전
- 리플레이
- 커스텀 룸
- 스킨
- 시즌 보상
- Windows PC 및 Steam 출시
- Steam 계정 연결
- Android-PC Cross-play

---

# 37. 차후 확장 기능

서비스가 안정된 이후 다음 기능을 검토합니다.

### Windows PC / Steam

Android 버전의 규칙 및 서버를 재사용하고 Steam을 통해 Windows PC 버전을 배포합니다.

추가 개발 범위:

- 마우스 입력과 PC용 UI
- 창 모드와 전체 화면
- 다양한 해상도
- Steam Overlay와 Steamworks 초기화
- Steam Session Ticket 인증
- Steam 업적 및 친구 기능 검토
- Android 계정 진행 상황 연결

PC판의 일반 로그인은 Steam 계정만 사용합니다. 다만 Android 진행 상황을 연결할 때는 계정 연결 메뉴에서 기존 Google 계정 또는 아이디·비밀번호를 한 번 재인증합니다.

Android와 PC 간 Cross-play는 기술적으로 가능한 구조를 유지하되 다음 조건을 검증한 후 도입합니다.

- Client와 Server 프로토콜 버전 일치
- 양 플랫폼의 게임 규칙 버전 일치
- 계정 연결 및 제재 정책 통합
- 플랫폼별 업데이트 심사 시점 차이에 대한 호환 정책

PC 확장은 Android 출시를 지연시키지 않도록 별도 단계로 유지합니다.

### 친구전

Room Code를 이용한 비공개 매치.

### Replay

Match에서 발생한 Edge 확정 순서만 저장합니다.

```text
17
22
18
26
...
```

게임이 결정론적으로 동작하므로 전체 영상 데이터를 저장하지 않고도 재생할 수 있습니다.

### Spectator

서버 Match Snapshot을 구독하여 관전 기능 제공.

### Cosmetic

- Dot Theme
- Board Theme
- Edge Effect
- Box Capture Effect
- Profile Frame

게임 밸런스에 영향을 주지 않는 Cosmetic 중심으로 구성합니다.

---

# 38. 핵심 개발 원칙

### 1. 게임 로직과 Unity View를 분리한다.

Unity Transform이나 GameObject가 게임 규칙을 결정하지 않습니다.

### 2. 서버가 게임의 최종 상태를 결정한다.

클라이언트 요청은 항상 검증합니다.

### 3. Preview와 Confirm을 분리한다.

Preview는 사용자 의사를 보여주는 실시간 연출이고 Confirm만 실제 게임 상태를 변경합니다.

### 4. 온라인 상태는 Snapshot 기반으로 복구 가능해야 한다.

모바일 네트워크 단절을 기본 상황으로 가정합니다.

### 5. 과도한 서버 구조를 만들지 않는다.

초기에는:

```text
ASP.NET Core
+
SignalR
+
PostgreSQL
+
Docker
```

만으로 구성합니다.

Redis나 다중 Game Server는 실제 확장 필요성이 발생했을 때 도입합니다.

---

# 39. 프로젝트의 핵심 포인트

본 프로젝트는 단순한 보드게임 구현이 아니라 다음을 보여주는 것을 목표로 합니다.

> **Unity 기반 모바일 게임 + 순수 C# 게임 로직 + AI + 실시간 통신 + 서버 권위형 멀티플레이 + Docker 기반 자체 서버 + DB + Matchmaking + Ranking**

게임 자체의 규칙은 간단하게 유지하고, 완성도 높은 모바일 UX와 안정적인 온라인 시스템을 프로젝트의 핵심 경쟁력으로 삼습니다.

---

# 40. 상태 머신

화면 상태, 네트워크 연결 상태, Match 진행 상태를 하나의 열거형으로 합치지 않습니다.

## 40.1 클라이언트 세션 상태

```text
STARTING
↓
AUTHENTICATING
↓
MAIN_MENU
↓
QUEUEING
↓
MATCH_LOADING
↓
PLAYING ←→ RECONNECTING
↓
RESULT
↓
MAIN_MENU
```

각 상태에서 허용되는 사용자 입력을 제한합니다.

| 상태 | 허용되는 주요 동작 |
|---|---|
| MAIN_MENU | 모드 선택, 프로필, 설정 |
| QUEUEING | Queue 취소 |
| MATCH_LOADING | 초기 Snapshot 수신 대기 |
| PLAYING | Preview, Confirm, Forfeit |
| RECONNECTING | 재접속 취소, 연결 상태 확인 |
| RESULT | 결과 확인, 메인 메뉴 이동 |

## 40.2 서버 Match 상태

```text
CREATED
↓
WAITING_FOR_PLAYERS
↓
ACTIVE
↓
FINISHING
↓
FINISHED
```

정상 완료할 수 없는 경우 `ABORTED`로 전환합니다.

- `ACTIVE`에서만 Preview와 Confirm을 허용합니다.
- 플레이어 한 명이 Disconnect되어도 Match 상태는 Grace Period 동안 `ACTIVE`로 유지합니다.
- `FINISHING`에서는 결과 계산과 DB Transaction을 수행하며 추가 Move를 받지 않습니다.
- 결과 저장이 완료된 뒤에만 `FINISHED`로 전환합니다.
- 서버 장애, 관리자 종료 등 결과를 확정할 수 없는 경우 `ABORTED`로 전환하고 Rating을 반영하지 않습니다.

## 40.3 동시성 원칙

각 MatchRoom의 상태 변경은 한 번에 하나의 명령만 처리되도록 직렬화합니다.

- 동일 Match의 Confirm을 동시에 병렬 처리하지 않음
- Match 단위 Lock 또는 단일 처리 Queue 사용
- DB Transaction을 기다리는 동안 새 Move 차단
- Match 종료 요청과 Confirm이 경쟁할 경우 먼저 확정된 서버 상태를 기준으로 처리
- 처리 순서를 MatchId, RequestId, Revision과 함께 로그에 기록

---

# 41. 비기능 요구사항

다음 수치는 출시 보장이 아니라 **초기 검증 목표**입니다. 측정 결과와 개인 서버 환경에 따라 조정합니다.

## 41.1 정확성

- 하나의 Edge가 두 플레이어에게 중복 소유되지 않음
- 하나의 Box가 두 플레이어에게 중복 지급되지 않음
- 동일 RequestId가 여러 번 도착해도 Move가 한 번만 적용됨
- 게임 결과와 Rating이 중복 저장되지 않음
- 동일 Move 기록으로 Replay 시 동일한 최종 상태가 생성됨

## 41.2 성능

- 서버 내부 Confirm 처리 시간 `p95 100ms 이하`
- 국내 일반 네트워크 기준 Confirm 요청부터 Snapshot 반영까지 `p95 300ms 이하`를 목표로 측정
- Android 기준 Edge 터치 후 로컬 Preview 표시 `100ms 이하`
- Hard AI 한 Turn 최대 `1초`

네트워크 왕복 시간과 서버 내부 처리 시간은 분리해 측정합니다.

## 41.3 부하 검증

N100 Mini PC 초기 목표:

```text
동시 연결 100명
동시 Match 50개
Match당 Preview 포함 실시간 메시지 처리
```

CPU, Memory, 메시지 처리량, DB 응답시간과 오류율을 기록합니다. 목표를 달성하지 못하면 수치를 숨기지 않고 병목 원인과 개선 결과를 포트폴리오에 포함합니다.

## 41.4 모바일 지원

- 최초 출시 플랫폼은 Android로 고정
- Android 최소 지원 버전은 실제 빌드 및 사용 패키지 확정 후 명시
- Google Play Store에는 APK가 아니라 Android App Bundle(AAB)을 기준으로 배포
- Debug, 내부 테스트, Release 서명 인증서를 구분하고 Google 인증 설정에 각각 등록
- 세로 화면을 기본 방향으로 사용
- Safe Area 대응
- 저사양, 중간 사양, 고사양 실기기에서 확인
- Wi-Fi, LTE/5G 전환과 일시적인 네트워크 손실 확인
- 백그라운드 전환 및 복귀 시 재접속 확인

## 41.5 접근성

- 색상만으로 Player와 Edge 상태를 구분하지 않음
- 선 패턴, 외곽선, 아이콘을 함께 사용
- 진동과 사운드를 개별 설정 가능
- 애니메이션 감소 옵션 검토
- 터치 영역은 표시 선보다 크게 유지하고 오입력률을 측정

---

# 42. 테스트 전략

## 42.1 게임 규칙 단위 테스트

필수 테스트:

- 첫 Edge 확정
- 이미 사용된 Edge 재선택 거부
- 잘못된 Player Turn 거부
- 한 개 Box 완성
- 한 Edge로 두 개 Box 동시 완성
- Box 획득 시 추가 Turn
- Box 미획득 시 Turn 변경
- 마지막 Edge 확정과 게임 종료
- 8:8 무승부
- 결정론적 Replay

보드의 불변 조건도 검사합니다.

```text
확정 Edge 수는 감소하지 않는다.
획득 Box 수는 감소하지 않는다.
Player1Score + Player2Score = 소유자가 있는 Box 수
게임 종료 시 모든 Box의 소유자가 결정되어 있다.
```

## 42.2 서버 통합 테스트

- 두 Client의 Queue 및 Match 생성
- Turn이 아닌 Client의 Confirm 거부
- 중복 RequestId 처리
- 오래된 ExpectedRevision 거부
- Preview Rate Limit
- Disconnect 후 Grace Period 내 재접속
- Grace Period 초과 기권
- Match 종료와 Rating Transaction
- 동일 Match 결과 중복 저장 방지

## 42.3 네트워크 장애 테스트

다음 상황을 인위적으로 재현합니다.

- 메시지 지연
- 메시지 순서 역전
- 응답 유실 후 요청 재전송
- Wi-Fi와 모바일 네트워크 전환
- 앱 백그라운드 전환
- 서버 연결 일시 중단
- DB 연결 실패

각 테스트에서 최종적으로 양쪽 Client와 Server의 Revision, Edge, Box, Score가 일치하는지 확인합니다.

## 42.4 부하 테스트

- Queue 진입과 취소 반복
- 다수 Match 동시 생성
- Preview 최대 빈도 전송
- 모든 Match에서 Confirm 반복
- 게임 종료 시 DB Transaction 집중

측정 항목:

- 요청 처리량
- 평균 및 p95/p99 응답시간
- SignalR 연결 수
- CPU 및 Memory
- DB Connection 수
- 오류율과 재접속 성공률

## 42.5 실제 기기 UX 테스트

- Edge 오터치율
- 확정 버튼 오입력 여부
- 상대 Preview와 내 Preview 구분 가능 여부
- 작은 화면에서 Score와 Timer 가독성
- 연속 Box 획득 연출 중 입력 잠금 여부
- 네트워크 상태 표시 이해도

---

# 43. 단계별 완료 조건

기능이 화면에서 한 번 동작한 것만으로 완료 처리하지 않습니다.

## Core 완료 조건

- UnityEngine에 의존하지 않는 규칙 프로젝트 완성
- 주요 규칙 단위 테스트 통과
- 4 × 4 게임을 시작부터 종료까지 진행 가능
- 무승부와 두 Box 동시 획득 처리

## Unity Gameplay 완료 조건

- Android 실제 기기에서 로컬 한 판 완료
- 모든 Edge를 안정적으로 터치 가능
- Preview, Confirm, Box 연출과 상태 데이터가 분리됨
- 화면 크기와 Safe Area 검증

## Online 완료 조건

- 서로 다른 네트워크의 두 Android 기기에서 한 판 완료
- Server만 게임 상태를 변경함
- 중복 요청과 Revision 불일치 테스트 통과
- Client 간 최종 Snapshot 일치

## Network Stability 완료 조건

- 네트워크 전환 후 Grace Period 내 복구
- 재접속 시 전체 Snapshot 적용
- 중복 연결과 만료된 ReconnectToken 거부
- Timeout과 기권 정책 통합 테스트 통과

## Account / Rank 완료 조건

- Google 계정으로 신규 내부 UserId 생성 및 재로그인 가능
- 아이디·비밀번호로 회원가입, 로그인 및 로그아웃 가능
- 동일 외부 계정이 여러 UserId에 중복 연결되지 않음
- Google 인증 정보가 서버에서 유효하지 않으면 로그인 거부
- 인증되지 않은 사용자의 랭크 요청 거부
- Match 결과와 Rating의 원자적 저장
- 무승부 Elo 반영
- 중복 결과 저장 방지
- Leaderboard 조회 성능 측정

## Release 완료 조건

- Android Release 빌드 설치 및 실행
- Google Play Console 내부 테스트 Track에서 AAB 설치 및 실행
- Google 계정과 자체 계정 로그인 모두 실제 기기에서 검증
- Google Play Games Services 테스트 계정과 Release 서명 인증서 검증
- 서버 재배포 절차와 롤백 절차 문서화
- 로그에 비밀 정보가 남지 않는지 확인
- 개인정보처리방침과 계정 삭제 정책 준비
- 알려진 문제와 미구현 범위 공개

---

# 44. 운영 및 관측

## 44.1 로그

구조화 로그에 다음 식별자를 포함합니다.

```text
TimestampUtc
LogLevel
EventName
UserId
MatchId
RequestId
Revision
ConnectionId
```

인증 토큰, 비밀번호, DB 비밀번호는 기록하지 않습니다.

주요 EventName 예시:

```text
MATCH_CREATED
PLAYER_JOINED
EDGE_CONFIRMED
REVISION_MISMATCH
PLAYER_DISCONNECTED
PLAYER_RECONNECTED
MATCH_FINISHED
MATCH_ABORTED
RATING_UPDATED
```

## 44.2 상태 확인

- 서버 Process 상태를 확인하는 Health Check
- DB 연결 상태를 확인하는 Readiness Check
- SignalR 연결 수
- 진행 중 Match 수
- Queue 대기 인원
- 평균 Match 시간
- 재접속 성공률
- 비정상 종료 Match 수

## 44.3 배포와 백업

- Docker Image에 버전 Tag 사용
- 배포 전 DB Migration 확인
- PostgreSQL 정기 백업
- 정상 배포 전 Queue 진입 중단
- 진행 중 Match 종료 후 서버 교체
- 문제 발생 시 이전 Docker Image로 롤백

---

# 45. 튜토리얼 및 오류 UX

## 45.1 최초 튜토리얼

튜토리얼은 설명문보다 직접 입력하는 짧은 단계로 구성합니다.

1. 비어 있는 Edge 선택
2. Preview 변경
3. Confirm
4. Box 완성 및 추가 Turn
5. 상대에게 세 번째 Edge를 넘길 때의 위험 설명
6. 간단한 Chain 예시

튜토리얼은 다시 볼 수 있으며, 이미 규칙을 아는 사용자는 건너뛸 수 있습니다.

## 45.2 온라인 오류 표시

사용자에게 내부 오류 코드를 그대로 노출하지 않습니다.

| 상황 | 사용자 표시 |
|---|---|
| Queue 서버 연결 실패 | 연결을 확인한 뒤 다시 시도하도록 안내 |
| Revision 불일치 | 최신 게임 상태를 동기화하는 중이라고 표시 |
| 상대 Disconnect | 상대 재접속 대기 시간 표시 |
| 본인 Disconnect | 입력을 잠그고 재접속 진행 표시 |
| Match 무효 | Rating이 변경되지 않았음을 명시 |
| DB 결과 저장 지연 | 결과 확인 중이며 중복 입력이 필요 없음을 안내 |

---

# 46. 주요 위험과 대응

| 위험 | 영향 | 초기 대응 |
|---|---|---|
| 전체 기능 범위 과다 | 완성도 저하, 일정 지연 | Vertical Slice를 먼저 완료 |
| 상대 Preview 도배 | UX 방해, 트래픽 증가 | Rate Limit과 Sequence 적용 |
| Hard AI 탐색 폭발 | 모바일 멈춤, 응답 지연 | 시간 제한, 탐색 깊이, 캐시 적용 |
| 개인 서버 장애 | 진행 Match 유실 | 무효 경기 정책과 Drain 배포 |
| 낮은 동시 접속자 | 랭크 매칭 실패 | 초기에는 일반전 중심 테스트, 탐색 범위 조정 |
| 선공/후공 불균형 | 경쟁 공정성 저하 | 승률 데이터 수집 후 정책 결정 |
| 기능은 많지만 증거 부족 | 포트폴리오 설득력 저하 | 테스트 결과와 수치 기록 |
| Google 계정과 자체 계정 중복 생성 | 진행 상황 분리, 문의 증가 | 명시적 계정 연결과 자동 병합 금지 |
| Google 인증과 Play Games 인증 혼동 | 잘못된 사용자 식별 | 게임 계정 인증과 플랫폼 인증 분리 |
| Steam 확장 조기 병행 | Android 일정 지연 | Android 출시 후 별도 Phase로 진행 |
| 플랫폼 업데이트 시점 차이 | Cross-play 접속 불가 | 프로토콜 버전 호환 정책 적용 |

---

# 47. 포트폴리오 결과물

최종 포트폴리오는 다음 자료를 포함합니다.

## 필수 결과물

- 실제 Android 플레이 영상
- Google Play 내부 테스트 또는 설치 가능한 Release AAB/APK 결과
- Google 계정 및 자체 계정 로그인 흐름 영상
- Client, Server, DB 관계를 보여주는 아키텍처 다이어그램
- 한 Turn의 Preview와 Confirm Sequence Diagram
- 재접속 Sequence Diagram
- 게임 규칙 단위 테스트 결과
- 서버 통합 및 부하 테스트 결과
- Docker 배포 구성과 운영 화면
- 주요 문제 해결 사례 2개 이상

## 문제 해결 사례 작성 형식

```text
문제
↓
재현 조건
↓
원인
↓
검토한 대안
↓
선택한 해결책과 이유
↓
테스트 결과
↓
남아 있는 한계
```

## 최종 회고

- 최초 계획과 실제 구현 범위의 차이
- 예상보다 어려웠던 부분
- 측정 결과에 따라 변경한 설계
- 현재 구조가 감당할 수 있는 규모
- 사용자가 늘어날 경우 먼저 개선할 부분
- 다시 개발한다면 바꿀 선택

구현하지 않은 기능은 구현한 것처럼 표현하지 않으며, 구조 확인만 끝난 항목과 실제 모바일·서버 환경에서 검증한 항목을 구분해 기록합니다.
