using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace McOnlineApp;

public sealed partial class WorldTab : TabViewItem
{
    private OnlineData OnlineData { get; set; }
    public WorldTab(OnlineData onlineData)
    {
        InitializeComponent();
        this.OnlineData = onlineData;
        this.Header = OnlineData.DisplayName;
    }

    private void TabViewItem_Loaded(object sender, RoutedEventArgs e) => Try();

    private void TabViewItem_CloseRequested(TabViewItem sender, TabViewTabCloseRequestedEventArgs args) => FlyoutBase.ShowAttachedFlyout(this);
    private void Close_Button_Click(object sender, RoutedEventArgs e)
    {
        exitFlyout.Hide();
        CancellationTokenSource?.Cancel();
        EventCenter.OnCloseTab(this);
    }

    // 开始/停止后台任务
    private CancellationTokenSource? CancellationTokenSource = null;
    bool CanceledRetry = false;
    private async void Cancel_Button_Click(object sender, RoutedEventArgs e)
    {
        CanceledRetry = true;
        CancellationTokenSource?.Cancel();
        cancelBu.Content = "取消";
        infoBar.Title = "操作已取消";
        infoBar.Message = string.Empty;
        infoBar.Severity = InfoBarSeverity.Warning;
        infoBar.IsOpen = true;
        infoBar.IsClosable = true;
    }
    private void RetryButton_Click(object sender, RoutedEventArgs e) => Try();
    private void Try()
    {
        if (CanceledRetry)
        {
            CanceledRetry = false;
            return;
        }
        retryBu.IsEnabled = false;
        cancelBu.IsEnabled = true;

        statusIcon1.Visibility = errorIcon.Visibility = Visibility.Collapsed;
        ring1.IsActive = true;
        statusIcon1.Glyph = "\uF78C";
        statusIcon2.Glyph = "\uE712";
        statusIcon2.Visibility = Visibility.Visible;

        _ = Task.Run(BackgroundTask);
    }

