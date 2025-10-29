using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop
{
    public class Role
    {
		[Key]
		public int RoleID { get; set; }
        public string RoleCode { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;

        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
