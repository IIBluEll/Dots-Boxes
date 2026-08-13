using System;
using HM.CodeBase;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameBoard_Presenter : APresenter
    {
        private readonly GameBoard_Model _model;
        private readonly GameBoard_View _view;

        private bool _isBound;
        private bool _isDisposed;

        public GameBoard_Presenter(GameBoard_Model model, GameBoard_View view)
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
            if (_isDisposed)
            {
                return;
            }

            _view.Close();
        }

        public override void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            UnbindEvents();
            _isDisposed = true;
        }

        private void BindEvents()
        {
            if (_isBound)
            {
                return;
            }

            _view.EdgeSelected += OnEdgeSelected;
            _isBound = true;
        }

        private void UnbindEvents()
        {
            if (!_isBound)
            {
                return;
            }

            _view.EdgeSelected -= OnEdgeSelected;
            _isBound = false;
        }

        private void RefreshView()
        {
            _view.ShowAllEdgesAvailable();

            if (_model.HasPreview)
            {
                _view.ShowLocalPreviewEdge(_model.PreviewEdgeId);
            }
        }

        private void OnEdgeSelected(int edgeId)
        {
            int previousPreviewEdgeId = _model.PreviewEdgeId;
            bool isPreviewChanged = _model.TrySetPreviewEdge(edgeId);

            if (!isPreviewChanged)
            {
                return;
            }

            if (previousPreviewEdgeId != GameBoard_Model.NO_PREVIEW_EDGE_ID && previousPreviewEdgeId != edgeId)
            {
                _view.ShowAvailableEdge(previousPreviewEdgeId);
            }

            _view.ShowLocalPreviewEdge(edgeId);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GameBoard_Presenter));
            }
        }
    }
}
