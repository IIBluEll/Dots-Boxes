# Dots & Boxes Core 스크립트 설명서

## 1. 문서 목적

이 문서는 `Assets/01_Main/02_Scripts/Shared`에 있는 테스트 이외의 Core 스크립트를 설명합니다.

현재 Core의 책임은 4×4 Dots and Boxes 게임에서 다음 내용을 처리하는 것입니다.

- 4×4 Box 보드의 Edge와 Box ID 구성
- Edge와 Box의 소유 상태 보관
- 현재 플레이어의 턴 관리
- Edge 확정 요청 검증
- Box 완성 및 소유권 판정
- Box 획득에 따른 추가 턴 처리
- 점수, 게임 종료, 승패 및 무승부 판정

이 코드는 `DotsAndBoxes.Shared` Assembly Definition에 포함되며 `No Engine References`가 활성화되어 있습니다. 따라서 `MonoBehaviour`, `GameObject`, `Transform` 같은 `UnityEngine` 기능에 의존하지 않습니다.

같은 규칙 코드를 이후 Unity Client, AI, ASP.NET Core Server에서 공통으로 사용하는 것이 현재 구조의 목적입니다.

---

## 2. 전체 구조

```text
사용자 또는 서버의 Edge 확정 요청
              │
              ▼
        DotsRule.TryConfirmEdge()
              │
              ├─ 요청 유효성 검사
              ├─ EdgeData 확정
              ├─ 인접 Box 검색
              ├─ BoxData 소유권 부여
              └─ 턴 변경 또는 추가 턴 유지
              │
              ▼
          MoveResult 반환

DotsBoard
├─ EdgeData[40]
├─ BoxData[16]
├─ 현재 플레이어
├─ 점수
└─ 게임 결과

BoardTopology
├─ Edge ID 계산
├─ Box ID 계산
├─ Box를 구성하는 Edge 계산
└─ Edge에 인접한 Box 계산
```

### 파일 구성

| 구분 | 파일 | 책임 |
|---|---|---|
| Board | `BoardTopology.cs` | 보드의 고정된 ID와 연결 관계 계산 |
| Board | `EdgeData.cs` | Edge 하나의 상태 보관 |
| Board | `BoxData.cs` | Box 하나의 구성과 소유 상태 보관 |
| Board | `DotsBoard.cs` | 전체 게임 상태 보관 |
| Rules | `PLAYER_INDEX_ENUM.cs` | 플레이어와 미소유 상태 구분 |
| Rules | `MOVE_ERROR_ENUM.cs` | Edge 요청 실패 원인 구분 |
| Rules | `GAME_RESULT_ENUM.cs` | 진행, 승리, 무승부 구분 |
| Rules | `MoveResult.cs` | 한 번의 수 처리 결과 전달 |
| Rules | `DotsRule.cs` | 실제 게임 규칙 실행 |

---

## 3. Board 스크립트

## 3.1 BoardTopology.cs

### 역할

`BoardTopology`는 보드의 공간 구조를 숫자 ID로 변환합니다.

Core 규칙에는 Unity 좌표나 `Transform`이 없으므로 다음과 같은 질문에 숫자로 답해야 합니다.

- `(row, column)` 위치의 가로 Edge ID는 무엇인가?
- 특정 Box를 구성하는 네 Edge는 무엇인가?
- 특정 Edge를 확정했을 때 검사해야 하는 Box는 무엇인가?

이 계산을 한곳에 모은 클래스가 `BoardTopology`입니다.

### 보드 크기 상수

```csharp
BOX_ROWS = 4
BOX_COLUMNS = 4
DOT_ROWS = 5
DOT_COLUMNS = 5
```

4×4는 Dot이 아니라 Box 개수입니다. Box 네 칸을 만들려면 경계가 다섯 줄 필요하므로 Dot은 5×5가 됩니다.

```text
Box                 4 × 4 = 16개
Dot                 5 × 5 = 25개
Horizontal Edge     5 × 4 = 20개
Vertical Edge       4 × 5 = 20개
전체 Edge                    40개
```

