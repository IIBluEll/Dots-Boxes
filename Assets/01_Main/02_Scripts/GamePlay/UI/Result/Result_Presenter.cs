using HM.CodeBase;
using System;

namespace DotsAndBoxes.Gameplay
{
    public sealed class Result_Presenter : APresenter
    {
        private readonly Result_Model _model;
        private readonly Result_View _view;

        private bool _isBound;
        private bool _isDisposed;

        public event Action LobbyRequested;
        public event Action CancelRequested;

        public Result_Presenter(Result_Model model , Result_View view)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public override void Open()
        {
            ThrowIfDisposed();
            BindEvents();

            _view.Open();
            RefreshView();
        }

        public void OpenMessage(string title , string message)
        {
            ThrowIfDisposed();
            BindEvents();

            _view.Open();
            _view.ShowMessage(title , message);
        }

        public void OpenConfirmation(string title , string message)
        {
            ThrowIfDisposed();
            BindEvents();

            _view.Open();
            _view.ShowConfirmation(title , message);
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
            if ( _isDisposed )
            {
                return;
            }

            UnbindEvents();
            
            LobbyRequested = null;
            CancelRequested = null;

            _isDisposed = true;
        }

        private void BindEvents()
        {
            if ( _isBound )
            {
                return;
            }

            _view.LobbyRequested += OnLobbyRequested;
            _view.CancelRequested += OnCancelRequestedActioned;

            _isBound = true;
        }

        private void UnbindEvents()
        {
            if ( !_isBound )
            {
                return;
            }

            _view.LobbyRequested -= OnLobbyRequested;
            _view.CancelRequested -= OnCancelRequestedActioned;

            _isBound = false;
        }

        private void RefreshView()
        {
            if ( !_model.HasResult )
            {
                throw new InvalidOperationException("Result_Model에 게임 결과가 설정되지 않았습니다.");
            }

            _view.ShowResult(_model.GameResult , _model.PlayerOneScore , _model.PlayerTwoScore);
        }

        private void OnLobbyRequested()
        {
            Close();
            LobbyRequested?.Invoke();
        }

        private void OnCancelRequestedActioned()
        {
            Close();
            CancelRequested?.Invoke();
        }

        private void ThrowIfDisposed()
        {
            if ( _isDisposed )
            {
                throw new ObjectDisposedException(nameof(Result_Presenter));
            }
        }
    }
}
