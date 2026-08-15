using System.Windows;

namespace CrosshairFree
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            base.OnStartup(e);
        }
    }
}
