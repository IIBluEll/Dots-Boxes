using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameBoard_Presenter : APresenter
    {
        private readonly GameBoard_Model _model;
        private readonly GameBoard_View _view;

        private bool _isBound;
        private bool _isDisposed;

        public event Action<GAME_RESULT_ENUM, int, int> GameFinished;

        public GameBoard_Presenter(GameBoard_Model model , GameBoard_View view)
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
            GameFinished = null;
            _isDisposed = true;
        }

        private void BindEvents()
        {
            if ( _isBound )
            {
                return;
            }

            _view.EdgeSelected += OnEdgeSelected;
            _view.ConfirmRequested += OnConfirmRequested;
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
            _isBound = false;
        }

        private void RefreshView()
        {
            _view.ShowAllEdgesAvailable();

            for ( int edgeId = 0; edgeId < BoardTopology.EDGE_COUNT; edgeId++ )
            {
                EdgeData edge = _model.Board.GetEdge(edgeId);

                if ( edge.IsConfirmed )
                {
                    _view.ShowConfirmedEdge(edgeId , edge.OwnerPlayerIndex);
                }
            }

            for ( int boxId = 0; boxId < BoardTopology.BOX_COUNT; boxId++ )
            {
                BoxData box = _model.Board.GetBox(boxId);

                if ( box.IsOwned )
                {
                    _view.ShowOwnedBox(boxId , box.OwnerPlayerIndex);
                }
            }

            if ( _model.HasPreview )
            {
                _view.ShowLocalPreviewEdge(_model.PreviewEdgeId);
            }

            _view.SetConfirmInteractable(_model.HasPreview);
            RefreshStatus();

            if ( _model.Board.IsGameFinished )
            {
                _view.SetBoardInteractable(false);
            }
        }

        private void RefreshStatus()
        {
            _view.ShowScores(_model.Board.PlayerOneScore , _model.Board.PlayerTwoScore);
            _view.ShowCurrentTurn(_model.Board.CurrentPlayerIndex);
        }

        private void OnEdgeSelected(int edgeId)
        {
            int previousPreviewEdgeId = _model.PreviewEdgeId;
            bool isPreviewChanged = _model.TrySetPreviewEdge(edgeId);

            if ( !isPreviewChanged )
            {
                return;
            }

            if ( previousPreviewEdgeId != GameBoard_Model.NO_PREVIEW_EDGE_ID && previousPreviewEdgeId != edgeId )
            {
                _view.ShowAvailableEdge(previousPreviewEdgeId);
            }

            _view.ShowLocalPreviewEdge(edgeId);
            _view.SetConfirmInteractable(true);
        }

        private void OnConfirmRequested()
        {
            PLAYER_INDEX_ENUM confirmingPlayerIndex = _model.Board.CurrentPlayerIndex;

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

            GameFinished?.Invoke(
                _model.Board.GameResult ,
                _model.Board.PlayerOneScore ,
                _model.Board.PlayerTwoScore);
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