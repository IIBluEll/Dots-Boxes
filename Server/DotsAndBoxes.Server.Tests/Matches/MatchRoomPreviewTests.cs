using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomPreviewTests
    {
        [Test]
        public async Task TrySetPreviewEdge_ValidRequest_ReturnsUpdateWithoutMutatingMatchState()
        {
            MatchRoom room = await ActiveMatchRoomTestFactory.Create_async();
            MatchSnapshot beforeSnapshot = await room.CreateSnapshot_async();
            PreviewEdgeRequest request = CreateRequest(room , 0 , 1);

            MatchRoomPreviewResult result =
                await room.TrySetPreviewEdge_async(room.PlayerOne.UserId , request);

            MatchSnapshot afterSnapshot = await room.CreateSnapshot_async();

            Assert.Multiple(() =>
            {
                Assert.That(result.IsAccepted , Is.True);
                Assert.That(result.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NONE));
                Assert.That(result.Update , Is.Not.Null);
                Assert.That(result.Update!.MatchId , Is.EqualTo(room.MatchId));
                Assert.That(result.Update.PlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
                Assert.That(result.Update.HasPreview , Is.True);
                Assert.That(result.Update.EdgeId , Is.EqualTo(0));
                Assert.That(result.Update.Revision , Is.EqualTo(beforeSnapshot.Revision));
                Assert.That(result.Update.PreviewSequence , Is.EqualTo(1));
                Assert.That(afterSnapshot.Revision , Is.EqualTo(beforeSnapshot.Revision));
                Assert.That(afterSnapshot.CurrentPlayerIndex , Is.EqualTo(beforeSnapshot.CurrentPlayerIndex));
                Assert.That(afterSnapshot.PlayerOneScore , Is.EqualTo(beforeSnapshot.PlayerOneScore));
                Assert.That(afterSnapshot.PlayerTwoScore , Is.EqualTo(beforeSnapshot.PlayerTwoScore));
                Assert.That(afterSnapshot.EdgeOwners , Is.EqualTo(beforeSnapshot.EdgeOwners));
                Assert.That(afterSnapshot.BoxOwners , Is.EqualTo(beforeSnapshot.BoxOwners));
                Assert.That(afterSnapshot.TurnDeadlineUtc , Is.EqualTo(beforeSnapshot.TurnDeadlineUtc));
            });
        }

        [Test]
        public async Task TrySetPreviewEdge_ClearRequest_ReturnsClearUpdate()
        {
            MatchRoom room = await ActiveMatchRoomTestFactory.Create_async();
            PreviewEdgeRequest request = CreateRequest(
                room ,
                OpponentPreviewUpdate.NO_PREVIEW_EDGE_ID ,
                1);

            MatchRoomPreviewResult result =
                await room.TrySetPreviewEdge_async(room.PlayerOne.UserId , request);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsAccepted , Is.True);
                Assert.That(result.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NONE));
                Assert.That(result.Update , Is.Not.Null);
                Assert.That(result.Update!.HasPreview , Is.False);
                Assert.That(result.Update.EdgeId , Is.EqualTo(OpponentPreviewUpdate.NO_PREVIEW_EDGE_ID));
                Assert.That(result.Update.PreviewSequence , Is.EqualTo(1));
            });
        }

        [Test]
        public async Task TrySetPreviewEdge_OtherPlayerTurn_ReturnsNotYourTurn()
        {
            MatchRoom room = await ActiveMatchRoomTestFactory.Create_async();
            PreviewEdgeRequest request = CreateRequest(room , 0 , 1);

            MatchRoomPreviewResult result =
                await room.TrySetPreviewEdge_async(room.PlayerTwo.UserId , request);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsAccepted , Is.False);
                Assert.That(result.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NOT_YOUR_TURN));
                Assert.That(result.Update , Is.Null);
            });
        }

        [Test]
        public async Task TrySetPreviewEdge_OlderSequence_ReturnsInvalidRequest()
        {
            MatchRoom room = await ActiveMatchRoomTestFactory.Create_async();

            MatchRoomPreviewResult firstResult =
                await room.TrySetPreviewEdge_async(
                    room.PlayerOne.UserId ,
                    CreateRequest(room , 0 , 2));

            MatchRoomPreviewResult olderResult =
                await room.TrySetPreviewEdge_async(
                    room.PlayerOne.UserId ,
                    CreateRequest(room , 1 , 1));

            Assert.Multiple(() =>
            {
                Assert.That(firstResult.IsAccepted , Is.True);
                Assert.That(olderResult.IsAccepted , Is.False);
                Assert.That(olderResult.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.INVALID_REQUEST));
                Assert.That(olderResult.Update , Is.Null);
            });
        }

        [Test]
        public async Task TrySetPreviewEdge_ConfirmedEdge_ReturnsEdgeAlreadyConfirmed()
        {
            MatchRoom room = await ActiveMatchRoomTestFactory.Create_async();
            ConfirmEdgeRequest confirmRequest = new ConfirmEdgeRequest
            {
                MatchId = room.MatchId ,
                EdgeId = 0 ,
                ExpectedRevision = room.Revision ,
                RequestId = Guid.NewGuid()
            };

            ConfirmEdgeResponse confirmResponse =
                await room.ConfirmEdge_async(room.PlayerOne.UserId , confirmRequest);

            MatchRoomPreviewResult previewResult =
                await room.TrySetPreviewEdge_async(
                    room.PlayerTwo.UserId ,
                    CreateRequest(room , 0 , 1));

            Assert.Multiple(() =>
            {
                Assert.That(confirmResponse.IsAccepted , Is.True);
                Assert.That(previewResult.IsAccepted , Is.False);
                Assert.That(previewResult.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.EDGE_ALREADY_CONFIRMED));
                Assert.That(previewResult.Update , Is.Null);
            });
        }

        [Test]
        public async Task TrySetPreviewEdge_StaleRevision_ReturnsRevisionMismatch()
        {
            MatchRoom room = await ActiveMatchRoomTestFactory.Create_async();
            PreviewEdgeRequest request = CreateRequest(room , 0 , 1);
            request.ExpectedRevision--;

            MatchRoomPreviewResult result =
                await room.TrySetPreviewEdge_async(room.PlayerOne.UserId , request);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsAccepted , Is.False);
                Assert.That(result.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.REVISION_MISMATCH));
                Assert.That(result.Update , Is.Null);
            });
        }

        [Test]
        public async Task TrySetPreviewEdge_OverRateLimit_ReturnsRateLimitedAndResetsNextWindow()
        {
            DateTimeOffset currentUtc =
                new DateTimeOffset(2026 , 8 , 28 , 0 , 0 , 0 , TimeSpan.Zero);

            MatchRoom room = await ActiveMatchRoomTestFactory.Create_async(
                utcNowProvider: () => currentUtc);

            for ( int previewSequence = 1; previewSequence <= 5; previewSequence++ )
            {
                MatchRoomPreviewResult acceptedResult =
                    await room.TrySetPreviewEdge_async(
                        room.PlayerOne.UserId ,
                        CreateRequest(room , previewSequence - 1 , previewSequence));

                Assert.That(acceptedResult.IsAccepted , Is.True);
            }

            MatchRoomPreviewResult limitedResult =
                await room.TrySetPreviewEdge_async(
                    room.PlayerOne.UserId ,
                    CreateRequest(room , 5 , 6));

            currentUtc = currentUtc.AddSeconds(1);

            MatchRoomPreviewResult nextWindowResult =
                await room.TrySetPreviewEdge_async(
                    room.PlayerOne.UserId ,
                    CreateRequest(room , 5 , 6));

            Assert.Multiple(() =>
            {
                Assert.That(limitedResult.IsAccepted , Is.False);
                Assert.That(limitedResult.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.RATE_LIMITED));
                Assert.That(nextWindowResult.IsAccepted , Is.True);
            });
        }

        private static PreviewEdgeRequest CreateRequest(
            MatchRoom room ,
            int edgeId ,
            long previewSequence)
        {
            return new PreviewEdgeRequest
            {
                MatchId = room.MatchId ,
                EdgeId = edgeId ,
                ExpectedRevision = room.Revision ,
                PreviewSequence = previewSequence
            };
        }
    }
}