    #region 后台代码，执行转发任务
    private async Task BackgroundTask()
    {
        CancellationTokenSource = new();

        // 连接到服务器
        NetworkStream? serverStream = null;
        TcpClient? serverClient = null;
        try
        {

            serverClient = new TcpClient();
            await serverClient.ConnectAsync(OnlineData.Server, OnlineData.ServerPort, CancellationTokenSource.Token);
            serverStream = serverClient.GetStream();

            // 第一步成功，更新 UI
            _ = this.DispatcherQueue.TryEnqueue(() =>
            {
                ring1.IsActive = false;
                statusIcon1.Visibility = Visibility.Visible;
                statusIcon2.Visibility = Visibility.Collapsed;
                ring2.IsActive = true;
                statusIcon2.Glyph = "\uF78C";
            });
        }
        catch (OperationCanceledException)
        {
            await ShowCancelMessage(2);
            EndTask();
            return;
        }
        catch (Exception ex)
        {
            _ = this.DispatcherQueue.TryEnqueue(() => ring1.IsActive = false);
            await ShowErrorMessage(ex.Message, "连接到服务器时发生错误", 2);
            await Task.Delay(100);
            EndTask();
            return;
        }
        if (serverStream == null || serverClient == null)
        {
            _ = this.DispatcherQueue.TryEnqueue(() => statusIcon1.Visibility = Visibility.Collapsed);
            serverClient?.Dispose();
            await ShowErrorMessage("", "与服务器的连接无效", 2);
            EndTask();
            return;
        }

        // 与对方握手
        TcpClient? gameClient = new();
        NetworkStream? gameStream = null;
        try
        {
            byte[] returnBuffer = new byte[16];
            int bytesRead = await serverStream.ReadAsync(returnBuffer, CancellationTokenSource.Token);
            if (bytesRead == 0)
                throw new Exception("服务器关闭了连接");
            string message = System.Text.Encoding.UTF8.GetString(returnBuffer, 0, bytesRead);
            if (message != "OK")
                throw new Exception("对方发送了错误的内容");

            await gameClient.ConnectAsync("127.0.0.1", OnlineData.LocalPort);
            gameStream = gameClient.GetStream();
        }
        catch (OperationCanceledException)
        {
            await ShowCancelMessage(3);
            EndTask();
            return;
        }
        catch (Exception ex)
        {
            _ = this.DispatcherQueue.TryEnqueue(() => ring2.IsActive = false);
            await ShowErrorMessage(ex.Message, "等待对方游戏时发生错误 ", 3);
            EndTask();
            return;
        }

        // 成功连接，UI 示意
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            statusIcon2.Glyph = "\uF78C";
            ring2.IsActive = false;
            statusIcon2.Visibility = Visibility.Visible;
            cancelBu.Content = "停止";

            infoBar.Title = "连接成功🎉🎉🎉";
            infoBar.Message = "尽情玩耍吧";
            infoBar.Severity = InfoBarSeverity.Success;
            infoBar.IsOpen = true;
            infoBar.IsClosable = false;
        });
        // 开始转发
        var task1 = Forward(serverStream, gameStream, CancellationTokenSource.Token);
        var task2 = Forward(gameStream, serverStream, CancellationTokenSource.Token);
        await Task.WhenAny(task1, task2);
        // 连接结束
        EndTask();
        await Task.WhenAll(task1, task2);

        serverClient?.Dispose();
        gameClient?.Dispose();

        _ = this.DispatcherQueue.TryEnqueue(async () =>
        {
            infoBar.Title = "连接结束";
            infoBar.Message = string.Empty;
            infoBar.Severity = InfoBarSeverity.Informational;
            infoBar.IsOpen = true;
            infoBar.IsClosable = true;

            statusIcon1.Glyph = statusIcon2.Glyph = "\uE712";

            retryBu.IsEnabled = cancelBu.IsEnabled = true;
            retryBu.Content = "开始";
            cancelBu.Content = "取消";

            if (OnlineData.EnableAutoReconnect)
            {
                infoBar.Message = "将在 3 秒后自动重新开始连接";
                await Task.Delay(3000);
                if (CancellationTokenSource == null && (!CanceledRetry))
                {
                    infoBar.Title = "正在重新连接";
                    infoBar.Message = string.Empty;
                    infoBar.IsOpen = true;
                    Try();
                }
            }
        });
    }
    private static async Task Forward(NetworkStream fromStream, NetworkStream toStream, CancellationToken token)
    {
        try
        {
            await fromStream.CopyToAsync(toStream, 4096, token);
        }
        catch { }
    }
    private void EndTask()
    {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        CancellationTokenSource = null;
    }
    // 在 UI 上展示错误
    private async Task ShowErrorMessage(string message, string title, int uiRow)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            Grid.SetRow(errorIcon, uiRow);
            errorIcon.Visibility = Visibility.Visible;

            infoBar.Title = title;
            infoBar.Message = message;
            infoBar.Severity = InfoBarSeverity.Error;
            infoBar.IsOpen = true;
            infoBar.IsClosable = true;

            retryBu.IsEnabled = true;
            cancelBu.IsEnabled = false;
        });
    }
    // 在 UI 上提示已取消
    private async Task ShowCancelMessage(int uiRow)
    {
        _ = this.DispatcherQueue.TryEnqueue(() =>
        {
            infoBar.Title = "操作已取消";
            infoBar.Message = string.Empty;
            infoBar.Severity = InfoBarSeverity.Warning;
            infoBar.IsOpen = true;
            infoBar.IsClosable = true;

            Grid.SetRow(errorIcon, uiRow);
            errorIcon.Visibility = Visibility.Visible;
            ring1.IsActive = ring2.IsActive = false;
            retryBu.IsEnabled = true;
            cancelBu.IsEnabled = false;
        });
    }
    #endregion
}
