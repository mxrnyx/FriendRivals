using System.Collections.Generic;
using Newtonsoft.Json;

namespace FriendRivals.BeatLeader
{
    /// <summary>Ответ GET /user (текущий авторизованный пользователь).</summary>
    internal class CurrentUser
    {
        [JsonProperty("player")] public PlayerInfo Player { get; set; }
        [JsonProperty("friends")] public List<PlayerInfo> Friends { get; set; } = new List<PlayerInfo>();
    }

    internal class PlayerInfo
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("avatar")] public string Avatar { get; set; }
        [JsonProperty("country")] public string Country { get; set; }
        [JsonProperty("pp")] public float Pp { get; set; }
        [JsonProperty("rank")] public int Rank { get; set; }
        [JsonProperty("countryRank")] public int CountryRank { get; set; }
    }

    /// <summary>Ответ GET /player/{id}/scores/compact.</summary>
    internal class CompactScoresPage
    {
        [JsonProperty("metadata")] public PageMetadata Metadata { get; set; }
        [JsonProperty("data")] public List<CompactScoreEntry> Data { get; set; } = new List<CompactScoreEntry>();
    }

    internal class PageMetadata
    {
        [JsonProperty("itemsPerPage")] public int ItemsPerPage { get; set; }
        [JsonProperty("page")] public int Page { get; set; }
        [JsonProperty("total")] public int Total { get; set; }
    }

    internal class CompactScoreEntry
    {
        [JsonProperty("score")] public CompactScore Score { get; set; }
        [JsonProperty("leaderboard")] public CompactLeaderboard Leaderboard { get; set; }
    }

    internal class CompactScore
    {
        [JsonProperty("id")] public long Id { get; set; }
        [JsonProperty("modifiedScore")] public int ModifiedScore { get; set; }
        [JsonProperty("accuracy")] public float Accuracy { get; set; }
        [JsonProperty("pp")] public float Pp { get; set; }
        [JsonProperty("modifiers")] public string Modifiers { get; set; }
        [JsonProperty("fullCombo")] public bool FullCombo { get; set; }
        [JsonProperty("epochTime")] public long EpochTime { get; set; }
    }

    internal class CompactLeaderboard
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("songHash")] public string SongHash { get; set; }
        [JsonProperty("modeName")] public string ModeName { get; set; }
        [JsonProperty("difficulty")] public int Difficulty { get; set; }
    }
}
