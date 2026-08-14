# MVP UI 코드 규칙

이 문서는 DOTS ARENA 프로젝트의 UI MVP 이름과 C# 작성 형식을 통일하기 위한 기준입니다.

## 1. MVP 이름 규칙

하나의 UI 기능에 속하는 Model, View, Presenter는 반드시 동일한 기능명을 접두어로 사용합니다.

```text
기능명_Model
기능명_View
기능명_Presenter
```

파일명과 클래스명도 동일하게 작성합니다.

```text
Title_Model.cs       → public sealed class Title_Model
Title_View.cs        → public sealed class Title_View
Title_Presenter.cs   → public sealed class Title_Presenter
```

다른 예시는 다음과 같습니다.

```text
MainMenu_Model
MainMenu_View
MainMenu_Presenter

GameBoard_Model
GameBoard_View
GameBoard_Presenter

Result_Model
Result_View
Result_Presenter
```

다음과 같이 MVP 구성요소의 기능명이 서로 달라지는 이름은 사용하지 않습니다.

```text
GameModel_model
GameBoard_view
GamePresenter_presenter
```

## 2. HM_CodeBase 적용 규칙

### Model

- 일반 C# 클래스로 작성합니다.
- Unity 화면 표시를 직접 처리하지 않습니다.
- UI가 사용할 상태와 상태 변경 규칙을 담당합니다.
- `MonoBehaviour`, `AView`, `APresenter`를 상속하지 않습니다.

```csharp
public sealed class Title_Model
{
    public string Title { get; private set; }
}
```

### View

- `HM.CodeBase.AView`를 상속합니다.
- Unity UI 컴포넌트 참조와 화면 표시만 담당합니다.
- 게임 규칙이나 상태 판정을 직접 처리하지 않습니다.
- 필요한 경우 `Open()`, `Close()`, `Clear()`를 재정의합니다.

```csharp
using HM.CodeBase;

public sealed class Title_View : AView
{
    public override void Clear()
    {
        // UI 표시 초기화
    }
}
```

### Presenter

- `HM.CodeBase.APresenter`를 상속합니다.
- 일반 C# 클래스이며 Unity 컴포넌트로 부착하지 않습니다.
- Model과 View를 생성자에서 전달받습니다.
- 사용자 입력 전달, 상태 변경 요청, View 갱신을 담당합니다.
- `Open()`에서 이벤트를 연결하고 `Dispose()`에서 반드시 해제합니다.

```csharp
using HM.CodeBase;

public sealed class Title_Presenter : APresenter
{
    private readonly Title_Model _model;
    private readonly Title_View _view;

    public Title_Presenter(Title_Model model, Title_View view)
    {
        _model = model;
        _view = view;
    }

    public override void Open()
    {
        _view.Open();
    }

    public override void Close()
    {
        _view.Close();
    }

    public override void Dispose()
    {
        // 이벤트 연결 해제
    }
}
```

### Unity 수명주기 연결 컴포넌트

`APresenter`는 `MonoBehaviour`가 아니므로 Unity 수명주기가 필요하면 별도의 UI 진입 컴포넌트를 사용합니다.

```text
GameBoardUI
TitleUI
ResultUI
```

이 컴포넌트는 다음 역할만 담당합니다.

```text
Awake      → Model과 Presenter 생성
Start      → Presenter.Open()
OnDisable  → Presenter.Close()
OnDestroy  → Presenter.Dispose()
```

## 3. 일반 UI 컴포넌트 이름

MVP의 일부가 아닌 재사용 UI 컴포넌트에는 `_Model`, `_View`, `_Presenter`를 붙이지 않습니다.

```text
BoardEdgeButton
ConfirmButton
PlayerScorePanel
NetworkStateIcon
```

UI 컴포넌트는 기능명과 UI 자체명을 조합합니다.

```text
ExampleButton
ExamplePanel
ExampleIcon
```

## 4. C# 줄바꿈 및 공백 형식

