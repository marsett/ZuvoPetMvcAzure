using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using ZuvoPetNuget.Models;
using ZuvoPetNuget.Dtos;
using System.Security.Claims;
using System.Text.Json;

namespace ZuvoPetMvcAzure.Services
{
    public class ServiceZuvoPet
    {
        private string urlApi;
        private MediaTypeWithQualityHeaderValue header;
        private IHttpContextAccessor contextAccessor;
        public ServiceZuvoPet(IConfiguration configuration, IHttpContextAccessor contextAccessor)
        {
            this.urlApi = configuration.GetValue<string>("ApiUrls:ZuvoPetApiAzure");
            this.header = new MediaTypeWithQualityHeaderValue("application/json");
            this.contextAccessor = contextAccessor;
        }

        public async Task<(bool Success, string FotoUrl, string Message)> SubirImagenHistoriaExitoAsync(IFormFile archivo)
        {
            try
            {
                string token = GetUserToken();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, null, "No se pudo obtener el token de autenticación");
                }

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.urlApi);
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(this.header);
                    client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);

                    // Crear un contenido multipart
                    using (var content = new MultipartFormDataContent())
                    {
                        // Convertir el archivo a stream content
                        using (var streamContent = new StreamContent(archivo.OpenReadStream()))
                        {
                            streamContent.Headers.ContentType = new MediaTypeHeaderValue(archivo.ContentType);
                            content.Add(streamContent, "archivo", archivo.FileName);

                            // Endpoint para subir la imagen de historia de éxito
                            string request = "api/adoptante/SubirImagen";
                            HttpResponseMessage response = await client.PostAsync(request, content);

                            if (response.IsSuccessStatusCode)
                            {
                                var responseContent = await response.Content.ReadAsStringAsync();
                                var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                                return (true, result.fotoUrl.ToString(), "Imagen subida correctamente");
                            }
                            else
                            {
                                var errorContent = await response.Content.ReadAsStringAsync();
                                var error = JsonConvert.DeserializeObject<dynamic>(errorContent);
                                return (false, null, error?.mensaje?.ToString() ?? "Error al subir la imagen");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al subir imagen de historia de éxito: {ex.Message}");
                return (false, null, $"Error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string FotoUrl, string Message)> ActualizarFotoPerfilAsync(IFormFile archivo)
        {
            try
            {
                string token = GetUserToken();
                if (string.IsNullOrEmpty(token))
                {
                    return (false, null, "No se pudo obtener el token de autenticación");
                }

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.urlApi);
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(this.header);
                    client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);

                    // Crear un contenido multipart
                    using (var content = new MultipartFormDataContent())
                    {
                        // Convertir el archivo a stream content
                        using (var streamContent = new StreamContent(archivo.OpenReadStream()))
                        {
                            streamContent.Headers.ContentType = new MediaTypeHeaderValue(archivo.ContentType);
                            content.Add(streamContent, "archivo", archivo.FileName);

                            // Verificar el rol del usuario para determinar el endpoint
                            string userRole = this.contextAccessor.HttpContext.User.FindFirst(ClaimTypes.Role)?.Value;
                            string request = userRole == "Adoptante"
                                ? "api/adoptante/PostFotoPerfil"
                                : "api/refugio/PostFotoPerfil";

                            HttpResponseMessage response = await client.PostAsync(request, content);

                            if (response.IsSuccessStatusCode)
                            {
                                var responseContent = await response.Content.ReadAsStringAsync();
                                var result = JsonConvert.DeserializeObject<dynamic>(responseContent);
                                return (true, result.fotoUrl.ToString(), "Foto actualizada correctamente");
                            }
                            else
                            {
                                var errorContent = await response.Content.ReadAsStringAsync();
                                var error = JsonConvert.DeserializeObject<dynamic>(errorContent);
                                return (false, null, error?.mensaje?.ToString() ?? "Error al actualizar la foto de perfil");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al actualizar foto de perfil: {ex.Message}");
                return (false, null, $"Error: {ex.Message}");
            }
        }

        public async Task<string> GetTokenAsync(string username, string password)
        {
            using (HttpClient client = new HttpClient())
            {
                string request = "api/auth/login";
                client.BaseAddress = new Uri(this.urlApi);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(this.header);
                LoginModel model = new LoginModel
                {
                    NombreUsuario = username,
                    Contrasena = password
                };
                string json = JsonConvert.SerializeObject(model);
                StringContent content = new StringContent
                    (json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(request, content);
                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    JObject keys = JObject.Parse(data);
                    string token = keys.GetValue("response").ToString();
                    return token;
                }
                else
                {
                    return null;
                }
            }
        }
        private async Task<T> CallApiAsync<T>(string request)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(this.urlApi);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(this.header);
                HttpResponseMessage response = await client.GetAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    T data = await response.Content.ReadAsAsync<T>();
                    return data;
                }
                else
                {
                    return default(T);
                }
            }
        }

        private async Task<T> CallApiAsync<T>(string request, string token)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(this.urlApi);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(this.header);
                client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);
                HttpResponseMessage response = await client.GetAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    T data = await response.Content.ReadAsAsync<T>();
                    return data;
                }
                else
                {
                    return default(T);
                }
            }
        }

        // Método genérico para manejar diferentes tipos de llamadas HTTP (GET, POST, PUT, DELETE)
        private async Task<T> CallApiAsync<T>(string request, string token, string method, object data = null)
        {
            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(this.urlApi);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(this.header);

                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);
                }

                HttpResponseMessage response;

                switch (method.ToUpper())
                {
                    case "POST":
                        string jsonPost = JsonConvert.SerializeObject(data);
                        StringContent contentPost = new StringContent(jsonPost, Encoding.UTF8, "application/json");
                        Console.WriteLine($"Enviando POST a {request} con datos: {jsonPost}");
                        response = await client.PostAsync(request, contentPost);
                        break;
                    case "PUT":
                        string jsonPut = JsonConvert.SerializeObject(data);
                        StringContent contentPut = new StringContent(jsonPut, Encoding.UTF8, "application/json");
                        response = await client.PutAsync(request, contentPut);
                        break;
                    case "DELETE":
                        response = await client.DeleteAsync(request);
                        break;
                    default:
                        throw new ArgumentException($"Método HTTP no soportado: {method}");
                }

                if (response.IsSuccessStatusCode)
                {
                    T resultado = await response.Content.ReadAsAsync<T>();
                    return resultado;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Error en llamada API: {response.StatusCode} - {errorContent}");
                    return default(T);
                }
            }
        }

        private string GetUserToken()
        {
            if (this.contextAccessor.HttpContext != null &&
                this.contextAccessor.HttpContext.User.Identity.IsAuthenticated)
            {
                ClaimsIdentity identity = this.contextAccessor.HttpContext.User.Identity as ClaimsIdentity;
                if (identity != null)
                {
                    Claim claim = identity.FindFirst("TOKEN");
                    if (claim != null)
                    {
                        return claim.Value;
                    }
                }
            }
            return null;
        }

        public async Task<T> CallSecureApiAsync<T>(string request)
        {
            string token = GetUserToken();
            if (token != null)
            {
                return await CallApiAsync<T>(request, token);
            }
            return default(T);
        }

        public async Task<bool> ValidateUserAsync(string username, string email)
        {
            using (HttpClient client = new HttpClient())
            {
                string request = $"api/auth/ValidateUser?username={username}&email={email}";
                client.BaseAddress = new Uri(this.urlApi);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(this.header);

                HttpResponseMessage response = await client.GetAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string data = await response.Content.ReadAsStringAsync();
                    var result = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, bool>>(data,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return result["exists"];
                }

                return false;
            }
        }

        public async Task<bool> RegistrarUsuarioAsync(RegistroDTO registro)
        {
            using (HttpClient client = new HttpClient())
            {
                string request = "api/auth/registro";
                client.BaseAddress = new Uri(this.urlApi);
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Accept.Add(this.header);

                string json = JsonConvert.SerializeObject(registro);
                StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(request, content);

                return response.IsSuccessStatusCode;
            }
        }

        public async Task<List<HistoriaExitoLandingDTO>> GetHistoriasExitoLanding()
        {
            string request = "api/auth/ObtenerHistoriasExitoLanding";
            List<HistoriaExitoLandingDTO> historias = await this.CallApiAsync<List<HistoriaExitoLandingDTO>>(request);
            return historias;
        }

        public async Task<string> GetFotoPerfilAdoptante(string token)
        {
            string request = "api/adoptante/ObtenerFotoPerfil";
            string foto = await this.CallApiAsync<string>(request, token);
            return foto;
        }

        public async Task<string> GetFotoPerfilRefugio(string token)
        {
            string request = "api/refugio/ObtenerFotoPerfil";
            string foto = await this.CallApiAsync<string>(request, token);
            return foto;
        }

        // Método correcto para obtener imágenes de adoptante
        public async Task<Stream> GetImagenAdoptanteAsync(string nombreImagen)
        {
            try
            {
                // Elimina el parámetro de versión si existe
                if (nombreImagen != null && nombreImagen.Contains("?v="))
                {
                    nombreImagen = nombreImagen.Split('?')[0];
                }
                // Usar el método que ya tienes para obtener el token
                string token = this.GetUserToken();
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("TOKEN VACÍO al intentar obtener imagen");
                }
                else
                {
                    Console.WriteLine($"TOKEN obtenido: {token.Substring(0, Math.Min(20, token.Length))}...");
                }

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.urlApi);
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(this.header);

                    if (!string.IsNullOrEmpty(token))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);
                    }

                    // Log para debug
                    Console.WriteLine($"Intentando obtener imagen: {nombreImagen} desde {this.urlApi}api/Adoptante/imagen/{nombreImagen}");

                    HttpResponseMessage response = await client.GetAsync($"api/adoptante/imagen/{nombreImagen}");

                    Console.WriteLine($"Respuesta API: {(int)response.StatusCode} {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStreamAsync();
                    }

                    // Obtener el mensaje de error detallado
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al obtener imagen: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excepción en GetImagenAdoptanteAsync: {ex.Message}");
                throw; // Reenviar la excepción para que se maneje en el controlador
            }
        }

        // Método correcto para obtener imágenes de refugio
        public async Task<Stream> GetImagenRefugioAsync(string nombreImagen)
        {
            try
            {
                // Usar el método que ya tienes para obtener el token
                string token = this.GetUserToken();
                if (string.IsNullOrEmpty(token))
                {
                    Console.WriteLine("TOKEN VACÍO al intentar obtener imagen");
                }
                else
                {
                    Console.WriteLine($"TOKEN obtenido: {token.Substring(0, Math.Min(20, token.Length))}...");
                }

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.urlApi);
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(this.header);

                    if (!string.IsNullOrEmpty(token))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);
                    }

                    // Log para debug
                    Console.WriteLine($"Intentando obtener imagen: {nombreImagen} desde {this.urlApi}api/Refugio/imagen/{nombreImagen}");

                    HttpResponseMessage response = await client.GetAsync($"api/Refugio/imagen/{nombreImagen}");

                    Console.WriteLine($"Respuesta API: {(int)response.StatusCode} {response.StatusCode}");

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStreamAsync();
                    }

                    // Obtener el mensaje de error detallado
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al obtener imagen: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excepción en GetImagenRefugioAsync: {ex.Message}");
                throw; // Reenviar la excepción para que se maneje en el controlador
            }
        }

        // Método para obtener imágenes de mascotas
        public async Task<Stream> GetImagenMascotaAsync(string nombreImagen)
        {
            try
            {
                // Elimina el parámetro de versión si existe
                if (nombreImagen != null && nombreImagen.Contains("?v="))
                {
                    nombreImagen = nombreImagen.Split('?')[0];
                }

                // Usar el método que ya tienes para obtener el token
                string token = this.GetUserToken();

                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri(this.urlApi);
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Accept.Add(this.header);

                    if (!string.IsNullOrEmpty(token))
                    {
                        client.DefaultRequestHeaders.Add("Authorization", "bearer " + token);
                    }

                    // Log para debug
                    Console.WriteLine($"Intentando obtener imagen de mascota: {nombreImagen}");

                    // Puedes elegir qué controlador usar basado en la lógica de tu aplicación
                    HttpResponseMessage response = await client.GetAsync($"api/adoptante/imagenMascota/{nombreImagen}");
                    // O si tienes un controlador específico para mascotas:
                    // HttpResponseMessage response = await client.GetAsync($"api/mascota/imagen/{nombreImagen}");

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStreamAsync();
                    }

                    // Obtener el mensaje de error detallado
                    string errorContent = await response.Content.ReadAsStringAsync();
                    throw new Exception($"Error al obtener imagen de mascota: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excepción en GetImagenMascotaAsync: {ex.Message}");
                throw; // Reenviar la excepción para que se maneje en el controlador
            }
        }

        public async Task<List<MascotaCard>> ObtenerMascotasDestacadasAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerMascotasDestacadas";
            List<MascotaCard> mascotas = await this.CallApiAsync<List<MascotaCard>>(request, token);
            return mascotas;
        }

        public async Task<List<HistoriaExito>> ObtenerHistoriasExitoAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerHistoriasExito";
            List<HistoriaExito> historias = await this.CallApiAsync<List<HistoriaExito>>(request, token);
            return historias;
        }

        public async Task<List<LikeHistoria>> ObtenerLikeHistoriaAsync(int idhistoria)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerLikesHistoria/" + idhistoria;
            List<LikeHistoria> likeshistorias = await this.CallApiAsync<List<LikeHistoria>>(request, token);
            return likeshistorias;
        }

        public async Task<List<Refugio>> ObtenerRefugiosAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerRefugios";
            List<Refugio> refugios = await this.CallApiAsync<List<Refugio>>(request, token);
            return refugios;
        }

        public async Task<Refugio> GetDetallesRefugioAsync(int idrefugio)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerDetallesRefugio/" + idrefugio;
            Refugio detalles = await this.CallApiAsync<Refugio>(request, token);
            return detalles;
        }

        public async Task<List<Mascota>> GetMascotasPorRefugioAsync(int idrefugio)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerMascotasRefugio/" + idrefugio;
            List<Mascota> mascotas = await this.CallApiAsync<List<Mascota>>(request, token);
            return mascotas;
        }

        public async Task<LikeHistoria> ObtenerLikeUsuarioHistoriaAsync(int idhistoria)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerLikeUsuarioHistoria/" + idhistoria;
            LikeHistoria likes = await this.CallApiAsync<LikeHistoria>(request, token);
            return likes;
        }

        public async Task<bool> EliminarLikeHistoriaAsync(int idhistoria)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/EliminarLikeHistoria/" + idhistoria;
            bool resultado = await this.CallApiAsync<bool>(request, token, "DELETE");
            return resultado;
        }

        public async Task<bool> CrearLikeHistoriaAsync(int idhistoria, string tipoReaccion)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/CrearLikeHistoria";

            LikeHistoriaDTO likeDTO = new LikeHistoriaDTO
            {
                IdHistoria = idhistoria,
                TipoReaccion = tipoReaccion
            };

            bool resultado = await this.CallApiAsync<bool>(request, token, "POST", likeDTO);
            return resultado;
        }

        public async Task<bool> ActualizarLikeHistoriaAsync(int idhistoria, string tipoReaccion)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ActualizarLikeHistoria";

            LikeHistoriaDTO likeDTO = new LikeHistoriaDTO
            {
                IdHistoria = idhistoria,
                TipoReaccion = tipoReaccion
            };

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT", likeDTO);
            return resultado;
        }

        public async Task<Dictionary<string, int>> ObtenerContadoresReaccionesAsync(int idhistoria)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerContadoresReacciones/" + idhistoria;
            Dictionary<string, int> contadores = await this.CallApiAsync<Dictionary<string, int>>(request, token);
            return contadores;
        }

        public async Task<VistaPerfilAdoptante> GetPerfilAdoptante()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerPerfilAdoptante";
            VistaPerfilAdoptante contadores = await this.CallApiAsync<VistaPerfilAdoptante>(request, token);
            return contadores;
        }

        public async Task<List<MascotaCard>> ObtenerMascotasFavoritas()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerMascotasFavoritas";
            List<MascotaCard> favoritas = await this.CallApiAsync<List<MascotaCard>>(request, token);
            return favoritas;
        }

        public async Task<List<MascotaAdoptada>> ObtenerMascotasAdoptadas()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerMascotasAdoptadas";
            List<MascotaAdoptada> adoptadas = await this.CallApiAsync<List<MascotaAdoptada>>(request, token);
            return adoptadas;
        }

        public async Task<List<MascotaCard>> ObtenerMascotasAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerMascotas";
            List<MascotaCard> mascotas = await this.CallApiAsync<List<MascotaCard>>(request, token);
            return mascotas;
        }

        public async Task<DateTime?> ObtenerUltimaAccionFavorito(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerUltimaAccionFavorito/" + idmascota;
            DateTime? ultimaaccion = await this.CallApiAsync<DateTime?>(request, token);
            return ultimaaccion;
        }

        public async Task<bool> EsFavorito(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerEsFavorito/" + idmascota;
            bool esfavorito = await this.CallApiAsync<bool>(request, token);
            return esfavorito;
        }

        public async Task<bool> EliminarFavorito(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/EliminarFavorito/" + idmascota;
            bool resultado = await this.CallApiAsync<bool>(request, token, "DELETE");
            return resultado;
        }

        public async Task<bool> InsertMascotaFavorita(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/CrearMascotaFavorita/" + idmascota;
            bool resultado = await this.CallApiAsync<bool>(request, token, "POST");
            return resultado;
        }

        public async Task<Mascota> GetDetallesMascotaAsync(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerDetallesMascota/" + idmascota;
            Mascota detalles = await this.CallApiAsync<Mascota>(request, token);
            return detalles;
        }

        public async Task<bool> ExisteSolicitudAdopcionAsync(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerExisteSolicitudAdopcion/" + idmascota;
            bool existe = await this.CallApiAsync<bool>(request, token);
            return existe;
        }

        public async Task<SolicitudAdopcion> CrearSolicitudAdopcionAsync(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/CrearSolicitudAdopcion/" + idmascota;
            SolicitudAdopcion resultado = await this.CallApiAsync<SolicitudAdopcion>(request, token, "POST");
            return resultado;
        }

        public async Task<string> GetNombreMascotaAsync(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerNombreMascota/" + idmascota;
            string nombre = await this.CallApiAsync<string>(request, token);
            return nombre;
        }

        public async Task<int?> IdRefugioPorMascotaAsync(int idmascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerIdRefugioPorMascota/" + idmascota;
            int? idrefugio = await this.CallApiAsync<int?>(request, token);
            return idrefugio;
        }

        public async Task<bool> CrearNotificacionAsync(int idsolicitud, int idrefugio, string nombremascota)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/CrearNotificacion";

            NotificacionCreacionDTO notificacion = new NotificacionCreacionDTO
            {
                IdSolicitud = idsolicitud,
                IdRefugio = idrefugio,
                NombreMascota = nombremascota
            };

            bool resultado = await this.CallApiAsync<bool>(request, token, "POST", notificacion);
            return resultado;
        }

        public async Task<List<Notificacion>> GetNotificacionesUsuarioAsync(int pagina = 1, int tamanopagina = 10)
        {
            if (pagina <= 0)
            {
                throw new ArgumentException("El número de página debe ser mayor que cero");
            }

            if (tamanopagina <= 0)
            {
                throw new ArgumentException("El tamaño de página debe ser mayor que cero");
            }

            string token = this.GetUserToken();
            string request = $"api/adoptante/ObtenerNotificacionesUsuario?pagina={pagina}&tamanopagina={tamanopagina}";

            List<Notificacion> notificaciones = await this.CallApiAsync<List<Notificacion>>(request, token);
            return notificaciones;
        }

        public async Task<int> GetTotalNotificacionesUsuarioAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerTotalNotificacionesUsuario";
            int total = await this.CallApiAsync<int>(request, token);
            return total;
        }

        public async Task<int> GetTotalNotificacionesNoLeidasAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerTotalNotificacionesNoLeidas";
            int total = await this.CallApiAsync<int>(request, token);
            return total;
        }

        public async Task<bool> HayNotificacionesNuevasDesdeAsync(DateTime desde)
        {
            string token = this.GetUserToken();
            string fechaFormateada = desde.ToString("o");
            string request = $"api/adoptante/ObtenerHayNotificacionesNuevasDesde?desde={Uri.EscapeDataString(fechaFormateada)}";

            bool hayNotificacionesNuevas = await this.CallApiAsync<bool>(request, token);
            return hayNotificacionesNuevas;
        }

        public async Task<bool> MarcarNotificacionComoLeidaAsync(int idnotificacion)
        {
            string token = this.GetUserToken();
            string request = $"api/adoptante/ActualizarMarcarNotificacionComoLeida/{idnotificacion}";

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT");
            return resultado;
        }

        public async Task<bool> MarcarTodasNotificacionesComoLeidasAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ActualizarMarcarTodasNotificacionesComoLeidas";

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT");
            return resultado;
        }

        public async Task<bool> EliminarNotificacionAsync(int idnotificacion)
        {
            string token = this.GetUserToken();
            string request = $"api/adoptante/EliminarNotificacion/{idnotificacion}";

            bool resultado = await this.CallApiAsync<bool>(request, token, "DELETE");
            return resultado;
        }

        public async Task<List<Mascota>> GetMascotasAdoptadasSinHistoria()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerMascotasAdoptadasSinHistoria";
            List<Mascota> mascotas = await this.CallApiAsync<List<Mascota>>(request, token);
            return mascotas;
        }

        public async Task<Adoptante> GetAdoptanteByUsuarioId()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerAdoptanteByUsuarioId";
            Adoptante adoptante = await this.CallApiAsync<Adoptante>(request, token);
            return adoptante;
        }

        public async Task<bool> CrearHistoriaExito(HistoriaExito historiaexito)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/CrearHistoriaExito";
            bool resultado = await this.CallApiAsync<bool>(request, token, "POST", historiaexito);
            return resultado;
        }

        public async Task<List<ConversacionViewModel>> GetConversacionesAdoptanteAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerConversacionesAdoptante";
            List<ConversacionViewModel> conversaciones = await this.CallApiAsync<List<ConversacionViewModel>>(request, token);
            return conversaciones;
        }

        public async Task<List<Mensaje>> GetMensajesConversacionAsync(int idotrousuario)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerMensajesConversacion/" + idotrousuario;
            List<Mensaje> mensajes = await this.CallApiAsync<List<Mensaje>>(request, token);
            return mensajes;
        }

        public async Task<Mensaje> AgregarMensajeAsync(int idreceptor, string contenido)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/CrearMensaje";

            MensajeCreacionDTO mensaje = new MensajeCreacionDTO
            {
                IdReceptor = idreceptor,
                Contenido = contenido
            };

            Mensaje m = await this.CallApiAsync<Mensaje>(request, token, "POST", mensaje);
            return m;
        }

        public async Task<bool> ActualizarDescripcionAdoptante(string descripcion)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ActualizarDescripcionAdoptante";

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT", descripcion);
            return resultado;
        }

        public async Task<bool> ActualizarDetallesAdoptante(VistaPerfilAdoptante modelo)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ActualizarDetallesAdoptante";

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT", modelo);
            return resultado;
        }

        public async Task<bool> ActualizarPerfilAdoptante(string email, string nombre)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ActualizarPerfilAdoptante";

            PerfilAdoptanteDTO datos = new PerfilAdoptanteDTO
            {
                Email = email,
                Nombre = nombre
            };

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT", datos);
            return resultado;
        }

        public async Task<bool> ActualizarFotoPerfilAdoptante(string nombreArchivo)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ActualizarFotoPerfil";

            FotoPerfilDTO datos = new FotoPerfilDTO
            {
                NombreArchivo = nombreArchivo
            };

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT", datos);
            return resultado;
        }

        public async Task<bool> IncrementarVistasMascota(int idmascota)
        {
            string token = this.GetUserToken();
            string request = $"api/adoptante/ActualizarVistasMascota/{idmascota}";

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT");
            return resultado;
        }

        public async Task<bool> MarcarMensajesComoLeidosAsync(int idotrousuario)
        {
            string token = this.GetUserToken();
            string request = $"api/adoptante/ActualizarMensajesComoLeidos/{idotrousuario}";

            bool resultado = await this.CallApiAsync<bool>(request, token, "PUT");
            return resultado;
        }

        public async Task<Adoptante> GetAdoptanteByUsuarioIdAsync()
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerAdoptanteByUsuarioIdAsync";

            Adoptante adoptante = await this.CallApiAsync<Adoptante>(request, token);
            return adoptante;
        }

        public async Task<Refugio> GetRefugioChatByIdAsync(int idrefugio)
        {
            string token = this.GetUserToken();
            string request = $"api/adoptante/ObtenerRefugioChatById/{idrefugio}";

            Refugio refugio = await this.CallApiAsync<Refugio>(request, token);
            return refugio;
        }

        public async Task<Refugio> GetRefugioChatDosByIdAsync(int idusuariorefugio)
        {
            string token = this.GetUserToken();
            string request = "api/adoptante/ObtenerRefugioChatDosById/" + idusuariorefugio;

            Refugio refugio = await this.CallApiAsync<Refugio>(request, token);
            return refugio;
        }
    }
}
