using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;                 // ★ 需要這個 using
using 會員.Models;
using 會員.Services.Pricing;
using 會員.Services.Coupons;
using 會員.Services.Points;
using 會員.Services.Orders;
using 會員.Areas.Reviews;
using 會員.Areas.Reviews.Rules;

var builder = WebApplication.CreateBuilder(args);

// DI 註冊
builder.Services.AddControllersWithViews();     // 你同時有 MVC + API，用這個即可（不用再 AddControllers）
builder.Services.AddDbContext<MemberContext>(options =>
	options.UseSqlServer(builder.Configuration.GetConnectionString("Member")));

builder.Services.AddScoped<IReviewRule>(sp =>
{
	var db = sp.GetRequiredService<MemberContext>(); // 取得同一個 scope 的 DbContext
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

// Swagger（產文件 & UI）
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo { Title = "Rules API", Version = "v1" });
	// 若之後加 Bearer Token，可在這裡設定安全性定義
});

var app = builder.Build();

// Middlewares
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();                           // ★ 建議只在開發環境開 Swagger UI
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 如果你目前沒有任何 Authentication 設定，可以先拿掉這兩行避免誤導
// app.UseAuthentication();
app.UseAuthorization();

// 路由（同時支援 API Attribute Routing 與 MVC 傳統路由）
app.MapControllers();                           // ★ 讓 [ApiController]/Attribute 路由生效
app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
