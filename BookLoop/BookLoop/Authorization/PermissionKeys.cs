namespace BookLoop.Authorization
{
	/// <summary>集中管理所有權限鍵，避免字串散落。</summary>
	public static class PermissionKeys
	{
		// 儀表/首頁
		public const string Dashboard_View = "Dashboard.View";

		// 書籍
		public const string Books_View = "Books.View";
		public const string Books_Edit = "Books.Edit";

		// 訂單
		public const string Orders_View = "Orders.View";
		public const string Orders_Edit = "Orders.Edit";

		// 二手書
		public const string UsedBooks_View = "UsedBooks.View";
		public const string UsedBooks_Edit = "UsedBooks.Edit";

		// 報表
		public const string Reports_View = "Reports.View";

		// 優惠券
		public const string Coupons_View = "Coupons.View";
		public const string Coupons_Edit = "Coupons.Edit";

		// 帳號與權限
		public const string Accounts_View = "Accounts.View";
		public const string Accounts_Edit = "Accounts.Edit";
		public const string Permissions_Manage = "Permissions.Manage";

		/// <summary>全部列出，給「權限管理」畫面產生勾選清單。</summary>
		public static readonly string[] All = new[]
		{
			Dashboard_View,
			Books_View, Books_Edit,
			Orders_View, Orders_Edit,
			UsedBooks_View, UsedBooks_Edit,
			Reports_View,
			Coupons_View, Coupons_Edit,
			Accounts_View, Accounts_Edit,
			Permissions_Manage
		};
	}
}
