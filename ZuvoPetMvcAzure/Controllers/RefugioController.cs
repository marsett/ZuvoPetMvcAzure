using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ZuvoPetMvcAzure.Filters;
using ZuvoPetMvcAzure.Helpers;
using ZuvoPetNuget.Models;
using ZuvoPetMvcAzure.Repositories;
using ZuvoPetMvcAzure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using ZuvoPetMvcAzure.Hubs;

namespace ZuvoPetMvcAzure.Controllers
{
    [AuthorizeZuvoPetMvcAzure("Refugio")]
    public class RefugioController : Controller
    {
        private readonly ZuvoPetMvcAzureContext context;
        private readonly IRepositoryZuvoPet repo;
        private HelperPathProvider helperPath;
        private readonly IHubContext<ChatHub> hubContext;
        public RefugioController(ZuvoPetMvcAzureContext context, IRepositoryZuvoPet repo, HelperPathProvider helperPath, IHubContext<ChatHub> hubContext)
        {
            this.repo = repo;
            this.helperPath = helperPath;
            this.context = context;
            this.hubContext = hubContext;
        }


        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return 0;
        }

        public async Task<IActionResult> Index()
        {
            int idusuario = GetCurrentUserId();
            var refugio = await this.repo.GetRefugioByUsuarioIdAsync(idusuario);

            if (refugio == null)
            {
                return RedirectToAction("Create");
            }

            // Obtener todas las mascotas de este refugio
            var mascotas = await this.repo.GetMascotasByRefugioIdAsync(refugio.Id);
            refugio.ListaMascotas = mascotas.ToList();

            // Contar las solicitudes pendientes, rechazadas y aprobadas
            int solicitudesPendientes = await this.repo.GetSolicitudesByEstadoAndRefugioAsync(refugio.Id, "Pendiente");
            int solicitudesRechazadas = await this.repo.GetSolicitudesByEstadoAndRefugioAsync(refugio.Id, "Rechazada");

            ViewBag.NuevasSolicitudes = solicitudesPendientes;
            ViewBag.SolicitudesRechazadas = solicitudesRechazadas;

            // Calcular mascotas adoptadas
            int mascotasAdoptadas = await this.repo.GetSolicitudesByEstadoAndRefugioAsync(refugio.Id, "Aprobada");
            ViewBag.MascotasAdoptadas = mascotasAdoptadas;

            // Preparar datos para el gráfico de especies
            var especies = mascotas.GroupBy(m => m.Especie)
                                  .Select(g => new { Especie = g.Key, Cantidad = g.Count() })
                                  .OrderByDescending(g => g.Cantidad)
                                  .ToList();

            ViewBag.EspeciesLabels = especies.Select(e => e.Especie).ToArray();
            ViewBag.EspeciesData = especies.Select(e => e.Cantidad).ToArray();

            return View(refugio);
        }

