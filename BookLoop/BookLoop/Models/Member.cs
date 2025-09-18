using BookLoop.Models;

namespace BookLoop;

public class Member
{
	public int MemberID { get; set; }
	public int? UserID { get; set; }
	public string Account { get; set; } = null!;
	public string Username { get; set; } = null!;
	public string? Email { get; set; }
	public string? Phone { get; set; }
	public byte Role { get; set; }          // 0=一般,1=管理會員(保留)
	public byte Status { get; set; }        // 0=未啟用,1=啟用,2=停權,3=關閉
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public int TotalBooks { get; set; }
	public int TotalBorrows { get; set; }
	public byte[] RowVersion { get; set; } = Array.Empty<byte>();
	public ICollection<MemberPoint> MemberPoints { get; set; } = new List<MemberPoint>();
	public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
    public virtual ICollection<PointsLedger> PointsLedgers { get; set; } = new List<PointsLedger>();
   	public virtual ICollection<RuleApplication> RuleApplications { get; set; } = new List<RuleApplication>();
	public virtual ICollection<BorrowRecord> BorrowRecords { get; set; } = new List<BorrowRecord>();
	public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
	public virtual ICollection<PenaltyTransaction> PenaltyTransactions { get; set; } = new List<PenaltyTransaction>();

}
