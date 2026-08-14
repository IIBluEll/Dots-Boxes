using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomIdempotencyTests
    {
        private static readonly DateTimeOffset TURN_DEADLINE_UTC =
            new DateTimeOffset(2030, 1, 1, 0, 0, 20, TimeSpan.Zero);

        [Test]
        public async Task ConfirmEdge_WithSameRequest_ReturnsCachedResponse()
        {
            MatchRoom room = CreateRoom();
            ConfirmEdgeRequest request = CreateRequest(room.MatchId, 0, 0, Guid.NewGuid());

            ConfirmEdgeResponse firstResponse =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, request);

            ConfirmEdgeResponse secondResponse =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, request);

            Assert.Multiple(() =>
            {
                Assert.That(firstResponse.IsAccepted , Is.True);
                Assert.That(secondResponse.IsAccepted , Is.True);
                Assert.That(firstResponse.Snapshot!.Revision , Is.EqualTo(1));
                Assert.That(secondResponse.Snapshot!.Revision , Is.EqualTo(1));
                Assert.That(room.Revision , Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ConfirmEdge_WithSameRequestIdAndDifferentContent_ReturnsConflict()
        {
            MatchRoom room = CreateRoom();
            Guid requestId = Guid.NewGuid();

            ConfirmEdgeRequest firstRequest =
                CreateRequest(room.MatchId, 0, 0, requestId);

            ConfirmEdgeRequest changedRequest =
                CreateRequest(room.MatchId, 1, 0, requestId);

            ConfirmEdgeResponse firstResponse =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, firstRequest);

            ConfirmEdgeResponse changedResponse =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, changedRequest);

            Assert.Multiple(() =>
            {
                Assert.That(firstResponse.IsAccepted , Is.True);
                Assert.That(changedResponse.IsAccepted , Is.False);
                Assert.That(
                    changedResponse.Error ,
                    Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.DUPLICATE_REQUEST_CONFLICT));

                Assert.That(changedResponse.Snapshot , Is.Not.Null);
                Assert.That(changedResponse.Snapshot!.EdgeOwners[ 0 ] , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
                Assert.That(changedResponse.Snapshot.EdgeOwners[ 1 ] , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
                Assert.That(room.Revision , Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ConfirmEdge_WithConcurrentSameRequests_ChangesStateOnlyOnce()
        {
            MatchRoom room = CreateRoom();
            ConfirmEdgeRequest request =
                CreateRequest(room.MatchId, 0, 0, Guid.NewGuid());

            TaskCompletionSource<bool> startSignal =
                new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            Task<ConfirmEdgeResponse>[] requestTasks = Enumerable
                .Range(0, 8)
                .Select(_ => Task.Run(async () =>
                {
                    await startSignal.Task;
                    return await room.ConfirmEdge_async(
                        room.PlayerOne.UserId,
                        request);
                }))
                .ToArray();

            startSignal.SetResult(true);

            ConfirmEdgeResponse[] responses =
                await Task.WhenAll(requestTasks);

            MatchSnapshot snapshot =
                await room.CreateSnapshot_async();

            int confirmedEdgeCount = snapshot.EdgeOwners.Count(
                owner => owner != PLAYER_INDEX_ENUM.NONE);

            Assert.Multiple(() =>
            {
                Assert.That(
                    responses.All(response => response.IsAccepted) ,
                    Is.True);

                Assert.That(
                    responses.All(response => response.Error == MATCH_COMMAND_ERROR_ENUM.NONE) ,
                    Is.True);

                Assert.That(
                    responses.All(response => response.Snapshot!.Revision == 1) ,
                    Is.True);

                Assert.That(room.Revision , Is.EqualTo(1));
                Assert.That(confirmedEdgeCount , Is.EqualTo(1));
                Assert.That(snapshot.EdgeOwners[ 0 ] , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
            });
        }

        private static MatchRoom CreateRoom()
        {
            MatchPlayer playerOne =
                new MatchPlayer(Guid.NewGuid(), "connection-one");

            MatchPlayer playerTwo =
                new MatchPlayer(Guid.NewGuid(), "connection-two");

            return new MatchRoom(
                Guid.NewGuid() ,
                playerOne ,
                playerTwo ,
                PLAYER_INDEX_ENUM.PLAYER_ONE ,
                TURN_DEADLINE_UTC);
        }

        private static ConfirmEdgeRequest CreateRequest(
            Guid matchId ,
            int edgeId ,
            long expectedRevision ,
            Guid requestId)
        {
            return new ConfirmEdgeRequest
            {
                MatchId = matchId ,
                EdgeId = edgeId ,
                ExpectedRevision = expectedRevision ,
                RequestId = requestId
            };
        }
    }
}