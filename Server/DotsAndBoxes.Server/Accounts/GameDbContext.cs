using Microsoft.EntityFrameworkCore;

namespace DotsAndBoxes.Server.Accounts;

public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public const string SCHEMA_NAME = "accounts";
    public DbSet<GameUser> Users => Set<GameUser>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(SCHEMA_NAME);
        var user = modelBuilder.Entity<GameUser>();
        user.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_nonempty_id", "user_id <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("ck_users_display_name", NonBlank("display_name"));
        });
        user.HasKey(x => x.UserId).HasName("pk_users");
        user.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();
        user.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired();
        user.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        user.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamp with time zone");

        var identity = modelBuilder.Entity<ExternalIdentity>();
        identity.ToTable("external_identities", table =>
        {
            table.HasCheckConstraint("ck_identities_provider", NonBlank("provider"));
            table.HasCheckConstraint("ck_identities_application", NonBlank("provider_application_id"));
            table.HasCheckConstraint("ck_identities_player", NonBlank("provider_player_id"));
        });
        identity.HasKey(x => new { x.Provider, x.ProviderApplicationId, x.ProviderPlayerId })
            .HasName("pk_external_identities");
        identity.Property(x => x.Provider).HasColumnName("provider").IsRequired();
        identity.Property(x => x.ProviderApplicationId).HasColumnName("provider_application_id").IsRequired();
        identity.Property(x => x.ProviderPlayerId).HasColumnName("provider_player_id").IsRequired();
        identity.Property(x => x.UserId).HasColumnName("user_id");
        identity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamp with time zone");
        identity.HasIndex(x => x.UserId).HasDatabaseName("ix_external_identities_user_id");
        identity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_external_identities_users");
    }

    private static string NonBlank(string column)
    {
        // Match .NET Char.IsWhiteSpace, including Unicode-only whitespace values.
        return $"length(btrim({column}, " +
            "chr(9)||chr(10)||chr(11)||chr(12)||chr(13)||chr(32)||chr(133)||chr(160)||chr(5760)||" +
            "chr(8192)||chr(8193)||chr(8194)||chr(8195)||chr(8196)||chr(8197)||chr(8198)||" +
            "chr(8199)||chr(8200)||chr(8201)||chr(8202)||chr(8232)||chr(8233)||chr(8239)||chr(8287)||chr(12288))) > 0";
    }
}
