namespace DotsAndBoxes.Server.Accounts;

public sealed class ExternalIdentity
{
    public string Provider { get; set; } = string.Empty;
    public string ProviderApplicationId { get; set; } = string.Empty;
    public string ProviderPlayerId { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public GameUser User { get; set; } = null!;
}
