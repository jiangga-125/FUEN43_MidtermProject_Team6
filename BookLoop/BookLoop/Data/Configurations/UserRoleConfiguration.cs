using BookLoop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
	public void Configure(EntityTypeBuilder<UserRole> builder)
	{
		builder.ToTable("USER_ROLES");
		builder.HasKey(ur => new { ur.UserID, ur.RoleID });

		builder.HasOne(ur => ur.User)
			   .WithMany()
			   .HasForeignKey(ur => ur.UserID)
			   .OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(ur => ur.Role)
			   .WithMany()
			   .HasForeignKey(ur => ur.RoleID)
			   .OnDelete(DeleteBehavior.Cascade);
	}
}
