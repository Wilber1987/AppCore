using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace APPCORE.Services
{
    /// <summary>
    /// Servicio de caché de sesión persistente usando SQL Server.
    /// Mantiene la misma API estática para compatibilidad con código existente.
    /// </summary>
    public static class SessionServices
    {
        // 🧹 Timer en segundo plano para limpiar sesiones expiradas
        private static readonly System.Threading.Timer _cleanupTimer;

        // Constructor estático: se ejecuta una sola vez al iniciar la aplicación
        static SessionServices()
        {
            // Ejecuta ClearExpiredSessions cada 10 minutos en segundo plano
            _cleanupTimer = new System.Threading.Timer(
                callback: async _ => await ClearExpiredSessionsAsync(),
                state: null,
                dueTime: TimeSpan.FromMinutes(10),
                period: TimeSpan.FromMinutes(10)
            );
        }

        /// <summary>
        /// Guarda un valor en caché (persistente en SQL Server).
        /// </summary>
        public static void Set(string key, object value, string sessionKey, int expirationMinutes = 720)
        {
            if (string.IsNullOrEmpty(sessionKey)) return;

            try
            {
                // Intentar encontrar una sesión existente
                var existing = new SessionData()
                {
                    KeyName = key,
                    idetify = sessionKey
                }.Find<SessionData>();

                if (existing != null)
                {
                    // Actualizar sesión existente
                    existing.Value = JsonSerializer.Serialize(value);
                    existing.ExpireTime = DateTime.UtcNow.AddMinutes(expirationMinutes);
                    existing.created = DateTime.UtcNow;
                    existing.Update();
                }
                else
                {
                    // Crear nueva sesión
                    var newSession = new SessionData()
                    {
                        KeyName = key,
                        Value = JsonSerializer.Serialize(value),
                        idetify = sessionKey,
                        created = DateTime.UtcNow,
                        ExpireTime = DateTime.UtcNow.AddMinutes(expirationMinutes)
                    };
                    newSession.Save();
                }
            }
            catch (Exception ex)
            {
                LoggerServices.AddMessageError($"Error guardando sesión: {key}", ex);
            }
        }

        /// <summary>
        /// Obtiene un valor del caché (desde SQL Server).
        /// </summary>
        public static T? Get<T>(string key, string? sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) return default;

            try
            {
                var session = new SessionData()
                {
                    KeyName = key,
                    idetify = sessionKey
                }.Find<SessionData>();

                if (session != null)
                {
                    // Validar si expiró
                    if (session.ExpireTime > DateTime.UtcNow)
                    {
                        return JsonSerializer.Deserialize<T>(session.Value ?? "{}");
                    }
                    
                    // Si expiró, lo eliminamos
                    session.Delete(true);
                }
            }
            catch (Exception ex)
            {
                LoggerServices.AddMessageError($"Error obteniendo sesión: {key}", ex);
            }

            return default;
        }

        /// <summary>
        /// Limpia todos los datos asociados a una sesión específica.
        /// </summary>
        public static void ClearSeason(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) return;

            try
            {
                // Buscar todas las sesiones de este usuario
                var sessions = new SessionData().Where<SessionData>(
                    FilterData.Equal("idetify", sessionKey)
                );

                // Eliminar cada sesión
                foreach (var session in sessions)
                {
                    session.Delete(true);
                }
            }
            catch (Exception ex)
            {
                LoggerServices.AddMessageError($"Error limpiando sesión: {sessionKey}", ex);
            }
        }

        /// <summary>
        /// Limpia sesiones expiradas automáticamente (llamado por el Timer cada 10 min).
        /// </summary>
        public static async Task ClearExpiredSessionsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;
                
                // Buscar todas las sesiones expiradas
                var expiredSessions = new SessionData().Where<SessionData>(
                    FilterData.LessEqual("ExpireTime", now.ToString("yyyy-MM-dd HH:mm:ss"))
                );

                // Eliminar cada sesión expirada
                foreach (var session in expiredSessions)
                {
                    session.Delete(true);
                }

                if (expiredSessions.Count > 0)
                {
                    LoggerServices.AddMessageInfo($"Limpieza de sesiones: {expiredSessions.Count} sesiones expiradas eliminadas");
                }
            }
            catch (Exception ex)
            {
                LoggerServices.AddMessageError("Error limpiando sesiones expiradas", ex);
            }
        }

        /// <summary>
        /// Versión síncrona de ClearExpiredSessions (para compatibilidad).
        /// </summary>
        public static void ClearExpiredSessions()
        {
            ClearExpiredSessionsAsync().GetAwaiter().GetResult();
        }
    }
}