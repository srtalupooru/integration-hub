using Microsoft.EntityFrameworkCore;
namespace IntegrationHub.Infrastructure.Persistence;

public class HubDbContext : DbContext
{
    public HubDbContext(DbContextOptions<HubDbContext> options) : base(options) { }
    protected HubDbContext(DbContextOptions options) : base(options) { }
    public DbSet<IntegrationEntity> Integrations => Set<IntegrationEntity>();
    public DbSet<SystemEntity> Systems => Set<SystemEntity>();
    public DbSet<DefinitionVersionEntity> Versions => Set<DefinitionVersionEntity>();
    public DbSet<IntegrationNodeEntity> Nodes => Set<IntegrationNodeEntity>();
    public DbSet<IntegrationMessageEntity> IntegrationMessages => Set<IntegrationMessageEntity>();
    public DbSet<ComponentEntity> Components => Set<ComponentEntity>();
    public DbSet<ComponentVersionEntity> ComponentVersions => Set<ComponentVersionEntity>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<ComponentEntity>().HasKey(e => e.Id);
        model.Entity<ComponentEntity>().Property(e => e.Id).HasMaxLength(96);
        model.Entity<ComponentEntity>().Property(e => e.Revision).IsConcurrencyToken();
        model.Entity<ComponentEntity>().HasIndex(e => e.IsArchived);
        model.Entity<ComponentVersionEntity>().HasKey(e => new { e.ComponentId, e.Revision });
        model.Entity<ComponentVersionEntity>().Property(e => e.ComponentId).HasMaxLength(96);
        model.Entity<ComponentVersionEntity>().HasOne<ComponentEntity>().WithMany().HasForeignKey(e => e.ComponentId).OnDelete(DeleteBehavior.Restrict);
        var integration = model.Entity<IntegrationEntity>();
        integration.HasKey(e => e.Id);
        integration.Property(e => e.Id).HasMaxLength(128);
        integration.Property(e => e.Name).HasMaxLength(256);
        integration.Property(e => e.Status).HasMaxLength(32);
        integration.Property(e => e.BusinessDomain).HasMaxLength(256);
        integration.Property(e => e.Owner).HasMaxLength(256);
        integration.Property(e => e.Criticality).HasMaxLength(32);
        integration.Property(e => e.Revision).IsConcurrencyToken();
        integration.HasIndex(e => new { e.Status, e.BusinessDomain });
        integration.HasIndex(e => e.UpdatedAt);
        integration.HasMany(e => e.Nodes).WithOne().HasForeignKey(e => e.IntegrationId);
        integration.HasMany(e => e.Edges).WithOne().HasForeignKey(e => e.IntegrationId);
        integration.HasMany(e => e.Messages).WithOne().HasForeignKey(e => e.IntegrationId);
        integration.HasMany(e => e.Tags).WithOne().HasForeignKey(e => e.IntegrationId);
        integration.HasMany(e => e.Repositories).WithOne().HasForeignKey(e => e.IntegrationId);
        integration.HasMany(e => e.Runbooks).WithOne().HasForeignKey(e => e.IntegrationId);
        integration.HasMany(e => e.Dependencies).WithOne().HasForeignKey(e => e.IntegrationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<IntegrationNodeEntity>().HasKey(e => new { e.IntegrationId, e.Id });
        model.Entity<IntegrationNodeEntity>().Property(e => e.Id).HasMaxLength(128);
        model.Entity<IntegrationNodeEntity>().HasOne<SystemEntity>().WithMany().HasForeignKey(e => e.SystemId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<IntegrationNodeEntity>().Property(e => e.SharedResourceId).HasMaxLength(256);
        model.Entity<IntegrationNodeEntity>().HasIndex(e => e.SharedResourceId);
        model.Entity<IntegrationEdgeEntity>().HasKey(e => new { e.IntegrationId, e.Id });
        model.Entity<IntegrationEdgeEntity>().Property(e => e.Id).HasMaxLength(128);
        model.Entity<IntegrationMessageEntity>().HasKey(e => new { e.IntegrationId, e.MessageName });
        model.Entity<IntegrationMessageEntity>().Property(e => e.MessageName).HasMaxLength(256);
        model.Entity<IntegrationMessageEntity>().HasOne<MessageEntity>().WithMany().HasForeignKey(e => e.MessageName).OnDelete(DeleteBehavior.Restrict);
        model.Entity<MessageEntity>().HasKey(e => e.Name);
        model.Entity<MessageEntity>().Property(e => e.Name).HasMaxLength(256);
        if (Database.IsSqlServer())
        {
            // Message and tag identities have the same ordinal semantics as the canonical graph.
            model.Entity<MessageEntity>().Property(e => e.Name).UseCollation("Latin1_General_100_BIN2");
            model.Entity<IntegrationMessageEntity>().Property(e => e.MessageName).UseCollation("Latin1_General_100_BIN2");
            model.Entity<TagEntity>().Property(e => e.Value).UseCollation("Latin1_General_100_BIN2");
        }
        model.Entity<TagEntity>().HasKey(e => new { e.IntegrationId, e.Value });
        model.Entity<TagEntity>().Property(e => e.Value).HasMaxLength(256);
        model.Entity<RepositoryEntity>().HasKey(e => e.Id);
        model.Entity<RunbookEntity>().HasKey(e => e.Id);
        model.Entity<DependencyEntity>().HasKey(e => new { e.IntegrationId, e.TargetIntegrationId });
        model.Entity<DependencyEntity>().HasOne<IntegrationEntity>().WithMany().HasForeignKey(e => e.TargetIntegrationId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<SystemEntity>().HasKey(e => e.Id);
        model.Entity<SystemEntity>().Property(e => e.Id).HasMaxLength(128);
        model.Entity<DefinitionVersionEntity>().HasKey(e => new { e.IntegrationId, e.Revision });
        model.Entity<DefinitionVersionEntity>().HasOne<IntegrationEntity>().WithMany().HasForeignKey(e => e.IntegrationId).OnDelete(DeleteBehavior.Restrict);
        // Normalise DateTimeOffset to UTC ticks for consistent SQL ordering and SQLite contract tests.
        foreach (var entity in model.Model.GetEntityTypes())
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(DateTimeOffset)))
                property.SetValueConverter(new Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter<DateTimeOffset, long>(v => v.UtcTicks, v => new DateTimeOffset(v, TimeSpan.Zero)));
    }
    private void EnsureHistoryImmutable()
    {
        if (ChangeTracker.Entries<DefinitionVersionEntity>().Any(e => e.State is EntityState.Modified or EntityState.Deleted) ||
            ChangeTracker.Entries<ComponentVersionEntity>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Historical definitions are immutable.");
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => SaveChangesAsync(true, cancellationToken);
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureHistoryImmutable();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureHistoryImmutable();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
}
