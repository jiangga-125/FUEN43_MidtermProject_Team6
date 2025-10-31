using System.ComponentModel.DataAnnotations;

namespace BookLoop
{
	public class UserFormVm
	{
		public int UserID { get; set; }

		[Required, EmailAddress, Display(Name = "Email")]
		public string Email { get; set; } = "";

		[Display(Name = "電話")]
		public string? Phone { get; set; }

		[Display(Name = "名稱")]
		public string? Name { get; set; }

		[Range(1, 2, ErrorMessage = "狀態僅允許啟用或停用。")]
		[Display(Name = "狀態")]
		public byte Status { get; set; } = 1;

		// 1=顧客(前台不使用), 2=員工, 3=書商
		[Range(1, 3, ErrorMessage = "帳號類型不正確。")]
		[Display(Name = "類型")]
		public byte UserType { get; set; } = 2;
	}
}
