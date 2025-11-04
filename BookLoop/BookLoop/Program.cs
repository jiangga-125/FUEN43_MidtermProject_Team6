using BookLoop.Areas.Reviews;
using BookLoop.Authorization;
using BookLoop.Data;
using BookLoop.Models;
using BookLoop.Services;
using BookLoop.Services.Coupons;
using BookLoop.Services.Export;
using BookLoop.Services.Import;
using BookLoop.Services.Mail;
using BookLoop.Services.Orders;
using BookLoop.Services.Points;
using BookLoop.Services.Pricing;
using BookLoop.Services.Reports;
using BookLoop.Services.Rules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using BookLoop.Services.Storage;
using Microsoft.Extensions.FileProviders;           // [KEEP]
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OfficeOpenXml;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;

namespace BookLoop
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);
			ExcelPackage.License.SetNonCommercialOrganization("FUEN43 Team6");

			#region context 統一共用 bookloopstr連線字串
			var bookloopStr = builder.Configuration.GetConnectionString("BookLoop")
				?? throw new InvalidOperationException("ConnectionStrings:BookLoop 未設定");

			builder.Services.AddDbContext<ApplicationDbContext>(options =>
				options.UseSqlServer(bookloopStr));

			builder.Services.AddDbContext<OrdersysContext>(options =>
				options.UseSqlServer(bookloopStr));

			builder.Services.AddDbContext<BookSystemContext>(options =>
				options.UseSqlServer(bookloopStr));

			builder.Services.AddDbContext<BorrowContext>(options =>
				options.UseSqlServer(bookloopStr));

			builder.Services.AddDbContext<ReportMailDbContext>(options =>
				options.UseSqlServer(bookloopStr,
					x => x.MigrationsAssembly(typeof(ReportMailDbContext).Assembly.FullName)));

			builder.Services.AddDbContext<ShopDbContext>(options =>
				options.UseSqlServer(bookloopStr));

			builder.Services.AddDbContext<MemberContext>(options =>
				options.UseSqlServer(bookloopStr));

			builder.Services.AddDbContext<AppDbContext>(options =>
				options.UseSqlServer(bookloopStr));
			#endregion

			builder.Services.AddDatabaseDeveloperPageExceptionFilter();

			// DataProtection（避免回收/重啟導致登出）
			builder.Services.AddDataProtection()
				.PersistKeysToFileSystem(new DirectoryInfo(
					Path.Combine(builder.Environment.ContentRootPath, "dpkeys")))
				.SetApplicationName("BookLoop");

			// CORS：使用 Vite(5173)
			builder.Services.AddCors(opts =>
			{
				opts.AddPolicy("DevCors", p => p
					.WithOrigins(
						"http://localhost:5173",
						"http://localhost:5174",
						"http://127.0.0.1:5173",
						"http://127.0.0.1:5174"
					)
					.AllowAnyHeader()
					.AllowAnyMethod()
					.AllowCredentials());
			});

			builder.Services.AddMemoryCache();
			builder.Services.AddHttpContextAccessor();

			// ===== 認證：/api => JWT；其餘 => Cookie =====
			var jwtKey = builder.Configuration["Jwt:Key"]
				?? throw new InvalidOperationException("Jwt:Key 未設定");
			SymmetricSecurityKey signingKey;
			try { signingKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtKey)); }
			catch { signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)); }

			builder.Services.AddAuthentication(options =>
			{
				options.DefaultScheme = "SmartScheme";
				options.DefaultChallengeScheme = "SmartScheme";
			})
			.AddPolicyScheme("SmartScheme", "Smart auth selection", options =>
			{
				options.ForwardDefaultSelector = context =>
				{
					if (context.Request.Path.StartsWithSegments("/api"))
						return JwtBearerDefaults.AuthenticationScheme; // API 一律 JWT
					return CookieAuthenticationDefaults.AuthenticationScheme; // 後台 MVC 用 Cookie
				};
			})
			// 外部登入暫存票證（必要，供 external callback 讀取）
			.AddCookie(Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme, opt =>
			{
				opt.Cookie.Name = "bookloop.external";
				opt.Cookie.HttpOnly = true;
				opt.Cookie.SameSite = SameSiteMode.Lax;
				opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
				opt.ExpireTimeSpan = TimeSpan.FromMinutes(5);
			})
			// 後台 Cookie
			.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, opt =>
			{
				opt.Cookie.Name = "bookloop.auth";
				opt.Cookie.HttpOnly = true;
				opt.Cookie.SameSite = SameSiteMode.None;
				opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
				opt.LoginPath = "/Auth/Login";
				opt.AccessDeniedPath = "/Auth/Denied";
				opt.ExpireTimeSpan = TimeSpan.FromHours(12);
				opt.SlidingExpiration = true;
				opt.Events = new CookieAuthenticationEvents
				{
					OnRedirectToLogin = ctx =>
					{
						if (ctx.Request.Path.StartsWithSegments("/api"))
						{
							ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
							return Task.CompletedTask;
						}
						ctx.Response.Redirect(ctx.RedirectUri);
						return Task.CompletedTask;
					},
					OnRedirectToAccessDenied = ctx =>
					{
						if (ctx.Request.Path.StartsWithSegments("/api"))
						{
							ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
							return Task.CompletedTask;
						}
						ctx.Response.Redirect(ctx.RedirectUri);
						return Task.CompletedTask;
					}
				};
			})
			// JWT（前台 Vue → /api）
			.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
			{
				options.RequireHttpsMetadata = false; // 上線請改 true
				options.SaveToken = false;
				options.TokenValidationParameters = new TokenValidationParameters
				{
					ValidateIssuer = true,
					ValidIssuer = builder.Configuration["Jwt:Issuer"],
					ValidateAudience = true,
					ValidAudience = builder.Configuration["Jwt:Audience"],
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = signingKey,
					ValidateLifetime = true,
					ClockSkew = TimeSpan.FromSeconds(30)
				};
				options.Events = new JwtBearerEvents
				{
					OnChallenge = async ctx =>
					{
						ctx.HandleResponse();
						ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
						await ctx.Response.WriteAsJsonAsync(new { error = "unauthorized" });
					},
					OnForbidden = ctx =>
					{
						ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
						return ctx.Response.WriteAsJsonAsync(new { error = "forbidden" });
					}
				};
			})
			// === 外部登入：Google / Facebook / LINE ===
			.AddGoogle("Google", opt =>
			{
				opt.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
				opt.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
				opt.SaveTokens = true;
				opt.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
			})
			.AddFacebook("Facebook", opt =>
			{
				opt.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
				opt.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
				opt.SaveTokens = true;
				opt.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;
			})
			.AddOAuth("LINE", opt =>
			{
				opt.ClientId = builder.Configuration["Authentication:Line:ClientId"]!;        // ← 對齊 appsettings
				opt.ClientSecret = builder.Configuration["Authentication:Line:ClientSecret"]!;
				opt.AuthorizationEndpoint = "https://access.line.me/oauth2/v2.1/authorize";
				opt.TokenEndpoint = "https://api.line.me/oauth2/v2.1/token";
				opt.UserInformationEndpoint = "https://api.line.me/v2/profile";
				opt.CallbackPath = "/api/auth/external/LINE/callback";
				opt.Scope.Add("profile");
				opt.SaveTokens = true;
				opt.SignInScheme = Microsoft.AspNetCore.Identity.IdentityConstants.ExternalScheme;

				opt.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "userId");
				opt.ClaimActions.MapJsonKey(ClaimTypes.Name, "displayName");
				opt.Events = new OAuthEvents
				{
					OnCreatingTicket = async ctx =>
					{
						using var req = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
						req.Headers.Authorization =
							new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ctx.AccessToken);
						using var res = await ctx.Backchannel.SendAsync(req);
						res.EnsureSuccessStatusCode();
						using var doc = System.Text.Json.JsonDocument.Parse(await res.Content.ReadAsStringAsync());
						var root = doc.RootElement;
						ctx.Identity!.AddClaim(new Claim(ClaimTypes.NameIdentifier, root.GetProperty("userId").GetString()!));
						ctx.Identity!.AddClaim(new Claim(ClaimTypes.Name, root.GetProperty("displayName").GetString()!));
						if (root.TryGetProperty("pictureUrl", out var pic))
							ctx.Identity!.AddClaim(new Claim("picture", pic.GetString()!));
					}
				};
			});

			// 授權：預設要求已登入（API -> JWT；MVC -> Cookie）
			builder.Services.AddAuthorization(options =>
			{
				options.FallbackPolicy = new AuthorizationPolicyBuilder()
					.RequireAuthenticatedUser()
					.Build();

				// 保留你先前新增的可匿名 Policy（目前未直接套用到中介層，但保留不動）
				options.AddPolicy("AllowAnonymousAccess", policy =>
				{
					policy.RequireAssertion(_ => true);
				});
			});

			// 你的其他服務（原樣保留）
			builder.Services.Configure<BookLoop.Services.ImageValidationOptions>(opts =>
			{
				opts.MaxFileBytes = 5 * 1024 * 1024;
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
			builder.Services.AddSingleton<IExcelExporter, EpplusExcelExporter>();
			builder.Services.AddScoped<IMailService, MailService>();
			builder.Services.AddSingleton<ITemplateRenderer, SimpleTemplateRenderer>();
			builder.Services.AddScoped<ITemplateMailer, TemplateMailer>();
            builder.Services.AddSingleton<IFileStorage, R2StorageService>();
            builder.Services.AddScoped<IMailJobRunner, MailJobRunner>();

            builder.Services.AddScoped<ICouponService, CouponService>();
			builder.Services.AddScoped<IPointsService, PointsService>();
			builder.Services.AddScoped<IPricingEngine, PricingEngine>();
			builder.Services.AddScoped<IOrderService, OrderService>();
			builder.Services.AddScoped<IReviewRulePipeline, ReviewRulePipeline>();
			builder.Services.AddScoped<IReviewModerationService, ReviewModerationService>();
			//builder.Services.AddScoped<IReviewRule, ForbiddenKeywordsRule>();
			builder.Services.AddScoped<IReviewRuleProvider, DbReviewRuleProvider>();
			builder.Services.AddScoped<IReviewRule>(sp =>
			{
				var db = sp.GetRequiredService<MemberContext>();
				return new RepeatedContentHintRule((authorMemberId, comment) =>
				{
					var nowUtc = DateTime.UtcNow;
					var text = comment.Trim();
					return db.Reviews.Any(r =>
						r.MemberID == authorMemberId &&
						r.Content == text &&
						r.CreatedAt >= nowUtc.AddHours(-24));
				});
			});

			// 後台 Users 登入用服務（保留）
			builder.Services.AddScoped<AuthService>();

			builder.Services.AddControllersWithViews();
			builder.Services.AddRazorPages();

            // Hangfire（開發期先用記憶體儲存；正式環境可改 SQL Storage）
            builder.Services.AddHangfire(cfg => cfg.UseMemoryStorage());
            builder.Services.AddHangfireServer();

			#endregion

			// ------------------------------
			// 應用程式管線
			// ------------------------------
			var app = builder.Build();

			if (app.Environment.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
				app.UseMigrationsEndPoint();

				// 開發中觀察排程與工作狀態
				app.UseHangfireDashboard("/hangfire");
			}
			else
			{
				app.UseExceptionHandler("/Home/Error");
				app.UseHsts();
			}

                // 啟動時印出實際連到的 DB（幫助你確認連線是否為空或指錯 DB）
                //using (var scope = app.Services.CreateScope())
                //{
                //	var appdb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                //	var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(appdb.Database.GetConnectionString());
                //	Console.WriteLine($"[AppDbContext] Server={csb.DataSource}, Database={csb.InitialCatalog}");

                //	var memdb = scope.ServiceProvider.GetRequiredService<MemberContext>();
                //	var csb2 = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(memdb.Database.GetConnectionString());
                //	Console.WriteLine($"[MemberContext] Server={csb2.DataSource}, Database={csb2.InitialCatalog}");

                //	// 啟動時資料初始化
                //	var init = scope.ServiceProvider.GetRequiredService<DbInitializer>();
                //	await init.EnsureAdminPasswordAsync("admin@bookstore.local", "Admin@12345!");
                //	await init.EnsurePermissionAndFeatureSeedAsync("admin@bookstore.local");
                //}

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

			// 放行 /images/ads 下的所有圖片，不需登入
			app.UseWhen(ctx => ctx.Request.Path.StartsWithSegments("/images/ads"), branch =>
			{
				branch.UseStaticFiles(new StaticFileOptions
				{
					FileProvider = new PhysicalFileProvider(
						Path.Combine(app.Environment.WebRootPath, "images", "ads")),
					RequestPath = "/images/ads",
					ServeUnknownFileTypes = true
				});
			});





			// ================================
			// 靜態檔案（順序極重要）
			// ================================

			// 1️⃣ 先放行廣告圖片：不需登入即可讀取
			//    這段會讓 /images/ads/* 優先被 StaticFileMiddleware 處理
			//    不會再被授權系統攔下導向 /Login?ReturnUrl=...
			app.UseStaticFiles(new StaticFileOptions
			{
				FileProvider = new PhysicalFileProvider(
					Path.Combine(app.Environment.WebRootPath, "images", "ads")),
				RequestPath = "/images/ads",
				ServeUnknownFileTypes = true // 支援 webp / jfif / bmp 等副檔名
			});

			// 2️⃣ 再開啟一般靜態資源服務（wwwroot 下的 CSS、JS、其他圖片）
			app.UseStaticFiles();
			app.UseRouting();

			app.UseCors("DevCors");
			app.UseAuthentication();
			app.UseAuthorization(); // 順序：UseCors -> Authentication -> Authorization

			app.MapControllers(); // 讓路由的 /api/* 運作
								  //app.MapFallbackToFile("index.html"); // 正式上線時 SPA 前端路由回傳 index.html

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
