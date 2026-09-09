namespace DotsAndBoxes.UI
{
    public sealed class MainLobby_model
    {
        public bool IsMatchMaking { get; private set; }
        public bool IsBgmEnabled { get; private set; }
        public bool IsSfxEnabled { get; private set; }

        public bool TryBeginMatchMaking()
        {
            if(IsMatchMaking)
            {
                return false;
            }

            IsMatchMaking = true;
            return true;
        }

        public void EndMatchMaking()
        {
            IsMatchMaking = false;
        }

        public void SetAudioSettings(bool isBgmEnabled , bool isSfxEnabled)
        {
            IsBgmEnabled = isBgmEnabled;
            IsSfxEnabled = isSfxEnabled;
        }

        public void SetBgmEnabled(bool isEnabled)
        {
            IsBgmEnabled = isEnabled;
        }

        public void SetSfxEnabled(bool isEnabled)
        {
            IsSfxEnabled = isEnabled;
        }
    }
}
