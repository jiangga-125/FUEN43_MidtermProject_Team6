// 路徑：BookLoop/BookLoop/Services/Mail/IMailServer.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BookLoop.Services.Mail
{
	/// <summary>
	/// 全專案統一寄信介面
	/// </summary>
	public interface IMailService
	{
		/// <summary>
		/// 寄送一般通知（無附件）
		/// </summary>
		Task SendAsync(
			string to,
			string subject,
			string body,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// 寄送郵件，可附加附件（例如 Excel、PDF）
		/// </summary>
		Task SendAsync(
			string to,
			string subject,
			string body,
			string? attachmentName,
			byte[]? attachmentBytes,
			string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			CancellationToken cancellationToken = default);
	}
}
