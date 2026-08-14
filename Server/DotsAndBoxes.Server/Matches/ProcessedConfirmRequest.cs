using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Matches
{
    internal sealed class ProcessedConfirmRequest
    {
        public Guid UserId { get; }
        public Guid MatchId { get; }
        public int EdgeId { get; }
        public long ExpectedRevision { get; }
        public ConfirmEdgeResponse Response { get; }

        public ProcessedConfirmRequest(Guid userId , ConfirmEdgeRequest request , ConfirmEdgeResponse response)
        {
            UserId = userId;
            MatchId = request.MatchId;
            EdgeId = request.EdgeId;
            ExpectedRevision = request.ExpectedRevision;
            Response = response;
        }

        public bool Matches(Guid userId , ConfirmEdgeRequest request)
        {
            return UserId == userId &&
                   MatchId == request.MatchId &&
                   EdgeId == request.EdgeId &&
                   ExpectedRevision == request.ExpectedRevision;
        }
    }
}