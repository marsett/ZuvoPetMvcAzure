using System.Security.Cryptography;
using System.Text;

namespace ZuvoPetMvcAzure.Helpers
{
    public class HelperCriptography
    {
        public static string GenerateSalt()
        {
            //Random random = new Random();
            //string salt = "";

            //for (int i = 1; i <= 50; i++)
            //{
            //    int aleat = random.Next(1, 255);
            //    char letra = Convert.ToChar(aleat);
            //    salt += letra;
            //}
            //return salt;
            byte[] saltBytes = new byte[32]; // 32 bytes = 256 bits de seguridad
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(saltBytes);
            }
            return Convert.ToBase64String(saltBytes); // Devuelve un Salt en Base64
        }
        public static bool CompararArrays(byte[] a, byte[] b)
        {
            bool iguales = true;
            if (a.Length != b.Length)
            {
                iguales = false;
            }
            else
            {
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i].Equals(b[i]) == false)
                    {
                        iguales = false;
                        break;
                    }
                }
            }
            return iguales;
        }

        public static byte[] EncryptPassword(string password, string salt)
        {
            string contenido = password + salt;
            SHA512 managed = SHA512.Create();

            byte[] salida = Encoding.UTF8.GetBytes(contenido);

            for (int i = 1; i <= 15; i++)
            {
                salida = managed.ComputeHash(salida);
            }
            managed.Clear();
            return salida;
        }
    }
}
