# Dots & Boxes 구현 코드 설명서

## 1. 문서 범위

이 문서는 현재 구현된 게임 규칙 Core와 `GameBoard` UI 코드를 설명합니다. 테스트 코드는 문서 대상에서 제외합니다.

현재 설명 대상은 다음 두 영역입니다.

```text
Assets/01_Main/02_Scripts/Shared
└─ Unity에 의존하지 않는 보드 데이터와 게임 규칙

Assets/01_Main/02_Scripts/GamePlay/UI/Board
└─ HM_CodeBase 기반 GameBoard MVP와 Unity UI
```

현재 한 화면에서 가능한 동작은 다음과 같습니다.

- 4×4 Box 보드 자동 생성
- 선택 가능한 Edge Preview
- Confirm 버튼을 통한 Edge 확정
- 확정한 플레이어 색상으로 Edge 표시
- 완성된 Box 소유 색상 표시
- Box 획득 여부에 따른 턴 변경 또는 추가 턴
- Player 1, Player 2 점수 표시
- 현재 턴 표시

---

## 2. 전체 구조

```text
GameBoardUI : MonoBehaviour
├─ GameBoard_Model 생성
├─ GameBoard_Presenter 생성
└─ Unity 수명주기를 Presenter에 연결
                 │
                 ▼
GameBoard_Presenter : APresenter
├─ View 입력 이벤트 수신
├─ Model 상태 변경 요청
└─ Model 결과를 View 표시 명령으로 변환
        │                         │
        ▼                         ▼
GameBoard_Model              GameBoard_View : AView
├─ DotsBoard                 ├─ Box 16개 생성
├─ Preview Edge ID           ├─ Edge 40개 생성
└─ DotsRule 호출             ├─ Dot 25개 생성
        │                     ├─ Preview/확정/소유 색상 표시
        ▼                     └─ 점수·턴·Confirm 상태 표시
DotsAndBoxes.Shared
├─ BoardTopology
├─ EdgeData / BoxData
├─ DotsBoard
├─ DotsRule
└─ MoveResult 및 Enum
```

핵심 의존 방향은 다음과 같습니다.

```text
View ──입력 이벤트──▶ Presenter ──상태 변경──▶ Model ──규칙 실행──▶ Shared Core
View ◀─표시 명령──── Presenter ◀─처리 결과──── Model ◀─MoveResult── Shared Core
```

View는 게임 규칙을 판정하지 않고, Core는 Unity UI를 알지 못합니다. Presenter가 두 영역을 연결합니다.

### Assembly 구분

| Assembly | 참조 | 역할 |
|---|---|---|
| `DotsAndBoxes.Shared` | 없음, `No Engine References` 활성화 | 순수 C# 보드 데이터와 규칙 |
| `DotsAndBoxes.Gameplay` | `DotsAndBoxes.Shared`, `HM.codebase.Runtime`, `Unity.TextMeshPro` | Unity UI와 MVP 연결 |

Shared가 Unity에 의존하지 않으므로 같은 규칙을 이후 Unity Client와 서버에서 함께 사용할 수 있습니다.

### 파일 구성

| 영역 | 파일 | 책임 |
|---|---|---|
| Shared/Board | `BoardTopology.cs` | Edge, Box ID와 인접 관계 계산 |
| Shared/Board | `EdgeData.cs` | Edge 하나의 소유 상태 보관 |
| Shared/Board | `BoxData.cs` | Box 구성 Edge와 소유 상태 보관 |
| Shared/Board | `DotsBoard.cs` | 전체 게임 상태, 턴, 점수, 결과 제공 |
| Shared/Rules | `PLAYER_INDEX_ENUM.cs` | 두 플레이어와 미소유 상태 구분 |
| Shared/Rules | `MOVE_ERROR_ENUM.cs` | 수 확정 실패 원인 구분 |
| Shared/Rules | `GAME_RESULT_ENUM.cs` | 진행, 승리, 무승부 구분 |
| Shared/Rules | `MoveResult.cs` | 한 번의 수 처리 결과 전달 |
| Shared/Rules | `DotsRule.cs` | Edge 확정, Box 획득, 턴 변경 실행 |
| GamePlay/UI/Board | `GameBoard_Model.cs` | 보드와 로컬 Preview 상태 보관 |
| GamePlay/UI/Board | `GameBoard_View.cs` | 보드 UI 생성과 화면 표시 |
| GamePlay/UI/Board | `GameBoard_Presenter.cs` | 입력 처리와 Model/View 동기화 |
| GamePlay/UI/Board | `GameBoardUI.cs` | Unity 수명주기와 MVP 연결 |
| GamePlay/UI/Board | `BoardEdgeButton.cs` | Edge 하나의 클릭과 선 표시 처리 |

