using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class ConfirmEdgeResponseFactoryTests
    {
        [Test]
        public void CreateSuccess_CreatesAcceptedResponse()
        {
            Guid requestId = Guid.NewGuid();
            MatchSnapshot snapshot = new MatchSnapshot();

            ConfirmEdgeResponse response =
                ConfirmEdgeResponseFactory.CreateSuccess(requestId, snapshot);

            Assert.Multiple(() =>
            {
                Assert.That(response.RequestId , Is.EqualTo(requestId));
                Assert.That(response.IsAccepted , Is.True);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NONE));
                Assert.That(response.ShouldRequestSync , Is.False);
                Assert.That(response.Snapshot , Is.SameAs(snapshot));
            });
        }

        [Test]
        public void CreateSuccess_WithoutSnapshot_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                _ = ConfirmEdgeResponseFactory.CreateSuccess(Guid.NewGuid() , null!);
            });
        }

        [Test]
        public void CreateFailure_CreatesRejectedResponse()
        {
            Guid requestId = Guid.NewGuid();
            MatchSnapshot snapshot = new MatchSnapshot();

            ConfirmEdgeResponse response =
                ConfirmEdgeResponseFactory.CreateFailure(
                    requestId,
                    MATCH_COMMAND_ERROR_ENUM.REVISION_MISMATCH,
                    true,
                    snapshot);

            Assert.Multiple(() =>
            {
                Assert.That(response.RequestId , Is.EqualTo(requestId));
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.REVISION_MISMATCH));
                Assert.That(response.ShouldRequestSync , Is.True);
                Assert.That(response.Snapshot , Is.SameAs(snapshot));
            });
        }

        [Test]
        public void CreateFailure_WithNoneError_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = ConfirmEdgeResponseFactory.CreateFailure(
                    Guid.NewGuid() ,
                    MATCH_COMMAND_ERROR_ENUM.NONE ,
                    false);
            });
        }
    }
}