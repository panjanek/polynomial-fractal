using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Application = System.Windows.Application;

namespace PolyFract
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            base.OnStartup(e);
        }
    }

}
