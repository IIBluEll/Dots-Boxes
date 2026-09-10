using System;
using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PauseUI : MonoBehaviour
    {
        [SerializeField] private Pause_view _pauseView;

        private PauseModel_model _pauseModel;
        private PausePresenter_presenter _pausePresenter;

        public event Action ExitRequested;

        private void Awake()
        {
            if ( _pauseView == null )
            {
                Debug.LogError("Pause_view 참조가 설정되지 않았습니다." , this);
                enabled = false;
                return;
            }

            if ( !_pauseView.Initialize() )
            {
                enabled = false;
                return;
            }

            _pauseModel = new PauseModel_model();
            _pausePresenter = new PausePresenter_presenter(_pauseModel , _pauseView);
            _pausePresenter.ExitRequested += OnExitRequestedActioned;
            _pausePresenter.Initialize();
        }

        private void OnDestroy()
        {
            if ( _pausePresenter == null )
            {
                return;
            }

            _pausePresenter.ExitRequested -= OnExitRequestedActioned;
            _pausePresenter.Dispose();
            _pausePresenter = null;
            _pauseModel = null;
            ExitRequested = null;
        }

        public void Close()
        {
            _pausePresenter?.Close();
        }

        private void OnExitRequestedActioned()
        {
            ExitRequested?.Invoke();
        }
    }
}