        public async Task<IActionResult> Gestion(int pagina = 1)
        {
            int idusuario = GetCurrentUserId();

            // Obtiene todas las mascotas del refugio
            List<MascotaCard> mascotas = await this.repo.ObtenerMascotasRefugioAsync(idusuario);

            // Configuración de la paginación
            int mascotasPorPagina = 6; // Puedes ajustar esta cantidad según tus necesidades

            // Calcula el total de páginas
            int totalRegistros = mascotas.Count;
            int totalPaginas = (int)Math.Ceiling((double)totalRegistros / mascotasPorPagina);

            // Validación de la página actual
            if (pagina < 1) pagina = 1;
            if (pagina > totalPaginas && totalPaginas > 0) pagina = totalPaginas;

            // Filtra las mascotas para la página actual
            var mascotasPaginadas = mascotas
                .Skip((pagina - 1) * mascotasPorPagina)
                .Take(mascotasPorPagina)
                .ToList();

            // Asigna las variables de paginación al ViewBag
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;

            return View(mascotasPaginadas);
        }
        public async Task<IActionResult> CrearMascota()
        {
            int idusuario = GetCurrentUserId();
            Refugio refugio = await this.repo.GetRefugio(idusuario);
            ViewData["LATITUD"] = refugio.Latitud;
            ViewData["LONGITUD"] = refugio.Longitud;
            ViewData["NOMBRE"] = refugio.NombreRefugio;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CrearMascota(Mascota mascota, IFormFile fichero)
        {
            int idusuario = GetCurrentUserId();
            // Obtenemos el refugio asociado al usuario
            //var refugio = await this.context.Refugios
            //    .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            var refugio = await this.repo.GetRefugioByUsuarioIdAsync(idusuario);

            // Verificamos si el refugio ya está en su capacidad máxima
            if (refugio.CantidadAnimales >= refugio.CapacidadMaxima)
            {
                // Si está lleno, guardamos un mensaje para mostrar el SweetAlert
                TempData["SweetAlert"] = "error";
                TempData["SweetAlertTitle"] = "Capacidad máxima alcanzada";
                TempData["SweetAlertText"] = "No se puede agregar más mascotas. El refugio ha alcanzado su capacidad máxima.";
                return RedirectToAction("CrearMascota");
            }
            if (fichero != null)
            {
                string fileName = Guid.NewGuid().ToString() + ".png";
                string path = this.helperPath.MapPath(fileName, Folders.Images);
                using (Stream stream = new FileStream(path, FileMode.Create))
                {
                    await fichero.CopyToAsync(stream);
                }
                mascota.Foto = fileName;
            }

            
            await this.repo.CrearMascotaRefugioAsync(mascota, idusuario);
            // Mensaje de éxito
            TempData["SweetAlert"] = "success";
            TempData["SweetAlertTitle"] = "¡Mascota agregada!";
            TempData["SweetAlertText"] = "La mascota ha sido registrada correctamente.";
            return RedirectToAction("Gestion");
        }

        public async Task<IActionResult> EditarMascota(int idmascota)
        {
            // Buscar la mascota por ID
            Mascota mascota = await this.repo.GetMascotaByIdAsync(idmascota);

            if (mascota == null)
            {
                return NotFound(); // Si no existe la mascota, devolver 404
            }

            // Verificar que el usuario logueado es dueño del refugio de esta mascota
            int idusuario = GetCurrentUserId();
            Refugio refugio = await this.repo.GetRefugio(idusuario);

            if (mascota.IdRefugio != refugio.Id)
            {
                return Forbid(); // Si no es el dueño, denegar acceso
            }

            // Pasar datos adicionales a la vista
            ViewData["LATITUD"] = refugio.Latitud;
            ViewData["LONGITUD"] = refugio.Longitud;
            ViewData["NOMBRE"] = refugio.NombreRefugio;

            return View(mascota);
        }

        // Método POST para procesar el formulario de edición
        [HttpPost]
        public async Task<IActionResult> EditarMascota(Mascota mascota, IFormFile fichero)
        {
            // Verificar si el usuario es dueño del refugio
            int idusuario = GetCurrentUserId();
            Refugio refugio = await this.repo.GetRefugio(idusuario);

            if (mascota.IdRefugio != refugio.Id)
            {
                return Forbid(); // Si no es el dueño, denegar acceso
            }

            // Procesar la nueva imagen si se proporcionó una
            if (fichero != null)
            {
                string fileName = Guid.NewGuid().ToString() + ".png";
                string path = this.helperPath.MapPath(fileName, Folders.Images);
                using (Stream stream = new FileStream(path, FileMode.Create))
                {
                    await fichero.CopyToAsync(stream);
                }

                // Eliminar la imagen antigua si existe
                if (!string.IsNullOrEmpty(mascota.Foto))
                {
                    string oldImagePath = this.helperPath.MapPath(mascota.Foto, Folders.Images);
                    if (System.IO.File.Exists(oldImagePath))
                    {
                        System.IO.File.Delete(oldImagePath);
                    }
                }

                mascota.Foto = fileName;
            }

            // Actualizar la mascota en la base de datos
            await this.repo.UpdateMascotaAsync(mascota);

            return RedirectToAction("Gestion");
        }

        // Método POST para procesar la eliminación
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Refugio/EliminarMascota/{idmascota}")]
        public async Task<IActionResult> EliminarMascota(int idmascota)
        {
            // Buscar la mascota por ID
            Mascota mascota = await this.repo.GetMascotaByIdAsync(idmascota);
            if (mascota == null)
            {
                return NotFound(); // Si no existe la mascota, devolver 404
            }

            // Verificar que el usuario logueado es dueño del refugio de esta mascota
            int idusuario = GetCurrentUserId();
            Refugio refugio = await this.repo.GetRefugio(idusuario);
            if (mascota.IdRefugio != refugio.Id)
            {
                return Forbid(); // Si no es el dueño, denegar acceso
            }

            // Eliminar la imagen si existe
            if (!string.IsNullOrEmpty(mascota.Foto))
            {
                string imagePath = this.helperPath.MapPath(mascota.Foto, Folders.Images);
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }

            // Eliminar la mascota y sus relaciones de la base de datos
            bool result = await this.repo.DeleteMascotaAsync(idmascota);
            
            // Para solicitudes AJAX, devolver un resultado JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = result });
            }

            // Para solicitudes regulares, redirigir a la página de gestión
            return RedirectToAction("Gestion");
        }

