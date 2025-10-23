using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Account.Controllers;

[Area("Account")]
[Authorize(Policy = "Account.Access")] // ← 門票
public abstract class AccountAreaController : Controller { }