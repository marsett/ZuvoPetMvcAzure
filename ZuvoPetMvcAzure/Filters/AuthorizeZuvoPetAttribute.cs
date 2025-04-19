using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Security.Claims;

namespace ZuvoPetMvcAzure.Filters
{
    public class AuthorizeZuvoPetMvcAzureAttribute : AuthorizeAttribute, IAuthorizationFilter
    {
        private readonly string[] _tiposUsuario;

        public AuthorizeZuvoPetMvcAzureAttribute(params string[] tiposUsuario)
        {
            _tiposUsuario = tiposUsuario;
        }
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var user = context.HttpContext.User;
            // Guardar información de la ruta actual para redirección después del login
            string controller = context.RouteData.Values["controller"].ToString();
            string action = context.RouteData.Values["action"].ToString();
            var id = context.RouteData.Values["id"];

            // Manejar el caso especial para páginas de Landing y Login
            if (controller == "Managed" && user.Identity.IsAuthenticated)
            {
                // Si el usuario ya está autenticado y trata de acceder a estas páginas,
                // redirigirlo a la página principal según su rol
                string userRole = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

                if (userRole == "Adoptante")
                {
                    context.Result = GetRoute("Adoptante", "Index");
                    return;
                }
                else if (userRole == "Refugio")
                {
                    context.Result = GetRoute("Refugio", "Index");
                    return;
                }
            }

            // Caso especial para Login: permitir acceso a usuarios NO autenticados
            if (controller == "Managed" && action == "Login" && !user.Identity.IsAuthenticated)
            {
                // Permitir acceso - simplemente retornar sin redirigir
                return;
            }


            // Obtener TempData para guardar la información de redirección
            ITempDataProvider provider = context.HttpContext.RequestServices.GetService<ITempDataProvider>();
            var TempData = provider.LoadTempData(context.HttpContext);

            TempData["controller"] = controller;
            TempData["action"] = action;

            if (id != null)
            {
                TempData["id"] = id.ToString();
            }
            else
            {
                TempData.Remove("id");
            }

            provider.SaveTempData(context.HttpContext, TempData);

            // Verificar si el usuario está autenticado
            if (!user.Identity.IsAuthenticated)
            {
                context.Result = GetRoute("Managed", "Login");
                return;
            }

            // Si se especificaron tipos de usuario, verificar que el usuario tenga uno de los roles requeridos
            if (_tiposUsuario.Length > 0)
            {
                string userRole = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

                if (userRole == null || !_tiposUsuario.Contains(userRole))
                {
                    // El usuario no tiene el rol requerido, redirigir a acceso denegado
                    context.Result = GetRoute("Managed", "Denied");
                    return;
                }
            }

            // Verificar si existen claims específicos para ciertas acciones
            if (RequiresAdminAccess(controller, action))
            {
                if (!user.HasClaim(c => c.Type == "Admin" && c.Value == "true"))
                {
                    context.Result = GetRoute("Managed", "AccessDenied");
                    return;
                }
            }

        }

        // Método para verificar si una acción requiere acceso de administrador
        private bool RequiresAdminAccess(string controller, string action)
        {
            // Define aquí las rutas que requieren acceso de administrador
            return (controller == "Admin" ||
                   (controller == "Refugio" && action == "AdminPanel") ||
                   (controller == "Adoptante" && action == "AdminPanel"));
        }

        private RedirectToRouteResult GetRoute(string controller, string action)
        {
            RouteValueDictionary ruta = new RouteValueDictionary(
                new { controller = controller, action = action }
            );
            return new RedirectToRouteResult(ruta);
        }
    }
}