한 줄로 읽을 수 있는 코드를 인위적으로 여러 줄로 나누지 않습니다.

### 함수 선언과 호출

매개변수와 인수가 한 줄로 읽히면 한 줄로 작성합니다.

```csharp
public GameBoard_Presenter(GameBoard_Model model, GameBoard_View view)
{
    _model = model;
    _view = view;
}

CreateEdge(edgeId, row, column, true);
```

다음과 같은 불필요한 세로 나열은 사용하지 않습니다.

```csharp
public GameBoard_Presenter(
    GameBoard_Model model,
    GameBoard_View view)

CreateEdge(
    edgeId,
    row,
    column,
    true);
```

### 조건문

짧은 조건은 한 줄로 작성합니다.

```csharp
if (edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT)
{
    return false;
}
```

조건이 너무 길어 실제로 읽기 어려운 경우에만 논리 단위로 나눕니다.

### 속성과 초기값

짧은 속성과 초기화는 한 줄로 작성합니다.

```csharp
public bool HasPreview => PreviewEdgeId != NO_PREVIEW_EDGE_ID;

[SerializeField] private float _dotSize = 28f;
[SerializeField] private Color _availableEdgeColor = new Color(0.31f, 0.31f, 0.31f, 1f);
```

### 빈 줄

빈 줄은 다음 경우에만 사용합니다.

- 필드, 속성, 이벤트, 함수 영역 구분
- 한 함수 안에서 서로 다른 처리 단계 구분
- 조기 반환 이후 본 처리와 구분

단순 대입문이나 연속된 함수 호출 사이에 불필요한 빈 줄을 넣지 않습니다.

### 기본 공백

괄호 안쪽에 불필요한 공백을 넣지 않습니다.

```csharp
if (isValid)
for (int i = 0; i < count; i++)
new Vector2(0.5f, 0.5f)
```

다음 형식은 사용하지 않습니다.

```csharp
if ( isValid )
for ( int i = 0; i < count; i++ )
new Vector2(0.5f , 0.5f)
```

## 5. 멤버 이름 규칙

```text
private/protected 멤버 변수  _exampleVariable
매개변수                      exampleVariable
정적 변수                     s_exampleVariable
const/readonly                EXAMPLE_VARIABLE
함수                          PascalCase
이벤트 처리 함수              OnExampleActioned
```

Unity UI 참조 접미사는 다음 기준을 사용합니다.

```text
GameObject       exampleObj
Transform        exampleTrans
RectTransform    exampleRectTrans
Text             exampleTxt
Button           exampleBtn
Image            exampleImg
InputField       exampleInput
Dropdown         exampleDrop
Sprite           exampleSprite
Animator         exampleAnimator
```

## 6. GameBoard UI 현재 구조

```text
GameBoardUI
├─ GameBoard_Model
├─ GameBoard_Presenter : APresenter
└─ GameBoard_View : AView
   └─ BoardEdgeButton
```

책임은 다음과 같습니다.

```text
GameBoard_Model      Board와 Preview 상태 보관
GameBoard_View       보드 생성과 Edge 시각 표현
GameBoard_Presenter  입력 전달과 Model/View 동기화
GameBoardUI          Unity 수명주기 연결
BoardEdgeButton      개별 Edge 클릭과 표시 처리
```

## 7. 작성 전 확인 목록

- MVP 세 클래스의 기능명 접두어가 같은가?
- 파일명과 클래스명이 정확히 같은가?
- View가 `AView`를 상속하는가?
- Presenter가 `APresenter`를 상속하는가?
- Presenter를 MonoBehaviour로 만들지 않았는가?
- 이벤트를 `Dispose()`에서 해제하는가?
- 한 줄로 읽을 수 있는 코드를 불필요하게 여러 줄로 나누지 않았는가?
- Model이 Unity 화면 표시를 직접 처리하지 않는가?
- View가 게임 규칙을 직접 판단하지 않는가?
