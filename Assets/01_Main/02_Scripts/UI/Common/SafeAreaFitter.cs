using UnityEngine;
using DeviceScreen = UnityEngine.Device.Screen;

namespace DotsAndBoxes.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _safeAreaRectTrans;

        private Rect _previousSafeArea;
        private Vector2Int _previousScreenSize;

        private void Awake()
        {
            _safeAreaRectTrans = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void Update()
        {
            Rect currentSafeArea = DeviceScreen.safeArea;
            Vector2Int currentScreenSize = new Vector2Int(DeviceScreen.width, DeviceScreen.height);

            bool isSafeAreaChanged = currentSafeArea != _previousSafeArea;
            bool isScreenSizeChanged = currentScreenSize != _previousScreenSize;

            if ( !isSafeAreaChanged && !isScreenSizeChanged )
            {
                return;
            }

            ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            int screenWidth = DeviceScreen.width;
            int screenHeight = DeviceScreen.height;

            if ( screenWidth <= 0 || screenHeight <= 0 )
            {
                return;
            }

            Rect safeArea = DeviceScreen.safeArea;

            Vector2 anchorMin = new Vector2(safeArea.xMin / screenWidth, safeArea.yMin / screenHeight);

            Vector2 anchorMax = new Vector2(safeArea.xMax / screenWidth, safeArea.yMax / screenHeight);

            _safeAreaRectTrans.anchorMin = anchorMin;
            _safeAreaRectTrans.anchorMax = anchorMax;
            _safeAreaRectTrans.offsetMin = Vector2.zero;
            _safeAreaRectTrans.offsetMax = Vector2.zero;

            _previousSafeArea = safeArea;
            _previousScreenSize = new Vector2Int(screenWidth , screenHeight);
        }
    }
}