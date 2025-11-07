using System.ComponentModel.DataAnnotations;

namespace BookLoop.Models.ViewModels
{
	public class CreateReviewVm
	{
		[Required(ErrorMessage = "會員編號必填")]
		public int MemberID { get; set; }

		public byte TargetType { get; set; } = 1;  // 預設書本，不再 Required

		[Required(ErrorMessage = "請選擇書籍")]
		public int TargetBookID { get; set; }  // ✅ 保留唯一版本

		[Required(ErrorMessage = "評分必填")]
		[Range(1, 5, ErrorMessage = "評分必須介於 1 到 5 之間")]
		public byte Rating { get; set; }

		[Required(ErrorMessage = "評論內容不能空白")]
		[MinLength(10, ErrorMessage = "評論至少需要 10 個字")]
		[MaxLength(200, ErrorMessage = "評論不能超過 200 個字")]
		public string Content { get; set; } = null!;

	}

}
