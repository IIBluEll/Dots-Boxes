using System;

namespace DotsAndBoxes.Shared
{
    public sealed class ConfirmEdgeRequest
    {
        public Guid MatchId { get; set; }           //어느 Match의 요청인지
        public int EdgeId { get; set; }
        public long ExpectedRevision { get; set; }  //Client가 알고 있는 현재 상태 버전
        public Guid RequestId { get; set; }         //중복 실행을 막는 고유 요청 ID
    }
}