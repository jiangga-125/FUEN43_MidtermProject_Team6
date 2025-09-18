namespace BookLoop
{
    public class PermissionFeature
    {
        public int PermissionID { get; set; }
        public int FeatureID { get; set; }

        public Permission? Permission { get; set; }
        public Feature? Feature { get; set; }
    }
}
