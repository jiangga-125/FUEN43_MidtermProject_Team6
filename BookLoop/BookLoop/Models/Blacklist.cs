namespace BookLoop;

public class Blacklist
{
	public int BlacklistID { get; set; }
	public int MemberID { get; set; }
	public string? Reason { get; set; }
	public byte SourceType { get; set; }    // 1=系統,2=人工
	public DateTime StartAt { get; set; }
	public DateTime? EndAt { get; set; }
	public int? LiftedByUserID { get; set; }
	public DateTime? LiftedAt { get; set; }
	public DateTime CreatedAt { get; set; }

	// 導航屬性（供 Include 使用）
	public Member? Member { get; set; }

	// 衍生屬性：是否已解封（不落庫）
	public bool IsLifted => LiftedAt.HasValue;
}
