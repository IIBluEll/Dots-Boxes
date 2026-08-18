using DotsAndBoxes.Gameplay;
using DotsAndBoxes.Shared;
using NUnit.Framework;
using System;

namespace DotsAndBoxes.Gameplay.Tests
{
    [TestFixture]
    public sealed class PendingConfirmRequestStoreTests
    {
        [Test]
        public void GetOrCreate_WithNoPending_CreatesRequest()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();
            Guid matchId = Guid.NewGuid();

            ConfirmEdgeRequest request = store.GetOrCreate(matchId, 3, 0);

            Assert.That(store.HasPendingRequest , Is.True);
            Assert.That(store.PendingEdgeId , Is.EqualTo(3));
            Assert.That(request.MatchId , Is.EqualTo(matchId));
            Assert.That(request.EdgeId , Is.EqualTo(3));
            Assert.That(request.ExpectedRevision , Is.Zero);
            Assert.That(request.RequestId , Is.Not.EqualTo(Guid.Empty));

        }

        [Test]
        public void GetOrCreate_WithSameContent_ReusesRequestId()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();
            Guid matchId = Guid.NewGuid();

            ConfirmEdgeRequest firstRequest = store.GetOrCreate(matchId, 4, 2);
            ConfirmEdgeRequest retriedRequest = store.GetOrCreate(matchId, 4, 2);


            Assert.That(retriedRequest.RequestId , Is.EqualTo(firstRequest.RequestId));
            Assert.That(retriedRequest.MatchId , Is.EqualTo(firstRequest.MatchId));
            Assert.That(retriedRequest.EdgeId , Is.EqualTo(firstRequest.EdgeId));
            Assert.That(retriedRequest.ExpectedRevision , Is.EqualTo(firstRequest.ExpectedRevision));
            Assert.That(retriedRequest , Is.Not.SameAs(firstRequest));
        }

        [Test]
        public void GetOrCreate_WithDifferentContent_Throws()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();
            Guid matchId = Guid.NewGuid();

            store.GetOrCreate(matchId , 1 , 0);

            Assert.Throws<InvalidOperationException>(() =>
            {
                store.GetOrCreate(matchId , 2 , 0);
            });

            Assert.That(store.HasPendingRequest , Is.True);
            Assert.That(store.PendingEdgeId , Is.EqualTo(1));
        }

        [Test]
        public void TryComplete_WithMatchingRequestId_ClearsPending()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();
            ConfirmEdgeRequest request = store.GetOrCreate(Guid.NewGuid(), 5, 0);

            bool wrongRequestCompleted = store.TryComplete(Guid.NewGuid());
            bool matchingRequestCompleted = store.TryComplete(request.RequestId);

            Assert.That(wrongRequestCompleted , Is.False);
            Assert.That(matchingRequestCompleted , Is.True);
            Assert.That(store.HasPendingRequest , Is.False);
            Assert.That(
                store.PendingEdgeId ,
                Is.EqualTo(PendingConfirmRequestStore.NO_PENDING_EDGE_ID));
        }

        [Test]
        public void TryComplete_WithSameRevision_KeepsPending()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();
            Guid matchId = Guid.NewGuid();

            store.GetOrCreate(matchId , 7 , 3);

            MatchSnapshot snapshot = new MatchSnapshot
            {
                MatchId = matchId,
                Revision = 3
            };

            bool completed = store.TryComplete(snapshot);

            Assert.That(completed , Is.False);
            Assert.That(store.HasPendingRequest , Is.True);
            Assert.That(store.PendingEdgeId , Is.EqualTo(7));
        }

        [Test]
        public void TryComplete_WithNewerSnapshot_ClearsPending()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();
            Guid matchId = Guid.NewGuid();

            store.GetOrCreate(matchId , 7 , 3);

            MatchSnapshot snapshot = new MatchSnapshot
            {
                MatchId = matchId,
                Revision = 4
            };

            bool completed = store.TryComplete(snapshot);

            Assert.That(completed , Is.True);
            Assert.That(store.HasPendingRequest , Is.False);
        }

        [Test]
        public void TryComplete_WithDifferentMatch_KeepsPending()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();

            store.GetOrCreate(Guid.NewGuid() , 7 , 3);

            MatchSnapshot snapshot = new MatchSnapshot
            {
                MatchId = Guid.NewGuid(),
                Revision = 4
            };

            bool completed = store.TryComplete(snapshot);

            Assert.That(completed , Is.False);
            Assert.That(store.HasPendingRequest , Is.True);
            Assert.That(store.PendingEdgeId , Is.EqualTo(7));
        }

        [Test]
        public void Clear_WithPending_RemovesRequest()
        {
            PendingConfirmRequestStore store = new PendingConfirmRequestStore();

            store.GetOrCreate(Guid.NewGuid() , 7 , 3);
            store.Clear();

            Assert.That(store.HasPendingRequest , Is.False);
            Assert.That(store.PendingEdgeId , Is.EqualTo(PendingConfirmRequestStore.NO_PENDING_EDGE_ID));
        }
    }
}
