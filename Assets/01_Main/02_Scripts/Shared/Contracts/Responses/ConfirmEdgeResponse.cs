using System;

namespace DotsAndBoxes.Shared
{
    public sealed class ConfirmEdgeResponse
    {
        public Guid RequestId { get; set; }
        public bool IsAccepted { get; set; }
        public MATCH_COMMAND_ERROR_ENUM Error { get; set; }
        public bool ShouldRequestSync { get; set; }
        public MatchSnapshot Snapshot { get; set; }
    }
}