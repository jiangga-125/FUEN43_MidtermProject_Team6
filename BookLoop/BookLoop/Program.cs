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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;

namespace BookLoop
{
	public class Program
	{
		public static async Task Main(string[] args)
		{
			var builder = WebApplication.CreateBuilder(args);

			// ===== 連線字串 =====
			var bookloopStr = builder.Configuration.GetConnectionString("BookLoop")
				?? throw new InvalidOperationException("ConnectionStrings:BookLoop 未設定");

			// 主要 DbContext（你的專案多 Context：保留）
			builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlServer(bookloopStr));
			builder.Services.AddDbContext<ApplicationDbContext>(opt => opt.UseSqlServer(bookloopStr));
			builder.Services.AddDbContext<OrdersysContext>(opt => opt.UseSqlServer(bookloopStr));
			builder.Services.AddDbContext<BookSystemContext>(opt => opt.UseSqlServer(bookloopStr));
			builder.Services.AddDbContext<BorrowContext>(opt => opt.UseSqlServer(bookloopStr));
			builder.Services.AddDbContext<ReportMailDbContext>(opt =>
				opt.UseSqlServer(bookloopStr, x => x.MigrationsAssembly(typeof(ReportMailDbContext).Assembly.FullName)));
			builder.Services.AddDbContext<ShopDbContext>(opt => opt.UseSqlServer(bookloopStr));
			builder.Services.AddDbContext<MemberContext>(opt => opt.UseSqlServer(bookloopStr));

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

			// 後台 Users 登入用服務（保留）
			builder.Services.AddScoped<AuthService>();

			builder.Services.AddControllersWithViews();
			builder.Services.AddRazorPages();

			var app = builder.Build();

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

			app.UseCors("DevCors");
			app.UseAuthentication();
			app.UseAuthorization();

			app.MapControllers();

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
