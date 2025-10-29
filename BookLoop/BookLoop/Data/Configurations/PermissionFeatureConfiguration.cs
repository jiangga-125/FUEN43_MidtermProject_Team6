using BookLoop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PermissionFeatureConfiguration : IEntityTypeConfiguration<PermissionFeature>
{
	public void Configure(EntityTypeBuilder<PermissionFeature> builder)
	{
		builder.ToTable("PERMISSION_FEATURES"); // ­Y
		builder.HasKey(pf => new { pf.PermissionID, pf.FeatureID });

		builder.HasOne(pf => pf.Permission)
			   .WithMany() // ­Y Permission ¦³ ICollection<PermissionFeature>¡A§ï¦¨ .WithMany(p => p.PermissionFeatures)
			   .HasForeignKey(pf => pf.PermissionID)
			   .OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(pf => pf.Feature)
			   .WithMany()
			   .HasForeignKey(pf => pf.FeatureID)
			   .OnDelete(DeleteBehavior.Cascade);
	}
}
