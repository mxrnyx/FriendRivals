using System;
using BeatSaberMarkupLanguage.MenuButtons;
using Zenject;

namespace FriendRivals.UI
{
    internal class MenuButtonManager : IInitializable, IDisposable
    {
        private readonly MenuButtons _menuButtons;
        private readonly MainFlowCoordinator _mainFlowCoordinator;
        private readonly FriendRivalsFlowCoordinator _flowCoordinator;
        private readonly MenuButton _button;

        public MenuButtonManager(MenuButtons menuButtons, MainFlowCoordinator mainFlowCoordinator, FriendRivalsFlowCoordinator flowCoordinator)
        {
            _menuButtons = menuButtons;
            _mainFlowCoordinator = mainFlowCoordinator;
            _flowCoordinator = flowCoordinator;
            _button = new MenuButton("Friend Rivals", "Карты, где друг обыграл вас на BeatLeader", OnClick, true);
        }

        public void Initialize() => _menuButtons.RegisterButton(_button);

        public void Dispose() => _menuButtons.UnregisterButton(_button);

        private void OnClick() => _mainFlowCoordinator.PresentFlowCoordinator(_flowCoordinator);
    }
}
