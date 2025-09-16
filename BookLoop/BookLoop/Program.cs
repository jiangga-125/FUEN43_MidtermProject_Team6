using BookLoop.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using BookLoop.Data.Shop;//����ȮɩʫO�d
using BookLoop.Data.Contexts;
using BookLoop.Services;
using BookLoop.Services.Export;
using BookLoop.Services.Reports;



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

			// ReportMail ����Ʈw�]����w�q/�ץX�������^
			builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("ReportMail")
					?? builder.Configuration.GetConnectionString("DefaultConnection")));

			// ����w�]�T�i�ϥΪ���ƨӷ��]��/�q��/�ɾ\�^
			builder.Services.AddDbContext<ShopDbContext>(options =>
				options.UseSqlServer(
					builder.Configuration.GetConnectionString("ShopConnection")
					?? builder.Configuration.GetConnectionString("DefaultConnection")));

            // �v���A��(�����v��)
            builder.Services.AddScoped<IPublisherScopeService, PublisherScopeService>();


            // ����A��
            builder.Services.AddScoped<IReportDataService, ShopReportDataService>();
			builder.Services.AddScoped<ReportQueryBuilder>();


			// �ץX/�H�H
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
