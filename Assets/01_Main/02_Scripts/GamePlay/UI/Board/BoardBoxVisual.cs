using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.Gameplay
{
    public sealed class BoardBoxVisual : MonoBehaviour
    {
        [SerializeField] private Image _backgroundImg;
        [SerializeField] private Image _crayonFillImg;

        public RectTransform RectTransform => (RectTransform)transform;

        private void Awake()
        {
            bool isValid = _backgroundImg != null && _crayonFillImg != null;

            if (isValid)
            {
                return;
            }

            Debug.LogError("BoardBoxVisual의 UI 참조가 설정되지 않았습니다.", this);
            enabled = false;
        }

        public void SetAvailable(Color backgroundColor)
        {
            if (!enabled)
            {
                return;
            }

            _backgroundImg.color = backgroundColor;
            _crayonFillImg.color = Color.clear;
            _crayonFillImg.gameObject.SetActive(false);
        }

        public void ShowOwned(Color ownerColor)
        {
            if (!enabled)
            {
                return;
            }

            _crayonFillImg.color = ownerColor;
            _crayonFillImg.gameObject.SetActive(true);
        }
    }
}
