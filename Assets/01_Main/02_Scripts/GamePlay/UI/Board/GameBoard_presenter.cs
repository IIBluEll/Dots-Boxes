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
        private bool _isLocalExtraTurn;

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

            _isLocalExtraTurn = false;
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
            _isLocalExtraTurn = false;
            _isOpen = false;
            _isDisposed = true;
        }

        public bool ApplySnapshot(MatchSnapshot snapshot)
        {
            ThrowIfDisposed();

            bool hadPreviousSnapshot = _model.HasServerSnapshot;
            long previousRevision = _model.Revision;
            PLAYER_INDEX_ENUM localPlayerIndex = GetLocalPlayerIndex();
            PLAYER_INDEX_ENUM previousPlayerIndex = _model.CurrentPlayerIndex;
            int previousLocalScore = GetPlayerScore(localPlayerIndex);
            bool isApplied = _model.ApplySnapshot(snapshot);

            if ( !isApplied )
            {
                return false;
            }

            _isLocalExtraTurn = IsLocalExtraTurn(
                hadPreviousSnapshot ,
                previousRevision ,
                previousPlayerIndex ,
                previousLocalScore ,
                localPlayerIndex);

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

        public void Tick(DateTimeOffset utcNow)
        {
            ThrowIfDisposed();

            if ( !_isOpen )
            {
                return;
            }

            if ( _model.MatchState != SERVER_MATCH_STATE_ENUM.STARTING && _model.MatchState != SERVER_MATCH_STATE_ENUM.ACTIVE )
            {
                return;
            }

            RefreshMatchState(utcNow);
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
                _session.OpponentPreviewChanged += OnOpponentPreviewChanged;
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
                _session.OpponentPreviewChanged -= OnOpponentPreviewChanged;
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

            if ( _model.HasOpponentPreview )
            {
                _view.ShowOpponentPreviewEdge(
                    _model.OpponentPreviewEdgeId ,
                    _model.OpponentPreviewPlayerIndex);
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
            PLAYER_INDEX_ENUM localPlayerIndex = GetLocalPlayerIndex();
            PLAYER_INDEX_ENUM opponentPlayerIndex = localPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE ? PLAYER_INDEX_ENUM.PLAYER_TWO : PLAYER_INDEX_ENUM.PLAYER_ONE;

            _view.ShowScores(GetPlayerScore(localPlayerIndex) , GetPlayerScore(opponentPlayerIndex));
            RefreshMatchState(DateTimeOffset.UtcNow);
        }

        private void RefreshMatchState(DateTimeOffset utcNow)
        {
            _view.SetTurnTimerVisible(false);
            _view.SetActionGuideVisible(false);

            switch ( _model.MatchState )
            {
                case SERVER_MATCH_STATE_ENUM.NONE:
                    _view.ShowMatchStatus("WAITING FOR SERVER...");
                    break;

                case SERVER_MATCH_STATE_ENUM.WAITING_FOR_PLAYERS:
                    _view.ShowMatchStatus("WAITING FOR PLAYERS...");
                    break;

                case SERVER_MATCH_STATE_ENUM.WAITING_FOR_READY:
                    _view.ShowMatchStatus("WAITING FOR READY...");
                    break;

                case SERVER_MATCH_STATE_ENUM.STARTING:
                    RefreshStartingStatus(utcNow);
                    break;

                case SERVER_MATCH_STATE_ENUM.ACTIVE:
                    RefreshActiveStatus(utcNow);
                    break;

                case SERVER_MATCH_STATE_ENUM.FINISHED:
                    _view.ShowMatchStatus("GAME FINISHED");
                    break;

                case SERVER_MATCH_STATE_ENUM.CANCELLED:
                    _view.ShowMatchStatus("MATCH CANCELLED");
                    break;

                default:
                    _view.ShowMatchStatus("UNKNOWN MATCH STATE");
                    break;
            }
        }

        private void RefreshStartingStatus(DateTimeOffset utcNow)
        {
            int countdownNumber = _model.GetStartCountdownNumber(utcNow);

            if ( countdownNumber <= 0 )
            {
                _view.ShowMatchStatus("STARTING...");
                return;
            }

            _view.ShowMatchStatus(countdownNumber.ToString());
        }

        private void RefreshActiveStatus(DateTimeOffset utcNow)
        {
            PLAYER_INDEX_ENUM localPlayerIndex = GetLocalPlayerIndex();
            bool isLocalPlayerTurn = _model.CurrentPlayerIndex == localPlayerIndex;

            _view.ShowCurrentTurn(_model.CurrentPlayerIndex , localPlayerIndex);

            if ( isLocalPlayerTurn )
            {
                _view.ShowActionGuide(_isLocalExtraTurn);
            }

            if ( !_model.TurnDeadLineUtc.HasValue )
            {
                return;
            }

            int turnCountdownNumber = _model.GetTurnCountdownNumber(utcNow);

            _view.SetTurnTimerVisible(true);
            _view.ShowTurnTimer(turnCountdownNumber);
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
            bool isSamePreviewSelected = previousPreviewEdgeId == edgeId;
            bool isPreviewChanged = isSamePreviewSelected
                ? _model.TryClearPreviewEdge(edgeId)
                : _model.TrySetPreviewEdge(edgeId);

            if ( !isPreviewChanged )
            {
                return;
            }

            RefreshView();

            if ( _session != null )
            {
                int previewEdgeId = isSamePreviewSelected
                    ? OpponentPreviewUpdate.NO_PREVIEW_EDGE_ID
                    : edgeId;

                _ = SetOnlinePreview_async(previewEdgeId);
            }
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

        private void OnOpponentPreviewChanged(OpponentPreviewUpdate update)
        {
            if ( _isDisposed || !_model.TryApplyOpponentPreview(update) )
            {
                return;
            }

            if ( _isOpen )
            {
                RefreshView();
            }
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

            _isLocalExtraTurn = confirmingPlayerIndex == GetLocalPlayerIndex() &&
                moveResult.CompletedBoxIds.Count > 0 &&
                !moveResult.IsGameFinished;

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

        private PLAYER_INDEX_ENUM GetLocalPlayerIndex()
        {
            if ( _session != null && _session.LocalPlayerIndex != PLAYER_INDEX_ENUM.NONE )
            {
                return _session.LocalPlayerIndex;
            }

            return PLAYER_INDEX_ENUM.PLAYER_ONE;
        }

        private int GetPlayerScore(PLAYER_INDEX_ENUM playerIndex)
        {
            return playerIndex == PLAYER_INDEX_ENUM.PLAYER_TWO
                ? _model.PlayerTwoScore
                : _model.PlayerOneScore;
        }

        private bool IsLocalExtraTurn(
            bool hadPreviousSnapshot ,
            long previousRevision ,
            PLAYER_INDEX_ENUM previousPlayerIndex ,
            int previousLocalScore ,
            PLAYER_INDEX_ENUM localPlayerIndex)
        {
            return hadPreviousSnapshot &&
                _model.Revision == previousRevision + 1 &&
                previousPlayerIndex == localPlayerIndex &&
                _model.CurrentPlayerIndex == localPlayerIndex &&
                GetPlayerScore(localPlayerIndex) > previousLocalScore &&
                _model.MatchState == SERVER_MATCH_STATE_ENUM.ACTIVE;
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

        private async Task SetOnlinePreview_async(int edgeId)
        {
            try
            {
                MATCH_COMMAND_ERROR_ENUM error = await _session.SetPreviewEdge_async(edgeId);

                if ( _isDisposed || error != MATCH_COMMAND_ERROR_ENUM.REVISION_MISMATCH )
                {
                    return;
                }

                await _session.RequestSync_async();
            }
            catch ( Exception exception )
            {
                if ( !_isDisposed )
                {
                    SessionFailed?.Invoke(exception);
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
