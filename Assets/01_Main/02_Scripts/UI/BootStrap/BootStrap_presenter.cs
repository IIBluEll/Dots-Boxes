using HM.CodeBase;
using System;

namespace DotsAndBoxes.UI
{
    public sealed class Bootstrap_presenter : APresenter
    {
        private readonly BootStrap_model _model;
        private readonly Bootstrap_view _view;

        private bool _isBound;
        private bool _isDisposed;

        public event Action RetryRequested;

        public Bootstrap_presenter(BootStrap_model model , Bootstrap_view view)
        {
            _model = model;
            _view = view;
        }

        public override void Open()
        {
            ThrowIfDisposed();
            BindEvents();

            _view.Open();
            RefreshView();
        }

        public override void Close()
        {
            if ( _isDisposed )
            {
                return;
            }

            _view.Close();
        }

        public override void Dispose()
        {
            if( _isDisposed )
            {
                return;
            }

            UnbindEvents();

            RetryRequested = null;
            _isDisposed = false;
        }

        public void BeginLoading()
        {
            ThrowIfDisposed();

            _model.BeginLoading();
            RefreshView();
        }

        public void UpdateLoading(float progress, string statusMessage)
        {
            ThrowIfDisposed();

            _model.UpdateLoading(progress, statusMessage);
            RefreshView();
        }

        public void Complete(string statusMessage)
        {
            ThrowIfDisposed();

            _model.Complete(statusMessage);
            RefreshView();
        }

        public void Fail(string errorMessage)
        {
            ThrowIfDisposed();

            _model.Fail(errorMessage);
            RefreshView();
        }

        private void BindEvents()
        {
            if ( _isBound )
            {
                return;
            }

            _view.RetryRequested += OnRetryRequestedActioned;
            _isBound = true;
        }

        private void UnbindEvents()
        {
            if ( !_isBound )
            {
                return;
            }

            _view.RetryRequested -= OnRetryRequestedActioned;
            _isBound = false;
        }


        private void OnRetryRequestedActioned()
        {
            if(_isDisposed || _model.IsLoading)
            {
                return;
            }

            RetryRequested?.Invoke();
        }

        private void RefreshView()
        {
            _view.SetProgress(_model.Progress);
            _view.SetStatus(_model.StatusMessage);
            _view.SetRetryInteractable(!_model.IsLoading);

            if ( _model.HasError )
            {
                _view.ShowError(_model.ErrorMessage);
            }
            else
            {
                _view.HideError();
            }
        }

        private void ThrowIfDisposed()
        {
            if ( _isDisposed )
            {
                throw new ObjectDisposedException(nameof(Bootstrap_presenter));
            }
        }
    }
}