using HM.CodeBase;
using System;

namespace DotsAndBoxes.Gameplay
{
    public sealed class PausePresenter_presenter : APresenter
    {
        private readonly PauseModel_model _model;
        private readonly Pause_view _view;

        private bool _isBound;
        private bool _isDisposed;

        public event Action ExitRequested;

        public PausePresenter_presenter(PauseModel_model model , Pause_view view)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void Initialize()
        {
            ThrowIfDisposed();
            BindEvents();
            Close();
        }

        public override void Open()
        {
            ThrowIfDisposed();

            if ( _model.IsOpen )
            {
                return;
            }

            _model.Open();
            _view.Open();
        }

        public override void Close()
        {
            if ( _isDisposed || !_model.IsOpen )
            {
                _view.Close();
                return;
            }

            _model.Close();
            _view.Close();
        }

        public override void Dispose()
        {
            if ( _isDisposed )
            {
                return;
            }

            UnbindEvents();
            ExitRequested = null;
            _isDisposed = true;
        }

        private void BindEvents()
        {
            if ( _isBound )
            {
                return;
            }

            _view.PauseRequested += OnPauseRequestedActioned;
            _view.ContinueRequested += OnContinueRequestedActioned;
            _view.ExitRequested += OnExitRequestedActioned;
            _isBound = true;
        }

        private void UnbindEvents()
        {
            if ( !_isBound )
            {
                return;
            }

            _view.PauseRequested -= OnPauseRequestedActioned;
            _view.ContinueRequested -= OnContinueRequestedActioned;
            _view.ExitRequested -= OnExitRequestedActioned;
            _isBound = false;
        }

        private void OnPauseRequestedActioned()
        {
            Open();
        }

        private void OnContinueRequestedActioned()
        {
            Close();
        }

        private void OnExitRequestedActioned()
        {
            Close();
            ExitRequested?.Invoke();
        }

        private void ThrowIfDisposed()
        {
            if ( _isDisposed )
            {
                throw new ObjectDisposedException(nameof(PausePresenter_presenter));
            }
        }
    }
}
