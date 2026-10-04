using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.ViewControllers;
using FriendRivals.BeatLeader;
using HMUI;
using UnityEngine;
using Zenject;

namespace FriendRivals.UI
{
    /// <summary>Центральный экран: список друзей с аватарками и местами в рейтинге.</summary>
    [ViewDefinition("FriendRivals.UI.FriendList.bsml")]
    internal class FriendListViewController : BSMLAutomaticViewController
    {
        private const int MaxParallelAvatars = 4;

        [Inject] private readonly BeatLeaderClient _client = null;

        [UIComponent("friend-list")] private readonly CustomListTableData _friendList = null;

        private readonly List<PlayerInfo> _friends = new List<PlayerInfo>();
        private readonly Dictionary<string, Sprite> _avatarCache = new Dictionary<string, Sprite>();
        private CancellationTokenSource _avatarsCts;
        private bool _parsed;

        public event Action<PlayerInfo> FriendSelected;

        [UIAction("#post-parse")]
        private void PostParse()
        {
            _parsed = true;
            _friendList.TableView.didSelectCellWithIdxEvent += OnCellSelected;
            ShowFriends();
        }

        protected override void OnDestroy()
        {
            _avatarsCts?.Cancel();
            if (_friendList != null && _friendList.TableView != null)
                _friendList.TableView.didSelectCellWithIdxEvent -= OnCellSelected;
            base.OnDestroy();
        }

        public void SetFriends(IEnumerable<PlayerInfo> friends)
        {
            _friends.Clear();
            _friends.AddRange(friends);
            if (_parsed)
                ShowFriends();
        }

        private void ShowFriends()
        {
            var cells = _friends
                .Select(f => new CustomListTableData.CustomCellInfo(f.Name, FormatRanks(f), GetCachedAvatar(f)))
                .ToList();
            _friendList.Data = cells;
            _friendList.TableView.ReloadData();
            _friendList.TableView.ClearSelection();
            FriendSelected?.Invoke(null);

            _avatarsCts?.Cancel();
            _avatarsCts = new CancellationTokenSource();
            _ = LoadAvatarsAsync(_friends.ToList(), cells, _avatarsCts.Token);
        }

        private void OnCellSelected(TableView tableView, int index)
        {
            FriendSelected?.Invoke(index >= 0 && index < _friends.Count ? _friends[index] : null);
        }

        private static string FormatRanks(PlayerInfo player)
        {
            if (player.Rank <= 0)
                return player.Country;

            var country = string.IsNullOrEmpty(player.Country) ? "Страна" : player.Country;
            var countryRank = player.CountryRank > 0 ? $"  ·  {country} #{player.CountryRank}" : "";
            return $"Мир #{player.Rank}{countryRank}  ·  {player.Pp:0}pp";
        }

        private Sprite GetCachedAvatar(PlayerInfo player) =>
            player.Avatar != null && _avatarCache.TryGetValue(player.Avatar, out var sprite) ? sprite : null;

        /// <summary>Подгружает аватарки в фоне и обновляет видимые ячейки по мере загрузки.</summary>
        private async Task LoadAvatarsAsync(List<PlayerInfo> friends, IList<CustomListTableData.CustomCellInfo> cells, CancellationToken token)
        {
            using var throttle = new SemaphoreSlim(MaxParallelAvatars);
            var tasks = friends.Select(async (friend, index) =>
            {
                if (cells[index].Icon != null || string.IsNullOrEmpty(friend.Avatar))
                    return;

                await throttle.WaitAsync(token);
                try
                {
                    var bytes = await _client.DownloadBytesAsync(friend.Avatar, token);
                    var sprite = await Utilities.LoadSpriteAsync(bytes, 100f);
                    if (sprite == null || token.IsCancellationRequested)
                        return;

                    _avatarCache[friend.Avatar] = sprite;
                    cells[index].Icon = sprite;
                    _friendList.TableView.RefreshCellsContent();
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    Plugin.Log.Debug($"Аватар {friend.Name} не загружен: {ex.Message}");
                }
                finally
                {
                    throttle.Release();
                }
            });

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
