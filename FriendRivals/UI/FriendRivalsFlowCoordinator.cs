using HMUI;
using Zenject;

namespace FriendRivals.UI
{
    internal class FriendRivalsFlowCoordinator : FlowCoordinator
    {
        private MainFlowCoordinator _mainFlowCoordinator;
        private FriendListViewController _friendList;
        private FriendRivalsViewController _actions;

        [Inject]
        public void Construct(MainFlowCoordinator mainFlowCoordinator, FriendListViewController friendList, FriendRivalsViewController actions)
        {
            _mainFlowCoordinator = mainFlowCoordinator;
            _friendList = friendList;
            _actions = actions;
        }

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            if (firstActivation)
            {
                SetTitle("Friend Rivals");
                showBackButton = true;
            }

            if (addedToHierarchy)
                ProvideInitialViewControllers(_friendList, rightScreenViewController: _actions);
        }

        protected override void BackButtonWasPressed(ViewController topViewController)
        {
            _mainFlowCoordinator.DismissFlowCoordinator(this);
        }
    }
}