---

## 3. Shared Board

### 3.1 BoardTopology.cs

`BoardTopology`는 4×4 보드의 공간 구조를 정수 ID로 변환합니다. Core에는 `Transform`이나 Unity 좌표가 없기 때문에 모든 Edge와 Box를 ID로 식별합니다.

```text
Box                 4 × 4 = 16개
Dot                 5 × 5 = 25개
Horizontal Edge     5 × 4 = 20개
Vertical Edge       4 × 5 = 20개
전체 Edge                    40개
```

4×4는 Dot이 아니라 Box 개수입니다. Box 네 칸을 만들려면 경계가 다섯 줄 필요하므로 Dot은 5×5가 됩니다.

가로 Edge는 `0~19`, 세로 Edge는 `20~39`를 사용합니다.

```text
가로 Edge ID = row × BOX_COLUMNS + column
세로 Edge ID = HORIZONTAL_EDGE_COUNT + row × DOT_COLUMNS + column
Box ID       = row × BOX_COLUMNS + column
```

`BoxEdgeIds`는 Box 하나를 구성하는 Top, Bottom, Left, Right Edge ID를 묶어서 반환합니다. 네 값을 하나의 의미 있는 결과로 전달하고, 서로 다른 코드에서 같은 연결 공식을 중복 작성하지 않기 위한 구조입니다.

`AdjacentBoxIDs`는 Edge 하나와 맞닿은 Box를 반환합니다. 외곽 Edge는 한 개, 내부 Edge는 두 개의 Box와 인접합니다. 따라서 수를 둘 때 16개 Box 전체가 아니라 최대 두 개만 검사합니다.

모든 좌표와 ID는 계산 전에 `ValidateRange()`로 검사합니다. 잘못된 값이 다른 정상 ID로 조용히 변환되는 것을 막기 위해 범위를 벗어나면 `ArgumentOutOfRangeException`을 발생시킵니다.

### 3.2 EdgeData.cs

`EdgeData`는 Edge 하나의 상태를 보관합니다.

```text
EdgeId
OwnerPlayerIndex
IsConfirmed
```

`IsConfirmed`는 별도 필드가 아니라 소유자가 `NONE`이 아닌지로 계산합니다. 소유자는 있는데 확정되지 않은 모순 상태를 만들지 않기 위해서입니다.

`Confirm()`은 중복 확정과 잘못된 플레이어를 검사한 뒤 소유자를 지정합니다. 접근 범위가 `internal`이므로 Gameplay UI가 직접 Edge를 바꿀 수 없고, Shared Assembly 안의 `DotsRule`을 통해서만 정상적으로 호출됩니다.

### 3.3 BoxData.cs

`BoxData`는 Box 하나를 구성하는 네 Edge ID와 소유자를 보관합니다.

```text
BoxId
TopEdgeId / BottomEdgeId / LeftEdgeId / RightEdgeId
OwnerPlayerIndex
IsOwned
```

생성할 때 `BoardTopology.GetBoxEdgeIDs()`를 사용하므로 연결 공식의 기준은 `BoardTopology` 한곳에만 존재합니다.

`IsOwned`도 별도 상태를 저장하지 않고 소유자가 있는지로 계산합니다. `Claim()`은 실제 플레이어만 소유자로 받을 수 있고 이미 소유된 Box를 다시 획득하지 못하게 합니다.

