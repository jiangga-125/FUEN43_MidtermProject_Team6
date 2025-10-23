using BookLoop.Areas.Reviews;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services;
using BookLoop.Services.Coupons;
using BookLoop.Services.Export;
using BookLoop.Services.Import;
using BookLoop.Services.Orders;
using BookLoop.Services.Points;
using BookLoop.Services.Pricing;
using BookLoop.Services.Reports;
using BookLoop.Services.Rules;
using BorrowSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using BookLoop.Authorization;
using Microsoft.AspNetCore.DataProtection;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace BookLoop
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// ------------------------------
			// 連線字串讀取（先 BookLoop，退回 Default）
			// ------------------------------
			string? defaultConn = builder.Configuration.GetConnectionString("DefaultConnection");
			string? bookLoopConn = builder.Configuration.GetConnectionString("BookLoop");
			string? appDbConn = !string.IsNullOrWhiteSpace(bookLoopConn) ? bookLoopConn :
								  !string.IsNullOrWhiteSpace(defaultConn) ? defaultConn : null;

			if (string.IsNullOrWhiteSpace(appDbConn))
				throw new InvalidOperationException("ConnectionStrings:BookLoop 或 DefaultConnection 未設定。");

			// 若你有 Member 模組，Member 沒設定就回退用 appDbConn
			string? memberConn = builder.Configuration.GetConnectionString("Member");
			if (string.IsNullOrWhiteSpace(memberConn)) memberConn = appDbConn;

			// ------------------------------
			// 資料庫註冊
			// ------------------------------
			builder.Services.AddDbContext<ApplicationDbContext>(options =>
				options.UseSqlServer(defaultConn ?? appDbConn));

			builder.Services.AddDbContext<OrdersysContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn));

			builder.Services.AddDbContext<BookSystemContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn));

			builder.Services.AddDbContext<BorrowSystemContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn));

			builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn,
					x => x.MigrationsAssembly(typeof(ReportMailDbContext).Assembly.FullName)));

			builder.Services.AddDbContext<ShopDbContext>(options =>
				options.UseSqlServer(bookLoopConn ?? defaultConn ?? appDbConn));

			builder.Services.AddDbContext<MemberContext>(options =>
				options.UseSqlServer(memberConn));

			builder.Services.AddDbContext<AppDbContext>(opt =>
				opt.UseSqlServer(appDbConn));

			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

			// ------------------------------
			// Data Protection 金鑰持久化（避免回收/重啟導致登出）
			// ------------------------------
			builder.Services.AddDataProtection()
				.PersistKeysToFileSystem(new DirectoryInfo(
					Path.Combine(builder.Environment.ContentRootPath, "dpkeys")))
				.SetApplicationName("BookLoop");

			// ------------------------------
			// 驗證與授權
			// ------------------------------
			builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
				.AddCookie(opt =>
				{
					opt.Cookie.Name = "bookloop.auth";
					opt.Cookie.HttpOnly = true;
					opt.Cookie.SameSite = SameSiteMode.Lax;
					opt.LoginPath = "/Auth/Login";
					opt.AccessDeniedPath = "/Auth/Denied";

					// 存活 + 自動延展
					opt.ExpireTimeSpan = TimeSpan.FromHours(12);
					opt.SlidingExpiration = true;

				});

			// --- 授權：全站預設要登入（未標 AllowAnonymous 的頁面） ---
			builder.Services.AddAuthorization(options =>
			{
				options.FallbackPolicy = new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser()
					.Build();
			});

			// --- 動態 Policy Provider + 授權處理器（用 permkey + DB/快取展開 feature） ---
			builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
			builder.Services.AddMemoryCache();
			builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

			// ------------------------------
			// 服務註冊
			// ------------------------------
			builder.Services.Configure<BookLoop.Services.ImageValidationOptions>(opts =>
			{
				opts.MaxFileBytes = 5 * 1024 * 1024; // 5MB
				opts.PermittedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
			});

			builder.Services.AddScoped<ImportCategoryDto>();
			builder.Services.AddScoped<BookService>();
			builder.Services.AddHttpClient<IImageValidator, ImageValidator>();

			builder.Services.AddScoped<IReportDataService, ShopReportDataService>();
			builder.Services.AddScoped<ReportQueryBuilder>();
			builder.Services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
			builder.Services.AddScoped<MailService>();

			builder.Services.AddScoped<ICouponService, CouponService>();
			builder.Services.AddScoped<IPointsService, PointsService>();
			builder.Services.AddScoped<IPricingEngine, PricingEngine>();
			builder.Services.AddScoped<IOrderService, OrderService>();

			builder.Services.AddScoped<IReviewRulePipeline, ReviewRulePipeline>();
			builder.Services.AddScoped<IReviewModerationService, ReviewModerationService>();
			builder.Services.AddScoped<IReviewRule, ForbiddenKeywordsRule>();
			builder.Services.AddScoped<IReviewRuleProvider, DbReviewRuleProvider>();
			builder.Services.AddScoped<IReviewRule>(sp =>
			{
				var db = sp.GetRequiredService<MemberContext>();
				return new RepeatedContentHintRule((authorMemberId, comment) =>
				{
					var nowUtc = DateTime.UtcNow;
					var text = comment.Trim();
					return db.Reviews.Any(r =>
						r.MemberId == authorMemberId &&
						r.Content == text &&
						r.CreatedAt >= nowUtc.AddHours(-24));
				});
			});

			builder.Services.AddHttpContextAccessor();

			// BorrowSystem 背景服務
			builder.Services.AddScoped<ReservationExpiryService>();
			builder.Services.AddHostedService<ReservationExpiryWorker>();
			builder.Services.AddScoped<ReservationQueueService>();

			builder.Services.AddScoped<AuthService>();
			builder.Services.AddScoped<PermissionService>();
			builder.Services.AddScoped<DbInitializer>();

			// MVC & Razor Pages
			builder.Services.AddControllersWithViews();
			builder.Services.AddRazorPages();

			// ------------------------------
			// 應用程式管線
			// ------------------------------
			var app = builder.Build();

			// 啟動時印出實際連到的 DB（幫助你確認連線是否為空或指錯 DB）
			using (var scope = app.Services.CreateScope())
			{
				var appdb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
				var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(appdb.Database.GetConnectionString());
				Console.WriteLine($"[AppDbContext] Server={csb.DataSource}, Database={csb.InitialCatalog}");

				var memdb = scope.ServiceProvider.GetRequiredService<MemberContext>();
				var csb2 = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(memdb.Database.GetConnectionString());
				Console.WriteLine($"[MemberContext] Server={csb2.DataSource}, Database={csb2.InitialCatalog}");

				// 啟動時資料初始化
				var init = scope.ServiceProvider.GetRequiredService<DbInitializer>();
				await init.EnsureAdminPasswordAsync("admin@bookstore.local", "Admin@12345!");
				await init.EnsurePermissionAndFeatureSeedAsync("admin@bookstore.local");
			}

			if (app.Environment.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseMigrationsEndPoint();
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
				app.UseHsts();
			}

			app.UseHttpsRedirection();
			app.UseStaticFiles();

			app.UseRouting();

			// 重要順序：Authentication -> Authorization
			app.UseAuthentication();
			app.UseAuthorization();

			app.MapControllerRoute(
				name: "areas",
				pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

			app.MapControllerRoute(
				name: "default",
				pattern: "{controller=Home}/{action=Index}/{id?}");

			app.MapRazorPages();

			app.Run();
		}
	}
}
