using Microsoft.AspNetCore.Mvc;
using ZuvoPetMvcAzure.Services;

namespace ZuvoPetMvcAzure.Controllers
{
    public class ImagenController : Controller
    {
        private readonly ServiceZuvoPet service;

        public ImagenController(ServiceZuvoPet service)
        {
            this.service = service;
        }

        public async Task<IActionResult> GetImagenAdoptante(string nombreImagen)
        {
            Console.WriteLine($"NombreImagen recibido: {nombreImagen}");
            try
            {
                var imagenStream = await this.service.GetImagenAdoptanteAsync(nombreImagen);

                // Determinar el tipo de contenido basado en la extensión
                string contentType = "image/png"; // Por defecto
                if (nombreImagen.EndsWith(".jpg") || nombreImagen.EndsWith(".jpeg"))
                    contentType = "image/jpeg";

                return File(imagenStream, contentType);
            }
            catch (Exception ex)
            {
                // Puedes loguear el error si lo necesitas
                return NotFound();
            }
        }

        public async Task<IActionResult> GetImagenRefugio(string nombreImagen)
        {
            try
            {
                var imagenStream = await this.service.GetImagenRefugioAsync(nombreImagen);

                // Determinar el tipo de contenido basado en la extensión
                string contentType = "image/png"; // Por defecto
                if (nombreImagen.EndsWith(".jpg") || nombreImagen.EndsWith(".jpeg"))
                    contentType = "image/jpeg";

                return File(imagenStream, contentType);
            }
            catch (Exception ex)
            {
                // Puedes loguear el error si lo necesitas
                return NotFound();
            }
        }
    }
}