Box의 네 Edge가 완성됐는지는 `BoxData`가 직접 판단하지 않습니다. 전체 보드를 조회해야 하는 규칙이므로 `DotsRule`이 담당하고, `BoxData`는 자신의 구성과 소유 상태만 책임집니다.

### 3.4 DotsBoard.cs

`DotsBoard`는 한 게임의 전체 상태를 보관하는 중심 객체입니다.

```text
EdgeData[40]
BoxData[16]
CurrentPlayerIndex
PlayerOneScore / PlayerTwoScore
IsGameFinished
GameResult
```

생성자는 선공 플레이어를 검증하고 Edge 40개와 Box 16개를 ID 순서대로 생성합니다. 기본 선공은 `PLAYER_ONE`이며 필요하면 생성자에서 `PLAYER_TWO`를 전달할 수 있습니다.

내부 배열은 `_edges`, `_boxes`로 보관하고 외부에는 `IReadOnlyList`로 제공합니다. 외부 코드가 상태를 조회할 수는 있지만 배열 요소를 임의의 객체로 교체하지 못하게 하기 위한 구조입니다.

다음 값은 별도 필드로 중복 저장하지 않고 현재 보드 상태에서 계산합니다.

```text
ConfirmedEdgeCount  = 확정된 Edge 수
OwnedBoxCount       = 소유자가 생긴 Box 수
플레이어 점수       = 해당 플레이어가 소유한 Box 수
게임 종료           = 16개 Box가 모두 소유됨
게임 결과           = 종료 여부와 두 점수 비교
```

보드 크기가 작아 순회 비용보다 상태 불일치를 방지하는 이점이 더 큽니다.

`SwitchTurn()`은 `internal`입니다. Box 획득 여부를 판정한 `DotsRule`만 턴을 변경할 수 있고, View가 임의로 턴을 넘기지 못합니다.

---

## 4. Shared Rules

### 4.1 PLAYER_INDEX_ENUM.cs

```text
NONE          아직 소유자가 없음
PLAYER_ONE    첫 번째 플레이어
PLAYER_TWO    두 번째 플레이어
```

이 값은 계정 ID나 닉네임이 아니라 한 Match 안에서 사용하는 논리적 플레이어 위치입니다.

### 4.2 MOVE_ERROR_ENUM.cs

`DotsRule.TryConfirmEdge()`가 수를 거부한 원인을 나타냅니다.

| 값 | 의미 |
|---|---|
| `NONE` | 오류 없이 처리됨 |
| `INVALID_PLAYER` | 실제 플레이어가 아닌 값으로 요청함 |
| `NOT_YOUR_TURN` | 현재 턴이 아닌 플레이어가 요청함 |
| `INVALID_EDGE` | Edge ID가 `0~39` 범위를 벗어남 |
| `EDGE_ALREADY_CONFIRMED` | 이미 확정된 Edge를 다시 요청함 |
| `GAME_ALREADY_FINISHED` | 종료된 게임에 추가 요청함 |

잘못된 사용자 입력이나 네트워크 요청은 서버 자체의 예외 상황이 아니므로 예외 대신 실패 결과로 반환합니다.

### 4.3 GAME_RESULT_ENUM.cs

```text
IN_PROGRESS       게임 진행 중
PLAYER_ONE_WIN    Player 1 승리
PLAYER_TWO_WIN    Player 2 승리
DRAW              무승부
```

`DotsBoard.GameResult`가 현재 종료 상태와 점수를 기준으로 계산합니다.

### 4.4 MoveResult.cs

`MoveResult`는 Edge 확정 시도 한 번의 결과를 전달합니다.

```text
IsValid           실제 보드에 반영됐는지
Error             실패 원인
EdgeId            요청한 Edge
CompletedBoxIds   이번 수로 획득한 Box ID 목록
HasExtraTurn      Box를 하나 이상 획득했는지
IsGameFinished    처리 직후 게임이 끝났는지
```

