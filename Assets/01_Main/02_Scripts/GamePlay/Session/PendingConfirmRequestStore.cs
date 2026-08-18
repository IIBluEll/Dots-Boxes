using DotsAndBoxes.Shared;
using System;

namespace DotsAndBoxes.Gameplay
{
    public sealed class PendingConfirmRequestStore
    {
        public const int NO_PENDING_EDGE_ID = -1;

        private readonly object REQUEST_LOCK = new object();

        private ConfirmEdgeRequest _pendingRequest;

        public bool HasPendingRequest
        {
            get
            {
                lock ( REQUEST_LOCK )
                {
                    return _pendingRequest != null;
                }
            }
        }

        public int PendingEdgeId
        {
            get
            {
                lock ( REQUEST_LOCK )
                {
                    return _pendingRequest == null ? NO_PENDING_EDGE_ID : _pendingRequest.EdgeId;
                }
            }
        }

        public ConfirmEdgeRequest GetOrCreate(Guid matchId , int edgeId , long expectedRevision)
        {
            if ( matchId == Guid.Empty )
            {
                throw new ArgumentException("MatchId가 비어 있습니다." , nameof(matchId));
            }

            if ( edgeId < 0 || edgeId >= BoardTopology.EDGE_COUNT )
            {
                throw new ArgumentOutOfRangeException(nameof(edgeId));
            }

            if ( expectedRevision < 0 )
            {
                throw new ArgumentOutOfRangeException(nameof(expectedRevision));
            }

            lock ( REQUEST_LOCK )
            {
                if ( _pendingRequest == null )
                {
                    _pendingRequest = new ConfirmEdgeRequest
                    {
                        MatchId = matchId ,
                        EdgeId = edgeId ,
                        ExpectedRevision = expectedRevision ,
                        RequestId = Guid.NewGuid()
                    };

                    return CopyRequest(_pendingRequest);
                }

                bool isSameRequest =
                    _pendingRequest.MatchId == matchId &&
                    _pendingRequest.EdgeId == edgeId &&
                    _pendingRequest.ExpectedRevision == expectedRevision;

                if ( !isSameRequest )
                {
                    throw new InvalidOperationException(
                        "처리 중인 Confirm이 끝나기 전에 다른 Confirm을 만들 수 없습니다.");
                }

                return CopyRequest(_pendingRequest);
            }
        }

        public bool TryComplete(Guid requestId)
        {
            if ( requestId == Guid.Empty )
            {
                return false;
            }

            lock ( REQUEST_LOCK )
            {
                if ( _pendingRequest == null || _pendingRequest.RequestId != requestId )
                {
                    return false;
                }

                _pendingRequest = null;
                return true;
            }
        }

        public bool TryComplete(MatchSnapshot snapshot)
        {
            if ( snapshot == null )
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            lock ( REQUEST_LOCK )
            {
                if ( _pendingRequest == null )
                {
                    return false;
                }

                if ( snapshot.MatchId != _pendingRequest.MatchId )
                {
                    return false;
                }

                if ( snapshot.Revision <= _pendingRequest.ExpectedRevision )
                {
                    return false;
                }

                _pendingRequest = null;
                return true;
            }
        }

        public void Clear()
        {
            lock ( REQUEST_LOCK )
            {
                _pendingRequest = null;
            }
        }

        private static ConfirmEdgeRequest CopyRequest(ConfirmEdgeRequest request)
        {
            return new ConfirmEdgeRequest
            {
                MatchId = request.MatchId ,
                EdgeId = request.EdgeId ,
                ExpectedRevision = request.ExpectedRevision ,
                RequestId = request.RequestId
            };
        }
    }
}