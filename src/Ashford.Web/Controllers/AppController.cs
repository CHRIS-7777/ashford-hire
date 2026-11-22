using Ashford.Web.Infrastructure;
using Ashford.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ashford.Web.Controllers;

public abstract class AppController : Controller
{
    protected User Me => HttpContext.CurrentUser();
    protected AppSession AppSession => HttpContext.AppSession();

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ViewData["Me"] = Me;
        base.OnActionExecuting(context);
    }

    protected IActionResult NeedLogin()
    {
        var back = Request.Path + Request.QueryString;
        return Redirect("/signin?returnUrl=" + Uri.EscapeDataString(back));
    }

    protected IActionResult Forbidden()
    {
        Response.StatusCode = 403;
        return View("Forbidden");
    }

    protected ContentResult JsonOut(object o, int status = 200, bool indented = false)
    {
        Response.StatusCode = status;
        return Content(J.Serialize(o, indented), "application/json; charset=utf-8");
    }

    protected void SignIn(User u)
    {
        var s = AppSession;
        if (s != null) s.UserId = u.Id;
        PricingCookie.Write(Response, u);
    }
}
