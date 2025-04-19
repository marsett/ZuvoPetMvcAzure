using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using ZuvoPetMvcAzure.Data;
using ZuvoPetMvcAzure.Filters;
using ZuvoPetMvcAzure.Helpers;
using ZuvoPetNuget.Models;
using ZuvoPetMvcAzure.Repositories;
using ZuvoPetMvcAzure.Services;
using ZuvoPetNuget.Dtos;

namespace ZuvoPetMvcAzure.Controllers
{
    public class ManagedController : Controller
    {
        private readonly ServiceZuvoPet service;
        private readonly IHttpContextAccessor httpContextAccessor;

        public ManagedController(ServiceZuvoPet service, IHttpContextAccessor httpContextAccessor)
        {
            this.service = service;
            this.httpContextAccessor = httpContextAccessor;
        }

        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(Usuario registro)
        {
            // Comprobar si el nombre de usuario o email ya existe
            bool userExists = await this.service.ValidateUserAsync(registro.NombreUsuario, registro.Email);
            if (userExists)
            {
                ModelState.AddModelError("", "El nombre de usuario o el correo ya están en uso.");
                return View(registro);
            }

            var passwordRegex = new System.Text.RegularExpressions.Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$");
            if (!passwordRegex.IsMatch(registro.ContrasenaLimpia))
            {
                ModelState.AddModelError("ContrasenaLimpia", "La contraseña debe tener al menos 8 caracteres, incluir mayúsculas, minúsculas, números y al menos un carácter especial.");
                return View(registro);
            }

            // Guardar datos del usuario en sesión
            HttpContext.Session.SetString("RegistroUsuario", JsonSerializer.Serialize(registro));

            // Redirigir según tipo de usuario
            if (registro.TipoUsuario == "Adoptante")
            {
                return RedirectToAction("FormularioAdoptante");
            }
            else if (registro.TipoUsuario == "Refugio")
            {
                return RedirectToAction("FormularioRefugio");
            }

            return RedirectToAction("Login");
        }

