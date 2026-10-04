using System;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace FriendRivals.BeatLeader
{
    /// <summary>
    /// Доступ к сессии, которую уже открыл плагин BeatLeader.
    /// BeatLeader логинится через Steam/Oculus тикет и хранит auth-cookie в статическом
    /// CookieContainer внутри WebRequestFactory. Мы переиспользуем этот же контейнер,
    /// поэтому запросы от нашего плагина идут от имени уже авторизованного пользователя.
    /// </summary>
    internal static class BeatLeaderSession
    {
        private const string DefaultApiUrl = "https://api.beatleader.com";

        private static Assembly _assembly;

        private static Assembly BeatLeaderAssembly =>
            _assembly ??= AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "BeatLeader");

        private static Type FindType(string fullName) => BeatLeaderAssembly?.GetType(fullName, false);

        /// <summary>Адрес API с учётом выбранного в BeatLeader сервера (.com / .xyz).</summary>
        public static string ApiUrl
        {
            get
            {
                try
                {
                    var property = FindType("BeatLeader.Utils.BLConstants")
                        ?.GetProperty("BEATLEADER_API_URL", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    if (property?.GetValue(null) is string url && !string.IsNullOrEmpty(url))
                        return url.TrimEnd('/');
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn($"Не удалось получить адрес API из BeatLeader: {ex.Message}");
                }
                return DefaultApiUrl;
            }
        }

        public static CookieContainer GetCookieContainer()
        {
            var field = FindType("BeatLeader.WebRequests.WebRequestFactory")
                ?.GetField("CookieContainer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
                throw new InvalidOperationException("Плагин BeatLeader не найден или его версия не поддерживается.");

            return field.GetValue(null) as CookieContainer
                   ?? throw new InvalidOperationException("BeatLeader ещё не инициализировал сессию.");
        }

        /// <summary>
        /// Ждёт завершения входа BeatLeader (Authentication.WaitLogin).
        /// Возвращает false, если вход не завершился за отведённое время.
        /// </summary>
        public static async Task<bool> WaitForLoginAsync(TimeSpan timeout)
        {
            Task<bool> loginTask;
            try
            {
                var method = FindType("BeatLeader.API.Authentication")
                    ?.GetMethod("WaitLogin", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                loginTask = method?.Invoke(null, null) as Task<bool>;
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn($"Не удалось дождаться входа BeatLeader: {ex.Message}");
                return false;
            }

            if (loginTask == null)
                return false;

            var finished = await Task.WhenAny(loginTask, Task.Delay(timeout));
            return finished == loginTask && loginTask.Status == TaskStatus.RanToCompletion && loginTask.Result;
        }
    }
}
