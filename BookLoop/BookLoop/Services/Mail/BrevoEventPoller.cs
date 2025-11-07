using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BookLoop.Data;
using BookLoop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class BrevoEventPoller : BackgroundService
{
	private readonly ILogger<BrevoEventPoller> _logger;
	private readonly IConfiguration _cfg;
	private readonly IServiceProvider _sp;

	public BrevoEventPoller(ILogger<BrevoEventPoller> logger, IConfiguration cfg, IServiceProvider sp)
	{
		_logger = logger;
		_cfg = cfg;
		_sp = sp;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var minutes = _cfg.GetValue<int?>("Brevo:PollMinutes") ?? 5;
		var lookback = _cfg.GetValue<int?>("Brevo:LookbackMinutesOnStart") ?? 30;
		var apiKey = _cfg["Brevo:ApiKey"];

		if (string.IsNullOrWhiteSpace(apiKey))
		{
			_logger.LogWarning("BrevoEventPoller disabled: Brevo:ApiKey not set.");
			return;
		}

		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using var scope = _sp.CreateScope();
				var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

				// DBG: 確認連到哪個 DB
				try
				{
					var cnn = db.Database.GetDbConnection();
					_logger.LogInformation("[DBG] DB={Db} DataSource={Src}", cnn.Database, cnn.DataSource);
				}
				catch { /* ignore */ }

				// 以「本地時間」作為游標
				var cursor = await db.IntegrationCursors.AsNoTracking()
					.FirstOrDefaultAsync(x => x.CursorKey == "Brevo:Events", stoppingToken);

				var startLocal = cursor?.CursorTime ?? DateTime.Now.AddMinutes(-lookback);
				var endLocal = DateTime.Now;

				// Brevo 只吃日期字串 → endDate +1 天避免跨日漏抓
				var startDate = startLocal.ToString("yyyy-MM-dd");
				var endDate = DateTime.Now.ToString("yyyy-MM-dd");

				// 事件名稱：opened / clicks
				var eventsToFetch = new[] { "opened", "clicks" };

				using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
				http.DefaultRequestHeaders.Add("api-key", apiKey);

				var baseUrl = "https://api.brevo.com/v3/smtp/statistics/events";

				var limit = 50;
				var totalProcessed = 0;
				var successThisRound = false;

				foreach (var ev in eventsToFetch)
				{
					var offset = 0;
					var pageIdx = 0;

					while (!stoppingToken.IsCancellationRequested)
					{
						var url = $"{baseUrl}?event={ev}&startDate={startDate}&endDate={endDate}&limit={limit}&offset={offset}&sort=asc";

						HttpResponseMessage resp = null!;
						for (int attempt = 0; attempt < 4; attempt++)
						{
							try
							{
								resp = await http.GetAsync(url, stoppingToken);
								var code = (int)resp.StatusCode;

								if (code == 429 || code >= 500)
								{
									var backoffMs = (int)(Math.Pow(2, attempt) * 600) + Random.Shared.Next(0, 400);
									_logger.LogWarning("Brevo events temp failure: {Status}. retry in {Delay}ms. Url={Url}",
										resp.StatusCode, backoffMs, url);
									await Task.Delay(backoffMs, stoppingToken);
									continue;
								}

								if (!resp.IsSuccessStatusCode)
								{
									var body = await resp.Content.ReadAsStringAsync(stoppingToken);
									_logger.LogError("Brevo events {Event} request failed {Status}. Url={Url}. Body={Body}",
										ev, resp.StatusCode, url, body);
								}

								resp.EnsureSuccessStatusCode();
								break;
							}
							catch (TaskCanceledException) when (!stoppingToken.IsCancellationRequested)
							{
								var backoffMs = (int)(Math.Pow(2, attempt) * 600) + Random.Shared.Next(0, 400);
								_logger.LogWarning("Brevo events timeout. retry in {Delay}ms. Url={Url}", backoffMs, url);
								await Task.Delay(backoffMs, stoppingToken);
							}
						}

						if (resp is null || !resp.IsSuccessStatusCode)
						{
							_logger.LogWarning("Brevo events fetch failed after retries. Url={Url}", url);
							break; // 不推進游標
						}

						var json = await resp.Content.ReadAsStringAsync(stoppingToken);
						using var doc = JsonDocument.Parse(json);
						var root = doc.RootElement;

						var arr = root.TryGetProperty("events", out var je) && je.ValueKind == JsonValueKind.Array
							? je.EnumerateArray().ToArray()
							: Array.Empty<JsonElement>();

						if (pageIdx == 0)
						{
							var samples = arr.Take(3).Select(e => new
							{
								eventType = e.TryGetProperty("event", out var ee) ? ee.GetString() : null,
								date = e.TryGetProperty("date", out var d) ? d.GetString() : null,
								emailHint = e.TryGetProperty("email", out var em) ? MaskEmail(em.GetString()) : null,
								msgIdRaw = e.TryGetProperty("messageId", out var mid) ? mid.GetString()
										  : (e.TryGetProperty("message-id", out var mid2) ? mid2.GetString() : null),
								msgIdNorm = NormalizeMsgId(e.TryGetProperty("messageId", out var mid3) ? mid3.GetString()
										  : (e.TryGetProperty("message-id", out var mid4) ? mid4.GetString() : null)),
								url = e.TryGetProperty("url", out var u) ? u.GetString() : null
							}).ToArray();
							_logger.LogInformation("[DBG] Brevo sample events: {Samples}", JsonSerializer.Serialize(samples));
						}

						int pageFetched = 0, matchedLog = 0, missLog = 0, missJr = 0, dup = 0, inserted = 0;

						foreach (var e in arr)
						{
							pageFetched++;

							var evt = e.TryGetProperty("event", out var evJ) ? evJ.GetString() : null;
							if (evt is null) continue;

							var dateStr = e.TryGetProperty("date", out var dJ) ? dJ.GetString() : null;
							var createdLocal = ParseBrevoDate(dateStr);

							// 本地端二次過濾：只處理「大於游標」的事件
							if (createdLocal <= startLocal) continue;

							var email = e.TryGetProperty("email", out var emJ) ? emJ.GetString() : null;

							string? messageId = null;
							if (e.TryGetProperty("messageId", out var midJ)) messageId = midJ.GetString();
							else if (e.TryGetProperty("message-id", out var mid2J)) messageId = mid2J.GetString();
							var normMid = NormalizeMsgId(messageId);

							var urlClicked = e.TryGetProperty("url", out var urlJ) ? urlJ.GetString() : null;
							var ua = e.TryGetProperty("userAgent", out var uaJ) ? uaJ.GetString() : null;
							var ip = e.TryGetProperty("ip", out var ipJ) ? ipJ.GetString() : null;

							// 強化匹配策略
							MailSendLog? log = null;

							// a) 先以 ProviderMsgId（正規化）嘗試
							if (!string.IsNullOrWhiteSpace(normMid))
							{
								var candidates = await db.MailSendLogs.AsNoTracking()
									.Where(x => x.SentAt >= createdLocal.AddDays(-3) && x.SentAt <= createdLocal.AddDays(1))
									.OrderByDescending(x => x.SentAt)
									.Take(200)
									.ToListAsync(stoppingToken);

								log = candidates.FirstOrDefault(x => NormalizeMsgId(x.ProviderMsgId) == normMid);
							}

							// b) 再以 Email + 時間窗（-72h ~ +24h），取最接近 createdLocal 的一封
							if (log == null && !string.IsNullOrWhiteSpace(email))
							{
								var min = createdLocal.AddHours(-72);
								var max = createdLocal.AddHours(+24);

								var list = await db.MailSendLogs.AsNoTracking()
									.Where(x => x.Recipient == email && x.SentAt >= min && x.SentAt <= max)
									.OrderByDescending(x => x.SentAt)
									.Take(300)
									.ToListAsync(stoppingToken);

								log = list
									.OrderBy(x => Math.Abs((x.SentAt - createdLocal).TotalSeconds))
									.FirstOrDefault();
							}

							// c) 最後保底：Email 最近一封
							if (log == null && !string.IsNullOrWhiteSpace(email))
							{
								log = await db.MailSendLogs.AsNoTracking()
									.Where(x => x.Recipient == email)
									.OrderByDescending(x => x.SentAt)
									.FirstOrDefaultAsync(stoppingToken);
							}

							if (log == null)
							{
								missLog++;
								_logger.LogWarning("[Brevo] MissLog evt={Evt} email={Email} date={Date} msgId={MsgId}",
									evt, email, createdLocal, messageId);
								continue;
							}

							matchedLog++;

							if (log.JobRecipientId == null)
							{
								missJr++;
								_logger.LogWarning("[Brevo] Log found but JobRecipientId is null. logId={LogId} email={Email} sentAt={SentAt}",
									log.LogId, log.Recipient, log.SentAt);
								continue;
							}

							var jr = await db.MailJobRecipients
								.FirstOrDefaultAsync(x => x.MailJobRecipientId == log.JobRecipientId.Value, stoppingToken);
							if (jr == null)
							{
								missJr++;
								_logger.LogWarning("[Brevo] JR not found. jobRecipientId={JRId} logId={LogId}", log.JobRecipientId, log.LogId);
								continue;
							}

							bool isOpen = evt.Equals("opened", StringComparison.OrdinalIgnoreCase) || evt.Equals("open", StringComparison.OrdinalIgnoreCase);
							bool isClick = evt.Equals("clicks", StringComparison.OrdinalIgnoreCase) || evt.Equals("click", StringComparison.OrdinalIgnoreCase);

							var newEvent = new MailEvent
							{
								MailJobId = jr.MailJobId,
								JobRecipientId = jr.MailJobRecipientId,
								LogId = log.LogId,
								EventType = isClick ? "Click" : "Open",
								Url = isClick ? urlClicked : null,
								UserAgent = ua,
								Ip = ip,
								CreatedAt = createdLocal
							};

							// 去重：JR + Type + Url + (CreatedAt ±2s)
							var createdMin = createdLocal.AddSeconds(-2);
							var createdMax = createdLocal.AddSeconds(+2);

							var isDup = await db.MailEvents.AnyAsync(x =>
								x.JobRecipientId == newEvent.JobRecipientId &&
								x.EventType == newEvent.EventType &&
								(newEvent.Url == null || x.Url == newEvent.Url) &&
								x.CreatedAt >= createdMin && x.CreatedAt <= createdMax, stoppingToken);

							if (isDup) { dup++; continue; }

							await db.MailEvents.AddAsync(newEvent, stoppingToken);

							if (isOpen)
							{
								if (jr.OpenCount == 0) jr.OpenedAt = createdLocal;
								jr.OpenCount += 1;
							}
							if (isClick)
							{
								jr.ClickCount += 1;
								jr.LastClickAt = createdLocal;
							}

							inserted++;
							totalProcessed++;
						}

						await db.SaveChangesAsync(stoppingToken);

						_logger.LogInformation("[DBG] pageSummary ev={Event} fetched={Fetched} matchedLog={Matched} missLog={MissLog} missJR={MissJR} dup={Dup} inserted={Inserted}",
							ev, pageFetched, matchedLog, missLog, missJr, dup, inserted);

						if (inserted > 0) successThisRound = true; // 只要本頁有寫入就算成功
						if (arr.Length < limit) break; // 不滿頁 → 這個事件抓完
						offset += limit;
						pageIdx++;
					}
				}

				// 成功才推進游標（本地時間）
				if (successThisRound)
				{
					var row = await db.IntegrationCursors.FirstOrDefaultAsync(x => x.CursorKey == "Brevo:Events", stoppingToken);
					if (row == null)
						db.IntegrationCursors.Add(new IntegrationCursor { CursorKey = "Brevo:Events", CursorTime = endLocal });
					else
						row.CursorTime = endLocal;

					await db.SaveChangesAsync(stoppingToken);
				}
				else
				{
					_logger.LogWarning("Skip cursor advance due to fetch failure or no inserts.");
				}

				_logger.LogInformation("Brevo poll OK. {Start} -> {End}, eventsWritten={N}", startLocal, endLocal, totalProcessed);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "BrevoEventPoller failed");
			}

			await Task.Delay(TimeSpan.FromMinutes(minutes), stoppingToken);
		}
	}

	// 解析 Brevo 的日期字串：支援 ISO8601 與 "yyyy-MM-dd HH:mm:ss"
	private static DateTime ParseBrevoDate(string? s)
	{
		if (string.IsNullOrWhiteSpace(s)) return DateTime.Now;
		if (DateTime.TryParse(s, out var dt))
			return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
		if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm:ss", null,
			System.Globalization.DateTimeStyles.AssumeLocal, out dt))
			return dt;
		return DateTime.Now;
	}

	private static string? NormalizeMsgId(string? s)
	{
		if (string.IsNullOrWhiteSpace(s)) return null;
		s = s.Trim().Trim('<', '>', ' ', '\t', '\r', '\n');
		return s.ToLowerInvariant();
	}

	private static string? MaskEmail(string? email)
	{
		if (string.IsNullOrEmpty(email)) return email;
		var at = email.IndexOf('@');
		if (at <= 1) return "***" + email;
		return email.Substring(0, 1) + "***" + email.Substring(at);
	}
}
