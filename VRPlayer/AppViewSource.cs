using Windows.ApplicationModel.Core;

namespace VRPlayer
{
    // The entry point for the app.
    public class AppViewSource : IFrameworkViewSource
    {
        public IFrameworkView CreateView()
        {
            return new AppView();
        }
    }
}
