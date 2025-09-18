namespace BookLoop;

public class UserFormVm
{
	public int? UserID { get; set; }        // null = Create；有值 = Edit
	public string Email { get; set; } = "";
	public byte UserType { get; set; } = 2;  // 2=員工, 3=書商
	public string? Phone { get; set; }
	public byte Status { get; set; } = 1;    // 0=未啟用,1=啟用,2=停用

	// 只在「新增」時可填（可選）
	public string? NewPassword { get; set; }
}