### Edge ID 배치

가로 Edge는 `0~19`를 사용합니다.

```text
 0   1   2   3
 4   5   6   7
 8   9  10  11
12  13  14  15
16  17  18  19
```

계산식은 다음과 같습니다.

```text
row × BOX_COLUMNS + column
```

세로 Edge는 `20~39`를 사용합니다.

```text
20  21  22  23  24
25  26  27  28  29
30  31  32  33  34
35  36  37  38  39
```

계산식은 다음과 같습니다.

```text
HORIZONTAL_EDGE_COUNT + row × DOT_COLUMNS + column
```

가로 Edge와 세로 Edge의 ID 범위를 나누면 `edgeId < 20` 검사만으로 방향을 구분할 수 있습니다.

### BoxEdgeIds

`BoxEdgeIds`는 특정 Box를 구성하는 네 Edge ID를 묶어서 반환하는 읽기 전용 구조체입니다.

```csharp
TopEdgeId
BottomEdgeId
LeftEdgeId
RightEdgeId
```

예를 들어 Box 0은 다음 Edge로 구성됩니다.

```text
Top       0
Bottom    4
Left     20
Right    21
```

구조체를 사용한 이유는 네 개의 연관된 ID를 개별 반환값보다 하나의 의미 있는 결과로 전달하기 위해서입니다.

### AdjacentBoxIDs

`AdjacentBoxIDs`는 Edge 하나에 인접한 Box를 나타냅니다.

```csharp
FirstBoxId
SecondBoxId
Count
```

보드 외곽 Edge는 인접 Box가 하나이고, 내부 Edge는 인접 Box가 두 개입니다.

```text
외곽 Edge: Count = 1, SecondBoxId = -1
내부 Edge: Count = 2
```

이 정보가 있기 때문에 Edge를 확정할 때 Box 16개 전체를 검사하지 않고 해당 Edge 주변의 Box 최대 두 개만 검사할 수 있습니다.

### ValidateRange()

모든 좌표와 ID 계산 전에 유효 범위를 검사합니다. 잘못된 ID가 조용히 다른 Edge나 Box로 변환되는 문제를 막기 위한 방어 코드입니다.

`BoardTopology`은 개발자가 직접 사용하는 기반 API이므로 잘못된 값에는 `ArgumentOutOfRangeException`을 발생시킵니다.

---

## 3.2 EdgeData.cs

### 역할

`EdgeData`는 Edge 하나의 현재 상태를 보관합니다.

```csharp
EdgeId
OwnerPlayerIndex
IsConfirmed
```

### IsConfirmed를 계산하는 이유

`IsConfirmed`는 별도 변수로 저장하지 않고 소유자가 있는지로 계산합니다.

```csharp
OwnerPlayerIndex != PLAYER_INDEX_ENUM.NONE
```

만약 소유자와 확정 여부를 각각 저장하면 다음처럼 모순된 상태가 만들어질 수 있습니다.

```text
OwnerPlayerIndex = PLAYER_ONE
IsConfirmed = false
```

현재 구조에서는 소유자가 생기는 순간 반드시 확정 상태가 되므로 이런 모순이 발생하지 않습니다.

### Confirm()

`Confirm()`은 다음 작업을 수행합니다.

1. 이미 확정된 Edge인지 검사합니다.
2. 실제 플레이어인지 검사합니다.
3. 해당 플레이어를 Edge 소유자로 지정합니다.

접근 제한자가 `internal`이므로 다른 Assembly의 Unity View가 직접 호출할 수 없습니다. 같은 `DotsAndBoxes.Shared` Assembly에 있는 `DotsRule`이 규칙 검증을 끝낸 뒤 호출하도록 제한한 구조입니다.

`Confirm()` 자체에도 중복 확정과 잘못된 플레이어 검사가 있는 이유는 `DotsRule`의 검사만 믿지 않고 데이터 객체도 자신의 불변 조건을 보호하기 위해서입니다.

