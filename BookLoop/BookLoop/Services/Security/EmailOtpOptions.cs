namespace BookLoop.Services.Security
{
	public class EmailOtpOptions
	{
		public int CodeLength { get; set; } = 6;
		public int ExpireMinutes { get; set; } = 10;
		public int MaxAttempts { get; set; } = 5;
		public int ResendCooldownSeconds { get; set; } = 60;
		public int BypassIfTrustedDeviceDays { get; set; } = 90;
	}
}
