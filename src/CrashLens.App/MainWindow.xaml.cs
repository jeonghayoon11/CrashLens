using Microsoft.UI.Xaml;
namespace CrashLens.App;
public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; } = new();
    public MainWindow() { InitializeComponent(); ExtendsContentIntoTitleBar = true; SetTitleBar(null); }
}
