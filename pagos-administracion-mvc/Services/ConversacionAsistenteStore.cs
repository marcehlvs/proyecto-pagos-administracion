using System.Collections.Concurrent;

namespace pagos_administracion_mvc.Services
{
    public class ConversacionAsistenteStore
    {
        // Diccionario seguro para concurrencia de hilos, mapea el UserId con su lista de turnos (strings JSON)
        private readonly ConcurrentDictionary<string, List<List<string>>> _sesiones = new();

        public List<List<string>> Obtener(string userId)
        {
            return _sesiones.GetOrAdd(userId, _ => new List<List<string>>());
        }

        public void AgregarTurno(string userId, List<string> turnosMensajes)
        {
            var sesion = Obtener(userId);
            lock (sesion)
            {
                sesion.Add(turnosMensajes);

                // Opcional: Limitamos a los últimos 10 turnos para evitar consumo excesivo de memoria o tokens
                if (sesion.Count > 10)
                {
                    sesion.RemoveAt(0);
                }
            }
        }

        public void Reiniciar(string userId)
        {
            _sesiones.TryRemove(userId, out _);
        }
    }
}