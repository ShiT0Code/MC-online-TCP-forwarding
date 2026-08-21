using Microsoft.UI.Xaml.Controls;
using System;

namespace McOnlineApp;
public class EventCenter
{
    public static event EventHandler<(OnlineData?, bool)>? CreatingClient;
    public static void OnCreatingClient(OnlineData? data, bool isWorld)
    {
        CreatingClient?.Invoke(null, new(data, isWorld));
    }

    public static event EventHandler<TabViewItem>? CloseTab;
    public static void OnCloseTab(TabViewItem item)
    {
        CloseTab?.Invoke(null, item);
    }

    public static bool IsDialogShowing { get; set; }
}
