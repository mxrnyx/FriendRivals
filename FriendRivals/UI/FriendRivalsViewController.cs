using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.ViewControllers;
using FriendRivals.BeatLeader;
using Zenject;

namespace FriendRivals.UI
{
    /// <summary>Правый экран: статус и действия (загрузка друзей, создание плейлиста).</summary>
    [ViewDefinition("FriendRivals.UI.FriendRivalsView.bsml")]
    internal class FriendRivalsViewController : BSMLAutomaticViewController
    {
        [Inject] private readonly BeatLeaderClient _client = null;
        [Inject] private readonly RivalPlaylistBuilder _playlistBuilder = null;
        [Inject] private readonly FriendListViewController _friendList = null;

        private PlayerInfo _me;
        private PlayerInfo _selectedFriend;
        private CancellationTokenSource _cts;

        private bool _busy;
        private string _statusText = "Загрузка друзей...";

        [UIValue("status-text")]
        public string StatusText
        {
            get => _statusText;
            private set
            {
                _statusText = value;
                NotifyPropertyChanged();
            }
        }

        [UIValue("song-limit")]
        public int SongLimit { get; set; } = 50;

        [UIValue("is-idle")]
        public bool IsIdle => !_busy;

        [UIValue("can-create")]
        public bool CanCreate => !_busy && _me != null && _selectedFriend != null;

        private bool Busy
        {
            set
            {
                _busy = value;
                NotifyPropertyChanged(nameof(IsIdle));
                NotifyPropertyChanged(nameof(CanCreate));
            }
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (firstActivation)
            {
                _friendList.FriendSelected += OnFriendSelected;
                _ = LoadFriendsAsync();
            }
        }

        protected override void OnDestroy()
        {
            _cts?.Cancel();
            if (_friendList != null)
                _friendList.FriendSelected -= OnFriendSelected;
            base.OnDestroy();
        }

        [UIAction("refresh-click")]
        private void OnRefreshClicked() => _ = LoadFriendsAsync();

        [UIAction("create-click")]
        private void OnCreateClicked() => _ = CreatePlaylistAsync();

        private void OnFriendSelected(PlayerInfo friend)
        {
            _selectedFriend = friend;
            NotifyPropertyChanged(nameof(CanCreate));
            if (_selectedFriend != null && !_busy)
                StatusText = $"Выбран: {_selectedFriend.Name}\nНажмите «Создать плейлист».";
        }

        private CancellationToken RestartToken()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            return _cts.Token;
        }

        private async Task LoadFriendsAsync()
        {
            if (_busy)
                return;

            var token = RestartToken();
            Busy = true;
            StatusText = "Получаем профиль и друзей из BeatLeader...";
            try
            {
                var user = await _client.GetCurrentUserAsync(token);
                _me = user.Player;
                _friendList.SetFriends(user.Friends
                    .Where(f => f?.Id != null)
                    .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase));

                StatusText = user.Friends.Count == 0
                    ? $"Вы вошли как {_me.Name}, но у вас нет друзей на BeatLeader. Добавьте их на сайте или в игре."
                    : $"Вы вошли как {_me.Name}.\nДрузей: {user.Friends.Count}. Выберите друга в списке.";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex);
                StatusText = $"Ошибка: {ex.Message}";
            }
            finally
            {
                Busy = false;
            }
        }

        private async Task CreatePlaylistAsync()
        {
            if (_busy || _me == null || _selectedFriend == null)
                return;

            var friend = _selectedFriend;
            var token = RestartToken();
            Busy = true;
            try
            {
                var myProgress = "0";
                var friendProgress = "0";
                void ShowProgress() => StatusText = $"Загрузка скоров...\nВы: {myProgress}\n{friend.Name}: {friendProgress}";

                var myTask = _client.GetAllScoresAsync(_me.Id, (done, total) => { myProgress = $"{done}/{total}"; ShowProgress(); }, token);
                var friendTask = _client.GetAllScoresAsync(friend.Id, (done, total) => { friendProgress = $"{done}/{total}"; ShowProgress(); }, token);
                var cover = DownloadCoverAsync(friend, token);
                await Task.WhenAll(myTask, friendTask);

                var lost = _playlistBuilder.FindLostMaps(myTask.Result, friendTask.Result, out var commonCount);
                if (lost.Count == 0)
                {
                    StatusText = commonCount == 0
                        ? $"У вас нет общих сыгранных карт с {friend.Name}."
                        : $"Из {commonCount} общих карт {friend.Name} не обыграл вас ни на одной!";
                    return;
                }

                StatusText = "Обновляем плейлист...";
                var result = _playlistBuilder.UpdatePlaylist(friend, lost, SongLimit, commonCount, await cover);
                StatusText = $"Готово! «{result.Playlist.Title}»\n" +
                             $"Добавлено: {result.Added}, убрано отыгранных: {result.Removed}.\n" +
                             $"В плейлисте {result.InPlaylist}, ещё не добавлено {result.Remaining} из {result.TotalLostSongs}.";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex);
                StatusText = $"Ошибка: {ex.Message}";
            }
            finally
            {
                Busy = false;
            }
        }

        private async Task<byte[]> DownloadCoverAsync(PlayerInfo friend, CancellationToken token)
        {
            if (string.IsNullOrEmpty(friend.Avatar))
                return null;
            try
            {
                return await _client.DownloadBytesAsync(friend.Avatar, token);
            }
            catch (Exception ex)
            {
                Plugin.Log.Debug($"Аватар друга не загружен: {ex.Message}");
                return null;
            }
        }
    }
}