Edge 하나가 내부 경계일 경우 양쪽 Box를 동시에 완성할 수 있으므로 `CompletedBoxIds`는 목록입니다. `HasExtraTurn`은 이 목록이 비어 있지 않은지로 계산하여 Box 획득 여부와 추가 턴 여부가 어긋나지 않게 합니다.

`CreateFailure()`와 `CreateSuccess()`는 `internal`입니다. 외부 코드가 임의의 성공 결과를 만드는 대신 실제 규칙 처리 결과만 전달되도록 제한합니다.

### 4.5 DotsRule.cs

`DotsRule`은 보드 상태를 변경하는 Core의 공개 진입점입니다.

```csharp
DotsRule.TryConfirmEdge(board, playerIndex, edgeId)
```

처리 순서는 다음과 같습니다.

```text
1. Board null 검사
2. 게임 종료 여부 검사
3. 플레이어 값 검사
4. 현재 턴 검사
5. Edge ID 검사
6. 중복 확정 검사
7. Edge 소유자 확정
8. 인접 Box 최대 두 개 검사
9. 완성된 Box 소유권 부여
10. 획득한 Box가 없으면 턴 변경
11. MoveResult 반환
```

상태 변경은 모든 요청 검증이 끝난 뒤 시작됩니다. 따라서 잘못된 요청이 Edge만 바꾸거나 턴만 넘기는 부분 변경을 만들지 않습니다.

Box를 획득하면 현재 플레이어가 추가 턴을 얻고, 획득하지 못하면 상대 플레이어로 턴이 넘어갑니다.

```text
완성 Box 0개 → 상대 플레이어 턴
완성 Box 1개 → 현재 플레이어 턴 유지
완성 Box 2개 → 현재 플레이어 턴 유지
```

---

## 5. GameBoard MVP

### 5.1 GameBoard_Model.cs

`GameBoard_Model`은 UI가 사용하는 게임 상태를 보관합니다. Unity 컴포넌트를 참조하지 않는 일반 C# 클래스입니다.

```text
Board           확정된 실제 게임 상태
PreviewEdgeId   현재 선택했지만 아직 확정하지 않은 Edge
HasPreview      Preview 존재 여부
```

Preview는 실제 게임 상태가 아니므로 `DotsBoard`나 `EdgeData`에 넣지 않았습니다. 사용자가 다른 Edge를 선택하거나 Confirm 전에 취소할 수 있는 UI 임시 상태이기 때문입니다.

`TrySetPreviewEdge()`는 다음 Edge를 Preview로 만들지 않습니다.

- 게임이 이미 종료된 경우
- ID가 `0~39` 범위를 벗어난 경우
- 이미 확정된 Edge인 경우

유효하면 기존 Preview ID를 새 ID로 교체합니다. 실제 Edge 소유자, 점수, 턴은 이 단계에서 바뀌지 않습니다.

`TryConfirmPreview()`는 Preview가 있을 때만 현재 플레이어와 Preview ID를 `DotsRule.TryConfirmEdge()`에 전달합니다. 성공한 경우 Preview를 지우고 `MoveResult`를 반환합니다.

이 구조를 사용한 이유는 Presenter가 Core 규칙을 직접 조합하지 않고 Model의 공개 기능만 사용하도록 하기 위해서입니다.

### 5.2 BoardEdgeButton.cs

`BoardEdgeButton`은 Edge 하나를 담당하는 재사용 UI 컴포넌트입니다. MVP 전체 View가 아니라 작은 UI 부품이므로 `_View` 접미사를 사용하지 않습니다.

주요 책임은 다음과 같습니다.

- 자신의 `EdgeId` 보관
- Unity `Button` 클릭을 `EdgeSelected(int)` 이벤트로 변환
- 가로/세로 방향에 맞게 실제 선 이미지 배치
- 선 색상과 버튼 활성 상태 변경

Edge 버튼의 전체 RectTransform은 터치 영역이고, 자식 `_visibleLineImg`는 실제로 보이는 얇은 선입니다. 시각적 선 두께와 터치 두께를 분리해 모바일에서도 누르기 쉽게 만들었습니다.

