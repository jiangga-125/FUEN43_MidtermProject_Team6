using BookLoop.Data;
using BookLoop.Ordersys.Models;
using BookLoop.BorrowSystem.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BorrowSystem.Services;

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
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

			builder.Services.AddDbContext<OrdersysContext>(options =>
	            options.UseSqlServer(builder.Configuration.GetConnectionString("Ordersys"))); // 新增

            builder.Services.AddDbContext<BorrowSystemContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("BorrowSystem"))); // 新增
            builder.Services.AddScoped<ReservationExpiryService>(); // BorrowSystem
            builder.Services.AddHostedService<ReservationExpiryWorker>(); // BorrowSystem
            builder.Services.AddScoped<ReservationQueueService>(); // BorrowSystem

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
