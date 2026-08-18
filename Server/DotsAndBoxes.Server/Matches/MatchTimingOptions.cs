namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchTimingOptions
    {
        public const string SECTION_NAME = "MatchTiming";

        public int JoinTimeoutSeconds { get; set; } = 10;
        public int ReadyTimeoutSeconds { get; set; } = 15;
        public int StartCountdownSeconds { get; set; } = 3;
        public int TurnDurationSeconds { get; set; } = 20;
        public int MaxTimeoutsPerPlayer { get; set; } = 3;
        public int SweepIntervalMilliseconds { get; set; } = 500;
    }
}
