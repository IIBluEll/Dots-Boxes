namespace DotsAndBoxes.Gameplay
{
    public sealed class PauseModel_model
    {
        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }
    }
}
