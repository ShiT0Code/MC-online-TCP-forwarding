using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel;

namespace McOnlineApp;
public sealed partial class HomeTab : TabViewItem
{
    public HomeTab() => InitializeComponent();

    private bool IsTabLoaded { get; set; }
    private List<string> ServerList { get; set; } = [];
    private readonly Windows.Storage.ApplicationDataContainer LocalSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
    private void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        var serversValue = LocalSettings.Values["serverList"];
        if (serversValue is string serversText)
            ServerList = [.. serversText.Split(';')];
        var lastServerValue = LocalSettings.Values["lastServer"];
        if (lastServerValue is string lastServerText)
            serverSuggestBox.Text = lastServerText;


        var serverPortValue = LocalSettings.Values["serverPort"];
        if (serverPortValue is double serverPort)
            portServerNum.Value = serverPort;

        var localPortValue = LocalSettings.Values["localPort"];
        if (localPortValue is double localPort)
            portLocalNum.Value = localPort;

        var displayValue = LocalSettings.Values["display"];
        if (displayValue is string display)
            displayText.Text = display;

        var autoReValue = LocalSettings.Values["autoReconnect"];
        if (autoReValue is bool autoRe)
            autoReSw.IsOn = autoRe;

        var lastParValue = LocalSettings.Values["lastParner"];
        if (lastParValue is int lastPar)
            comboBox.SelectedIndex = lastPar;


        IsTabLoaded = true;
        CheckCanStart();
        serverSuggestBox.ItemsSource = ServerList;
        if (comboBox.SelectedItem == null)
            comboBox.SelectedIndex = 0;

        var ver = Package.Current.Id.Version;
        infoCard.Content = $"{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
    }

    private void ParnerSettingsCard_Click(object sender, RoutedEventArgs e) => comboBox.IsDropDownOpen = true;

    private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => CheckCanStart();

    private void Num_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) => CheckCanStart();

    private void AutoReSettingsCard_Click(object sender, RoutedEventArgs e) => autoReSw.IsOn = !autoReSw.IsOn;

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        bool canNext = CheckCanStart();
        if (!canNext)
            return;

        bool isWorldClient = comboBox.SelectedIndex == 0;
        string server = serverSuggestBox.Text;
        double localPort = portLocalNum.Value;
        double serverPort = portServerNum.Value;
        string display = displayText.Text;
        bool enableAutoRe = autoReSw.IsOn;
        var data = new OnlineData
        {
            Server = server,
            LocalPort = (int)localPort,
            ServerPort = (int)serverPort,
            DisplayName = display,
            EnableAutoReconnect = enableAutoRe
        };
        EventCenter.OnCreatingClient(data, isWorldClient);
        LocalSettings.Values["lastServer"] = server;
        LocalSettings.Values["serverPort"] = serverPort;
        LocalSettings.Values["localPort"] = localPort;
        LocalSettings.Values["display"] = display;
        LocalSettings.Values["autoReconnect"] = enableAutoRe;
        LocalSettings.Values["lastParner"] = comboBox.SelectedIndex;
        if (!ServerList.Contains(server))
        {
            ServerList.Add(server);
            serverSuggestBox.ItemsSource = ServerList;
            LocalSettings.Values["serverList"] = string.Join(';', ServerList);
        }
    }
    private bool CheckCanStart()
    {
        if(!IsTabLoaded)
            return false;
        bool localPortOK = !double.IsNaN(portLocalNum.Value);
        if (localPortOK)
            portLocalNum.Value = Math.Truncate(portLocalNum.Value);
        bool serverPortOK = !double.IsNaN(portServerNum.Value);
        if (serverPortOK)
            portServerNum.Value = Math.Truncate(portServerNum.Value);
        bool serverOK = !string.IsNullOrEmpty(serverSuggestBox.Text);
        bool result = (localPortOK & serverPortOK & serverOK);
        startCard.IsEnabled = result;
        return result;
    }

    private async void LICENSEs_SettingsCard_Click(object sender, RoutedEventArgs e)
    {
        EventCenter.IsDialogShowing = true;
        await new TextViewerDialog("引用的开源项目的许可协议", "THIRD-PARTY-LICENSE", true) { XamlRoot = this.XamlRoot }.ShowAsync();
        EventCenter.IsDialogShowing = false;
    }

    private async void ThisLICENSE_HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        EventCenter.IsDialogShowing = true;
        await new TextViewerDialog("MIT 协议", "LICENSE", false) { XamlRoot = this.XamlRoot }.ShowAsync();
        EventCenter.IsDialogShowing = false;
    }

    private void UserGuide_SettingsCard_Click(object sender, RoutedEventArgs e) => EventCenter.OnCreatingClient(null, false);
}
