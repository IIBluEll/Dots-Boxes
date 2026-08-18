using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomLifecycleServiceTests
    {
        [Test]
        public async Task TryRemoveTerminalWithoutConnections_OpenRoom_KeepsRoom()
        {
            TestContextData context = await CreateContext_async(false);

            bool removed = await context.Service
                .TryRemoveTerminalWithoutConnections_async(context.Room.MatchId);

            Assert.Multiple(() =>
            {
                Assert.That(removed , Is.False);
                Assert.That(context.Provider.TryGet(context.Room.MatchId , out _) , Is.True);
            });
        }

        [Test]
        public async Task TryRemoveTerminalWithoutConnections_ConnectedFinishedRoom_KeepsRoom()
        {
            TestContextData context = await CreateContext_async(true);

            context.Registry.TryRegister(
                "connection-one" ,
                context.Room.MatchId ,
                context.Room.PlayerOne.UserId);

            await context.Room.TryHandlePlayerExit_async(context.Room.PlayerOne.UserId);

            bool removed = await context.Service
                .TryRemoveTerminalWithoutConnections_async(context.Room.MatchId);

            Assert.Multiple(() =>
            {
                Assert.That(removed , Is.False);
                Assert.That(context.Provider.TryGet(context.Room.MatchId , out _) , Is.True);
            });
        }

        [Test]
        public async Task TryRemoveTerminalWithoutConnections_NoConnections_RemovesRoomOnce()
        {
            TestContextData context = await CreateContext_async(true);

            await context.Room.TryHandlePlayerExit_async(context.Room.PlayerOne.UserId);

            bool firstRemoved = await context.Service
                .TryRemoveTerminalWithoutConnections_async(context.Room.MatchId);

            bool secondRemoved = await context.Service
                .TryRemoveTerminalWithoutConnections_async(context.Room.MatchId);

            Assert.Multiple(() =>
            {
                Assert.That(firstRemoved , Is.True);
                Assert.That(secondRemoved , Is.False);
                Assert.That(context.Provider.TryGet(context.Room.MatchId , out _) , Is.False);
                Assert.That(context.Provider.Count , Is.Zero);
            });
        }

        private static async Task<TestContextData> CreateContext_async(bool createActiveRoom)
        {
            MatchRoomProvider provider = new MatchRoomProvider();
            MatchConnectionRegistry registry = new MatchConnectionRegistry();
            MatchRoom room = createActiveRoom
                ? await ActiveMatchRoomTestFactory.Create_async()
                : new MatchRoom(
                    Guid.NewGuid() ,
                    new MatchPlayer(Guid.NewGuid()) ,
                    new MatchPlayer(Guid.NewGuid()) ,
                    PLAYER_INDEX_ENUM.PLAYER_ONE);

            provider.TryAdd(room);

            MatchRoomLifecycleService service = new MatchRoomLifecycleService(
                provider ,
                registry ,
                NullLogger<MatchRoomLifecycleService>.Instance);

            return new TestContextData(provider , registry , room , service);
        }

        private sealed class TestContextData
        {
            public MatchRoomProvider Provider { get; }
            public MatchConnectionRegistry Registry { get; }
            public MatchRoom Room { get; }
            public MatchRoomLifecycleService Service { get; }

            public TestContextData(
                MatchRoomProvider provider ,
                MatchConnectionRegistry registry ,
                MatchRoom room ,
                MatchRoomLifecycleService service)
            {
                Provider = provider;
                Registry = registry;
                Room = room;
                Service = service;
            }
        }
    }
}
