using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace CrashLens.App;
public sealed partial class DetailSection : UserControl
{
    public DetailSection() => InitializeComponent();
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(DetailSection), new PropertyMetadata(""));
    public IEnumerable<FieldRow> Items { get => (IEnumerable<FieldRow>)GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(nameof(Items), typeof(IEnumerable<FieldRow>), typeof(DetailSection), new PropertyMetadata(null));
}
