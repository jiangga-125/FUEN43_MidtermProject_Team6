using BookLoop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class SupplierUserConfiguration : IEntityTypeConfiguration<SupplierUser>
{
	public void Configure(EntityTypeBuilder<SupplierUser> builder)
	{
		builder.ToTable("SupplierUser");
		builder.HasKey(su => new { su.SupplierID, su.UserID });

		builder.HasOne(su => su.Supplier)
			   .WithMany()
			   .HasForeignKey(su => su.SupplierID)
			   .OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(su => su.User)
			   .WithMany()
			   .HasForeignKey(su => su.UserID)
			   .OnDelete(DeleteBehavior.Cascade);
	}
}