        public async Task<IActionResult> Solicitudes(int pagina = 1)
        {
            int tamañoPagina = 3; // Número de solicitudes por página
            int idusuario = GetCurrentUserId();

            List<SolicitudAdopcion> todasSolicitudes =
                await this.repo.GetSolicitudesRefugioAsync(idusuario);

            // Calcular total de páginas
            int totalSolicitudes = todasSolicitudes.Count;
            int totalPaginas = (int)Math.Ceiling((double)totalSolicitudes / tamañoPagina);

            // Ajustar página actual si está fuera de rango
            if (pagina < 1) pagina = 1;
            if (pagina > totalPaginas && totalPaginas > 0) pagina = totalPaginas;

            // Obtener solicitudes de la página actual
            List<SolicitudAdopcion> solicitudesPaginadas = todasSolicitudes
                .Skip((pagina - 1) * tamañoPagina)
                .Take(tamañoPagina)
                .ToList();

            // Pasar información de paginación a la vista
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;

            return View(solicitudesPaginadas);
        }

        public async Task<IActionResult> DetallesSolicitud(int idsolicitud)
        {
            int idusuario = GetCurrentUserId();

            SolicitudAdopcion soli =
                await this.repo.GetSolicitudByIdAsync(idusuario, idsolicitud);
            return View(soli);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcesarSolicitud(int idSolicitud, string nuevoEstado)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Usuarios");
            }

            bool resultado = await this.repo.ProcesarSolicitudAdopcionAsync(idSolicitud, nuevoEstado);

