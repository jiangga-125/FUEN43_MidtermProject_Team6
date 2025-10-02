namespace BookLoop.Models.Dto
{
	public class ImportBookDto
	{
		public string? ISBN { get; set; }
		public string? Title { get; set; }
		public string? Author { get; set; }
		public string? Publisher { get; set; }
		public string? PublishDate { get; set; }
		public string? Category { get; set; }
		public string? ImagePath { get; set; }
	}
}
