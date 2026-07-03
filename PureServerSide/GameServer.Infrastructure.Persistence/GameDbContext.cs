using GameServer.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Infrastructure.Persistence;

public class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<AccountCharacter> AccountCharacters => Set<AccountCharacter>();
    public DbSet<Player> Players => Set<Player>();
    public DbSet<Ship> Ships => Set<Ship>();
    public DbSet<MarketOrder> MarketOrders => Set<MarketOrder>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Guild> Guilds => Set<Guild>();
    public DbSet<Fleet> Fleets => Set<Fleet>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<GameInstance> Instances => Set<GameInstance>();

    // ── 宇宙/世界层级 ──
    public DbSet<LocationNode> Locations => Set<LocationNode>();
    public DbSet<TerrainModification> TerrainModifications => Set<TerrainModification>();
    public DbSet<WorldEnvironmentState> WorldEnvironmentStates => Set<WorldEnvironmentState>();
    public DbSet<WorldSpawnEntry> WorldSpawns => Set<WorldSpawnEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountName).HasMaxLength(32).IsRequired();
            entity.Property(e => e.NormalizedName).HasMaxLength(32).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(e => e.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<AccountCharacter>(entity =>
        {
            entity.HasKey(e => new { e.AccountId, e.CharacterId });
            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(e => e.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.CharacterId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => e.CharacterId).IsUnique();
        });

        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Faction).HasMaxLength(32).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<Ship>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ShipType).HasMaxLength(64).IsRequired();
        });

        modelBuilder.Entity<MarketOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.StationId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.PricePerUnit).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.ItemId, e.StationId });
        });

        modelBuilder.Entity<Station>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<Guild>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Ignore(e => e.MemberIds);
        });

        modelBuilder.Entity<Fleet>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(64).IsRequired();
            entity.Ignore(e => e.MemberIds);
        });

        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Rarity).HasMaxLength(32).IsRequired();
            entity.HasIndex(e => new { e.OwnerId, e.ItemId });
        });

        modelBuilder.Entity<GameInstance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TemplateId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Difficulty).HasMaxLength(32).IsRequired();
            entity.Ignore(e => e.PlayerIds);
        });

        // ══════════════════════════════════════════════════════════════
        // 宇宙/世界层级实体
        // ══════════════════════════════════════════════════════════════

        modelBuilder.Entity<LocationNode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(128).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(64).IsRequired();
            entity.Property(e => e.HierarchyPath).HasMaxLength(512);
            entity.Property(e => e.BiomeId).HasMaxLength(32);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");

            entity.HasIndex(e => e.ParentLocationId);
            entity.HasIndex(e => e.LocationType);
            entity.HasIndex(e => e.Code);
            entity.HasIndex(e => e.HierarchyPath);
            entity.HasIndex(e => new { e.LocalX, e.LocalZ });

            entity.HasOne<LocationNode>()
                .WithMany()
                .HasForeignKey(e => e.ParentLocationId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TerrainModification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.ModificationType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ChunkKey).HasMaxLength(128);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => new { e.LocationId, e.ChunkKey, e.SequenceTick });
        });

        modelBuilder.Entity<WorldEnvironmentState>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.LocationId).IsUnique();
        });

        modelBuilder.Entity<WorldSpawnEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SpawnType).HasMaxLength(32).IsRequired();
            entity.Property(e => e.TemplateId).HasMaxLength(128).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(128);
            entity.Property(e => e.MetadataJson).HasColumnType("jsonb");
            entity.HasIndex(e => e.LocationId);
            entity.HasIndex(e => new { e.LocationId, e.SpawnType });
        });

        // ── Player 位置扩展列 ──
        modelBuilder.Entity<Player>(entity =>
        {
            entity.Property(e => e.CurrentLocationId);
            entity.Property(e => e.LocalPositionX);
            entity.Property(e => e.LocalPositionY);
            entity.Property(e => e.LocalPositionZ);
            entity.Property(e => e.SolarSystemId);
            entity.HasIndex(e => e.CurrentLocationId);
            entity.HasIndex(e => e.SolarSystemId);
        });
    }
}
