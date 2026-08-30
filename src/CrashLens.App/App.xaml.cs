using Microsoft.UI.Xaml;
namespace CrashLens.App;
public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args) { MainWindow = new MainWindow(); MainWindow.Activate(); }
}
