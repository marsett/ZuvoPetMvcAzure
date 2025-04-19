using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ZuvoPetMvcAzure.Filters
{
    public class AjaxAuthenticationAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                // Verificar si el usuario está autenticado utilizando claims
                var user = filterContext.HttpContext.User;

                // Verificar si el usuario está autenticado y tiene el claim de rol
                if (!user.Identity.IsAuthenticated || user.FindFirstValue(ClaimTypes.Role) == null)
                {
                    filterContext.Result = new JsonResult(new
                    {
                        sessionExpired = true,
                        redirectUrl = "/Managed/Login" // Ajusta esta ruta según tu configuración
                    });
                }
            }
            base.OnActionExecuting(filterContext);
        }
    }
}
