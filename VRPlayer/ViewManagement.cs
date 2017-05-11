using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.ViewManagement;

namespace VRPlayer
{
    public static class ViewManagement
    {
        static CoreApplicationView coreView2d;
        static CoreApplicationView coreView3d;

        public static async void SwitchTo3DViewAsync()
        {
            if (coreView2d == null)
            {
                coreView2d = CoreApplication.MainView;
            }
            if (coreView3d == null)
            {
                coreView3d = CoreApplication.CreateNewView(new VRPlayer.AppViewSource());
            }
            await RunOnDispatcherAsync(coreView3d, SwitchViewsAsync);
        }

        public static async void SwitchTo2DViewAsync()
        {
            await RunOnDispatcherAsync(coreView2d, SwitchViewsAsync);
        }

        static async Task RunOnDispatcherAsync(CoreApplicationView view, Func<Task> action)
        {
            await view.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal, () => action()
              );
        }
        
        static async Task SwitchViewsAsync()
        {
            var view = ApplicationView.GetForCurrentView();
            await ApplicationViewSwitcher.SwitchAsync(view.Id);
            CoreWindow.GetForCurrentThread().Activate();
        }
        
    }
}
