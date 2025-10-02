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
			// Identity/Razor 預設用的
			builder.Services.AddDbContext<ApplicationDbContext>(options =>
				options.UseSqlServer(defaultConn ?? appDbConn));

			builder.Services.AddDbContext<OrdersysContext>(options =>
<<<<<<< HEAD
				options.UseSqlServer(bookLoopConn ?? appDbConn));

			builder.Services.AddDbContext<BookSystemContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn));

=======
<<<<<<< HEAD
				options.UseSqlServer(builder.Configuration.GetConnectionString("BookLoop"))); // 新增 OrdersysContext

			builder.Services.AddDbContext<BookSystemContext>(options =>
				options.UseSqlServer(builder.Configuration.GetConnectionString("BookLoop"))); // 新增 BookSystem
			builder.Services.AddDbContext<BorrowSystemContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("BookLoop"))); // 新增 BorrowSystemContext
            builder.Services.AddScoped<ReservationExpiryService>(); // BorrowSystem 服務
            builder.Services.AddHostedService<ReservationExpiryWorker>(); // BorrowSystem 服務
            builder.Services.AddScoped<ReservationQueueService>(); // BorrowSystem 服務

            // ReportMail 的資料庫（報表定義/匯出紀錄等）
            builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("BookLoop"),
=======
				options.UseSqlServer(bookLoopConn ?? appDbConn));

			builder.Services.AddDbContext<BookSystemContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn));

>>>>>>> dev
			builder.Services.AddDbContext<BorrowSystemContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn));

			builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(bookLoopConn ?? appDbConn,
<<<<<<< HEAD
					x => x.MigrationsAssembly(typeof(ReportMailDbContext).Assembly.FullName)));

			builder.Services.AddDbContext<ShopDbContext>(options =>
				options.UseSqlServer(bookLoopConn ?? defaultConn ?? appDbConn));
=======
>>>>>>> RMupload
					x => x.MigrationsAssembly(typeof(ReportMailDbContext).Assembly.FullName)));

			builder.Services.AddDbContext<ShopDbContext>(options =>
<<<<<<< HEAD
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("BookLoop")
					?? builder.Configuration.GetConnectionString("DefaultConnection")));
=======
				options.UseSqlServer(bookLoopConn ?? defaultConn ?? appDbConn));
>>>>>>> RMupload
>>>>>>> dev

			builder.Services.AddDbContext<MemberContext>(options =>
				options.UseSqlServer(memberConn));

			// ★ 這裡修正：AppDbContext 改用 BookLoop→Default 回退，不要用不存在的 "BookLoop1" key
			builder.Services.AddDbContext<AppDbContext>(opt =>
				opt.UseSqlServer(appDbConn));

			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

			// ------------------------------
			// 驗證與授權
			// ------------------------------
			builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
				.AddCookie(opt =>
				{
					opt.LoginPath = "/Account/Auth/Login";
					opt.AccessDeniedPath = "/Account/Auth/Denied";
					opt.ExpireTimeSpan = TimeSpan.FromHours(8);
					opt.SlidingExpiration = true;
				});

			builder.Services.AddAuthorization(options =>
			{
				foreach (var key in new[]
				{
					"Accounts.View","Accounts.Edit",
					"Permissions.Manage",
					"Blacklists.View","Blacklists.Manage",
					"Members.View","Members.Edit"
				})
				{
					options.AddPolicy(key, p => p.RequireClaim("perm", key));
				}
			});

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

<<<<<<< HEAD
=======
<<<<<<< HEAD
			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddControllersWithViews();

			// 會員模組

			if (builder.Environment.IsDevelopment())
			{
				builder.Configuration.AddUserSecrets<Program>();
			}
			// DI 註冊
			builder.Services.AddControllersWithViews();
			builder.Services.AddDbContext<MemberContext>(options =>
				options.UseSqlServer(builder.Configuration.GetConnectionString("Member")));

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
						r.CreatedAt >= nowUtc.AddHours(-24)); // 24 小時內有同樣留言就視為重複
				});
			});


			builder.Services.AddScoped<ICouponService, CouponService>();   // 優惠券服務
			builder.Services.AddScoped<IPointsService, PointsService>();   // 點數服務
			builder.Services.AddScoped<IPricingEngine, PricingEngine>();   // 試算引擎
			builder.Services.AddScoped<IOrderService, OrderService>();     // 下單服務（若採外部訂單可不必）
			builder.Services.AddScoped<IReviewRulePipeline, ReviewRulePipeline>();
			builder.Services.AddScoped<IReviewModerationService, ReviewModerationService>();
			builder.Services.AddScoped<IReviewRule, ForbiddenKeywordsRule>(); // 用預設字詞
																			  // 或用工廠載入你自訂清單再 new ForbiddenKeywordsRule(list)
			builder.Services.AddScoped<IReviewRuleProvider, DbReviewRuleProvider>(); // 從 DB 讀規則
			builder.Services.AddScoped<IReviewRulePipeline>(sp =>
			{
				var provider = sp.GetRequiredService<IReviewRuleProvider>();
				return new ReviewRulePipeline(provider.GetRules()); // 每次請求依 DB 設定組出規則
			});

			builder.Services.AddHttpContextAccessor();

			// ① DbContext 註冊
			builder.Services.AddDbContext<AppDbContext>(opt =>
				opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

			// ② Cookie 驗證（沒用 Identity 時）
			builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
				.AddCookie(opt =>
				{
					opt.LoginPath = "/Auth/Login";        // 若你的登入頁在外面，這樣即可
					opt.AccessDeniedPath = "/Auth/Denied";
					opt.ExpireTimeSpan = TimeSpan.FromHours(8);
					opt.SlidingExpiration = true;
				});

			// ③ 授權政策（依你的權限鍵）
			builder.Services.AddAuthorization(options =>
=======
>>>>>>> dev
			builder.Services.AddScoped<IReviewRulePipeline, ReviewRulePipeline>();
			builder.Services.AddScoped<IReviewModerationService, ReviewModerationService>();
			builder.Services.AddScoped<IReviewRule, ForbiddenKeywordsRule>();
			builder.Services.AddScoped<IReviewRuleProvider, DbReviewRuleProvider>();
			builder.Services.AddScoped<IReviewRule>(sp =>
<<<<<<< HEAD
=======
>>>>>>> RMupload
>>>>>>> dev
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
