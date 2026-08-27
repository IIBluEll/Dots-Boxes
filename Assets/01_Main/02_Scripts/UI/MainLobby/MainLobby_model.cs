namespace DotsAndBoxes.UI
{
    public sealed class MainLobby_model
    {
        public bool IsMatchMaking { get; private set; } 

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
    }
}