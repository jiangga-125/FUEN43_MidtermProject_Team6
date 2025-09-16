using BookLoop.Data;
using BookLoop.Data.Contexts;
using BookLoop.Data.Shop;//報表暫時性保留
using BookLoop.Ordersys.Models;
using BookLoop.Services;
using BookLoop.Services.Export;
using BookLoop.Services.Reports;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;



namespace BookLoop
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

			builder.Services.AddDbContext<OrdersysContext>(options =>
				options.UseSqlServer(builder.Configuration.GetConnectionString("Ordersys"))); // 新增 OrdersysContext

			// ReportMail 的資料庫（報表定義/匯出紀錄等）
			builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("ReportMail")
					?? builder.Configuration.GetConnectionString("DefaultConnection")));

			// 報表預設三張圖用的資料來源（書/訂單/借閱）
			builder.Services.AddDbContext<ShopDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("ShopConnection")
					?? builder.Configuration.GetConnectionString("DefaultConnection")));

            // 權限服務(報表權限)
            builder.Services.AddScoped<IPublisherScopeService, PublisherScopeService>();


            // 報表服務
            builder.Services.AddScoped<IReportDataService, ShopReportDataService>();
			builder.Services.AddScoped<ReportQueryBuilder>();


			// 匯出/寄信
			builder.Services.AddSingleton<IExcelExporter, ClosedXmlExcelExporter>();
			builder.Services.AddScoped<MailService>();


			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
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
