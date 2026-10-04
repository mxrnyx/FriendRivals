using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace FriendRivals.BeatLeader
{
    internal class BeatLeaderClient : IDisposable
    {
        private const int PageSize = 100; // максимум, который принимает API
        private const int MaxParallelPages = 4;

        private HttpClient _http;

        private HttpClient Http
        {
            get
            {
                if (_http != null)
                    return _http;

                var handler = new HttpClientHandler
                {
                    CookieContainer = BeatLeaderSession.GetCookieContainer(),
                    UseCookies = true,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
                _http.DefaultRequestHeaders.UserAgent.ParseAdd("FriendRivals/1.0");
                return _http;
            }
        }

        public async Task<CurrentUser> GetCurrentUserAsync(CancellationToken token)
        {
            if (!await BeatLeaderSession.WaitForLoginAsync(TimeSpan.FromSeconds(20)))
                Plugin.Log.Warn("BeatLeader не подтвердил вход, пробуем запрос всё равно.");

            using var response = await Http.GetAsync($"{BeatLeaderSession.ApiUrl}/user", token);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("BeatLeader не авторизован. Войдите в BeatLeader в игре и попробуйте снова.");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var user = JsonConvert.DeserializeObject<CurrentUser>(json);
            if (user?.Player?.Id == null)
                throw new InvalidOperationException("BeatLeader вернул пустой профиль.");
            user.Friends ??= new List<PlayerInfo>();
            return user;
        }

        /// <summary>Загружает все лучшие скоры игрока (по одному на лидерборд).</summary>
        public async Task<List<CompactScoreEntry>> GetAllScoresAsync(string playerId, Action<int, int> onProgress, CancellationToken token)
        {
            var first = await GetScoresPageAsync(playerId, 1, token);
            var result = new List<CompactScoreEntry>(first.Data);
            var total = first.Metadata?.Total ?? result.Count;
            onProgress?.Invoke(result.Count, total);

            var pageCount = (int)Math.Ceiling(total / (double)PageSize);
            using var throttle = new SemaphoreSlim(MaxParallelPages);
            var pageTasks = Enumerable.Range(2, Math.Max(0, pageCount - 1)).Select(async page =>
            {
                await throttle.WaitAsync(token);
                try
                {
                    return await GetScoresPageAsync(playerId, page, token);
                }
                finally
                {
                    throttle.Release();
                }
            }).ToList();

            foreach (var task in pageTasks)
            {
                var page = await task;
                result.AddRange(page.Data);
                onProgress?.Invoke(result.Count, total);
            }

            // Пока мы листали страницы, игрок мог поставить новый скор и сдвинуть пагинацию.
            return result
                .Where(e => e?.Leaderboard?.Id != null && e.Score != null)
                .GroupBy(e => e.Leaderboard.Id)
                .Select(g => g.OrderByDescending(e => e.Score.ModifiedScore).First())
                .ToList();
        }

        private async Task<CompactScoresPage> GetScoresPageAsync(string playerId, int page, CancellationToken token)
        {
            var url = $"{BeatLeaderSession.ApiUrl}/player/{Uri.EscapeDataString(playerId)}/scores/compact" +
                      $"?page={page}&count={PageSize}&sortBy=date&order=desc";

            for (var attempt = 1; ; attempt++)
            {
                using var response = await Http.GetAsync(url, token);
                if ((int)response.StatusCode == 429 && attempt < 4)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2 * attempt), token);
                    continue;
                }
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<CompactScoresPage>(json) ?? new CompactScoresPage();
            }
        }

        public async Task<byte[]> DownloadBytesAsync(string url, CancellationToken token)
        {
            using var response = await Http.GetAsync(url, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        public void Dispose()
        {
            _http?.Dispose();
            _http = null;
        }
    }
}