---

## 3.3 BoxData.cs

### 역할

`BoxData`는 Box 하나의 구성 정보와 소유 상태를 보관합니다.

```csharp
BoxId
TopEdgeId
BottomEdgeId
LeftEdgeId
RightEdgeId
OwnerPlayerIndex
IsOwned
```

### 생성 과정

`BoxData` 생성자는 `BoardTopology.GetBoxEdgeIDs()`를 호출하여 자신의 네 Edge를 결정합니다.

따라서 Box 연결 공식을 여러 파일에 중복 작성하지 않고 `BoardTopology` 하나를 기준으로 사용합니다. ID 공식이 바뀌면 연결 계산도 한곳에서만 수정할 수 있습니다.

### IsOwned를 계산하는 이유

`EdgeData.IsConfirmed`와 같은 원칙으로, Box 소유 여부는 다음 조건에서 계산합니다.

```csharp
OwnerPlayerIndex != PLAYER_INDEX_ENUM.NONE
```

소유자와 소유 여부가 서로 다른 값을 가지는 상태를 방지합니다.

### Claim()

`Claim()`은 완성된 Box의 소유자를 지정합니다.

1. 이미 소유된 Box인지 검사합니다.
2. 실제 플레이어인지 검사합니다.
3. 소유 플레이어를 기록합니다.

이 함수도 `internal`이므로 정상적인 게임 진행에서는 `DotsRule`을 통해서만 호출됩니다.

`BoxData`는 네 Edge가 실제로 완성됐는지 직접 검사하지 않습니다. 그 판단은 여러 객체를 조회해야 하므로 전체 보드를 알고 있는 `DotsRule`이 담당합니다. `BoxData`는 자신의 데이터와 소유 상태만 책임집니다.

---

## 3.4 DotsBoard.cs

### 역할

`DotsBoard`는 한 게임의 전체 상태를 보관하는 중심 객체입니다.

```csharp
EdgeData[40]
BoxData[16]
CurrentPlayerIndex
점수
게임 종료 여부
게임 결과
```

### 생성자

보드를 생성하면 다음 작업이 실행됩니다.

1. 선공 플레이어가 유효한지 검사합니다.
2. ID `0~39`의 Edge 40개를 생성합니다.
3. ID `0~15`의 Box 16개를 생성합니다.
4. 현재 턴을 선공 플레이어로 설정합니다.

기본 선공은 `PLAYER_ONE`이지만 생성자 인수로 `PLAYER_TWO`를 지정할 수 있습니다.

온라인 단계에서는 서버가 무작위로 정한 선공을 생성자에 전달할 수 있습니다. 무작위 결정 자체는 보드 규칙의 책임이 아니므로 `DotsBoard` 안에서 난수를 사용하지 않습니다.

### Edges와 Boxes

내부 배열은 `_edges`, `_boxes`로 보관하고 외부에는 `IReadOnlyList`로 제공합니다.

```csharp
public IReadOnlyList<EdgeData> Edges
public IReadOnlyList<BoxData> Boxes
```

호출자는 Edge와 Box 상태를 조회할 수 있지만 배열의 요소를 새 객체로 교체할 수는 없습니다.

### ConfirmedEdgeCount와 OwnedBoxCount

현재 확정된 Edge와 소유된 Box를 순회하여 계산합니다.

별도 카운터를 저장하지 않기 때문에 실제 객체 상태와 개수가 서로 어긋나는 문제를 피할 수 있습니다.

현재 보드는 Edge 40개와 Box 16개뿐이므로 매번 순회해도 비용이 작습니다. 이후 매우 빈번한 서버 조회에서 성능 문제가 측정될 때만 캐시된 카운터 도입을 검토하는 편이 안전합니다.

### 점수

점수는 별도 정수로 저장하지 않고 해당 플레이어가 소유한 Box 수로 계산합니다.

```csharp
PlayerOneScore
PlayerTwoScore
GetScore(playerIndex)
```

따라서 다음 불변 조건이 구조적으로 유지됩니다.

