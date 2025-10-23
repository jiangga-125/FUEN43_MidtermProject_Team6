using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using BookLoop.Models;

namespace BookLoop.Areas.Members.ViewModels
{
	// 後台建立/編輯優惠券用的 ViewModel（把 Coupon 欄位 + 多選分類包在一起）
	public class CouponEditVm
	{
		// ======== 基本券資料（與 Coupon 對應） ========
		public int? CouponID { get; set; }                      // 編輯時用；新增時為 null

		[Required(ErrorMessage = "請輸入名稱")]
		public string Name { get; set; } = string.Empty;

		[Required(ErrorMessage = "請輸入券碼")]
		public string Code { get; set; } = string.Empty;

		[Range(0, 1, ErrorMessage = "折扣類型錯誤")]
		public byte DiscountType { get; set; } = 0;             // 0=金額, 1=%

		[Range(0.01, 999999, ErrorMessage = "請輸入正確折扣值")]
		public decimal DiscountValue { get; set; }              // 100=折100；10=打九折(90%)

		public decimal? MinOrderAmount { get; set; }            // 最低消費（選填）
		public decimal? MaxDiscountAmount { get; set; }         // 最高折抵（選填，%時常用）

		public DateTime? StartAt { get; set; }                  // 開始時間（選填）
		public DateTime? EndAt { get; set; }                    // 結束時間（選填）

		public bool IsActive { get; set; } = true;              // 是否啟用
		public int? MaxUsesPerMember { get; set; }              // 每會員可用次數（選填）
		public string? Description { get; set; }                // 簡介（選填）

		// ======== 額外表單欄位（非 Coupon 資料表直屬） ========
		public string? TermsText { get; set; }                  // 條款（你可先放別表或未來加欄位）

		// 多選分類：表單提交時會帶回這個集合
		public List<int> SelectedCategoryIds { get; set; } = new();

		// 用於畫面顯示的分類清單（<select multiple> 的資料來源）
		public List<SelectListItem> CategoryOptions { get; set; } = new();
	}
}
