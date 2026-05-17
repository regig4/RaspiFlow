using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RaspberryAzure.ImageRecognition;

public class SceneSnapshotConfiguration : IEntityTypeConfiguration<SceneSnapshot>
{
    public void Configure(EntityTypeBuilder<SceneSnapshot> builder)
    {
        builder.ToTable("SceneSnapshots");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Label)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        // Vector column
        builder.Property(x => x.DescriptionEmbedding)
            .HasColumnType("vector(768)");

        // Tags as JSON column
        builder.Property(x => x.Tags)
            .HasColumnType("json");
    }
}