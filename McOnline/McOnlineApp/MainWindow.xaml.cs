using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;

namespace McOnlineApp;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredTheme = TitleBarTheme.UseDefaultAppMode;
        SetTitleBar(titleBarDragPart);
    }

    private void TabView_Loaded(object sender, RoutedEventArgs e)
    {
        tabView.TabItems.Add(new HomeTab());

        EventCenter.CreatingClient += EventCenter_CreatingClient;
        EventCenter.CloseTab += EventCenter_CloseTab;

        OverlappedPresenter presenter = OverlappedPresenter.Create();
        presenter.PreferredMinimumWidth = 530;
        presenter.PreferredMinimumHeight = 490;
        AppWindow.SetPresenter(presenter);
        AppWindow.SetIcon("Assets/Icon.ico");
    }

    private async void Window_Closed(object sender, WindowEventArgs args)
    {
        if ((tabView.TabItems.Count > 1))
        {
            args.Handled = true;
            if (!EventCenter.IsDialogShowing)
            {
                EventCenter.IsDialogShowing = true;
                if (await new ExitWarningDialog { XamlRoot = tabView.XamlRoot }.ShowAsync() == Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                {
                    this.Closed -= Window_Closed;
                    this.Close();
                }
                EventCenter.IsDialogShowing = false;
            }
        }
    }

    UserGuideTab? UserGuideTab = null;
    private void EventCenter_CreatingClient(object? sender, (OnlineData?, bool) e)
    {
        if (e.Item1 == null)
        {
            if (UserGuideTab == null)
            {
                UserGuideTab = new();
                tabView.TabItems.Add(UserGuideTab);
                UserGuideTab.CloseRequested += UserGuideTab_CloseRequested;
            }
            tabView.SelectedItem = UserGuideTab;
        }
        else
        {
            if (e.Item2)
            {
                var tab = new WorldTab(e.Item1);
                tabView.TabItems.Add(tab);
                tabView.SelectedItem = tab;
            }
            else
            {
                var tab = new PlayerTab(e.Item1);
                tabView.TabItems.Add(tab); ;
                tabView.SelectedItem = tab;
            }
        }
    }

    private void UserGuideTab_CloseRequested(Microsoft.UI.Xaml.Controls.TabViewItem sender, Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs args)
    {
        if (UserGuideTab != null)
        {
            tabView.TabItems.Remove(UserGuideTab);
            UserGuideTab = null;
        }
    }

    private void EventCenter_CloseTab(object? sender, Microsoft.UI.Xaml.Controls.TabViewItem e)
    {
        tabView.TabItems.Remove(e);
    }
}
