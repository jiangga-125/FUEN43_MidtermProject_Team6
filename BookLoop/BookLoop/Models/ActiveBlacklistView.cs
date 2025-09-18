namespace BookLoop;

public class ActiveBlacklistView
{
	public int MemberID { get; set; }
	public DateTime StartAt { get; set; }
	public DateTime? EndAt { get; set; }
	public DateTime? LiftedAt { get; set; }
}