```text
PlayerOneScore + PlayerTwoScore = OwnedBoxCount
```

### 게임 종료

모든 Box의 소유자가 결정되면 게임이 종료됩니다.

```csharp
OwnedBoxCount == BoardTopology.BOX_COUNT
```

4×4 보드에서는 16개 Box가 모두 소유된 상태입니다.

### GameResult

`GameResult`는 보드의 종료 여부와 점수로부터 계산됩니다.

```text
게임 진행 중                IN_PROGRESS
Player 1 점수가 더 높음     PLAYER_ONE_WIN
Player 2 점수가 더 높음     PLAYER_TWO_WIN
두 점수가 같음              DRAW
```

결과를 별도 변수로 저장하지 않는 이유는 점수와 결과가 불일치하는 상태를 막기 위해서입니다.

### SwitchTurn()

현재 플레이어를 상대 플레이어로 변경합니다.

```text
PLAYER_ONE → PLAYER_TWO
PLAYER_TWO → PLAYER_ONE
```

`internal` 함수이므로 `DotsRule`이 Box 획득 여부를 판단한 뒤에만 호출합니다. Unity View가 임의로 턴을 넘기는 구조가 아닙니다.

---

## 4. Rules 스크립트

## 4.1 PLAYER_INDEX_ENUM.cs

플레이어와 미소유 상태를 구분합니다.

```text
NONE          아직 소유자가 없음
PLAYER_ONE    첫 번째 플레이어
PLAYER_TWO    두 번째 플레이어
```

`NONE`이 `-1`인 이유는 실제 플레이어 인덱스 `0`, `1`과 구분하기 위해서입니다.

Unity에서 표시하는 닉네임이나 계정 ID는 이 enum의 책임이 아닙니다. 이 값은 한 Match 안에서 규칙이 사용하는 두 플레이어의 논리적 위치만 나타냅니다.

---

## 4.2 MOVE_ERROR_ENUM.cs

`TryConfirmEdge()`가 요청을 거부했을 때 그 원인을 나타냅니다.

| 값 | 의미 |
|---|---|
| `NONE` | 오류 없이 처리됨 |
| `INVALID_PLAYER` | `NONE` 등 유효하지 않은 플레이어가 요청함 |
| `NOT_YOUR_TURN` | 현재 턴이 아닌 플레이어가 요청함 |
| `INVALID_EDGE` | Edge ID가 `0~39` 범위를 벗어남 |
| `EDGE_ALREADY_CONFIRMED` | 이미 확정된 Edge를 다시 요청함 |
| `GAME_ALREADY_FINISHED` | 종료된 게임에 추가 요청함 |

예외를 발생시키는 대신 오류 결과를 반환하는 이유는 잘못된 사용자 입력이나 네트워크 요청이 게임 서버 자체의 예외 상황은 아니기 때문입니다.

서버에서는 이 값을 프로토콜 오류 코드로 변환하고, Unity에서는 사용자용 안내 문구로 변환할 수 있습니다.

---

## 4.3 GAME_RESULT_ENUM.cs

게임 전체의 결과를 나타냅니다.

```text
IN_PROGRESS       아직 게임이 끝나지 않음
PLAYER_ONE_WIN    Player 1 승리
PLAYER_TWO_WIN    Player 2 승리
DRAW              무승부
```

이 값은 `DotsBoard.GameResult`에서 현재 상태를 기준으로 계산됩니다.

---

## 4.4 MoveResult.cs

### 역할

`MoveResult`는 Edge 하나를 확정하려고 시도한 결과를 호출자에게 전달합니다.

```csharp
IsValid
Error
EdgeId
CompletedBoxIds
HasExtraTurn
IsGameFinished
```

### 주요 값

`IsValid`는 요청이 실제 게임 상태에 반영됐는지를 나타냅니다.

`Error`는 실패했을 때의 원인입니다. 성공한 경우 `NONE`입니다.

