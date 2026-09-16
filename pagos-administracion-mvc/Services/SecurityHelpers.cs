using System.Security.Cryptography;

namespace pagos_administracion_mvc.Services
{
    /// <summary>
    /// Utilidades de seguridad compartidas entre controllers y servicios.
    /// Centraliza la generación de contraseñas temporales para evitar duplicación
    /// entre FamiliasController y AlumnosController.
    /// </summary>
    public static class SecurityHelpers
    {
        /// <summary>
        /// Genera una contraseña temporal aleatoria y criptográficamente segura de 10 caracteres.
        /// Garantiza al menos una mayúscula, una minúscula, un número y un símbolo,
        /// luego mezcla los caracteres con Fisher-Yates para que las posiciones no sean predecibles.
        /// Se excluyen I/O de mayúsculas y l de minúsculas para evitar confusión visual.
        /// </summary>
        public static string GenerarPasswordTemporal()
        {
            const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ"; // sin I/O
            const string minusculas = "abcdefghijkmnpqrstuvwxyz"; // sin l
            const string numeros    = "23456789";                  // sin 0/1
            const string simbolos   = "!@#$%&*";
            const string todos      = mayusculas + minusculas + numeros + simbolos;

            Span<char> clave = stackalloc char[10];
            // Garantizamos al menos un carácter de cada clase
            clave[0] = mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)];
            clave[1] = minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)];
            clave[2] = numeros[RandomNumberGenerator.GetInt32(numeros.Length)];
            clave[3] = simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)];
            for (int i = 4; i < clave.Length; i++)
                clave[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];

            // Fisher-Yates shuffle para que las posiciones fijas de arriba no sean predecibles
            for (int i = clave.Length - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (clave[i], clave[j]) = (clave[j], clave[i]);
            }

            return new string(clave);
        }
    }
}
