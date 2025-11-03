namespace BookLoop.Services.Common
{
	/// <summary>
	/// 通用泛型結果封裝類別，與舊版本完全相容 (支援 Ok 屬性、Ok 方法、ErrorMessage、Data)
	/// </summary>
	public class Result<T>
	{
		// === 狀態屬性 ===
		public bool IsSuccess { get; private set; }
		public bool Ok => IsSuccess;              // ✅ 提供給 if (!result.Ok) 使用
		public string? Message { get; private set; }
		public string? ErrorMessage { get; private set; }

		// === 資料內容 ===
		public T? Value { get; private set; }
		public T? Data => Value;                  // ✅ 舊程式中用 result.Data 的別名

		private Result() { }

		// === 成功方法 ===
		public static Result<T> Success(T value, string? message = null)
		{
			return new Result<T>
			{
				IsSuccess = true,
				Value = value,
				Message = message,
				ErrorMessage = message
			};
		}

		// ✅ 舊專案用法：Result.Ok(value)
		public static Result<T> OkResult(T value, string? message = null)
		{
			return Success(value, message);
		}

		// === 失敗方法 ===
		public static Result<T> Fail(string message)
		{
			return new Result<T>
			{
				IsSuccess = false,
				Message = message,
				ErrorMessage = message
			};
		}

		// ✅ 舊專案用法：Result.Error("錯誤訊息")
		public static Result<T> Error(string message)
		{
			return Fail(message);
		}
	}
}
