using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomProviderTests
    {
        private static readonly DateTimeOffset TURN_DEADLINE_UTC =
            new DateTimeOffset(2030, 1, 1, 0, 0, 20, TimeSpan.Zero);

        [Test]
        public void TryAdd_WithNewMatch_AddsRoom()
        {
            MatchRoomProvider provider = new MatchRoomProvider();
            MatchRoom room = CreateRoom();

            bool added = provider.TryAdd(room);
            bool found = provider.TryGet(room.MatchId, out MatchRoom? foundRoom);

            Assert.Multiple(() =>
            {
                Assert.That(added , Is.True);
                Assert.That(found , Is.True);
                Assert.That(foundRoom , Is.SameAs(room));
                Assert.That(provider.Count , Is.EqualTo(1));
            });
        }

        [Test]
        public void TryAdd_WithDuplicatedMatchId_KeepsOriginalRoom()
        {
            MatchRoomProvider provider = new MatchRoomProvider();
            Guid matchId = Guid.NewGuid();

            MatchRoom originalRoom = CreateRoom(matchId);
            MatchRoom duplicatedRoom = CreateRoom(matchId);

            bool firstAdded = provider.TryAdd(originalRoom);
            bool secondAdded = provider.TryAdd(duplicatedRoom);

            provider.TryGet(matchId , out MatchRoom? foundRoom);

            Assert.Multiple(() =>
            {
                Assert.That(firstAdded , Is.True);
                Assert.That(secondAdded , Is.False);
                Assert.That(foundRoom , Is.SameAs(originalRoom));
                Assert.That(provider.Count , Is.EqualTo(1));
            });
        }

        [Test]
        public void TryRemove_WithExistingMatch_RemovesRoom()
        {
            MatchRoomProvider provider = new MatchRoomProvider();
            MatchRoom room = CreateRoom();

            provider.TryAdd(room);

            bool removed = provider.TryRemove(
                room.MatchId,
                out MatchRoom? removedRoom);

            bool foundAfterRemove = provider.TryGet(
                room.MatchId,
                out _);

            Assert.Multiple(() =>
            {
                Assert.That(removed , Is.True);
                Assert.That(removedRoom , Is.SameAs(room));
                Assert.That(foundAfterRemove , Is.False);
                Assert.That(provider.Count , Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TryAdd_WithConcurrentDuplicatedMatches_AddsOnlyOne()
        {
            MatchRoomProvider provider = new MatchRoomProvider();
            Guid matchId = Guid.NewGuid();

            Task<bool>[] tasks = Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() =>
                {
                    MatchRoom room = CreateRoom(matchId);
                    return provider.TryAdd(room);
                }))
                .ToArray();

            bool[] results = await Task.WhenAll(tasks);

            Assert.Multiple(() =>
            {
                Assert.That(results.Count(result => result) , Is.EqualTo(1));
                Assert.That(provider.Count , Is.EqualTo(1));
                Assert.That(provider.TryGet(matchId , out _) , Is.True);
            });
        }

        private static MatchRoom CreateRoom(Guid? matchId = null)
        {
            MatchPlayer playerOne =
                new MatchPlayer(Guid.NewGuid(), "connection-one");

            MatchPlayer playerTwo =
                new MatchPlayer(Guid.NewGuid(), "connection-two");

            return new MatchRoom(
                matchId ?? Guid.NewGuid() ,
                playerOne ,
                playerTwo ,
                PLAYER_INDEX_ENUM.PLAYER_ONE ,
                TURN_DEADLINE_UTC);
        }
    }
}