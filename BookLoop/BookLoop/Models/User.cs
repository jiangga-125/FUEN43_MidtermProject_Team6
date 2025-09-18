using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop
{
    public class User
    {
        public int UserID { get; set; }
        /// <summary>1=顧客,2=員工,3=書商</summary>
        public byte UserType { get; set; }
        [MaxLength(254)] public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public byte Status { get; set; } = 1;
        public DateTime? LastLoginAt { get; set; }
        public DateTime? LockoutEndAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public string PasswordHash { get; set; } = string.Empty;
        public bool MustChangePassword { get; set; }

        // Navigations (供 EF 關聯使用)
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<SupplierUser> SupplierUsers { get; set; } = new List<SupplierUser>();
        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
    }
}
