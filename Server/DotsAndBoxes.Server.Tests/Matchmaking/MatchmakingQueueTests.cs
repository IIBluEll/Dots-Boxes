using DotsAndBoxes.Server.Matchmaking;

namespace DotsAndBoxes.Server.Tests.Matchmaking
{
    [TestFixture]
    public sealed class MatchmakingQueueTests
    {
        [Test]
        public void Enqueue_TwoUsers_MatchesInFifoOrder()
        {
            MatchmakingQueue queue = new MatchmakingQueue();
            Guid firstUserId = Guid.NewGuid();
            Guid secondUserId = Guid.NewGuid();

            MatchmakingEnqueueResult firstResult = queue.Enqueue(firstUserId);
            MatchmakingEnqueueResult secondResult = queue.Enqueue(secondUserId);

            Assert.Multiple(() =>
            {
                Assert.That(firstResult.State , Is.EqualTo(MATCHMAKING_ENQUEUE_STATE_ENUM.QUEUED));
                Assert.That(secondResult.State , Is.EqualTo(MATCHMAKING_ENQUEUE_STATE_ENUM.MATCHED));
                Assert.That(secondResult.OpponentUserId , Is.EqualTo(firstUserId));
                Assert.That(queue.Count , Is.Zero);
            });
        }

        [Test]
        public void Enqueue_SameWaitingUser_DoesNotDuplicateEntry()
        {
            MatchmakingQueue queue = new MatchmakingQueue();
            Guid userId = Guid.NewGuid();

            MatchmakingEnqueueResult firstResult = queue.Enqueue(userId);
            MatchmakingEnqueueResult secondResult = queue.Enqueue(userId);

            Assert.Multiple(() =>
            {
                Assert.That(firstResult.State , Is.EqualTo(MATCHMAKING_ENQUEUE_STATE_ENUM.QUEUED));
                Assert.That(secondResult.State , Is.EqualTo(MATCHMAKING_ENQUEUE_STATE_ENUM.ALREADY_QUEUED));
                Assert.That(queue.Count , Is.EqualTo(1));
            });
        }

        [Test]
        public void TryCancel_WaitingUser_RemovesUserFromFutureMatch()
        {
            MatchmakingQueue queue = new MatchmakingQueue();
            Guid cancelledUserId = Guid.NewGuid();
            Guid nextUserId = Guid.NewGuid();

            queue.Enqueue(cancelledUserId);
            bool wasCancelled = queue.TryCancel(cancelledUserId);
            MatchmakingEnqueueResult nextResult = queue.Enqueue(nextUserId);

            Assert.Multiple(() =>
            {
                Assert.That(wasCancelled , Is.True);
                Assert.That(nextResult.State , Is.EqualTo(MATCHMAKING_ENQUEUE_STATE_ENUM.QUEUED));
                Assert.That(queue.Count , Is.EqualTo(1));
            });
        }
    }
}
