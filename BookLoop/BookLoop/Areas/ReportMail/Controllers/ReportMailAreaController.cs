using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ReportMail.Areas.ReportMail.Controllers
{
    [Area("ReportMail")]
    [Authorize(Policy = "ReportMail.Access")] // 統一門票
    public abstract class ReportMailAreaController : Controller { }
}