`CompletedBoxIds`는 이번 Edge로 완성된 Box ID 목록입니다. Dots and Boxes에서는 Edge 하나가 최대 두 Box를 동시에 완성할 수 있습니다.

`HasExtraTurn`은 별도로 저장하지 않고 완성된 Box가 하나 이상인지로 계산합니다.

```csharp
_completeBoxIds.Length > 0
```

즉, Box 획득과 추가 턴 여부가 서로 모순될 수 없습니다.

`IsGameFinished`는 이번 요청 처리 직후 게임이 종료됐는지 알려줍니다.

### CreateFailure()

거부된 요청 결과를 생성합니다.

실패 결과에는 완성된 Box가 없으며, 기본적으로 게임은 진행 중인 것으로 반환합니다. 단, `GAME_ALREADY_FINISHED`의 경우 호출부에서 `isGameFinished = true`를 전달합니다.

### CreateSuccess()

성공한 요청의 Edge, 획득한 Box 목록, 게임 종료 여부를 묶어서 반환합니다.

생성자와 팩터리 함수가 `internal`인 이유는 외부 코드가 임의로 성공 결과를 만들어 내는 것을 제한하기 위해서입니다. 정상 결과는 같은 Assembly의 규칙 코드가 생성합니다.

### Unity와 서버에서의 사용 예

```text
IsValid = false
→ 오류에 맞는 안내 표시 또는 최신 Snapshot 요청

IsValid = true
→ Edge 확정 표시
→ CompletedBoxIds에 포함된 Box 연출
→ HasExtraTurn에 따라 턴 UI 갱신
→ IsGameFinished면 결과 화면 표시
```

Unity View는 Box 완성 여부를 다시 계산할 필요가 없습니다. Core가 반환한 결과를 표현하는 데 집중합니다.

---

## 4.5 DotsRule.cs

### 역할

`DotsRule`은 현재 Core에서 게임 상태를 변경하는 핵심 진입점입니다.

외부에서는 다음 함수로 Edge 확정을 요청합니다.

```csharp
DotsRule.TryConfirmEdge(board, playerIndex, edgeId)
```

### 요청 처리 순서

```text
1. Board가 null인지 확인
2. 게임이 이미 종료됐는지 확인
3. 플레이어 값이 유효한지 확인
4. 해당 플레이어의 턴인지 확인
5. Edge ID가 유효한지 확인
6. Edge가 이미 확정됐는지 확인
7. Edge 확정
8. 해당 Edge와 인접한 Box 검사
9. 완성된 Box 소유권 부여
10. Box를 얻지 못했으면 턴 변경
11. MoveResult 반환
```

상태를 바꾸는 `edge.Confirm()`은 모든 요청 검증이 끝난 뒤 실행됩니다. 따라서 실패한 요청이 Edge나 턴 일부만 변경하는 문제를 방지합니다.

### null에서 예외를 발생시키는 이유

잘못된 플레이어, 잘못된 Edge, 중복 요청은 정상적으로 발생할 수 있는 게임 요청이므로 `MoveResult` 실패로 반환합니다.

반면 `board == null`은 호출자가 게임 객체 자체를 전달하지 않은 프로그래밍 오류입니다. 정상적인 사용자 행동으로 발생하는 상황이 아니므로 `ArgumentNullException`으로 빠르게 문제를 드러냅니다.

### ClaimCompletedBoxes()

확정된 Edge에 인접한 Box를 가져와 완성 여부를 확인합니다.

```text
외곽 Edge → Box 1개 검사
내부 Edge → Box 2개 검사
```

임시 배열의 크기가 2인 이유도 Edge 하나가 동시에 완성할 수 있는 Box가 최대 두 개이기 때문입니다.

완성된 Box가 없다면 `Array.Empty<int>()`를 반환합니다. 빈 배열을 매번 새로 할당하지 않고 런타임이 공유하는 빈 배열을 사용합니다.

### TryClaimBox()

다음 조건에서는 Box를 획득하지 않습니다.

- 이미 다른 플레이어가 소유한 Box
- 네 Edge가 아직 모두 확정되지 않은 Box

