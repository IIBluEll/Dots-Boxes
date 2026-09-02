using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay
{
    [MovedFrom(true, sourceNamespace: "DotsAndBoxes.Gameplay", sourceAssembly: "DotsAndBoxes.Gameplay", sourceClassName: "GameBoard_view")]
    public sealed class GameBoard_View : AView
    {
        [Header("Root")]
        [SerializeField] private RectTransform _boardRootRectTrans;

        [Space(5f), Header("Prefabs")]
        [SerializeField] private Image _dotPrefabImg;
        [SerializeField] private Image _boxPrefabImg;
        [SerializeField] private BoardEdgeButton _edgePrefabBtn;

        [Space(5f), Header("Layout")]
        [SerializeField] private float _dotSize = 28f;
        [SerializeField] private float _edgeVisibleThickness = 12f;
        [SerializeField] private float _edgeTouchThickness = 44f;
        [SerializeField] private float _boxInset = 8f;

        [Space(5f), Header("Colors")]
        [SerializeField] private Color _availableEdgeColor = new Color(0.31f, 0.31f, 0.31f, 1f);
        [SerializeField] private Color _availableBoxColor = new Color(0.55f, 0.55f, 0.55f, 0.56f);
        [SerializeField] private Color _localPreviewEdgeColor = new Color(0.15f, 0.85f, 1f, 1f);
        [SerializeField, Range(0f , 1f)] private float _opponentPreviewAlpha = 0.55f;

        [Space(5f), Header("Buttons")]
        [SerializeField] private Button _confirmBtn;

        [Space(5f), Header("Player Colors")]
        [SerializeField] private Color _playerOneEdgeColor = new Color(0.1f, 0.45f, 1f, 1f);
        [SerializeField] private Color _playerTwoEdgeColor = new Color(1f, 0.4f, 0.1f, 1f);
        [SerializeField] private Color _playerOneBoxColor = new Color(0.1f, 0.45f, 1f, 0.35f);
        [SerializeField] private Color _playerTwoBoxColor = new Color(1f, 0.4f, 0.1f, 0.35f);

        [Space(5f), Header("Status")]
        [FormerlySerializedAs("_playerOneScoreTxt")]
        [SerializeField] private TMP_Text _localPlayerScoreTxt;
        [FormerlySerializedAs("_playerTwoScoreTxt")]
        [SerializeField] private TMP_Text _opponentScoreTxt;
        [SerializeField] private TMP_Text _turnTxt;
        [SerializeField] private TMP_Text _turnTimerTxt;
        [SerializeField] private TMP_Text _connectionStateTxt;
        [SerializeField] private TMP_Text _actionGuideTxt;

        private BoardEdgeButton[] _edgeButtons;
        private BoardBoxVisual[] _boxVisuals;

        public IReadOnlyList<BoardEdgeButton> EdgeButtons => _edgeButtons;

        public event Action<int> EdgeSelected;
        public event Action ConfirmRequested;
        private void Awake()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            BuildBoard();

            _confirmBtn.onClick.AddListener(OnConfirmButtonClicked);

            SetConfirmInteractable(false);
        }

        private void OnDestroy()
        {
            if (_edgeButtons == null)
            {
                return;
            }

            if ( _confirmBtn != null )
            {
                _confirmBtn.onClick.RemoveListener(OnConfirmButtonClicked);
            }

            for (int i = 0; i < _edgeButtons.Length; i++)
            {
                if (_edgeButtons[i] != null)
                {
                    _edgeButtons[i].EdgeSelected -= OnEdgeSelected;
                }
            }
        }

        public override void Clear()
        {
            if ( _edgeButtons == null || _boxVisuals == null )
            {
                return;
            }

            ShowAllEdgesAvailable();

            for ( int boxId = 0; boxId < _boxVisuals.Length; boxId++ )
            {
                _boxVisuals[ boxId ].SetAvailable(_availableBoxColor);
            }

            SetConfirmInteractable(false);
        }

        public BoardEdgeButton GetEdgeButton(int edgeId)
        {
            if (edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT)
            {
                throw new ArgumentOutOfRangeException(nameof(edgeId));
            }

            return _edgeButtons[edgeId];
        }

        public void ShowScores(int localPlayerScore , int opponentScore)
        {
            _localPlayerScoreTxt.text = $"{localPlayerScore}";
            _opponentScoreTxt.text = $"{opponentScore}";
        }

        public void ShowMatchStatus(string status)
        {
            _turnTxt.text = status;
            _turnTxt.color = Color.white;
        }

        public void ShowCurrentTurn(
            PLAYER_INDEX_ENUM currentPlayerIndex ,
            PLAYER_INDEX_ENUM localPlayerIndex)
        {
            bool isLocalPlayerTurn = currentPlayerIndex == localPlayerIndex;
            bool isPlayerOneTurn = currentPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE;

            _turnTxt.text = isLocalPlayerTurn ? "플레이어 턴" : "상대방 턴";
            _turnTxt.color = isPlayerOneTurn ? _playerOneEdgeColor : _playerTwoEdgeColor;
        }

        public void ShowActionGuide(bool isExtraTurn)
        {
            _actionGuideTxt.gameObject.SetActive(true);
            _actionGuideTxt.text = isExtraTurn ? "추가 턴입니다!" : "선을 선택하고 확인을 눌러주세요";
        }

        public void SetActionGuideVisible(bool isVisible)
        {
            if ( _actionGuideTxt.gameObject.activeSelf == isVisible )
            {
                return;
            }

            _actionGuideTxt.gameObject.SetActive(isVisible);
        }

        public void SetTurnTimerVisible(bool isVisible)
        {
            if ( _turnTimerTxt.gameObject.activeSelf == isVisible )
            {
                return;
            }

            _turnTimerTxt.gameObject.SetActive(isVisible);
        }

        public void ShowTurnTimer(int turnCountdownNumber)
        {
            _turnTimerTxt.text = $"{turnCountdownNumber}";
        }

        public void SetConnectionStateVisible(bool isVisible)
        {
            _connectionStateTxt.gameObject.SetActive(isVisible);
        }

        public void ShowConnectionState(GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            switch (connectionState)
            {
                case GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED:
                    _connectionStateTxt.text = "DISCONNECTED";
                    break;

                case GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTING:
                    _connectionStateTxt.text = "CONNECTING...";
                    break;

                case GAME_SESSION_CONNECTION_STATE_ENUM.CONNECTED:
                    _connectionStateTxt.text = "CONNECTED";
                    break;

                case GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED:
                    _connectionStateTxt.text = "CONNECTION FAILED";
                    break;

                default:
                    _connectionStateTxt.text = "UNKNOWN";
                    break;
            }
        }

        public void ShowAllEdgesAvailable()
        {
            for (int edgeId = 0; edgeId < BoardTopology.EDGE_COUNT; edgeId++)
            {
                ShowAvailableEdge(edgeId);
            }
        }

        public void ShowAvailableEdge(int edgeId)
        {
            BoardEdgeButton edgeBtn = GetEdgeButton(edgeId);
            edgeBtn.SetVisual(_availableEdgeColor, EDGE_LINE_STYLE_ENUM.DASHED, true);
        }

        public void ShowLocalPreviewEdge(int edgeId)
        {
            BoardEdgeButton edgeBtn = GetEdgeButton(edgeId);
            edgeBtn.SetVisual(_localPreviewEdgeColor, EDGE_LINE_STYLE_ENUM.SOLID, true);
        }

        public void ShowOpponentPreviewEdge(int edgeId , PLAYER_INDEX_ENUM opponentPlayerIndex)
        {
            Color previewColor = opponentPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                ? _playerOneEdgeColor
                : _playerTwoEdgeColor;

            previewColor.a = _opponentPreviewAlpha;
            GetEdgeButton(edgeId).SetVisual(previewColor, EDGE_LINE_STYLE_ENUM.SOLID, false);
        }

        private void BuildBoard()
        {
            _edgeButtons = new BoardEdgeButton[BoardTopology.EDGE_COUNT];
            _boxVisuals = new BoardBoxVisual[BoardTopology.BOX_COUNT];

            BuildBoxes();
            BuildEdges();
            BuildDots();
        }

        public void SetConfirmInteractable(bool isInteractable)
        {
            _confirmBtn.interactable = isInteractable;
        }

        public void SetBoardInteractable(bool isInteractable)
        {
            for ( int edgeId = 0; edgeId < _edgeButtons.Length; edgeId++ )
            {
                _edgeButtons[ edgeId ].SetInteractable(isInteractable);
            }
        }

        public void ShowConfirmedEdge(int edgeId , PLAYER_INDEX_ENUM ownerPlayerIndex)
        {
            Color edgeColor = ownerPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE ? _playerOneEdgeColor : _playerTwoEdgeColor;

            GetEdgeButton(edgeId).SetVisual(edgeColor, EDGE_LINE_STYLE_ENUM.SOLID, false);
        }

        public void ShowOwnedBox(int boxId , PLAYER_INDEX_ENUM ownerPlayerIndex)
        {
            if ( boxId < 0 || boxId >= BoardTopology.BOX_COUNT )
            {
                throw new ArgumentOutOfRangeException(nameof(boxId));
            }

            Color ownerColor = ownerPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE ? _playerOneBoxColor : _playerTwoBoxColor;

            _boxVisuals[ boxId ].ShowOwned(ownerColor);
        }

        private void BuildBoxes()
        {
            for (int row = 0; row < BoardTopology.BOX_ROWS; row++)
            {
                for (int column = 0; column < BoardTopology.BOX_COLUMNS; column++)
                {
                    int boxId = BoardTopology.GetBoxID(row, column);
                    Image boxImg = Instantiate(_boxPrefabImg, _boardRootRectTrans, false);
                    BoardBoxVisual boxVisual = boxImg.GetComponent<BoardBoxVisual>();

                    boxImg.name = $"boxImg_{boxId:00}";
                    boxImg.raycastTarget = false;
                    boxVisual.SetAvailable(_availableBoxColor);
                    ConfigureBoxRect(boxImg.rectTransform, row, column);

                    _boxVisuals[boxId] = boxVisual;
                }
            }
        }

        private void BuildEdges()
        {
            for (int row = 0; row < BoardTopology.DOT_ROWS; row++)
            {
                for (int column = 0; column < BoardTopology.BOX_COLUMNS; column++)
                {
                    int edgeId = BoardTopology.GetHorizontalEdgeID(row, column);
                    CreateEdge(edgeId, row, column, true);
                }
            }

            for (int row = 0; row < BoardTopology.BOX_ROWS; row++)
            {
                for (int column = 0; column < BoardTopology.DOT_COLUMNS; column++)
                {
                    int edgeId = BoardTopology.GetVerticalEdgeID(row, column);
                    CreateEdge(edgeId, row, column, false);
                }
            }
        }

        private void BuildDots()
        {
            for (int row = 0; row < BoardTopology.DOT_ROWS; row++)
            {
                for (int column = 0; column < BoardTopology.DOT_COLUMNS; column++)
                {
                    Image dotImg = Instantiate(_dotPrefabImg, _boardRootRectTrans, false);

                    dotImg.name = $"dotImg_{row:00}_{column:00}";
                    dotImg.raycastTarget = false;
                    ConfigureDotRect(dotImg.rectTransform, row, column);
                }
            }
        }

        private void CreateEdge(int edgeId, int row, int column, bool isHorizontal)
        {
            BoardEdgeButton edgeBtn = Instantiate(_edgePrefabBtn, _boardRootRectTrans, false);

            edgeBtn.Initialize(edgeId, isHorizontal, _edgeVisibleThickness, _dotSize * 0.5f);
            edgeBtn.EdgeSelected += OnEdgeSelected;

            RectTransform edgeRectTrans = edgeBtn.GetComponent<RectTransform>();

            if (isHorizontal)
            {
                ConfigureHorizontalEdgeRect(edgeRectTrans, row, column);
            }
            else
            {
                ConfigureVerticalEdgeRect(edgeRectTrans, row, column);
            }

            _edgeButtons[edgeId] = edgeBtn;
        }

        private void ConfigureBoxRect(RectTransform boxRectTrans, int row, int column)
        {
            float minimumX = column / (float)BoardTopology.BOX_COLUMNS;
            float maximumX = (column + 1) / (float)BoardTopology.BOX_COLUMNS;
            float minimumY = 1f - (row + 1) / (float)BoardTopology.BOX_ROWS;
            float maximumY = 1f - row / (float)BoardTopology.BOX_ROWS;

            boxRectTrans.anchorMin = new Vector2(minimumX, minimumY);
            boxRectTrans.anchorMax = new Vector2(maximumX, maximumY);
            boxRectTrans.offsetMin = Vector2.one * _boxInset;
            boxRectTrans.offsetMax = Vector2.one * -_boxInset;

            ResetRectTransform(boxRectTrans);
        }

        private void ConfigureHorizontalEdgeRect(RectTransform edgeRectTrans, int row, int column)
        {
            float minimumX = column / (float)BoardTopology.BOX_COLUMNS;
            float maximumX = (column + 1) / (float)BoardTopology.BOX_COLUMNS;
            float anchorY = 1f - row / (float)BoardTopology.BOX_ROWS;

            edgeRectTrans.anchorMin = new Vector2(minimumX, anchorY);
            edgeRectTrans.anchorMax = new Vector2(maximumX, anchorY);
            edgeRectTrans.offsetMin = new Vector2(_dotSize * 0.5f, -_edgeTouchThickness * 0.5f);
            edgeRectTrans.offsetMax = new Vector2(-_dotSize * 0.5f, _edgeTouchThickness * 0.5f);

            ResetRectTransform(edgeRectTrans);
        }

        private void ConfigureVerticalEdgeRect(RectTransform edgeRectTrans, int row, int column)
        {
            float anchorX = column / (float)BoardTopology.BOX_COLUMNS;
            float minimumY = 1f - (row + 1) / (float)BoardTopology.BOX_ROWS;
            float maximumY = 1f - row / (float)BoardTopology.BOX_ROWS;

            edgeRectTrans.anchorMin = new Vector2(anchorX, minimumY);
            edgeRectTrans.anchorMax = new Vector2(anchorX, maximumY);
            edgeRectTrans.offsetMin = new Vector2(-_edgeTouchThickness * 0.5f, _dotSize * 0.5f);
            edgeRectTrans.offsetMax = new Vector2(_edgeTouchThickness * 0.5f, -_dotSize * 0.5f);

            ResetRectTransform(edgeRectTrans);
        }

        private void ConfigureDotRect(RectTransform dotRectTrans, int row, int column)
        {
            float anchorX = column / (float)BoardTopology.BOX_COLUMNS;
            float anchorY = 1f - row / (float)BoardTopology.BOX_ROWS;
            Vector2 anchor = new Vector2(anchorX, anchorY);

            dotRectTrans.anchorMin = anchor;
            dotRectTrans.anchorMax = anchor;
            dotRectTrans.sizeDelta = new Vector2(_dotSize, _dotSize);
            dotRectTrans.anchoredPosition = Vector2.zero;

            ResetRectTransform(dotRectTrans);
        }

        private static void ResetRectTransform(RectTransform targetRectTrans)
        {
            targetRectTrans.pivot = new Vector2(0.5f, 0.5f);
            targetRectTrans.localScale = Vector3.one;
            targetRectTrans.localRotation = Quaternion.identity;
        }

        private void OnConfirmButtonClicked()
        {
            ConfirmRequested?.Invoke();
        }

        private void OnEdgeSelected(int edgeId)
        {
            EdgeSelected?.Invoke(edgeId);
        }

        private bool ValidateReferences()
        {
            bool isValid = _boardRootRectTrans != null &&
               _dotPrefabImg != null &&
               _boxPrefabImg != null &&
               _edgePrefabBtn != null &&
               _confirmBtn != null &&
               _localPlayerScoreTxt != null &&
               _opponentScoreTxt != null &&
               _turnTxt != null &&
               _turnTimerTxt != null &&
               _connectionStateTxt != null &&
               _actionGuideTxt != null;

            if (!isValid)
            {
                Debug.LogError("GameBoard_View의 참조가 설정되지 않았습니다.", this);
            }

            return isValid;
        }
    }
}
