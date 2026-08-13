using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class GameBoardUI : MonoBehaviour
    {
        [SerializeField] private GameBoard_View _gameBoardView;

        private GameBoard_Model _gameBoardModel;
        private GameBoard_Presenter _gameBoardPresenter;
        private bool _hasStarted;

        private void Awake()
        {
            if (_gameBoardView == null)
            {
                Debug.LogError("GameBoard_View 참조가 설정되지 않았습니다.", this);
                enabled = false;
                return;
            }

            _gameBoardModel = new GameBoard_Model();
            _gameBoardPresenter = new GameBoard_Presenter(_gameBoardModel, _gameBoardView);
        }

        private void Start()
        {
            _hasStarted = true;
            _gameBoardPresenter.Open();
        }

        private void OnEnable()
        {
            if (_hasStarted)
            {
                _gameBoardPresenter.Open();
            }
        }

        private void OnDisable()
        {
            if (_hasStarted)
            {
                _gameBoardPresenter.Close();
            }
        }

        private void OnDestroy()
        {
            _gameBoardPresenter?.Dispose();
        }

        public void Open()
        {
            _gameBoardPresenter?.Open();
        }

        public void Close()
        {
            _gameBoardPresenter?.Close();
        }
    }
}
