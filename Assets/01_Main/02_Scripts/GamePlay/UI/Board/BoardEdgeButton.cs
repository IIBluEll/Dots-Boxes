using System;
using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay
{
    public enum EDGE_LINE_STYLE_ENUM
    {
        DASHED,
        SOLID
    }

    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class BoardEdgeButton : MonoBehaviour
    {
        [SerializeField] private Button _edgeBtn;
        [SerializeField] private Image _visibleLineImg;

        [Space(5f), Header("Edge Sprites")]
        [SerializeField] private Sprite _horizontalDashedEdgeSprite;
        [SerializeField] private Sprite _verticalDashedEdgeSprite;
        [SerializeField] private Sprite _horizontalSolidEdgeSprite;
        [SerializeField] private Sprite _verticalSolidEdgeSprite;

        private bool _isHorizontal;

        public int EdgeId { get; private set; } = -1;

        public event Action<int> EdgeSelected;

        private void Awake()
        {
            if (_edgeBtn == null)
            {
                _edgeBtn = GetComponent<Button>();
            }

            _edgeBtn.onClick.AddListener(OnEdgeButtonClicked);
        }

        private void OnDestroy()
        {
            if (_edgeBtn != null)
            {
                _edgeBtn.onClick.RemoveListener(OnEdgeButtonClicked);
            }
        }

        public void Initialize(int edgeId, bool isHorizontal, float visibleThickness, float visibleOverlap)
        {
            EdgeId = edgeId;
            _isHorizontal = isHorizontal;

            gameObject.name = $"edgeBtn_{edgeId:00}";
            ConfigureVisibleLine(isHorizontal, visibleThickness, visibleOverlap);
            ApplyLineSprite(EDGE_LINE_STYLE_ENUM.DASHED);
        }

        public void SetVisual(Color visibleColor, EDGE_LINE_STYLE_ENUM lineStyle, bool isInteractable)
        {
            ApplyLineSprite(lineStyle);

            _visibleLineImg.color = visibleColor;
            _edgeBtn.interactable = isInteractable;
        }

        public void SetInteractable(bool isInteractable)
        {
            _edgeBtn.interactable = isInteractable;
        }

        private void ApplyLineSprite(EDGE_LINE_STYLE_ENUM lineStyle)
        {
            if ( lineStyle == EDGE_LINE_STYLE_ENUM.DASHED )
            {
                _visibleLineImg.sprite = _isHorizontal ? _horizontalDashedEdgeSprite : _verticalDashedEdgeSprite;

                return;
            }

            _visibleLineImg.sprite = _isHorizontal ? _horizontalSolidEdgeSprite : _verticalSolidEdgeSprite;
        }

        private void ConfigureVisibleLine(bool isHorizontal, float visibleThickness, float visibleOverlap)
        {
            RectTransform visibleLineRectTrans = _visibleLineImg.rectTransform;

            visibleLineRectTrans.pivot = new Vector2(0.5f, 0.5f);
            visibleLineRectTrans.localScale = Vector3.one;
            visibleLineRectTrans.localRotation = Quaternion.identity;

            if (isHorizontal)
            {
                visibleLineRectTrans.anchorMin = new Vector2(0f, 0.5f);
                visibleLineRectTrans.anchorMax = new Vector2(1f, 0.5f);
                visibleLineRectTrans.offsetMin = new Vector2(-visibleOverlap, -visibleThickness * 0.5f);
                visibleLineRectTrans.offsetMax = new Vector2(visibleOverlap, visibleThickness * 0.5f);
                return;
            }

            visibleLineRectTrans.anchorMin = new Vector2(0.5f, 0f);
            visibleLineRectTrans.anchorMax = new Vector2(0.5f, 1f);
            visibleLineRectTrans.offsetMin = new Vector2(-visibleThickness * 0.5f, -visibleOverlap);
            visibleLineRectTrans.offsetMax = new Vector2(visibleThickness * 0.5f, visibleOverlap);
        }

        private void OnEdgeButtonClicked()
        {
            if (EdgeId < 0)
            {
                return;
            }

            EdgeSelected?.Invoke(EdgeId);
        }
    }
}
