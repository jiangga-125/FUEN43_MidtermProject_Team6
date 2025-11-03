namespace BookLoop.Models
{
	public class TemplateVersion
	{
		public int TemplateVersionId { get; set; }
		public int TemplateId { get; set; }
		public Template Template { get; set; } = null!;
		public string TemplateName { get; set; } = ""; // 變體名
		public string? Subject { get; set; }
		public string? BodyHtml { get; set; }
		public string? DesignJson { get; set; }
		public bool IsActive { get; set; } = true;
		public bool IsDefault { get; set; } = false;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
	}
}
