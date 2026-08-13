using UnityEngine;

namespace DotsAndBoxes.Gameplay
{
    public sealed class GameBoard_presenter : MonoBehaviour
    {
        [SerializeField] private GameBoard_view _view;
        private GameBoard_model _model;

        private void Awake()
        {
            if ( _view == null )
            {
                Debug.LogError("View 참조 안됨" , this);

                enabled = false;
                return;
            }

            _model = new GameBoard_model();
        }

        private void OnEnable()
        {
            if ( _view != null )
            {
                _view.EdgeSelected += OnEdgeSelected;
            }
        }

        private void Start()
        {
            _view.ShowAllEdgesAvaliable();
        }

        private void OnDisable()
        {
            if ( _view != null )
            {
                _view.EdgeSelected -= OnEdgeSelected;
            }
        }

        private void OnEdgeSelected(int edgeId)
        {
            int previousPreviewEdgeId = _model.PreviewEdgeId;

            bool isPreviewChanged = _model.TrySetPreviewEdge(edgeId);

            if ( !isPreviewChanged )
            {
                return;
            }

            if ( previousPreviewEdgeId != GameBoard_model.NO_PREVIEW_EDGE_ID && previousPreviewEdgeId != edgeId )
            {
                _view.ShowAvailableEdge(previousPreviewEdgeId);
            }

            _view.ShowLocalPreviewEdge(edgeId);
        }
    }
}