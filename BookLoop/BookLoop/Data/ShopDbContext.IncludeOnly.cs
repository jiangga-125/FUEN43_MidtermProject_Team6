using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using BookLoop.Models;

namespace BookLoop.Data;

public partial class ShopDbContext
{
	// 這份 Context 允許加入模型的實體（白名單）
	private static readonly HashSet<Type> __AllowedEntities = new()
	{
		typeof(Book),
		typeof(BorrowRecord),
		typeof(Category),
		typeof(Listing),
		typeof(Order),
		typeof(OrderDetail),
		typeof(Publisher),
		typeof(Supplier),
		typeof(SupplierUser),
		};

	// Scaffold 產生的主檔最後會呼叫這個 partial；我們在這裡進行「總過濾」
	partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
	{
		// 先把目前已被發現的所有實體抓出來
		var discovered = modelBuilder.Model.GetEntityTypes().ToList();

		foreach (var et in discovered)
		{
			var clr = et.ClrType;
			if (clr == null) continue;

			// 不是白名單 → 一律忽略，避免要求主鍵或生成關聯
			if (!__AllowedEntities.Contains(clr))
			{
				modelBuilder.Ignore(clr);
			}
		}
	}
}
