using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DotsAndBoxes.UI
{
    public sealed class MainLobby_presenter : APresenter
    {
        private readonly MainLobby_model _model;
        private readonly MainLobby_view _view;

        private readonly IOnlineSession _session;
        private readonly CancellationToken _cancellationToken;

        private bool _isBound;
        private bool _isDisposed;
        private bool _isCancellingMatchMaking;

        public event Action<MatchAssignment> MatchFound;
        public event Action<Exception> MatchMakingFailed;

        public MainLobby_presenter(MainLobby_model model , MainLobby_view view , IOnlineSession session , CancellationToken cancellationToken)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _cancellationToken = cancellationToken;
        }

        public override void Open()
        {
            ThrowIfDisposed();
            BindEvents();

            _view.Open();

            if ( _session.IsQueueing )
            {
                _model.TryBeginMatchMaking();
            }

            RefreshView();
        }

        public override void Close()
        {
            if(_isDisposed)
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

            MatchFound = null;
            MatchMakingFailed = null;
            _isDisposed = true;
        }

        private void BindEvents()
        {
            if(_isBound)
            {
                return;
            }

            _view.QuickMatchRequested += OnQuickMatchActioned;
            _view.MatchMakingCancelRequested += OnMatchMakingCancelActioned;
            _session.MatchFound += OnMatchFoundActioned;

            _isBound = true;
        }

        private void UnbindEvents()
        {
            if(!_isBound)
            {
                return;
            }

            _view.QuickMatchRequested -= OnQuickMatchActioned;
            _view.MatchMakingCancelRequested -= OnMatchMakingCancelActioned;
            _session.MatchFound -= OnMatchFoundActioned;

            _isBound = false;
        }

        private void OnQuickMatchActioned() => RequestQuickMatch_async().Forget();

        private async UniTask RequestQuickMatch_async()
        {
            if ( !_model.TryBeginMatchMaking() )
            {
                return;
            }

            RefreshView();

            try
            {
                await _session.Start_async(_cancellationToken);
                await _session.EnterMatchmaking_async(_cancellationToken);
            }
            catch ( OperationCanceledException ) when ( _cancellationToken.IsCancellationRequested )
            {
                //로비가 종료되면서 취소됨
            }
            catch ( Exception ex )
            {
                _model.EndMatchMaking();
                RefreshView();
                MatchMakingFailed?.Invoke(ex);
            }
        }

        private void OnMatchFoundActioned(MatchAssignment assignment)
        {
            if(_isDisposed)
            {
                return;
            }

            MatchFound?.Invoke(assignment);
        }

        private void OnMatchMakingCancelActioned() => CancelMatchMaking_async().Forget();

        private async UniTask CancelMatchMaking_async()
        {
            if ( _isDisposed || _isCancellingMatchMaking || !_model.IsMatchMaking )
            {
                return;
            }

            _isCancellingMatchMaking = true;
            RefreshView();

            try
            {
                bool wasCancelled = await _session.CancelMatchmaking_async(_cancellationToken);

                if ( wasCancelled )
                {
                    _model.EndMatchMaking();
                }
            }
            catch ( OperationCanceledException ) when ( _cancellationToken.IsCancellationRequested )
            {
                // Lobby가 종료되면서 취소된 경우입니다.
            }
            catch ( Exception exception )
            {
                MatchMakingFailed?.Invoke(exception);
            }
            finally
            {
                _isCancellingMatchMaking = false;

                if ( !_isDisposed )
                {
                    RefreshView();
                }
            }
        }

        private void RefreshView()
        {
            _view.SetQuickMatchInteractable(!_model.IsMatchMaking);
            _view.SetMatchMakingCancelInteractable(_model.IsMatchMaking && !_isCancellingMatchMaking);
            _view.SetWaitMatchingVisible(_model.IsMatchMaking);
        }

        private void ThrowIfDisposed()
        {
            if( _isDisposed )
            {
                throw new ObjectDisposedException(nameof(MainLobby_presenter));
            }
        }
    }
}