확정된 Edge는 `SetVisual(color, false)`로 버튼을 비활성화하여 다시 선택하지 못하게 합니다.

### 5.3 GameBoard_View.cs

`GameBoard_View`는 `HM.CodeBase.AView`를 상속하며 Unity UI 생성과 표시만 담당합니다.

`Awake()`에서 Inspector 참조를 검사한 뒤 `BuildBoard()`를 호출합니다. 생성 순서는 다음과 같습니다.

```text
1. Box 16개
2. Edge 40개
3. Dot 25개
```

생성 순서를 Box → Edge → Dot으로 둔 이유는 Canvas의 자식 순서상 뒤에 생성된 요소가 앞에 표시되므로, Box 배경 위에 Edge가 놓이고 가장 위에 Dot이 보이게 하기 위해서입니다.

모든 위치는 `BoardTopology`의 행과 열을 0~1 Anchor 비율로 변환해 계산합니다. `_boardRootRectTrans` 크기가 바뀌어도 보드 요소가 함께 비례해 배치됩니다.

View의 표시 함수는 다음과 같이 역할이 나뉩니다.

```text
ShowAvailableEdge()       선택 가능한 기본 Edge 표시
ShowLocalPreviewEdge()    현재 Preview 색상 표시
ShowConfirmedEdge()       소유 플레이어 색상 표시 후 입력 비활성화
ShowOwnedBox()            완성된 Box를 소유 플레이어 색상으로 표시
ShowScores()              두 플레이어 점수 표시
ShowCurrentTurn()         현재 턴 문구와 플레이어 색상 표시
SetConfirmInteractable()  Confirm 버튼 활성 상태 변경
```

View는 클릭 결과를 직접 처리하지 않고 다음 이벤트만 외부로 전달합니다.

```text
EdgeSelected(int edgeId)
ConfirmRequested()
```

어떤 Edge가 유효한지, Box가 완성됐는지, 누구 턴인지는 View가 판정하지 않습니다.

`[MovedFrom]`은 이전 클래스명 `GameBoard_view`에서 현재 `GameBoard_View`로 이름을 바꾼 뒤에도 Unity Scene과 Prefab의 직렬화 연결을 최대한 유지하기 위한 속성입니다.

### 5.4 GameBoard_Presenter.cs

`GameBoard_Presenter`는 `HM.CodeBase.APresenter`를 상속하는 일반 C# 클래스입니다. Model과 View를 생성자에서 전달받아 두 객체 사이의 흐름을 조정합니다.

```text
Open()     이벤트 연결, View 열기, 전체 화면 동기화
Close()    View 닫기
Dispose()  View 이벤트 연결 해제
```

`_isBound`는 `Open()`이 여러 번 호출되어 같은 이벤트가 중복 등록되는 것을 막고, `_isDisposed`는 폐기된 Presenter가 다시 동작하지 않도록 보호합니다.

`RefreshView()`는 Model의 현재 상태를 기준으로 화면 전체를 다시 구성합니다.

```text
1. 모든 Edge를 선택 가능 상태로 초기화
2. 확정된 Edge를 소유자 색상으로 복원
3. 소유된 Box를 소유자 색상으로 복원
4. Preview가 있으면 Preview 색상 복원
5. Preview 여부에 맞춰 Confirm 버튼 갱신
6. 점수와 현재 턴 갱신
```

따라서 UI를 닫았다 다시 열어도 View의 이전 표시를 신뢰하지 않고 Model을 기준으로 복구합니다.

Edge를 선택했을 때는 Model에 Preview 변경을 먼저 요청합니다. 성공하면 이전 Preview를 기본색으로 돌리고 새 Preview를 표시하며 Confirm 버튼을 활성화합니다.

Confirm 요청 시에는 확정 직전의 현재 플레이어를 보관한 후 Model에 확정을 요청합니다. 성공하면 `MoveResult.EdgeId`와 `CompletedBoxIds`만 사용해 변경된 Edge와 Box를 표시하고, 점수와 턴을 갱신합니다.

