using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ZuvoPetMvcAzure.Controllers
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor actionDescriptor &&
                (actionDescriptor.ControllerName == "Managed" &&
                (actionDescriptor.ActionName == "Login" ||
                 actionDescriptor.ActionName == "Denied" ||
                 actionDescriptor.ActionName == "Landing" ||
                 actionDescriptor.ActionName == "Error")))
            {
                base.OnActionExecuting(context);
                return;
            }

            if (!User.Identity.IsAuthenticated)
            {
                context.Result = new ChallengeResult();
                return;
            }

            var tipoUsuario = User.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(tipoUsuario))
            {
                context.Result = new RedirectToActionResult("Login", "Managed", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
