using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services;
using BookLoop.Services.Export;
using BookLoop.Services.Reports;
using BorrowSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;




namespace BookLoop
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

			builder.Services.AddDbContext<OrdersysContext>(options =>
				options.UseSqlServer(builder.Configuration.GetConnectionString("Ordersys"))); // 新增 OrdersysContext

            builder.Services.AddDbContext<BorrowSystemContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("BorrowSystem"))); // 新增 BorrowSystemContext
            builder.Services.AddScoped<ReservationExpiryService>(); // BorrowSystem 服務
            builder.Services.AddHostedService<ReservationExpiryWorker>(); // BorrowSystem 服務
            builder.Services.AddScoped<ReservationQueueService>(); // BorrowSystem 服務

            // ReportMail 的資料庫（報表定義/匯出紀錄等）
            builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("ReportMail"),
					x => x.MigrationsAssembly(typeof(ReportMailDbContext).Assembly.FullName)));

			// Shop（唯讀查詢層；一定要有 ShopConnection，不回退 DefaultConnection）
			builder.Services.AddDbContext<ShopDbContext>(options =>
			{
				var shopConn = builder.Configuration.GetConnectionString("ShopConnection");
				if (string.IsNullOrWhiteSpace(shopConn))
					throw new InvalidOperationException("缺少連線字串：ShopConnection（請指向合併後的資料庫）。");

				options.UseSqlServer(shopConn);
				options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // 唯讀最佳化
#if DEBUG
				options.EnableDetailedErrors();
				options.EnableSensitiveDataLogging();
#endif
			});


			// 權限服務(報表權限)
			//builder.Services.AddScoped<IPublisherScopeService, PublisherScopeService>();


			// 報表服務
			builder.Services.AddScoped<IReportDataService, ShopReportDataService>();
			builder.Services.AddScoped<ReportQueryBuilder>();


			// 匯出/寄信
			builder.Services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
			builder.Services.AddScoped<MailService>();


			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true).AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddControllersWithViews();

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
			{
				foreach (var key in new[] {
		"Accounts.View","Accounts.Edit",
		"Permissions.Manage",
		"Blacklists.View","Blacklists.Manage",
		"Members.View","Members.Edit"
	})
				{
					options.AddPolicy(key, p => p.RequireClaim("perm", key));
				}
			});

			// ④ 服務註冊
			builder.Services.AddScoped<AuthService>();
			builder.Services.AddScoped<PermissionService>();
			builder.Services.AddScoped<DbInitializer>();



			var app = builder.Build();

			// ⑤ 啟動時資料初始化（可放在 builder.Build() 之後）
			using (var scope = app.Services.CreateScope())
			{
				var init = scope.ServiceProvider.GetRequiredService<DbInitializer>();
				await init.EnsureAdminPasswordAsync("admin@bookstore.local", "Admin@12345!");
				await init.EnsurePermissionAndFeatureSeedAsync("admin@bookstore.local");
			}


			// Configure the HTTP request pipeline.
			if (app.Environment.IsDevelopment())
            {
				app.UseDeveloperExceptionPage();   // ← 新增：讓 500 直接顯示堆疊細節
				app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

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
