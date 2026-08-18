using DotsAndBoxes.Server.Matches;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchConnectionRegistryTests
    {
        [Test]
        public void TryRegister_SameParticipantWithSecondConnection_RejectsSecondConnection()
        {
            MatchConnectionRegistry registry = new MatchConnectionRegistry();
            Guid matchId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();

            bool firstRegistered = registry.TryRegister(
                "connection-one" ,
                matchId ,
                userId);

            bool secondRegistered = registry.TryRegister(
                "connection-two" ,
                matchId ,
                userId);

            Assert.Multiple(() =>
            {
                Assert.That(firstRegistered , Is.True);
                Assert.That(secondRegistered , Is.False);
            });
        }

        [Test]
        public void TryRemove_RegisteredConnection_ReturnsParticipantAndAllowsNewConnection()
        {
            MatchConnectionRegistry registry = new MatchConnectionRegistry();
            Guid matchId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();

            registry.TryRegister("connection-one" , matchId , userId);

            bool removed = registry.TryRemove(
                "connection-one" ,
                out Guid removedMatchId ,
                out Guid removedUserId);

            bool registeredAgain = registry.TryRegister(
                "connection-two" ,
                matchId ,
                userId);

            Assert.Multiple(() =>
            {
                Assert.That(removed , Is.True);
                Assert.That(removedMatchId , Is.EqualTo(matchId));
                Assert.That(removedUserId , Is.EqualTo(userId));
                Assert.That(registeredAgain , Is.True);
            });
        }

        [Test]
        public void TryRegister_SameConnectionWithSameParticipant_IsIdempotent()
        {
            MatchConnectionRegistry registry = new MatchConnectionRegistry();
            Guid matchId = Guid.NewGuid();
            Guid userId = Guid.NewGuid();

            bool firstRegistered = registry.TryRegister(
                "connection-one" ,
                matchId ,
                userId);

            bool secondRegistered = registry.TryRegister(
                "connection-one" ,
                matchId ,
                userId);

            Assert.Multiple(() =>
            {
                Assert.That(firstRegistered , Is.True);
                Assert.That(secondRegistered , Is.True);
            });
        }

        [Test]
        public void ConnectionQueries_RegisteredParticipants_ReturnCurrentState()
        {
            MatchConnectionRegistry registry = new MatchConnectionRegistry();
            Guid matchId = Guid.NewGuid();
            Guid playerOneUserId = Guid.NewGuid();
            Guid playerTwoUserId = Guid.NewGuid();

            registry.TryRegister("connection-one" , matchId , playerOneUserId);
            registry.TryRegister("connection-two" , matchId , playerTwoUserId);

            bool found = registry.TryGetParticipant(
                "connection-one" ,
                out Guid foundMatchId ,
                out Guid foundUserId);

            Assert.Multiple(() =>
            {
                Assert.That(found , Is.True);
                Assert.That(foundMatchId , Is.EqualTo(matchId));
                Assert.That(foundUserId , Is.EqualTo(playerOneUserId));
                Assert.That(registry.ContainsUser(playerTwoUserId) , Is.True);
                Assert.That(registry.GetMatchConnectionCount(matchId) , Is.EqualTo(2));
                Assert.That(registry.Count , Is.EqualTo(2));
            });
        }
    }
}