            if (resultado)
            {
                // Retornar un resultado JSON para solicitudes AJAX
                return Json(new { success = true, mensaje = $"Solicitud {nuevoEstado.ToLower()} correctamente" });
            }
            else
            {
                return Json(new { success = false, mensaje = "Error al procesar la solicitud" });
            }
        }

        public async Task<IActionResult> DetallesMascota(int idmascota)
        {
            Mascota mascota = await this.repo.GetDetallesMascotaAsync(idmascota);
            return View(mascota);
        }

        public async Task<IActionResult> HistoriasExito()
        {
            List<HistoriaExito> historiasExito = await this.repo.ObtenerHistoriasExitoAsync();

            var historiasConDetalles = new List<HistoriaExitoConDetalles>();

            foreach (var historia in historiasExito)
            {
                var likeHistorias = await this.repo.ObtenerLikeHistoriaAsync(historia.Id);

                // Crear un objeto con la historia, comentarios y likes
                var historiaConDetalles = new HistoriaExitoConDetalles
                {
                    HistoriaExito = historia,
                    //ComentariosHistoria = comentariosHistoria,
                    LikeHistorias = likeHistorias
                };

                // Añadir el objeto a la lista
                historiasConDetalles.Add(historiaConDetalles);
            }
            return View(historiasConDetalles);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReaccionarHistoria(int idHistoria, string tipoReaccion)
        {
            // Verificar que el usuario esté autenticado
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            // Obtener el ID del usuario de la sesión
            int idusuario = GetCurrentUserId();

            // Verificar si el usuario ya tiene una reacción para esta historia
            var reaccionExistente = await this.repo.ObtenerLikeUsuarioHistoriaAsync(idHistoria, idusuario);
            bool resultado;
            string accion;

            // Si la reacción existente es del mismo tipo, eliminarla (toggle)
            if (reaccionExistente != null && reaccionExistente.TipoReaccion == tipoReaccion)
            {
                resultado = await this.repo.EliminarLikeHistoriaAsync(idHistoria, idusuario);
                accion = "eliminado";
            }
            // Si la reacción es de otro tipo o no existe, crearla o actualizarla
            else
            {
                if (reaccionExistente == null)
                {
                    resultado = await this.repo.CrearLikeHistoriaAsync(idHistoria, idusuario, tipoReaccion);
                }
                else
                {
                    resultado = await this.repo.ActualizarLikeHistoriaAsync(idHistoria, idusuario, tipoReaccion);
                }
                accion = "agregado";
            }

            if (resultado)
            {
                // Obtener contadores actualizados
                var contadores = await this.repo.ObtenerContadoresReaccionesAsync(idHistoria);
                return Json(new
                {
                    success = true,
                    accion = accion,
                    contadores = contadores
                });
            }

            return Json(new { success = false, message = "Error al procesar la reacción" });
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ObtenerEstadoReaccion(int idHistoria)
        {
            // Verificar que el usuario esté autenticado
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, message = "Usuario no autenticado" });
            }

            // Obtener el ID del usuario de la sesión
            int idusuario = GetCurrentUserId();

            // Obtener la reacción actual del usuario para esta historia
            var reaccion = await this.repo.ObtenerLikeUsuarioHistoriaAsync(idHistoria, idusuario);

            if (reaccion != null)
            {
                return Json(new
                {
                    success = true,
                    tipoReaccion = reaccion.TipoReaccion
                });
            }

            return Json(new
            {
                success = true,
                tipoReaccion = ""
            });
        }

        public async Task<IActionResult> Notificaciones(int pagina = 1)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Usuarios");
            }

            int idUsuario = GetCurrentUserId();
            int tamañoPagina = 10; // Número de notificaciones por página

            // Obtener notificaciones del usuario
            var notificaciones = await this.repo.GetNotificacionesUsuarioAsync(idUsuario, pagina, tamañoPagina);

            // Calcular información de paginación
            int totalNotificaciones = await this.repo.GetTotalNotificacionesUsuarioAsync(idUsuario);
            int totalPaginas = (int)Math.Ceiling((double)totalNotificaciones / tamañoPagina);

            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalNotificaciones = totalNotificaciones;
            ViewBag.NoLeidas = await this.repo.GetTotalNotificacionesNoLeidasAsync(idUsuario);

            return View(notificaciones);
        }

        public async Task<IActionResult> VerificarNuevasNotificaciones()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { hayNuevas = false });
            }

            int idUsuario = GetCurrentUserId();

            // Obtén la última vez que se verificaron notificaciones (desde la sesión)
            DateTime ultimaVerificacion = DateTime.Now.AddDays(-1); // Valor predeterminado: ayer
            if (HttpContext.Session.GetString("ULTIMA_VERIFICACION_NOTIF") != null)
            {
                ultimaVerificacion = DateTime.Parse(HttpContext.Session.GetString("ULTIMA_VERIFICACION_NOTIF"));
            }

            // Verificar si hay notificaciones nuevas desde la última verificación
            bool hayNuevas = await this.repo.HayNotificacionesNuevasDesdeAsync(idUsuario, ultimaVerificacion);

            // Actualizar el timestamp de última verificación
            HttpContext.Session.SetString("ULTIMA_VERIFICACION_NOTIF", DateTime.Now.ToString("o"));

            return Json(new { hayNuevas });
        }

        [HttpPost]
        public async Task<IActionResult> MarcarComoLeida(int idNotificacion)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, mensaje = "Sesión no válida" });
            }

            int idUsuario = GetCurrentUserId();
            bool resultado = await this.repo.MarcarNotificacionComoLeidaAsync(idNotificacion, idUsuario);

            return Json(new { success = resultado });
        }

        [HttpPost]
        public async Task<IActionResult> MarcarTodasComoLeidas()
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, mensaje = "Sesión no válida" });
            }

            int idUsuario = GetCurrentUserId();
            bool resultado = await this.repo.MarcarTodasNotificacionesComoLeidasAsync(idUsuario);

            return Json(new { success = resultado });
        }

        [HttpPost]
        public async Task<IActionResult> EliminarNotificacion(int idNotificacion)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json(new { success = false, mensaje = "Sesión no válida" });
            }

            int idUsuario = GetCurrentUserId();
            bool resultado = await this.repo.EliminarNotificacionAsync(idNotificacion, idUsuario);

            return Json(new { success = resultado });
        }

        public async Task<IActionResult> Perfil()
        {
            int idusuario = GetCurrentUserId();
            VistaPerfilRefugio perfil = await this.repo.GetPerfilRefugio(idusuario);
            return View(perfil);
        }

        //[HttpPost]
        //public async Task<IActionResult> ActualizarDescripcion(VistaPerfilRefugio modelo)
        //{
        //    int idusuario = GetCurrentUserId();

        //    var refugio = await repo.GetPerfilRefugio(idusuario);

        //    refugio.Descripcion = modelo.Descripcion;

        //    await context.SaveChangesAsync();

        //    return RedirectToAction("Perfil");
        //}

        [HttpPost]
        public async Task<IActionResult> ActualizarDescripcion(VistaPerfilRefugio modelo)
        {
            int idusuario = GetCurrentUserId();
            bool resultado = await repo.ActualizarDescripcionAsync(idusuario, modelo.Descripcion);

            if (resultado)
                return RedirectToAction("Perfil");
            else
                return View("Error");
        }

        //[HttpPost]
        //public async Task<IActionResult> ActualizarDetalles(VistaPerfilRefugio modelo)
        //{
        //    int idusuario = GetCurrentUserId();

        //    var refugio = await repo.GetPerfilRefugio(idusuario);

        //    refugio.ContactoRefugio = modelo.ContactoRefugio;
        //    refugio.CantidadAnimales = modelo.CantidadAnimales;
        //    refugio.CapacidadMaxima = modelo.CapacidadMaxima;
        //    //refugio.Latitud = modelo.Latitud;
        //    //refugio.Longitud = modelo.Longitud;

        //    await context.SaveChangesAsync();

        //    return RedirectToAction("Perfil");
        //}

        [HttpPost]
        public async Task<IActionResult> ActualizarDetalles(VistaPerfilRefugio modelo)
        {
            int idUsuario = GetCurrentUserId();
            bool resultado = await repo.ActualizarDetallesRefugioAsync(
                idUsuario,
                modelo.ContactoRefugio,
                modelo.CantidadAnimales,
                modelo.CapacidadMaxima
            );

            if (resultado)
                return RedirectToAction("Perfil");
            else
                return View("Error"); // O manejar el error de otra manera
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> ActualizarUbicacionRefugio(double Latitud, double Longitud)
        //{
        //    try
        //    {
        //        // Validate input
        //        if (Latitud == 0 || Longitud == 0)
        //        {
        //            return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
        //                ? Json(new { success = false, error = "Coordenadas inválidas" })
        //                : RedirectToAction("Perfil");
        //        }

        //        // Redondea los valores a 6 decimales
        //        Latitud = Math.Round(Latitud, 6);
        //        Longitud = Math.Round(Longitud, 6);

        //        int usuarioId = GetCurrentUserId();
        //        var refugio = await repo.GetRefugioByUsuarioIdAsync(usuarioId)
        //            ?? throw new InvalidOperationException("Refugio no encontrado");

        //        refugio.Latitud = Latitud;
        //        refugio.Longitud = Longitud;
        //        await context.SaveChangesAsync();

        //        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
        //            ? Json(new { success = true })
        //            : RedirectToAction("Perfil");
        //    }
        //    catch (Exception ex)
        //    {
        //        // Log the error (consider using a logging framework)
        //        Console.Error.WriteLine($"Error actualizando ubicación: {ex.Message}");
        //        return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
        //            ? Json(new { success = false, error = "Error al actualizar la ubicación" })
        //            : RedirectToAction("Perfil");
        //    }
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ActualizarUbicacionRefugio(double Latitud, double Longitud)
        {
            try
            {
                // Validate input
                if (Latitud == 0 || Longitud == 0)
                {
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                        ? Json(new { success = false, error = "Coordenadas inválidas" })
                        : RedirectToAction("Perfil");
                }

                int usuarioId = GetCurrentUserId();
                bool resultado = await repo.ActualizarUbicacionRefugioAsync(usuarioId, Latitud, Longitud);

                if (!resultado)
                {
                    return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                        ? Json(new { success = false, error = "No se pudo actualizar la ubicación" })
                        : RedirectToAction("Perfil");
                }

                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = true })
                    : RedirectToAction("Perfil");
            }
            catch (Exception ex)
            {
                // Log the error (consider using a logging framework)
                Console.Error.WriteLine($"Error actualizando ubicación: {ex.Message}");
                return Request.Headers["X-Requested-With"] == "XMLHttpRequest"
                    ? Json(new { success = false, error = "Error al actualizar la ubicación" })
                    : RedirectToAction("Perfil");
            }
        }

        //[HttpPost]
        //public async Task<IActionResult> ActualizarPerfil(VistaPerfilRefugio modelo)
        //{
        //    int idusuario = GetCurrentUserId();

        //    var usuario = await context.Usuarios.FindAsync(idusuario);
        //    if (usuario != null)
        //    {
        //        usuario.Email = modelo.Email;
        //    }

        //    var refugio = await context.Refugios.FirstOrDefaultAsync(r => r.IdUsuario == idusuario);
        //    if (refugio != null)
        //    {
        //        refugio.NombreRefugio = modelo.NombreRefugio;
        //        refugio.ContactoRefugio = modelo.ContactoRefugio;
        //    }

        //    await context.SaveChangesAsync();

        //    return RedirectToAction("Perfil");
        //}

        [HttpPost]
        public async Task<IActionResult> ActualizarPerfil(VistaPerfilRefugio modelo)
        {
            int idusuario = GetCurrentUserId();
            bool resultado = await repo.ActualizarPerfilRefugioAsync(
                idusuario,
                modelo.Email,
                modelo.NombreRefugio,
                modelo.ContactoRefugio
            );

            if (!resultado)
                return View("Error"); // O manejar el error de otra manera

            return RedirectToAction("Perfil");
        }

        public IActionResult SubirFichero()
        {
            return View();
        }

        //[HttpPost]
        //public async Task<IActionResult> SubirFichero(IFormFile fichero)
        //{
        //    string fileName = Guid.NewGuid().ToString() + ".png";

        //    string path = this.helperPath.MapPath(fileName, Folders.Images);
        //    string pathServer = this.helperPath.MapUrlPathServer(fileName, Folders.Images);

        //    using (Stream stream = new FileStream(path, FileMode.Create))
        //    {
        //        await fichero.CopyToAsync(stream);
        //    }

        //    string pathAccessor = this.helperPath.MapUrlPath(fileName, Folders.Images);

        //    int idusuario = GetCurrentUserId();

        //    var refugio = await repo.GetPerfilRefugio(idusuario);

        //    // Eliminar la foto de perfil anterior si existe
        //    if (!string.IsNullOrEmpty(refugio.FotoPerfil))
        //    {
        //        string oldFilePath = this.helperPath.MapPath(refugio.FotoPerfil, Folders.Images);
        //        if (System.IO.File.Exists(oldFilePath))
        //        {
        //            System.IO.File.Delete(oldFilePath);
        //        }
        //    }

        //    refugio.FotoPerfil = fileName;
        //    await context.SaveChangesAsync();

        //    // Obtener la nueva foto para actualizar la sesión y los claims
        //    string fotoPerfil = await this.repo.GetFotoPerfilAsync(idusuario);

        //    var user = HttpContext.User;
        //    var identity = user.Identity as ClaimsIdentity;

        //    if (identity != null)
        //    {
        //        // Eliminar el claim existente
        //        var existingClaim = identity.FindFirst("FotoPerfil");
        //        if (existingClaim != null)
        //        {
        //            identity.RemoveClaim(existingClaim);
        //        }

        //        // Agregar el nuevo claim con la nueva foto
        //        Claim claimFoto = new Claim("FotoPerfil", fotoPerfil);
        //        identity.AddClaim(claimFoto);

        //        // REFRESCAR LA AUTENTICACIÓN DEL USUARIO
        //        await HttpContext.SignInAsync(
        //            CookieAuthenticationDefaults.AuthenticationScheme,
        //            new ClaimsPrincipal(identity)
        //        );
        //    }

        //    return RedirectToAction("Perfil");
        //}

        [HttpPost]
        public async Task<IActionResult> SubirFichero(IFormFile fichero)
        {
            string fileName = Guid.NewGuid().ToString() + ".png";
            string path = this.helperPath.MapPath(fileName, Folders.Images);

            using (Stream stream = new FileStream(path, FileMode.Create))
            {
                await fichero.CopyToAsync(stream);
            }

            int idusuario = GetCurrentUserId();

            // Obtener la foto de perfil actual antes de actualizarla
            var refugio = await repo.GetPerfilRefugio(idusuario);
            string oldFileName = refugio?.FotoPerfil;

            // Si hay una foto anterior, eliminarla del sistema de archivos
            if (!string.IsNullOrEmpty(oldFileName))
            {
                string oldFilePath = this.helperPath.MapPath(oldFileName, Folders.Images);
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            // Actualizar la foto de perfil en la base de datos
            string fotoPerfil = await this.repo.ActualizarFotoPerfilAsync(idusuario, fileName);

            if (fotoPerfil == null)
            {
                // Si hubo un error, eliminar el archivo recién subido
                string uploadedFilePath = this.helperPath.MapPath(fileName, Folders.Images);
                if (System.IO.File.Exists(uploadedFilePath))
                {
                    System.IO.File.Delete(uploadedFilePath);
                }
                return View("Error");
            }

            // Actualizar los claims del usuario con la nueva foto
            var user = HttpContext.User;
            var identity = user.Identity as ClaimsIdentity;

            if (identity != null)
            {
                // Eliminar el claim existente
                var existingClaim = identity.FindFirst("FotoPerfil");
                if (existingClaim != null)
                {
                    identity.RemoveClaim(existingClaim);
                }

                // Agregar el nuevo claim con la nueva foto
                Claim claimFoto = new Claim("FotoPerfil", fotoPerfil);
                identity.AddClaim(claimFoto);

                // REFRESCAR LA AUTENTICACIÓN DEL USUARIO
                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity)
                );
            }

            return RedirectToAction("Perfil");
        }

        private async Task<int> GetIdUsuarioActual()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
            //var refugio = await context.Refugios.FirstOrDefaultAsync(a => a.IdUsuario == userId);
            var refugio = await this.repo.GetRefugioByUsuarioIdAsync(userId);
            return userId;
        }

        // Lista de conversaciones
        [HttpGet]
        public async Task<IActionResult> Mensajes()
        {
            //int usuarioId = await GetIdUsuarioActual();
            int usuarioId = GetCurrentUserId();
            var conversaciones = await this.repo.GetConversacionesRefugioAsync(usuarioId);
            // Convertir las fechas de UTC a la zona horaria local
            foreach (var conversacion in conversaciones)
            {
                // Asumiendo que FechaUltimoMensaje está en UTC
                conversacion.FechaUltimoMensaje = TimeZoneInfo.ConvertTimeFromUtc(
                    conversacion.FechaUltimoMensaje,
                    TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time")); // Para España
            }
            return View(conversaciones);
        }

        // Ver chat específico
        //[HttpGet]
        //public async Task<IActionResult> Chat(int id)
        //{
        //    //int usuarioActualId = await GetIdUsuarioActual();
        //    int usuarioActualId = GetCurrentUserId();

        //    // Obtener mensajes
        //    var mensajes = await this.repo.GetMensajesConversacionAsync(usuarioActualId, id);

        //    // Marcar como leídos los mensajes recibidos
        //    foreach (var mensaje in mensajes.Where(m => m.IdEmisor == id && !m.Leido))
        //    {
        //        mensaje.Leido = true;
        //    }
        //    await context.SaveChangesAsync();

        //    // Obtener nombre del refugio
        //    var adoptante = await context.Adoptantes
        //        .Include(adoptante => adoptante.Usuario.PerfilUsuario)
        //        .FirstOrDefaultAsync(r => r.IdUsuario == id);
        //    string nombreDestinatario = adoptante != null ? adoptante.Nombre : "UsuarioOL";

        //    var viewModel = new ChatViewModel
        //    {
        //        Mensajes = mensajes,
        //        NombreDestinatario = nombreDestinatario,
        //        IdDestinatario = id,
        //        FotoDestinatario = adoptante.Usuario.PerfilUsuario.FotoPerfil
        //    };

        //    return View(viewModel);
        //}

        [HttpGet]
        public async Task<IActionResult> Chat(int id)
        {
            int usuarioActualId = GetCurrentUserId();

            // Obtener mensajes
            var mensajes = await this.repo.GetMensajesConversacionAsync(usuarioActualId, id);

            // Convertir las fechas de UTC a la zona horaria local
            foreach (var mensaje in mensajes)
            {
                // Asumiendo que la propiedad Fecha en los mensajes es DateTime
                mensaje.Fecha = TimeZoneInfo.ConvertTimeFromUtc(
                    mensaje.Fecha,
                    TimeZoneInfo.FindSystemTimeZoneById("Romance Standard Time")); // Para España
                                                                                   // Alternativa: mensaje.Fecha = mensaje.Fecha.ToLocalTime(); // Si prefieres usar la zona horaria del servidor
            }

            // Marcar los mensajes como leídos
            await repo.MarcarMensajesComoLeidosAsync(usuarioActualId, id);

            // Obtener nombre del destinatario
            //var adoptante = await context.Adoptantes
            //    .Include(adoptante => adoptante.Usuario.PerfilUsuario)
            //    .FirstOrDefaultAsync(r => r.IdUsuario == id);
            var adoptante = await this.repo.GetAdoptanteChatByUsuarioId(id);

            string nombreDestinatario = adoptante != null ? adoptante.Nombre : "UsuarioOL";

            var viewModel = new ChatViewModel
            {
                Mensajes = mensajes,
                NombreDestinatario = nombreDestinatario,
                IdDestinatario = id,
                FotoDestinatario = adoptante.Usuario.PerfilUsuario.FotoPerfil
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnviarMensaje(int destinatarioId, string contenido)
        {
            if (string.IsNullOrWhiteSpace(contenido))
            {
                return BadRequest("El mensaje no puede estar vacío");
            }

            int emisorId = await GetIdUsuarioActual();
            var mensaje = await this.repo.AgregarMensajeAsync(emisorId, destinatarioId, contenido);

            // Notificar por SignalR
            await hubContext.Clients.User(destinatarioId.ToString())
                .SendAsync("RecibirMensaje", emisorId, mensaje.Contenido, mensaje.Fecha);

            return Json(new { success = true });
        }

        public async Task<IActionResult> GestionVeterinarios()
        {
            return View();
        }
    }
}
