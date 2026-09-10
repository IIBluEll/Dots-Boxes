using System;

namespace DotsAndBoxes.Shared
{
    public sealed class OpponentPreviewUpdate
    {
        public const int NO_PREVIEW_EDGE_ID = -1;

        public Guid MatchId { get; set; }
        public PLAYER_INDEX_ENUM PlayerIndex { get; set; } = PLAYER_INDEX_ENUM.NONE;
        public bool HasPreview { get; set; }
        public int EdgeId { get; set; } = NO_PREVIEW_EDGE_ID;
        public long Revision { get; set; }
        public long PreviewSequence { get; set; }
    }
}