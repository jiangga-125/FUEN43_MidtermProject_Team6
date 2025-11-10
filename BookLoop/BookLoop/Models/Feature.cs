using System.ComponentModel.DataAnnotations;

namespace BookLoop
{
    public class Feature
    {
		[Key]
		public int FeatureID { get; set; }
        public string? Code { get; set; } = "";
        public string? Name { get; set; } = "";
        public string FeatureGroup { get; set; } = "";
        public bool IsPageLevel { get; set; }
        public int SortOrder { get; set; }
		public ICollection<PermissionFeature> PermissionFeatures { get; set; } = new List<PermissionFeature>();
	}
}
