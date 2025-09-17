using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.OpenApi.Models;                 
using �|��.Models;
using �|��.Services.Pricing;
using �|��.Services.Coupons;
using �|��.Services.Points;
using �|��.Services.Orders;
using �|��.Areas.Reviews;
using �|��.Areas.Reviews.Rules;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
	builder.Configuration.AddUserSecrets<Program>();
}

// DI ���U
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
			r.CreatedAt >= nowUtc.AddHours(-24)); // 24 �p�ɤ����P�˯d���N��������
	});
});


builder.Services.AddScoped<ICouponService, CouponService>();   // �u�f��A��
builder.Services.AddScoped<IPointsService, PointsService>();   // �I�ƪA��
builder.Services.AddScoped<IPricingEngine, PricingEngine>();   // �պ����
builder.Services.AddScoped<IOrderService, OrderService>();     // �U��A�ȡ]�Y�ĥ~���q��i�����^
builder.Services.AddScoped<IReviewRulePipeline, ReviewRulePipeline>();
builder.Services.AddScoped<IReviewModerationService, ReviewModerationService>();
builder.Services.AddScoped<IReviewRule, ForbiddenKeywordsRule>(); // �ιw�]�r��
																  // �ΥΤu�t���J�A�ۭq�M��A new ForbiddenKeywordsRule(list)
builder.Services.AddScoped<IReviewRuleProvider, DbReviewRuleProvider>(); // �q DB Ū�W�h
builder.Services.AddScoped<IReviewRulePipeline>(sp =>
{
	var provider = sp.GetRequiredService<IReviewRuleProvider>();
	return new ReviewRulePipeline(provider.GetRules()); // �C���ШD�� DB �]�w�եX�W�h
});

builder.Services.AddHttpContextAccessor();

// Swagger�]����� & UI�^
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo { Title = "Rules API", Version = "v1" });
	// �Y����[ Bearer Token�A�i�b�o�̳]�w�w���ʩw�q
});

var app = builder.Build();

// Middlewares
if (app.Environment.IsDevelopment())
{
	app.UseSwagger();                           
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();


app.UseAuthorization();

// ���ѡ]�P�ɤ䴩 API Attribute Routing �P MVC �ǲθ��ѡ^
app.MapControllers();                           
app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
