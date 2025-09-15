// Models/ViewModels/CreateReviewVm.cs
using System.ComponentModel.DataAnnotations;

namespace 會員.Models
{
	public class CreateReviewVm
	{
		[Required(ErrorMessage = "請輸入會員編號")]
		public int MemberID { get; set; }                 // 發評會員

		[Range(0, 255, ErrorMessage = "TargetType 不正確")]
		public byte TargetType { get; set; }              // 0=商品, 2=會員(依你設定)

		[Required(ErrorMessage = "請輸入評論對象 ID")]
		public int TargetID { get; set; }                 // 目標ID

		[Range(1, 5, ErrorMessage = "評分需介於 {1}~{2}")]
		public byte Rating { get; set; }                  // 1~5

		[Required(ErrorMessage = "請輸入評論內容")]
		[MinLength(10, ErrorMessage = "內容至少 {1} 個字")]
		public string Content { get; set; } = "";         // 內容( ≥ 10 字)
	}
}
