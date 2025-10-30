namespace BookLoop.Models
{
	public class Template
	{
		public int TemplateId { get; set; }
		public string TemplateKey { get; set; } = "";
		public string? Description { get; set; }
		public ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
	}
}
