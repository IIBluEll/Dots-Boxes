using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

        [Space(5f), Header("Buttons")]
        [SerializeField] private Button _confirmBtn;

        [Space(5f), Header("Player Colors")]
        [SerializeField] private Color _playerOneEdgeColor = new Color(0.1f, 0.45f, 1f, 1f);
        [SerializeField] private Color _playerTwoEdgeColor = new Color(1f, 0.4f, 0.1f, 1f);
        [SerializeField] private Color _playerOneBoxColor = new Color(0.1f, 0.45f, 1f, 0.35f);
        [SerializeField] private Color _playerTwoBoxColor = new Color(1f, 0.4f, 0.1f, 0.35f);

        [Space(5f), Header("Status")]
        [SerializeField] private TMP_Text _playerOneScoreTxt;
        [SerializeField] private TMP_Text _playerTwoScoreTxt;
        [SerializeField] private TMP_Text _turnTxt;

        private BoardEdgeButton[] _edgeButtons;
        private Image[] _boxImages;

        public IReadOnlyList<BoardEdgeButton> EdgeButtons => _edgeButtons;
        public IReadOnlyList<Image> BoxImages => _boxImages;

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
            if ( _edgeButtons == null || _boxImages == null )
            {
                return;
            }

            ShowAllEdgesAvailable();

            for ( int boxId = 0; boxId < _boxImages.Length; boxId++ )
            {
                _boxImages[ boxId ].color = _availableBoxColor;
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

        public void ShowScores(int playerOneScore , int playerTwoScore)
        {
            _playerOneScoreTxt.text = $"PLAYER 1 : {playerOneScore}";
            _playerTwoScoreTxt.text = $"PLAYER 2 : {playerTwoScore}";
        }

        public void ShowCurrentTurn(PLAYER_INDEX_ENUM currentPlayerIndex)
        {
            bool isPlayerOneTurn = currentPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE;

            _turnTxt.text = isPlayerOneTurn ? "PLAYER 1 TURN" : "PLAYER 2 TURN";
            _turnTxt.color = isPlayerOneTurn ? _playerOneEdgeColor : _playerTwoEdgeColor;
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
            edgeBtn.SetVisual(_availableEdgeColor, true);
        }

        public void ShowLocalPreviewEdge(int edgeId)
        {
            BoardEdgeButton edgeBtn = GetEdgeButton(edgeId);
            edgeBtn.SetVisual(_localPreviewEdgeColor, true);
        }

        private void BuildBoard()
        {
            _edgeButtons = new BoardEdgeButton[BoardTopology.EDGE_COUNT];
            _boxImages = new Image[BoardTopology.BOX_COUNT];

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

            GetEdgeButton(edgeId).SetVisual(edgeColor , false);
        }

        public void ShowOwnedBox(int boxId , PLAYER_INDEX_ENUM ownerPlayerIndex)
        {
            if ( boxId < 0 || boxId >= BoardTopology.BOX_COUNT )
            {
                throw new ArgumentOutOfRangeException(nameof(boxId));
            }

            _boxImages[ boxId ].color = ownerPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE ? _playerOneBoxColor : _playerTwoBoxColor;
        }

        private void BuildBoxes()
        {
            for (int row = 0; row < BoardTopology.BOX_ROWS; row++)
            {
                for (int column = 0; column < BoardTopology.BOX_COLUMNS; column++)
                {
                    int boxId = BoardTopology.GetBoxID(row, column);
                    Image boxImg = Instantiate(_boxPrefabImg, _boardRootRectTrans, false);

                    boxImg.name = $"boxImg_{boxId:00}";
                    boxImg.color = _availableBoxColor;
                    boxImg.raycastTarget = false;
                    ConfigureBoxRect(boxImg.rectTransform, row, column);

                    _boxImages[boxId] = boxImg;
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

            edgeBtn.Initialize(edgeId, isHorizontal, _edgeVisibleThickness);
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
                           _playerOneScoreTxt != null &&
                           _playerTwoScoreTxt != null &&
                           _turnTxt != null;

            if (!isValid)
            {
                Debug.LogError("GameBoard_View의 참조가 설정되지 않았습니다.", this);
            }

            return isValid;
        }
    }
}