        public IActionResult FormularioAdoptante()
        {
            // Verificar si hay datos de registro en sesión
            if (!HttpContext.Session.Keys.Contains("RegistroUsuario"))
            {
                return RedirectToAction("Register");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FormularioAdoptante(Adoptante adoptante)
        {
            if (!HttpContext.Session.Keys.Contains("RegistroUsuario"))
            {
                return RedirectToAction("Register");
            }

            // Recuperar datos del usuario
            var registroJson = HttpContext.Session.GetString("RegistroUsuario");
            var registro = JsonSerializer.Deserialize<Usuario>(registroJson);

            try
            {
                // Crear el DTO de registro con los datos de adoptante
                RegistroDTO registroDTO = new RegistroDTO
                {
                    NombreUsuario = registro.NombreUsuario,
                    Email = registro.Email,
                    ContrasenaLimpia = registro.ContrasenaLimpia,
                    TipoUsuario = registro.TipoUsuario,
                    DatosAdoptante = new AdoptanteDTO
                    {
                        Nombre = adoptante.Nombre,
                        TipoVivienda = adoptante.TipoVivienda,
                        TieneJardin = adoptante.TieneJardin,
                        OtrosAnimales = adoptante.OtrosAnimales,
                        RecursosDisponibles = adoptante.RecursosDisponibles,
                        TiempoEnCasa = adoptante.TiempoEnCasa
                    },
                    DatosRefugio = new RefugioDTO
                    {
                        NombreRefugio = "",
                        ContactoRefugio = "",
                        CantidadAnimales = 0,
                        CapacidadMaxima = 0,
                        LatitudStr = "",
                        LongitudStr = ""
                    }
                };

                // Llamar al servicio para registrar al usuario
                var resultado = await this.service.RegistrarUsuarioAsync(registroDTO);

                if (resultado)
                {
                    // Limpiar datos de sesión
                    HttpContext.Session.Remove("RegistroUsuario");
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "Error al completar el registro");
                    return View(adoptante);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al completar el registro: " + ex.Message);
                return View(adoptante);
            }
        }

        public IActionResult FormularioRefugio()
        {
            // Verificar si hay datos de registro en sesión
            if (!HttpContext.Session.Keys.Contains("RegistroUsuario"))
            {
                return RedirectToAction("Register");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FormularioRefugio(Refugio refugio)
        {
            // Verificar si hay datos de registro en sesión
            if (!HttpContext.Session.Keys.Contains("RegistroUsuario"))
            {
                return RedirectToAction("Register");
            }

            // Recuperar datos del usuario
            var registroJson = HttpContext.Session.GetString("RegistroUsuario");
            var registro = JsonSerializer.Deserialize<Usuario>(registroJson);

            try
            {
                // Crear el DTO de registro con los datos de refugio
                RegistroDTO registroDTO = new RegistroDTO
                {
                    NombreUsuario = registro.NombreUsuario,
                    Email = registro.Email,
                    ContrasenaLimpia = registro.ContrasenaLimpia,
                    TipoUsuario = registro.TipoUsuario,
                    DatosAdoptante = new AdoptanteDTO
                    {
                        Nombre = "",
                        TipoVivienda = "",
                        TieneJardin = false,
                        OtrosAnimales = false,
                        RecursosDisponibles = [],
                        TiempoEnCasa = ""
                    },
                    DatosRefugio = new RefugioDTO
                    {
                        NombreRefugio = refugio.NombreRefugio,
                        ContactoRefugio = refugio.ContactoRefugio,
                        CantidadAnimales = refugio.CantidadAnimales,
                        CapacidadMaxima = refugio.CapacidadMaxima,
                        LatitudStr = refugio.LatitudStr,
                        LongitudStr = refugio.LongitudStr
                    }
                };

                // Llamar al servicio para registrar al usuario
                var resultado = await this.service.RegistrarUsuarioAsync(registroDTO);

                if (resultado)
                {
                    // Limpiar datos de sesión
                    HttpContext.Session.Remove("RegistroUsuario");
                    return RedirectToAction("Login");
                }
                else
                {
                    ModelState.AddModelError("", "Error al completar el registro");
                    return View(refugio);
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error al completar el registro: " + ex.Message);
                return View(refugio);
            }
        }

        public IActionResult Login()
        {
            // Configurar encabezados para evitar caché
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            // Si el usuario ya está autenticado, redirigir según su rol
            if (User.Identity.IsAuthenticated)
            {
                string userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                if (userRole == "Adoptante")
                {
                    return RedirectToAction("Index", "Adoptante");
                }
                else if (userRole == "Refugio")
                {
                    return RedirectToAction("Index", "Refugio");
                }
            }

            // Ya no necesitamos verificar la sesión
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string nombreusuario, string contrasena)
        {
            // Obtener el token JWT
            string token = await this.service.GetTokenAsync(nombreusuario, contrasena);

            if (token == null)
            {
                ViewData["MENSAJE"] = "Credenciales incorrectas token";
                return View();
            }

            // Decodificar el token para extraer los claims
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // Extraer la información necesaria del token
            var userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var userName = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
            var userRole = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

            // Crear la identidad de Claims para la autenticación por cookies
            ClaimsIdentity identity = new ClaimsIdentity(
                CookieAuthenticationDefaults.AuthenticationScheme,
                ClaimTypes.Name, ClaimTypes.Role);

            // Agregar los claims básicos
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
            identity.AddClaim(new Claim(ClaimTypes.Name, userName));
            identity.AddClaim(new Claim(ClaimTypes.Role, userRole));
            // Almacenar el token en los claims - ESTA ES LA PARTE IMPORTANTE
            identity.AddClaim(new Claim("TOKEN", token));

            // Crear el principal y realizar el login
            ClaimsPrincipal userPrincipal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                userPrincipal,
                new AuthenticationProperties
                {
                    ExpiresUtc = DateTime.UtcNow.AddMinutes(30)
                });

            // Redirigir según el tipo de usuario
            if (userRole == "Adoptante")
            {
                return RedirectToAction("Index", "Adoptante");
            }
            else if (userRole == "Refugio")
            {
                return RedirectToAction("Index", "Refugio");
            }
            else
            {
                return RedirectToAction("Login");
            }
        }

        public IActionResult Denied()
        {
            return View();
        }
        public async Task<IActionResult> Landing()
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            // Si ya está autenticado, redirigir
            if (User.Identity.IsAuthenticated)
            {
                string userRole = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                if (userRole == "Adoptante")
                {
                    return RedirectToAction("Index", "Adoptante");
                }
                else if (userRole == "Refugio")
                {
                    return RedirectToAction("Index", "Refugio");
                }
            }

            List<HistoriaExitoLandingDTO> historias = await this.service.GetHistoriasExitoLanding();
            return View(historias);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Landing", "Managed");
        }
    }
}
