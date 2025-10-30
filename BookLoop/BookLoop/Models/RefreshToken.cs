namespace BookLoop.Models
{
	public class RefreshToken
	{
		public int RefreshID { get; set; }
		public int UserID { get; set; }

		// 在 DB 存的是 hash（例如 SHA256 的 Base64）
		public string TokenHash { get; set; } = null!;

		public DateTime CreatedAt { get; set; }
		public DateTime ExpiresAt { get; set; }
		public bool IsRevoked { get; set; }

		// 如果 User entity 名為 User 並且 FK 正確）
		// public virtual User? User { get; set; }
	}
}
