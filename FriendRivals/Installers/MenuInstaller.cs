using FriendRivals.BeatLeader;
using FriendRivals.UI;
using Zenject;

namespace FriendRivals.Installers
{
    internal class MenuInstaller : Installer
    {
        public override void InstallBindings()
        {
            Container.BindInterfacesAndSelfTo<BeatLeaderClient>().AsSingle();
            Container.Bind<RivalPlaylistBuilder>().AsSingle();

            Container.Bind<FriendListViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<FriendRivalsViewController>().FromNewComponentAsViewController().AsSingle();
            Container.Bind<FriendRivalsFlowCoordinator>().FromNewComponentOnNewGameObject().AsSingle();
            Container.BindInterfacesTo<MenuButtonManager>().AsSingle();
        }
    }
}
