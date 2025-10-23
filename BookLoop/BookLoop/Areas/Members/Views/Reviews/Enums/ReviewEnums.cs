// ReviewEnums/Enums.cs
namespace BookLoop.ReviewEnums
{
	public static class ReviewStatusCodes
	{
		public const byte Pending = 0;
		public const byte Approved = 1;
		public const byte Rejected = 2;
	}

	public static class ModerationDecisionCodes
	{
		public const byte AutoPass = 0;
		public const byte NeedsManual = 1;
		public const byte Rejected = 2;
		public const byte ApprovedByAdmin = 3;
		public const byte RejectedByAdmin = 4;
	}

	// ReviewRuleSettings.DuplicatePolicy 對應
	public static class DuplicatePolicyCodes
	{
		public const byte Ignore = 0;
		public const byte WarnWithinWindow = 1;
		public const byte ForbidWithinWindow = 2;
	}

	// 你如果有 TargetType 的定義，也可以集中在這裡
	public static class TargetTypes
	{
		public const byte Book = 1;
		public const byte Member = 2; // 你在設定檔中當作「禁止自評」的判斷用
	}
}
