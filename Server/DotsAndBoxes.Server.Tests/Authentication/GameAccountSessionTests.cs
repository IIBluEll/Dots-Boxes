using DotsAndBoxes.Gameplay;

namespace DotsAndBoxes.Server.Tests.Authentication;

public sealed class GameAccountSessionTests
{
    [SetUp]
    public void SetUp() => GameAccountSession.Clear();
    [TearDown]
    public void TearDown() => GameAccountSession.Clear();

    [Test]
    public void CachedLoginIsScopedToServerAndUser()
    {
        Guid userId = Guid.NewGuid();
        GameAccountSession.Set("https://game.test", userId, "플레이어", "test-token", DateTimeOffset.UtcNow.AddHours(1));
        Assert.That(GameAccountSession.TryGet("https://game.test/", out Guid actual, out _), Is.True);
        Assert.That(actual, Is.EqualTo(userId));
        Assert.That(GameAccountSession.TryGet("https://other.test", out _, out _), Is.False);
        Assert.Throws<InvalidOperationException>(() => GameAccountSession.GetAccessToken("https://other.test", userId));
        Assert.Throws<InvalidOperationException>(() => GameAccountSession.GetAccessToken("https://game.test", Guid.NewGuid()));
    }

    [Test]
    public void NearExpiryRequiresReloginAndNewTokenReplacesOldOne()
    {
        Guid userId = Guid.NewGuid();
        GameAccountSession.Set("https://game.test", userId, "이전", "old-test-token", DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.That(GameAccountSession.TryGet("https://game.test", out _, out _), Is.False);
        GameAccountSession.Set("https://game.test", userId, "새 이름", "new-test-token", DateTimeOffset.UtcNow.AddHours(1));
        Assert.That(GameAccountSession.TryGet("https://game.test", out Guid actual, out string name), Is.True);
        Assert.That(actual, Is.EqualTo(userId));
        Assert.That(name, Is.EqualTo("새 이름"));
        Assert.That(GameAccountSession.GetAccessToken("https://game.test", userId), Is.EqualTo("new-test-token"));
        GameAccountSession.Clear();
        Assert.Throws<InvalidOperationException>(() => GameAccountSession.GetAccessToken("https://game.test", userId));
    }

    [Test]
    public void ExpiredResponseCannotBecomeAnAuthenticatedSession()
    {
        Assert.Throws<ArgumentException>(() => GameAccountSession.Set("https://game.test", Guid.NewGuid(), "플레이어",
            "expired-test-token", DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.That(GameAccountSession.TryGet("https://game.test", out _, out _), Is.False);
    }
}
