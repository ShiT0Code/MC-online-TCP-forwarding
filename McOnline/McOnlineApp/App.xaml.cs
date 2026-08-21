using Microsoft.UI.Xaml;

namespace McOnlineApp;
public partial class App : Application
{
    public App() => InitializeComponent();

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args) => new MainWindow().Activate();
}
