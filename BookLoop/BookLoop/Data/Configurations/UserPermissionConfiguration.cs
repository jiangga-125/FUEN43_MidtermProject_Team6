using BookLoop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
	public void Configure(EntityTypeBuilder<UserPermission> builder)
	{
		builder.ToTable("USER_PERMISSIONS");
		builder.HasKey(up => new { up.UserID, up.PermissionID });

		builder.HasOne(up => up.User)
			   .WithMany()
			   .HasForeignKey(up => up.UserID)
			   .OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(up => up.Permission)
			   .WithMany()
			   .HasForeignKey(up => up.PermissionID)
			   .OnDelete(DeleteBehavior.Cascade);
	}
}
