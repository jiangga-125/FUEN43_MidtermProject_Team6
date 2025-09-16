// Services/Security/PublisherScopeService.cs
using System.Security.Claims;
using System.Linq;

public interface IPublisherScopeService
{
    bool IsAdminOrMarketing(ClaimsPrincipal u);
    bool IsPublisher(ClaimsPrincipal u);
    int[] GetPublisherIds(ClaimsPrincipal u); // 沒設定就回空陣列＝不限制
}

public class PublisherScopeService : IPublisherScopeService
{
    public bool IsAdminOrMarketing(ClaimsPrincipal u) =>
        u.IsInRole("Admin") || u.IsInRole("管理員") ||
        u.IsInRole("Marketing") || u.IsInRole("行銷員工");

    public bool IsPublisher(ClaimsPrincipal u) =>
        u.IsInRole("Publisher") || u.IsInRole("書商");

    public int[] GetPublisherIds(ClaimsPrincipal u)
    {
        var raw = u.FindFirst("publisher_ids")?.Value
                ?? u.FindFirst("publisher_id")?.Value
                ?? "";
        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                  .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
                  .Where(n => n.HasValue).Select(n => n!.Value)
                  .Distinct().ToArray();
    }
}
