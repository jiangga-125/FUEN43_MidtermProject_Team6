namespace BookLoop
{
    public class Feature
    {
        public int FeatureID { get; set; }
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public string FeatureGroup { get; set; } = "";
        public bool IsPageLevel { get; set; }
        public int SortOrder { get; set; }
        public System.Collections.Generic.ICollection<PermissionFeature> PermissionFeatures { get; set; }
            = new System.Collections.Generic.List<PermissionFeature>();
    }
}