확정 플레이어를 먼저 보관하는 이유는 Box를 얻지 못한 수에서는 Core가 처리 중에 다음 플레이어로 턴을 넘기기 때문입니다. 처리 후의 `CurrentPlayerIndex`로 Edge 색을 정하면 방금 둔 Edge가 상대 색으로 표시될 수 있습니다.

### 5.5 GameBoardUI.cs

`GameBoardUI`는 `MonoBehaviour`가 아닌 Presenter를 Unity 수명주기에 연결하는 진입 컴포넌트입니다.

```text
Awake()      GameBoard_Model과 GameBoard_Presenter 생성
Start()      Presenter.Open()
OnEnable()   시작 이후 다시 활성화되면 Presenter.Open()
OnDisable()  Presenter.Close()
OnDestroy()  Presenter.Dispose()
```

`GameBoard_View`가 스스로 Model이나 Presenter를 생성하지 않기 때문에 View의 표시 책임이 흐려지지 않습니다. 또한 나중에 서버 Match나 저장 데이터를 사용할 때 진입점에서 다른 Model 생성 방식으로 교체할 수 있습니다.

---

## 6. 실제 입력 처리 흐름

### 6.1 보드 시작

```text
GameBoardUI.Awake()
→ 새 GameBoard_Model 생성
→ 새 GameBoard_Presenter 생성

GameBoard_View.Awake()
→ Prefab으로 Box, Edge, Dot 생성
→ Confirm 버튼 비활성화

GameBoardUI.Start()
→ Presenter.Open()
→ Model 상태 기준으로 전체 View 갱신
```

### 6.2 Edge Preview

```text
Edge 버튼 클릭
→ BoardEdgeButton.EdgeSelected
→ GameBoard_View.EdgeSelected
→ GameBoard_Presenter.OnEdgeSelected()
→ GameBoard_Model.TrySetPreviewEdge()
→ 이전 Preview는 기본색, 새 Preview는 Preview 색
→ Confirm 버튼 활성화
```

이때 확정 Edge 수, 점수, Box 소유권, 현재 턴은 변하지 않습니다.

### 6.3 Edge Confirm

```text
Confirm 버튼 클릭
→ GameBoard_View.ConfirmRequested
→ GameBoard_Presenter.OnConfirmRequested()
→ GameBoard_Model.TryConfirmPreview()
→ DotsRule.TryConfirmEdge()
→ Edge 확정 및 인접 Box 판정
→ MoveResult 반환
→ 확정 Edge와 획득 Box 표시
→ Confirm 버튼 비활성화
→ 점수와 현재 턴 갱신
```

### 6.4 턴 처리

```text
Box를 완성하지 못함
→ Core가 상대 플레이어로 턴 변경
→ Presenter가 변경된 턴 표시

Box를 하나 이상 완성함
→ Core가 현재 플레이어 턴 유지
→ 점수 증가 및 Box 소유 색상 표시
```

---

## 7. Scene과 Inspector 연결

현재 GameBoard 화면에는 다음 참조가 필요합니다.

```text
GameBoardUI
└─ Game Board View → Scene의 GameBoard_View

GameBoard_View
├─ Board Root Rect Trans → 보드 요소가 생성될 RectTransform
├─ Dot Prefab Img        → Dot Image Prefab
├─ Box Prefab Img        → Box Image Prefab
├─ Edge Prefab Btn       → BoardEdgeButton Prefab
├─ Confirm Btn           → Confirm Button
├─ Player One Score Txt  → Player 1 TMP 점수 텍스트
├─ Player Two Score Txt  → Player 2 TMP 점수 텍스트
└─ Turn Txt              → 현재 턴 TMP 텍스트

BoardEdgeButton Prefab
├─ Edge Btn              → 자기 자신의 Button
└─ Visible Line Img      → 자식 선 Image
```

Box와 Dot Prefab의 `Raycast Target`은 꺼야 Edge 버튼 입력을 가리지 않습니다. Edge의 루트 Image와 Button은 넓은 터치 영역으로 사용하고, 자식 선 Image의 `Raycast Target`은 끕니다.

---

