using HM.CodeBase;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DotsAndBoxes.UI
{
    public sealed class MainLobby_view : AView
    {
        [Header("Buttons")]
        [SerializeField] private Button _quickMatchBtn;
        [SerializeField] private Button _localMatchBtn;
        [SerializeField] private Button _matchMakingCancelBtn;
        [SerializeField] private Button _matchMakingErrorConfirmBtn;
        [SerializeField] private Button _settingBtn;
        [SerializeField] private Button _settingCloseBtn;

        [Space(5f), Header("Setting")]
        [SerializeField] private Toggle _bgmToggle;
        [SerializeField] private Toggle _sfxToggle;

        [Space(5f), Header("Roots")]
        [SerializeField] private GameObject _waitMatchingRootObj;
        [SerializeField] private GameObject _matchMakingErrorRootObj;
        [SerializeField] private GameObject _settingRootObj;

        [Space(5f), Header("Texts")]
        [SerializeField] private TMP_Text _matchMakingErrorTxt;

        public event Action QuickMatchRequested;
        public event Action LocalMatchRequested;
        public event Action MatchMakingCancelRequested;
        public event Action SettingOpenRequested;
        public event Action SettingCloseRequested;
        public event Action<bool> BgmEnabledChanged;
        public event Action<bool> SfxEnabledChanged;

        private void Awake()
        {
            ResolveSettingReferences();

            if ( !ValidateReferences() )
            {
                enabled = false;
                return;
            }

            _quickMatchBtn.onClick.AddListener(OnQuickMatchActioned);
            _localMatchBtn.onClick.AddListener(OnLocalMatchActioned);
            _matchMakingCancelBtn.onClick.AddListener(OnMatchMakingCancelActioned);
            _matchMakingErrorConfirmBtn.onClick.AddListener(OnMatchMakingErrorConfirmActioned);
            _settingBtn.onClick.AddListener(OnSettingOpenActioned);
            _settingCloseBtn.onClick.AddListener(OnSettingCloseActioned);
            _bgmToggle.onValueChanged.AddListener(OnBgmToggleValueChangedActioned);
            _sfxToggle.onValueChanged.AddListener(OnSfxToggleValueChangedActioned);

            Clear();
        }

        private void OnDestroy()
        {
            if ( _quickMatchBtn != null )
            {
                _quickMatchBtn.onClick.RemoveListener(OnQuickMatchActioned);
            }

            if ( _localMatchBtn != null )
            {
                _localMatchBtn.onClick.RemoveListener(OnLocalMatchActioned);
            }

            if ( _matchMakingCancelBtn != null )
            {
                _matchMakingCancelBtn.onClick.RemoveListener(OnMatchMakingCancelActioned);
            }

            if ( _matchMakingErrorConfirmBtn != null )
            {
                _matchMakingErrorConfirmBtn.onClick.RemoveListener(OnMatchMakingErrorConfirmActioned);
            }

            if ( _settingBtn != null )
            {
                _settingBtn.onClick.RemoveListener(OnSettingOpenActioned);
            }

            if ( _settingCloseBtn != null )
            {
                _settingCloseBtn.onClick.RemoveListener(OnSettingCloseActioned);
            }

            if ( _bgmToggle != null )
            {
                _bgmToggle.onValueChanged.RemoveListener(OnBgmToggleValueChangedActioned);
            }

            if ( _sfxToggle != null )
            {
                _sfxToggle.onValueChanged.RemoveListener(OnSfxToggleValueChangedActioned);
            }

            QuickMatchRequested = null;
            LocalMatchRequested = null;
            MatchMakingCancelRequested = null;
            SettingOpenRequested = null;
            SettingCloseRequested = null;
            BgmEnabledChanged = null;
            SfxEnabledChanged = null;
        }

        public override void Clear()
        {
            SetQuickMatchInteractable(true);
            SetLocalMatchInteractable(true);
            SetMatchMakingCancelInteractable(true);
            SetWaitMatchingVisible(false);
            SetMatchMakingErrorVisible(false);
            SetSettingVisible(false);
        }

        public void SetQuickMatchInteractable(bool isInteractable)
        {
            _quickMatchBtn.interactable = isInteractable;
        }

        public void SetLocalMatchInteractable(bool isInteractable)
        {
            _localMatchBtn.interactable = isInteractable;
        }

        public void SetMatchMakingCancelInteractable(bool isInteractable)
        {
            _matchMakingCancelBtn.interactable = isInteractable;
        }

        public void SetWaitMatchingVisible(bool isVisible)
        {
            _waitMatchingRootObj.SetActive(isVisible);
        }

        public void SetMatchMakingErrorVisible(bool isVisible)
        {
            _matchMakingErrorRootObj.SetActive(isVisible);
        }

        public void SetSettingVisible(bool isVisible)
        {
            _settingRootObj.SetActive(isVisible);
        }

        public void SetAudioToggleStates(bool isBgmEnabled , bool isSfxEnabled)
        {
            _bgmToggle.SetIsOnWithoutNotify(isBgmEnabled);
            _sfxToggle.SetIsOnWithoutNotify(isSfxEnabled);
        }

        public void ShowMatchMakingError(string errorMessage)
        {
            _matchMakingErrorTxt.text = string.IsNullOrWhiteSpace(errorMessage)
                ? "매칭 중 오류가 발생했습니다."
                : errorMessage;

            SetMatchMakingErrorVisible(true);
        }

        private void OnQuickMatchActioned()
        {
            QuickMatchRequested?.Invoke();
        }

        private void OnLocalMatchActioned()
        {
            LocalMatchRequested?.Invoke();
        }

        private void OnMatchMakingCancelActioned()
        {
            MatchMakingCancelRequested?.Invoke();
        }

        private void OnMatchMakingErrorConfirmActioned()
        {
            SetMatchMakingErrorVisible(false);
        }

        private void OnSettingOpenActioned()
        {
            SettingOpenRequested?.Invoke();
        }

        private void OnSettingCloseActioned()
        {
            SettingCloseRequested?.Invoke();
        }

        private void OnBgmToggleValueChangedActioned(bool isEnabled)
        {
            BgmEnabledChanged?.Invoke(isEnabled);
        }

        private void OnSfxToggleValueChangedActioned(bool isEnabled)
        {
            SfxEnabledChanged?.Invoke(isEnabled);
        }

        private void ResolveSettingReferences()
        {
            Transform rootTrans = transform.root;

            if ( _settingRootObj == null )
            {
                Transform settingRootTrans = FindDescendantByName(rootTrans , "Setting_Popup");
                _settingRootObj = settingRootTrans != null ? settingRootTrans.gameObject : null;
            }

            if ( _settingBtn == null )
            {
                Transform settingBtnTrans = FindDescendantByName(rootTrans , "Setting_Btn");
                _settingBtn = settingBtnTrans != null ? settingBtnTrans.GetComponent<Button>() : null;
            }

            if ( _settingCloseBtn == null && _settingRootObj != null )
            {
                Transform settingCloseBtnTrans = FindDescendantByName(
                    _settingRootObj.transform ,
                    "Close_Btn");

                _settingCloseBtn = settingCloseBtnTrans != null
                    ? settingCloseBtnTrans.GetComponent<Button>()
                    : null;
            }

            if ( _bgmToggle == null )
            {
                Transform bgmToggleTrans = FindDescendantByName(rootTrans , "BGMToggle");
                _bgmToggle = bgmToggleTrans != null
                    ? bgmToggleTrans.GetComponentInChildren<Toggle>(true)
                    : null;
            }

            if ( _sfxToggle == null )
            {
                Transform sfxToggleTrans = FindDescendantByName(rootTrans , "SFXToggle");
                _sfxToggle = sfxToggleTrans != null
                    ? sfxToggleTrans.GetComponentInChildren<Toggle>(true)
                    : null;
            }
        }

        private static Transform FindDescendantByName(Transform rootTrans , string objectName)
        {
            if ( rootTrans.name == objectName )
            {
                return rootTrans;
            }

            for ( int i = 0; i < rootTrans.childCount; i++ )
            {
                Transform resultTrans = FindDescendantByName(rootTrans.GetChild(i) , objectName);

                if ( resultTrans != null )
                {
                    return resultTrans;
                }
            }

            return null;
        }

        private bool ValidateReferences()
        {
            bool isValid =
                _quickMatchBtn != null &&
                _localMatchBtn != null &&
                _matchMakingCancelBtn != null &&
                _matchMakingErrorConfirmBtn != null &&
                _settingBtn != null &&
                _settingCloseBtn != null &&
                _bgmToggle != null &&
                _sfxToggle != null &&
                _waitMatchingRootObj != null &&
                _matchMakingErrorRootObj != null &&
                _settingRootObj != null &&
                _matchMakingErrorTxt != null;

            if ( !isValid )
            {
                Debug.LogError("MainLobby_view의 UI 참조가 설정되지 않았습니다." , this);
            }

            return isValid;
        }
    }
}