조건을 통과하면 `BoxData.Claim()`을 호출하고 결과 배열에 해당 Box ID를 추가합니다.

### IsBoxComplete()

Box의 Top, Bottom, Left, Right Edge를 모두 조회하여 확정 여부를 검사합니다.

```text
Top.IsConfirmed
AND Bottom.IsConfirmed
AND Left.IsConfirmed
AND Right.IsConfirmed
```

네 조건이 모두 참일 때만 Box가 완성됩니다.

### 추가 턴 처리

완성된 Box가 하나도 없을 때만 `SwitchTurn()`을 호출합니다.

```text
완성 Box 0개 → 상대 플레이어 턴
완성 Box 1개 → 현재 플레이어 추가 턴
완성 Box 2개 → 현재 플레이어 추가 턴
```

이는 Box를 획득한 플레이어가 계속 진행한다는 Dots and Boxes 규칙을 그대로 표현합니다.

---

## 5. 한 번의 수가 처리되는 예시

Player 1의 턴에 Edge 21을 확정한다고 가정합니다.

```csharp
MoveResult result = DotsRule.TryConfirmEdge(
    board,
    PLAYER_INDEX_ENUM.PLAYER_ONE,
    21);
```

처리 과정은 다음과 같습니다.

1. 게임이 진행 중인지 확인합니다.
2. Player 1의 턴인지 확인합니다.
3. Edge 21이 유효하며 비어 있는지 확인합니다.
4. Edge 21의 소유자를 Player 1로 설정합니다.
5. Edge 21에 인접한 Box 0과 Box 1을 가져옵니다.
6. 각 Box의 네 Edge가 모두 확정됐는지 검사합니다.
7. 완성된 Box를 Player 1 소유로 설정합니다.
8. Box를 하나 이상 얻었다면 Player 1의 턴을 유지합니다.
9. 획득한 Box ID와 현재 게임 종료 여부를 `MoveResult`로 반환합니다.

두 Box가 동시에 완성된 결과 예시는 다음과 같습니다.

```text
IsValid          true
Error            NONE
EdgeId           21
CompletedBoxIds  [0, 1]
HasExtraTurn     true
IsGameFinished   false
```

---

## 6. 현재 설계에서 지키는 원칙

### 게임 규칙과 Unity View 분리

Core에는 `UnityEngine` 참조가 없습니다. Unity UI는 규칙을 결정하지 않고 Core 상태를 화면에 표시합니다.

### 상태 변경 경로 제한

`EdgeData.Confirm()`, `BoxData.Claim()`, `DotsBoard.SwitchTurn()`은 `internal`입니다. 외부 Assembly는 공개된 `DotsRule.TryConfirmEdge()`를 통해 정상적인 규칙 검증을 거쳐야 합니다.

### 중복 상태 최소화

다음 값들은 별도 저장하지 않고 기존 상태에서 계산합니다.

```text
Edge 확정 여부      ← Edge 소유자
Box 소유 여부       ← Box 소유자
점수                ← 소유 Box 수
게임 종료 여부      ← 전체 소유 Box 수
게임 결과           ← 종료 여부와 점수
추가 턴 여부        ← 이번에 완성한 Box 수
```

이 방식은 같은 의미의 변수가 서로 다른 값을 가지는 문제를 줄입니다.

### 실패한 요청의 상태 불변

유효성 검사는 상태 변경 전에 수행됩니다. 따라서 잘못된 턴, 잘못된 Edge, 중복 Edge 요청은 보드 상태를 변경하지 않습니다.

### 결정론적 규칙

현재 Core에는 시간, 난수, 네트워크, Unity 프레임 의존성이 없습니다. 같은 초기 상태에서 같은 순서로 Edge를 확정하면 같은 최종 상태가 나옵니다.

---

## 7. 현재 구현 범위와 아직 없는 기능

현재 구현된 항목은 다음과 같습니다.