## 8. 현재 설계 원칙

### 규칙과 화면 분리

Shared Core는 Unity를 참조하지 않습니다. View는 규칙을 계산하지 않고 Presenter가 전달한 상태만 표시합니다.

### Preview와 확정 상태 분리

Preview는 `GameBoard_Model`, 실제 Edge 소유권은 `DotsBoard`에 있습니다. 선택만 한 Edge가 점수나 턴에 영향을 주지 않습니다.

### 상태 변경 경로 제한

`EdgeData.Confirm()`, `BoxData.Claim()`, `DotsBoard.SwitchTurn()`은 외부 Assembly에서 호출할 수 없습니다. 실제 확정은 `DotsRule.TryConfirmEdge()`를 통과해야 합니다.

### 중복 상태 최소화

Edge 확정, Box 소유, 점수, 종료, 결과, 추가 턴은 기존 상태에서 계산합니다. 같은 의미의 값 두 개가 서로 달라질 가능성을 줄입니다.

### 이벤트 수명 관리

View와 Presenter는 등록한 이벤트를 `OnDestroy()` 또는 `Dispose()`에서 해제합니다. 화면을 다시 열거나 Scene을 이동할 때 중복 호출과 죽은 객체 참조를 막기 위한 처리입니다.

### HM_CodeBase MVP 규칙

```text
GameBoard_Model      일반 C# 상태 클래스
GameBoard_View       AView 상속, 화면 표시
GameBoard_Presenter  APresenter 상속, 입력과 동기화
GameBoardUI          Unity 수명주기 연결
```

프로젝트의 이름 및 형식 기준은 루트의 `MVP_UI_CODING_CONVENTIONS.md`를 따릅니다.

---

## 9. 현재 구현 범위와 다음 확장 후보

### 구현됨

- 고정 4×4 Box 보드
- 40개 Edge, 16개 Box, 25개 Dot UI 자동 생성
- 로컬 Preview 이동
- Confirm 버튼 활성/비활성
- 두 플레이어의 Edge 확정 색상
- Box 한 개 또는 두 개 동시 완성
- Box 소유 색상
- 점수 표시
- 일반 턴 변경과 Box 획득 추가 턴
- 현재 턴 표시
- 게임 종료와 승리/무승부 결과 계산
- View 재오픈 시 Model 상태 기준 화면 복원

### 아직 UI에 연결되지 않음

- 게임 종료 결과 화면
- Preview 취소 전용 버튼
- 턴 제한 시간
- AI 플레이어
- 온라인 상대의 Preview 표현
- 서버 Snapshot, Revision, MatchId, RequestId
- SignalR 통신과 재접속
- 애니메이션, 사운드, 진동 피드백
- 상태 저장 및 복원
- 가변 크기 보드

Core에는 이미 `IsGameFinished`와 `GameResult`가 있으므로 결과 화면을 추가할 때 View가 승패를 재계산할 필요는 없습니다. Presenter가 Model의 결과를 읽어 Result UI에 전달하는 방향이 현재 구조와 맞습니다.

온라인 기능을 추가할 때도 Client가 점수나 Box 소유권을 직접 결정하면 안 됩니다. 서버가 같은 Shared 규칙으로 처리한 Snapshot 또는 Move 결과를 권위 상태로 사용해야 합니다.

---

## 10. 수정 시 확인할 불변 조건

```text
전체 Edge 수 = 40
전체 Box 수 = 16
PlayerOneScore + PlayerTwoScore = OwnedBoxCount
확정된 Edge는 다시 Preview 또는 Confirm할 수 없음
Preview만으로 Board 상태는 바뀌지 않음
Box를 얻지 못하면 턴 변경
Box를 얻으면 턴 유지
View는 DotsRule을 직접 호출하지 않음
게임 상태 변경은 Model을 통해 요청
Presenter가 등록한 이벤트는 Dispose에서 해제
```

이 조건이 깨지면 UI 표시 문제처럼 보여도 실제 원인은 Core, Model, Presenter의 책임 경계가 무너진 것일 가능성이 큽니다.
