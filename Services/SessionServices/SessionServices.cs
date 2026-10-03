using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace APPCORE.Services
{
    /// <summary>
    /// Servicio de caché de sesión 100% estático, Thread-Safe y sin Inyección de Dependencias.
    /// </summary>
    public static class SessionServices
    {
        // 🛡️ ConcurrentDictionary reemplaza a List<T>. Es Thread-Safe y las búsquedas son O(1) (instantáneas).
        private static readonly ConcurrentDictionary<string, SessionCacheItem> _cache = new();
        
        // 🧹 Timer en segundo plano para limpiar sesiones expiradas y evitar Memory Leaks.
        private static readonly Timer _cleanupTimer;

        // Constructor estático: se ejecuta una sola vez al iniciar la aplicación.
        static SessionServices()
        {
            // Ejecuta ClearExpiredSessions cada 5 minutos en segundo plano
            _cleanupTimer = new Timer(
                callback: _ => ClearExpiredSessions(), 
                state: null, 
                dueTime: TimeSpan.FromMinutes(5), 
                period: TimeSpan.FromMinutes(5)
            );
        }

        /// <summary>
        /// Guarda un valor en caché.
        /// </summary>
        public static void Set(string key, object value, string sessionKey, int expirationMinutes = 120)
        {
            if (string.IsNullOrEmpty(sessionKey)) return;

            string cacheKey = BuildKey(key, sessionKey);
            var item = new SessionCacheItem
            {
                JsonValue = JsonSerializer.Serialize(value),
                ExpireTime = DateTime.UtcNow.AddMinutes(expirationMinutes)
            };
            
            // AddOrUpdate es atómico y Thread-Safe
            _cache.AddOrUpdate(cacheKey, item, (k, oldValue) => item);
        }

        /// <summary>
        /// Obtiene un valor del caché.
        /// </summary>
        public static T? Get<T>(string key, string? sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) return default;

            string cacheKey = BuildKey(key, sessionKey);

            if (_cache.TryGetValue(cacheKey, out var item))
            {
                // Validar si expiró
                if (item.ExpireTime > DateTime.UtcNow)
                {
                    return JsonSerializer.Deserialize<T>(item.JsonValue);
                }
                
                // Si expiró, lo eliminamos al vuelo para liberar memoria
                _cache.TryRemove(cacheKey, out _);
            }

            return default;
        }

        /// <summary>
        /// Limpia todos los datos asociados a una sesión específica.
        /// </summary>
        public static void ClearSeason(string sessionKey)
        {
            if (string.IsNullOrEmpty(sessionKey)) return;

            // Buscamos todas las keys que pertenezcan a esta sesión y las eliminamos
            string prefix = $"{sessionKey}::";
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith(prefix)).ToList();
            
            foreach (var key in keysToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Limpia sesiones expiradas automáticamente (llamado por el Timer cada 5 min).
        /// </summary>
        public static void ClearExpiredSessions()
        {
            var now = DateTime.UtcNow;
            var expiredKeys = _cache.Where(kvp => kvp.Value.ExpireTime <= now).Select(kvp => kvp.Key).ToList();
            
            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }
        }

        /// <summary>
        /// Construye la clave única combinando sesión y clave.
        /// </summary>
        private static string BuildKey(string key, string sessionKey)
        {
            return $"{sessionKey}::{key}";
        }

        /// <summary>
        /// Clase interna para almacenar el valor y su tiempo de expiración.
        /// </summary>
        private class SessionCacheItem
        {
            public string JsonValue { get; set; } = string.Empty;
            public DateTime ExpireTime { get; set; }
        }
    }
}