namespace BookLoop;

public class UserLoginHistory
{
	public int LoginID { get; set; }
	public int UserID { get; set; }
	public DateTime OccurredAt { get; set; }
	public bool IsSuccess { get; set; }
	public string? IpAddress { get; set; }
	public string? UserAgent { get; set; }
	public string? FailReason { get; set; }

	public User? User { get; set; }
}
