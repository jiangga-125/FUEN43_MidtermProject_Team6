namespace BookLoop.Models;

public class SearchHit
{
	public string Type { get; set; } = "";   // 類別
	public string Title { get; set; } = "";  // 文字
	public string? Sub { get; set; }         // 補充
	public string Url { get; set; } = "";    // 點擊導向位置
}

public class SearchResultVm
{
	public string Query { get; set; } = "";
	public List<SearchHit> Hits { get; set; } = new();
}
