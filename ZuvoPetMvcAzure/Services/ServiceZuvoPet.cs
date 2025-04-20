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
    }
}
