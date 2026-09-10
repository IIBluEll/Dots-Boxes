using DotsAndBoxes.Shared;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace DotsAndBoxes.Gameplay.Tests
{
    [TestFixture]
    public sealed class LocalGameSessionTests
    {
        [Test]
        public async Task Ready_async_AfterStart_DoesNotChangeLocalSnapshot()
        {
            LocalGameSession session = new LocalGameSession();

            await session.Start_async();

            long revisionBeforeReady =
        session.CurrentSnapshot.Revision;

            await session.Ready_async();

            Assert.That(
                session.CurrentSnapshot.Revision ,
                Is.EqualTo(revisionBeforeReady));

            Assert.That(
                session.CurrentSnapshot.MatchState ,
                Is.EqualTo(SERVER_MATCH_STATE_ENUM.ACTIVE));

            session.Dispose();
        }

        [Test]
        public void OnlineSessionLaunchOptions_WithNoOnlineArguments_UsesInspectorValues()
        {
            bool parsed = OnlineSessionLaunchOptions.TryCreate(
                new[] { "DotsAndBoxes.exe" } ,
                out OnlineSessionLaunchOptions launchOptions ,
                out string errorMessage);

            Assert.That(parsed , Is.True);
            Assert.That(launchOptions , Is.Null);
            Assert.That(errorMessage , Is.Empty);
        }

        [Test]
        public void OnlineSessionLaunchOptions_WithCompleteArguments_CreatesOptions()
        {
            Guid matchId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();

            bool parsed = OnlineSessionLaunchOptions.TryCreate(
                new[]
                {
                    "DotsAndBoxes.exe" ,
                    "--server-url=http://localhost:5049/" ,
                    $"--match-id={matchId:D}" ,
                    $"--user-id={userId:D}" ,
                    "--simulate-confirm-response-loss-once"
                } ,
                out OnlineSessionLaunchOptions launchOptions ,
                out string errorMessage);

            Assert.That(parsed , Is.True);
            Assert.That(errorMessage , Is.Empty);
            Assert.That(launchOptions , Is.Not.Null);
            Assert.That(launchOptions.ServerUrl , Is.EqualTo("http://localhost:5049"));
            Assert.That(launchOptions.MatchId , Is.EqualTo(matchId));
            Assert.That(launchOptions.UserId , Is.EqualTo(userId));
            Assert.That(launchOptions.SimulateConfirmResponseLossOnce , Is.True);
        }

        [Test]
        public void OnlineSessionLaunchOptions_WithPartialArguments_ReturnsError()
        {
            bool parsed = OnlineSessionLaunchOptions.TryCreate(
                new[]
                {
                    "DotsAndBoxes.exe" ,
                    "--server-url=http://localhost:5049"
                } ,
                out OnlineSessionLaunchOptions launchOptions ,
                out string errorMessage);

            Assert.That(parsed , Is.False);
            Assert.That(launchOptions , Is.Null);
            Assert.That(errorMessage , Is.Not.Empty);
        }

        [Test]
        public void OnlineSessionLaunchOptions_WithInvalidUserId_ReturnsError()
        {
            bool parsed = OnlineSessionLaunchOptions.TryCreate(
                new[]
                {
                    "DotsAndBoxes.exe" ,
                    "--server-url=http://localhost:5049" ,
                    $"--match-id={Guid.NewGuid():D}" ,
                    "--user-id=invalid"
                } ,
                out OnlineSessionLaunchOptions launchOptions ,
                out string errorMessage);

            Assert.That(parsed , Is.False);
            Assert.That(launchOptions , Is.Null);
            Assert.That(errorMessage , Does.Contain("user-id"));
        }

        [Test]
        public async Task Start_async_CreatesInitialSnapshotAndPublishesEvent()
        {
            LocalGameSession session = new LocalGameSession();
            MatchSnapshot publishedSnapshot = null;
            int snapshotChangedCount = 0;

            session.SnapshotChanged += snapshot =>
            {
                publishedSnapshot = snapshot;
                snapshotChangedCount++;
            };

            await session.Start_async();

            Assert.That(session.HasSnapshot , Is.True);
            Assert.That(session.MatchId , Is.Not.EqualTo(System.Guid.Empty));
            Assert.That(session.LocalPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));
            Assert.That(session.CanConfirmCurrentTurn , Is.True);
            Assert.That(session.HasPendingConfirm , Is.False);
            Assert.That(session.PendingConfirmEdgeId , Is.EqualTo(PendingConfirmRequestStore.NO_PENDING_EDGE_ID));
            Assert.That(session.CurrentSnapshot.Revision , Is.EqualTo(0));
            Assert.That(session.CurrentSnapshot.MatchState , Is.EqualTo(SERVER_MATCH_STATE_ENUM.ACTIVE));
            Assert.That(publishedSnapshot , Is.Not.Null);
            Assert.That(snapshotChangedCount , Is.EqualTo(1));

            session.Dispose();
        }

        [Test]
        public async Task ConfirmEdge_async_WithValidEdge_IncrementsRevisionAndPublishesSnapshot()
        {
            LocalGameSession session = new LocalGameSession();
            int snapshotChangedCount = 0;

            session.SnapshotChanged += snapshot => snapshotChangedCount++;

            await session.Start_async();
            ConfirmEdgeResponse response = await session.ConfirmEdge_async(0);

            Assert.That(response.IsAccepted , Is.True);
            Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NONE));
            Assert.That(response.Snapshot , Is.Not.Null);
            Assert.That(response.Snapshot.Revision , Is.EqualTo(1));
            Assert.That(response.Snapshot.EdgeOwners[ 0 ] , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
            Assert.That(response.Snapshot.CurrentPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_TWO));
            Assert.That(session.CurrentSnapshot.Revision , Is.EqualTo(1));
            Assert.That(snapshotChangedCount , Is.EqualTo(2));

            session.Dispose();
        }

        [Test]
        public async Task ConfirmEdge_async_WithConfirmedEdge_DoesNotChangeRevisionOrPublishSnapshot()
        {
            LocalGameSession session = new LocalGameSession();

            await session.Start_async();
            await session.ConfirmEdge_async(0);

            int snapshotChangedCount = 0;
            session.SnapshotChanged += snapshot => snapshotChangedCount++;

            ConfirmEdgeResponse response = await session.ConfirmEdge_async(0);

            Assert.That(response.IsAccepted , Is.False);
            Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.EDGE_ALREADY_CONFIRMED));
            Assert.That(response.Snapshot.Revision , Is.EqualTo(1));
            Assert.That(session.CurrentSnapshot.Revision , Is.EqualTo(1));
            Assert.That(snapshotChangedCount , Is.EqualTo(0));

            session.Dispose();
        }

        [Test]
        public async Task RequestSync_async_ReturnsIndependentCopyWithoutChangingRevision()
        {
            LocalGameSession session = new LocalGameSession();

            await session.Start_async();

            MatchSnapshot firstSnapshot = await session.RequestSync_async();
            firstSnapshot.EdgeOwners[ 0 ] = PLAYER_INDEX_ENUM.PLAYER_ONE;

            MatchSnapshot secondSnapshot = await session.RequestSync_async();

            Assert.That(secondSnapshot.Revision , Is.EqualTo(0));
            Assert.That(secondSnapshot.EdgeOwners[ 0 ] , Is.EqualTo(PLAYER_INDEX_ENUM.NONE));

            session.Dispose();
        }
    }
}
