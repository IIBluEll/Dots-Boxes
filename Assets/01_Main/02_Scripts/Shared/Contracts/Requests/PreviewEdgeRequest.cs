using System;

namespace DotsAndBoxes.Shared
{
    public sealed class PreviewEdgeRequest
    {
        public Guid MatchId { get; set; }
        public int EdgeId { get; set; }
        public long PreviewSequence { get; set; }
    }
}