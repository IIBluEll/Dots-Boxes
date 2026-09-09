using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Gameplay.Audio;
using DotsAndBoxes.Shared;
using HM.CodeBase;
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

namespace DotsAndBoxes.UI
{
    public sealed class MainLobby_presenter : APresenter
    {
        private readonly MainLobby_model _model;
        private readonly MainLobby_view _view;

        private IOnlineSession _session;
        private readonly Func<Task<IOnlineSession>> PREPARE_SESSION;
        private readonly CancellationToken _cancellationToken;

        private bool _isBound;
        private bool _isDisposed;
        private bool _isCancellingMatchMaking;
        private bool _isPreparingSession;

        public event Action<MatchAssignment> MatchFound;
        public event Action LocalMatchRequested;
        public event Action<Exception> MatchMakingFailed;

        public MainLobby_presenter(MainLobby_model model , MainLobby_view view , IOnlineSession session , CancellationToken cancellationToken,
            Func<Task<IOnlineSession>> prepareSession = null)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _view = view ?? throw new ArgumentNullException(nameof(view));
            _session = session;
            PREPARE_SESSION = prepareSession;
            _cancellationToken = cancellationToken;
        }

        public override void Open()
        {
            ThrowIfDisposed();
            BindEvents();

            _view.Open();

            if ( _session != null && _session.IsQueueing )
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
            LocalMatchRequested = null;
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
            _view.LocalMatchRequested += OnLocalMatchActioned;
            _view.MatchMakingCancelRequested += OnMatchMakingCancelActioned;
            _view.SettingOpenRequested += OnSettingOpenActioned;
            _view.SettingCloseRequested += OnSettingCloseActioned;
            _view.BgmEnabledChanged += OnBgmEnabledChangedActioned;
            _view.SfxEnabledChanged += OnSfxEnabledChangedActioned;
            BindSession();

            _isBound = true;
        }

        private void UnbindEvents()
        {
            if(!_isBound)
            {
                return;
            }

            _view.QuickMatchRequested -= OnQuickMatchActioned;
            _view.LocalMatchRequested -= OnLocalMatchActioned;
            _view.MatchMakingCancelRequested -= OnMatchMakingCancelActioned;
            _view.SettingOpenRequested -= OnSettingOpenActioned;
            _view.SettingCloseRequested -= OnSettingCloseActioned;
            _view.BgmEnabledChanged -= OnBgmEnabledChangedActioned;
            _view.SfxEnabledChanged -= OnSfxEnabledChangedActioned;
            UnbindSession();

            _isBound = false;
        }

        private void OnQuickMatchActioned() => RequestQuickMatch_async().Forget();

        private void BindSession()
        {
            if (_session == null) return;
            _session.MatchFound += OnMatchFoundActioned;
            _session.ConnectionStateChanged += OnSessionConnectionStateChanged;
        }

        private void UnbindSession()
        {
            if (_session == null) return;
            _session.MatchFound -= OnMatchFoundActioned;
            _session.ConnectionStateChanged -= OnSessionConnectionStateChanged;
        }

        private void OnSessionConnectionStateChanged(GAME_SESSION_CONNECTION_STATE_ENUM state)
        {
            if (_isDisposed || _isPreparingSession || !_model.IsMatchMaking ||
                (state != GAME_SESSION_CONNECTION_STATE_ENUM.DISCONNECTED &&
                 state != GAME_SESSION_CONNECTION_STATE_ENUM.FAULTED)) return;
            _model.EndMatchMaking();
            RefreshView();
            MatchMakingFailed?.Invoke(new InvalidOperationException("연결이 종료되었습니다. 다시 로그인해 주세요."));
        }

        private void OnLocalMatchActioned()
        {
            if ( _isDisposed || _model.IsMatchMaking )
            {
                return;
            }

            LocalMatchRequested?.Invoke();
        }

        private void OnSettingOpenActioned()
        {
            if ( !AudioProvider.HasInstance )
            {
                return;
            }

            AudioProvider audioProvider = AudioProvider.Instance;

            _model.SetAudioSettings(
                audioProvider.IsBgmEnabled ,
                audioProvider.IsSfxEnabled);

            _view.SetAudioToggleStates(
                _model.IsBgmEnabled ,
                _model.IsSfxEnabled);

            _view.SetSettingVisible(true);
        }

        private void OnSettingCloseActioned()
        {
            _view.SetSettingVisible(false);
        }

        private void OnBgmEnabledChangedActioned(bool isEnabled)
        {
            if ( !AudioProvider.HasInstance )
            {
                return;
            }

            _model.SetBgmEnabled(isEnabled);
            AudioProvider.Instance.SetBgmEnabled(isEnabled);
        }

        private void OnSfxEnabledChangedActioned(bool isEnabled)
        {
            if ( !AudioProvider.HasInstance )
            {
                return;
            }

            _model.SetSfxEnabled(isEnabled);
            AudioProvider.Instance.SetSfxEnabled(isEnabled);
        }

        private async UniTask RequestQuickMatch_async()
        {
            if ( !_model.TryBeginMatchMaking() )
            {
                return;
            }

            _isPreparingSession = true;
            RefreshView();

            try
            {
                if (PREPARE_SESSION != null)
                {
                    UnbindSession();
                    IOnlineSession prepared = await PREPARE_SESSION();
                    _cancellationToken.ThrowIfCancellationRequested();
                    _session = prepared;
                    BindSession();
                }
                if (_session == null) throw new InvalidOperationException("서버 로그인이 필요합니다.");
                await _session.Start_async(_cancellationToken);
                await _session.EnterMatchmaking_async(_cancellationToken);
            }
            catch ( OperationCanceledException ) when ( _cancellationToken.IsCancellationRequested )
            {
                //로비가 종료되면서 취소됨
            }
            catch ( Exception ex )
            {
                if (_isDisposed) return;
                _model.EndMatchMaking();
                RefreshView();
                MatchMakingFailed?.Invoke(ex);
            }
            finally
            {
                _isPreparingSession = false;
                if (!_isDisposed) RefreshView();
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
            if ( _isDisposed || _isPreparingSession || _isCancellingMatchMaking || !_model.IsMatchMaking || _session == null )
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
            _view.SetLocalMatchInteractable(!_model.IsMatchMaking);
            _view.SetMatchMakingCancelInteractable(_model.IsMatchMaking && !_isPreparingSession && !_isCancellingMatchMaking && _session != null);
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

