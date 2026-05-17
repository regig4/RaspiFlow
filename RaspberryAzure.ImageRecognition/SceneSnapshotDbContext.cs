using Microsoft.EntityFrameworkCore;

public class SceneSnapshotDbContext : DbContext
{
    public SceneSnapshotDbContext(DbContextOptions<SceneSnapshotDbContext> options) : base(options) { }

    public DbSet<SceneSnapshot> Snapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SceneSnapshotDbContext).Assembly);
    }
}