using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using BookLoop.Data;
using BookLoop.Models;

namespace BookLoop.Areas.Members.Controllers
{
	[Area("Members")]
	public class AdsController : Controller
	{
		private readonly MemberContext _db;
		private readonly IWebHostEnvironment _env;

		public AdsController(MemberContext db, IWebHostEnvironment env)
		{
			_db = db;
			_env = env;
		}

		// 廣告清單
		public async Task<IActionResult> Index()
		{
			var ads = await _db.Advertisements.OrderBy(a => a.DisplayOrder).ToListAsync();
			return View(ads);
		}

		// 新增畫面
		public IActionResult Create() => View();

		// 新增處理
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Create(Advertisement ad, IFormFile imageFile)
		{
			if (imageFile != null)
			{
				string fileName = $"{Guid.NewGuid()}_{imageFile.FileName}";
				string path = Path.Combine(_env.WebRootPath, "images/ads", fileName);
				using (var stream = new FileStream(path, FileMode.Create))
				{
					await imageFile.CopyToAsync(stream);
				}
				ad.ImageUrl = $"/images/ads/{fileName}";
			}

			ad.CreatedAt = DateTime.Now;
			ad.UpdatedAt = DateTime.Now;
			_db.Advertisements.Add(ad);
			await _db.SaveChangesAsync();

			TempData["Msg"] = "廣告新增成功！";
			return RedirectToAction(nameof(Index));
		}

		// 編輯
		public async Task<IActionResult> Edit(int id)
		{
			var ad = await _db.Advertisements.FindAsync(id);
			if (ad == null) return NotFound();
			return View(ad);
		}

		// 編輯儲存
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Edit(Advertisement ad, IFormFile? imageFile)
		{
			var dbAd = await _db.Advertisements.FindAsync(ad.AdvertisementID);
			if (dbAd == null) return NotFound();

			if (imageFile != null)
			{
				// 驗證圖片檔案
				var allowedTypes = new[] { "image/jpeg", "image/png", "image/gif" };
				if (!allowedTypes.Contains(imageFile.ContentType))
				{
					TempData["Msg"] = "❌ 圖片格式錯誤，請上傳 JPG / PNG / GIF 檔案。";
					return RedirectToAction(nameof(Edit), new { id = ad.AdvertisementID });
				}

				if (imageFile.Length > 5 * 1024 * 1024) // 5MB 限制
				{
					TempData["Msg"] = "❌ 圖片太大，請上傳小於 5MB 的檔案。";
					return RedirectToAction(nameof(Edit), new { id = ad.AdvertisementID });
				}

				// 儲存圖片
				string fileName = $"{Guid.NewGuid()}_{imageFile.FileName}";
				string path = Path.Combine(_env.WebRootPath, "images/ads", fileName);
				using (var stream = new FileStream(path, FileMode.Create))
				{
					await imageFile.CopyToAsync(stream);
				}
				dbAd.ImageUrl = $"/images/ads/{fileName}";
			}

			// 更新其他欄位
			dbAd.Title = ad.Title;
			dbAd.LinkUrl = ad.LinkUrl;
			dbAd.Position = ad.Position;
			dbAd.DisplayOrder = ad.DisplayOrder;
			dbAd.IsActive = ad.IsActive;
			dbAd.UpdatedAt = DateTime.Now;

			await _db.SaveChangesAsync();

			TempData["Msg"] = "✅ 廣告已成功更新！";
			return RedirectToAction(nameof(Edit), new { id = ad.AdvertisementID });
		}


		// ✅ 刪除
		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> Delete(int id)
		{
			var ad = await _db.Advertisements.FindAsync(id);
			if (ad == null)
			{
				TempData["Msg"] = "❌ 找不到指定的廣告。";
				return RedirectToAction(nameof(Index));
			}

			_db.Advertisements.Remove(ad);
			await _db.SaveChangesAsync();
			TempData["Msg"] = "🗑 廣告已刪除。";
			return RedirectToAction(nameof(Index));
		}

		// ✅ 狀態切換（啟用 / 停用）
		[HttpPost]
		public async Task<IActionResult> ToggleActive(int id)
		{
			var ad = await _db.Advertisements.FindAsync(id);
			if (ad == null)
			{
				TempData["Msg"] = "❌ 找不到廣告。";
				return RedirectToAction(nameof(Index));
			}

			ad.IsActive = !ad.IsActive;
			ad.UpdatedAt = DateTime.Now;
			await _db.SaveChangesAsync();

			TempData["Msg"] = ad.IsActive ? "✅ 已啟用該廣告。" : "⚙️ 已停用該廣告。";
			return RedirectToAction(nameof(Index));
		}


	}
}