- 고정 4×4 Box 보드
- 로컬 게임 상태
- Edge 확정
- Box 한 개 및 두 개 동시 완성
- 점수와 추가 턴
- 게임 종료
- Player 1 승리, Player 2 승리, 무승부 판정
- 잘못된 요청 거부

다음 항목은 아직 구현되지 않았습니다.

- Edge Preview
- Unity Board View와 터치 입력
- 로컬 2인 화면
- AI
- 서버 Snapshot과 Revision
- MatchId와 RequestId
- SignalR 통신
- 재접속과 Turn Timer
- 상태 직렬화 및 복원
- Replay 전용 데이터 구조
- 3×3 또는 5×5 가변 보드

Preview는 실제 게임 상태를 변경하지 않기 때문에 `EdgeData.IsConfirmed`와 분리하여 Unity Gameplay 또는 Match 상태 계층에서 다루는 것이 적절합니다.

Revision과 RequestId도 순수 보드 규칙이 아니라 온라인 Match 명령 처리 계층에서 추가할 대상입니다.

---

## 8. 이후 확장 시 주의점

### 보드 크기 변경

현재 `BoardTopology`의 크기는 상수 4×4로 고정되어 있습니다. 단순히 상수만 변경하면 기존 ID와 네트워크 프로토콜의 의미도 함께 바뀝니다.

여러 보드 크기를 지원할 때는 보드 크기를 인스턴스 데이터로 옮기고 Snapshot에 크기를 포함하는 구조를 별도로 설계해야 합니다.

### Snapshot 복원

현재 `DotsBoard`는 새 게임 생성만 지원합니다. 서버 Snapshot에서 복원하려면 아무 필드나 직접 덮어쓰기보다 다음 내용을 검증하는 복원 경로가 필요합니다.

- Edge ID와 배열 길이
- Edge 소유자 유효성
- Box 소유자와 네 Edge 완성 상태 일치
- 점수 합계와 소유 Box 수 일치
- 현재 플레이어 유효성
- 종료 상태와 결과 일치

### 서버 권위 처리

서버에서도 최종 수 판정은 반드시 `DotsRule.TryConfirmEdge()`를 사용해야 합니다. Unity Client가 보내는 점수, Box 소유권, 추가 턴 결과를 그대로 신뢰하면 안 됩니다.

### UI 연출

Unity는 `MoveResult.CompletedBoxIds`를 이용해 획득된 Box만 연출해야 합니다. 모든 Box를 다시 검사하여 결과를 재계산하면 Core와 View가 서로 다른 규칙을 가지게 됩니다.

### 성능 최적화

현재 보드는 작기 때문에 상태를 계산할 때 배열을 순회하는 방식이 충분합니다. 성능 측정 없이 점수와 개수 캐시를 추가하면 상태 동기화 지점만 늘어날 수 있습니다.

서버 부하 테스트에서 실제 병목이 확인된 뒤에 캐시나 비트 보드 최적화를 검토하는 것이 안전합니다.

---

## 9. 현재 검증 상태

현재 Core는 다음 항목을 Edit Mode 테스트로 검증하고 있습니다.

- 보드의 Edge 및 Box 개수
- Edge와 Box ID 공식
- Edge와 Box 인접 관계
- 새 보드의 초기 상태
- 잘못된 ID 처리
- 턴 검증
- Edge 중복 확정 거부
- Box 한 개 완성
- Box 두 개 동시 완성
- 추가 턴과 일반 턴 변경
- 전체 게임 종료
- 8:8 무승부
- 종료 후 추가 요청 거부

2026년 8월 13일 기준 `DotsAndBoxes.Shared`와 `DotsAndBoxes.Shared.Tests` 프로젝트 빌드는 경고 0개, 오류 0개로 통과했습니다.

Unity Test Runner 실행과 C# 프로젝트 빌드는 서로 다른 검증입니다. 규칙 변경 후에는 둘 다 확인하고, Unity View가 추가된 이후에는 Android 실제 기기에서 한 판을 끝까지 플레이하는 검증도 별도로 수행해야 합니다.
