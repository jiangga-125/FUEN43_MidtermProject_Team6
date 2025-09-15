// /Services/Common/Result.cs
namespace 會員.Services.Common
{
	// 泛型結果：統一回傳成功/失敗與訊息
	public class Result<T>
	{
		public bool Ok { get; private set; }                  // 是否成功
		public string? Message { get; private set; }          // 失敗或提示訊息
		public T? Data { get; private set; }                  // 成功時的資料載體

		public static Result<T> Success(T data, string? msg = null)
			=> new Result<T> { Ok = true, Data = data, Message = msg };

		public static Result<T> Fail(string msg)
			=> new Result<T> { Ok = false, Message = msg };
	}
}
