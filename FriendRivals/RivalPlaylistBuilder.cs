using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberPlaylistsLib;
using BeatSaberPlaylistsLib.Types;
using FriendRivals.BeatLeader;

namespace FriendRivals
{
    /// <summary>Карта (сложность), на которой друг обыграл игрока.</summary>
    internal class LostMap
    {
        public string SongHash { get; set; }
        public string Characteristic { get; set; }
        public string Difficulty { get; set; }
        public float MyAccuracy { get; set; }
        public float FriendAccuracy { get; set; }
        /// <summary>Когда друг поставил этот скор (unix time).</summary>
        public long FriendTimestamp { get; set; }
        public float Gap => FriendAccuracy - MyAccuracy;
    }

    internal class PlaylistUpdateResult
    {
        public IPlaylist Playlist { get; set; }
        public int Added { get; set; }
        public int Removed { get; set; }
        public int InPlaylist { get; set; }
        public int TotalLostSongs { get; set; }
        /// <summary>Сколько проигранных песен ещё не попало в плейлист.</summary>
        public int Remaining { get; set; }
    }

    internal class RivalPlaylistBuilder
    {
        private const string PlaylistAuthor = "FriendRivals";

        /// <summary>
        /// Находит общие сыгранные лидерборды и возвращает те, где точность друга выше.
        /// Точность BeatLeader уже учитывает множитель модификаторов, поэтому это то же,
        /// что сравнение позиций на лидерборде.
        /// </summary>
        public List<LostMap> FindLostMaps(IEnumerable<CompactScoreEntry> myScores, IEnumerable<CompactScoreEntry> friendScores, out int commonCount)
        {
            var mine = myScores.ToDictionary(s => s.Leaderboard.Id);
            var lost = new List<LostMap>();
            commonCount = 0;

            foreach (var theirs in friendScores)
            {
                if (!mine.TryGetValue(theirs.Leaderboard.Id, out var my))
                    continue;

                commonCount++;
                if (theirs.Score.Accuracy <= my.Score.Accuracy || string.IsNullOrEmpty(theirs.Leaderboard.SongHash))
                    continue;

                lost.Add(new LostMap
                {
                    SongHash = theirs.Leaderboard.SongHash.ToUpperInvariant(),
                    Characteristic = string.IsNullOrEmpty(theirs.Leaderboard.ModeName) ? "Standard" : theirs.Leaderboard.ModeName,
                    Difficulty = DifficultyName(theirs.Leaderboard.Difficulty),
                    MyAccuracy = my.Score.Accuracy,
                    FriendAccuracy = theirs.Score.Accuracy,
                    FriendTimestamp = theirs.Score.EpochTime
                });
            }

            return lost;
        }

        /// <summary>
        /// Обновляет плейлист друга, не пересоздавая его:
        /// убирает песни, на которых вы уже обогнали друга, и добавляет до <paramref name="songLimit"/>
        /// новых песен, которых ещё нет в плейлисте, начиная с самых свежих. Вызывать из главного потока.
        /// </summary>
        public PlaylistUpdateResult UpdatePlaylist(PlayerInfo friend, IReadOnlyList<LostMap> lostMaps, int songLimit, int commonCount, byte[] cover)
        {
            var manager = PlaylistManager.DefaultManager;
            var fileName = $"FriendRivals_{friend.Id}";
            var title = $"{friend.Name} обыграл меня";

            if (!manager.TryGetPlaylist(fileName, out var playlist))
                playlist = manager.CreatePlaylist(fileName, title, PlaylistAuthor, (string)null);

            playlist.Title = title;
            playlist.Author = PlaylistAuthor;
            playlist.AllowDuplicates = false;
            if (cover != null && cover.Length > 0)
            {
                try
                {
                    playlist.SetCover(cover);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn($"Не удалось установить обложку плейлиста: {ex.Message}");
                }
            }

            // Песни, где друг вас больше не обыгрывает, из плейлиста убираем.
            var lostHashes = new HashSet<string>(lostMaps.Select(m => m.SongHash));
            var removed = playlist.RemoveAll(s => s.Hash == null || !lostHashes.Contains(s.Hash.ToUpperInvariant()));

            // Новые песни: самые свежие (по дате скора друга), которых ещё нет в плейлисте.
            var existing = new HashSet<string>(playlist.Where(s => s.Hash != null).Select(s => s.Hash.ToUpperInvariant()));
            var newSongs = lostMaps
                .GroupBy(m => m.SongHash)
                .Where(song => !existing.Contains(song.Key))
                .OrderByDescending(song => song.Max(m => m.FriendTimestamp))
                .Take(Math.Max(1, songLimit))
                .ToList();

            // Одна песня — одна запись, все проигранные сложности внутри неё.
            foreach (var song in newSongs)
            {
                var level = SongCore.Loader.GetLevelByHash(song.Key);
                var mapper = level?.allMappers != null ? string.Join(", ", level.allMappers) : null;
                var entry = playlist.Add(song.Key, level?.songName, null, mapper);
                if (entry == null)
                    continue;

                foreach (var diff in song)
                    entry.AddDifficulty(new Difficulty { Characteristic = diff.Characteristic, Name = diff.Difficulty });

                var biggestGap = song.OrderByDescending(m => m.Gap).First();
                entry.SetCustomData("friendRivals", new Dictionary<string, object>
                {
                    ["myAccuracy"] = Math.Round(biggestGap.MyAccuracy * 100, 2),
                    ["friendAccuracy"] = Math.Round(biggestGap.FriendAccuracy * 100, 2)
                });
            }

            var result = new PlaylistUpdateResult
            {
                Playlist = playlist,
                Added = newSongs.Count,
                Removed = removed,
                InPlaylist = playlist.Count,
                TotalLostSongs = lostHashes.Count,
                Remaining = lostHashes.Count - playlist.Count
            };

            playlist.Description =
                $"Карты, где {friend.Name} обыграл вас на BeatLeader.\n" +
                $"Общих сыгранных сложностей: {commonCount}, проиграно песен: {result.TotalLostSongs}.\n" +
                $"Ещё не в плейлисте: {result.Remaining}.\n" +
                $"Обновлено: {DateTime.Now:dd.MM.yyyy HH:mm}";

            manager.StorePlaylist(playlist);
            manager.RequestRefresh(PlaylistAuthor);
            return result;
        }

        private static string DifficultyName(int difficulty) => difficulty switch
        {
            1 => "Easy",
            3 => "Normal",
            5 => "Hard",
            7 => "Expert",
            9 => "ExpertPlus",
            _ => "ExpertPlus"
        };
    }
}
