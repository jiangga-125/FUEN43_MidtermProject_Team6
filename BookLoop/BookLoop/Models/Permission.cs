using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace BookLoop
{
    public class Permission
    {
		[Key]
		public int PermissionID { get; set; }
        public string PermKey { get; set; } = "";
        public string PermName { get; set; } = "";
        public string? PermGroup { get; set; }
        public string? Note { get; set; }

        public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
        public ICollection<PermissionFeature> PermissionFeatures { get; set; } = new List<PermissionFeature>();
    }
}
