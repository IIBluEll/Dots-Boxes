using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameBoard_Presenter : APresenter
    {
        private readonly GameBoard_Model _model;
        private readonly GameBoard_View _view;
        private readonly IGameSession _session;

        private bool _isBound;
        private bool _isDisposed;
        private bool _isOpen;
        private bool _isConfirming;

        public event Action<GAME_RESULT_ENUM, int, int> GameFinished;
        public event Action<Exception> SessionFailed;

        public GameBoard_Presenter(GameBoard_Model model , GameBoard_View view)
            : this(model , view , null)
        {
        }

        public GameBoard_Presenter(GameBoard_Model model , GameBoard_View view , IGameSession session)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session;
        }

        public override void Open()
        {
            ThrowIfDisposed();
            BindEvents();

            _isOpen = true;
            _view.Open();
            RefreshConnectionState();

            if ( _session != null && _session.HasSnapshot )
            {
                _model.ApplySnapshot(_session.CurrentSnapshot);
            }

            RefreshView();
        }

        public override void Close()
        {
            if ( _isDisposed )
            {
                return;
            }

            _isOpen = false;
            _view.Close();
        }

        public override void Dispose()
        {
            if ( _isDisposed )
            {
                return;
            }

            UnbindEvents();

            GameFinished = null;
            SessionFailed = null;

            _isConfirming = false;
            _isOpen = false;
            _isDisposed = true;
        }

        public bool ApplySnapshot(MatchSnapshot snapshot)
        {
            ThrowIfDisposed();

            bool isApplied = _model.ApplySnapshot(snapshot);

            if ( !isApplied )
            {
                return false;
            }

            if ( !_isOpen )
            {
                return true;
            }

            RefreshView();

            if ( _model.IsGameFinished )
            {
                NotifyGameFinished();
            }

            return true;
        }

        private void BindEvents()
        {
            if ( _isBound )
            {
                return;
            }

            _view.EdgeSelected += OnEdgeSelected;
            _view.ConfirmRequested += OnConfirmRequested;

            if ( _session != null )
            {
                _session.SnapshotChanged += OnSnapshotChanged;
                _session.ConnectionStateChanged += OnConnectionStateChanged;
            }

            _isBound = true;
        }

        private void UnbindEvents()
        {
            if ( !_isBound )
            {
                return;
            }

            _view.EdgeSelected -= OnEdgeSelected;
            _view.ConfirmRequested -= OnConfirmRequested;

            if ( _session != null )
            {
                _session.SnapshotChanged -= OnSnapshotChanged;
                _session.ConnectionStateChanged -= OnConnectionStateChanged;
            }

            _isBound = false;
        }

        private void RefreshView()
        {
            bool canSelectEdge = CanSelectEdge();
            bool canConfirmPreview = CanConfirmPreview();

            _view.ShowAllEdgesAvailable();

            for ( int edgeId = 0; edgeId < BoardTopology.EDGE_COUNT; edgeId++ )
            {
                PLAYER_INDEX_ENUM ownerPlayerIndex = _model.GetEdgeOwner(edgeId);

                if ( ownerPlayerIndex != PLAYER_INDEX_ENUM.NONE )
                {
                    _view.ShowConfirmedEdge(edgeId , ownerPlayerIndex);
                }
            }

            for ( int boxId = 0; boxId < BoardTopology.BOX_COUNT; boxId++ )
            {
                PLAYER_INDEX_ENUM ownerPlayerIndex = _model.GetBoxOwner(boxId);

                if ( ownerPlayerIndex != PLAYER_INDEX_ENUM.NONE )
                {
                    _view.ShowOwnedBox(boxId , ownerPlayerIndex);
                }
            }

            if ( _model.HasPreview )
            {
                _view.ShowLocalPreviewEdge(_model.PreviewEdgeId);
            }

            if ( !canSelectEdge )
            {
                _view.SetBoardInteractable(false);
            }

            _view.SetConfirmInteractable(canConfirmPreview);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            _view.ShowScores(_model.PlayerOneScore , _model.PlayerTwoScore);
            _view.ShowCurrentTurn(_model.CurrentPlayerIndex);
        }

        private void RefreshConnectionState()
        {
            bool hasOnlineSession = _session != null;

            _view.SetConnectionStateVisible(hasOnlineSession);

            if (hasOnlineSession)
            {
                _view.ShowConnectionState(_session.ConnectionState);
            }
        }

        private bool CanSelectEdge()
        {
            if ( !_model.CanSelectEdge || _isConfirming )
            {
                return false;
            }

            if ( _session == null )
            {
                return true;
            }

            return _session.CanConfirmCurrentTurn && !_session.HasPendingConfirm;
        }

        private bool CanConfirmPreview()
        {
            if ( !_model.HasPreview || _isConfirming )
            {
                return false;
            }

            if ( _session == null )
            {
                return _model.CanSelectEdge;
            }

            if ( !_session.CanConfirmCurrentTurn )
            {
                return false;
            }

            return !_session.HasPendingConfirm || _session.PendingConfirmEdgeId == _model.PreviewEdgeId;
        }

        private void OnEdgeSelected(int edgeId)
        {
            if ( !CanSelectEdge() )
            {
                return;
            }

            int previousPreviewEdgeId = _model.PreviewEdgeId;
            bool isPreviewChanged = _model.TrySetPreviewEdge(edgeId);

            if ( !isPreviewChanged )
            {
                return;
            }

            if ( previousPreviewEdgeId != GameBoard_Model.NO_PREVIEW_EDGE_ID &&
                previousPreviewEdgeId != edgeId )
            {
                _view.ShowAvailableEdge(previousPreviewEdgeId);
            }

            _view.ShowLocalPreviewEdge(edgeId);
            _view.SetConfirmInteractable(true);
        }

        private async void OnConfirmRequested()
        {
            if ( _session == null )
            {
                ConfirmLocalPreview();
                return;
            }

            await ConfirmOnlinePreview_async();
        }

        private void OnSnapshotChanged(MatchSnapshot snapshot)
        {
            ApplySnapshot(snapshot);
        }

        private void OnConnectionStateChanged(GAME_SESSION_CONNECTION_STATE_ENUM connectionState)
        {
            if (!_isOpen)
            {
                return;
            }

            _view.ShowConnectionState(connectionState);
            RefreshView();
        }

        private void ConfirmLocalPreview()
        {
            PLAYER_INDEX_ENUM confirmingPlayerIndex = _model.CurrentPlayerIndex;

            if ( !_model.TryConfirmPreview(out MoveResult moveResult) )
            {
                return;
            }

            _view.ShowConfirmedEdge(moveResult.EdgeId , confirmingPlayerIndex);

            for ( int i = 0; i < moveResult.CompletedBoxIds.Count; i++ )
            {
                int boxId = moveResult.CompletedBoxIds[i];
                _view.ShowOwnedBox(boxId , confirmingPlayerIndex);
            }

            _view.SetConfirmInteractable(false);
            RefreshStatus();

            if ( !moveResult.IsGameFinished )
            {
                return;
            }

            _view.SetBoardInteractable(false);
            NotifyGameFinished();
        }

        private async Task ConfirmOnlinePreview_async()
        {
            if ( !CanConfirmPreview() )
            {
                return;
            }

            int previewEdgeId = _model.PreviewEdgeId;

            _isConfirming = true;
            _view.SetConfirmInteractable(false);
            _view.SetBoardInteractable(false);

            try
            {
                await _session.ConfirmEdge_async(previewEdgeId);
            }
            catch ( Exception exception )
            {
                if ( !_isDisposed )
                {
                    SessionFailed?.Invoke(exception);
                }
            }
            finally
            {
                _isConfirming = false;

                if ( _isOpen && !_isDisposed )
                {
                    RefreshView();
                }
            }
        }

        private void NotifyGameFinished()
        {
            GameFinished?.Invoke(_model.GameResult , _model.PlayerOneScore , _model.PlayerTwoScore);
        }

        private void ThrowIfDisposed()
        {
            if ( _isDisposed )
            {
                throw new ObjectDisposedException(nameof(GameBoard_Presenter));
            }
        }
    }
}
