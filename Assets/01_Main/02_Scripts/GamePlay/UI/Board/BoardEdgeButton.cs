using System;
using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay
{
    [RequireComponent(typeof(Button), typeof(Image))]
    public sealed class BoardEdgeButton : MonoBehaviour
    {
        [SerializeField] private Button _edgeBtn;
        [SerializeField] private Image _visibleLineImg;

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

        public void Initialize(int edgeId, bool isHorizontal, float visibleThickness)
        {
            EdgeId = edgeId;
            gameObject.name = $"edgeBtn_{edgeId:00}";
            ConfigureVisibleLine(isHorizontal, visibleThickness);
        }

        public void SetVisual(Color visibleColor, bool isInteractable)
        {
            _visibleLineImg.color = visibleColor;
            _edgeBtn.interactable = isInteractable;
        }

        private void ConfigureVisibleLine(bool isHorizontal, float visibleThickness)
        {
            RectTransform visibleLineRectTrans = _visibleLineImg.rectTransform;

            visibleLineRectTrans.pivot = new Vector2(0.5f, 0.5f);
            visibleLineRectTrans.localScale = Vector3.one;
            visibleLineRectTrans.localRotation = Quaternion.identity;

            if (isHorizontal)
            {
                visibleLineRectTrans.anchorMin = new Vector2(0f, 0.5f);
                visibleLineRectTrans.anchorMax = new Vector2(1f, 0.5f);
                visibleLineRectTrans.offsetMin = new Vector2(0f, -visibleThickness * 0.5f);
                visibleLineRectTrans.offsetMax = new Vector2(0f, visibleThickness * 0.5f);
                return;
            }

            visibleLineRectTrans.anchorMin = new Vector2(0.5f, 0f);
            visibleLineRectTrans.anchorMax = new Vector2(0.5f, 1f);
            visibleLineRectTrans.offsetMin = new Vector2(-visibleThickness * 0.5f, 0f);
            visibleLineRectTrans.offsetMax = new Vector2(visibleThickness * 0.5f, 0f);
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
