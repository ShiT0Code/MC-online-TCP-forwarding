using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace McOnlineApp;

public sealed partial class PlayerTab : TabViewItem
{
    private OnlineData OnlineData { get; set; }
    public PlayerTab(OnlineData onlineData)
    {
        InitializeComponent();
        this.OnlineData = onlineData;
        this.Header = OnlineData.DisplayName;
    }

    private void TabViewItem_Loaded(object sender, RoutedEventArgs e) => Try();

    private async void TabViewItem_CloseRequested(TabViewItem sender, TabViewTabCloseRequestedEventArgs args) => FlyoutBase.ShowAttachedFlyout(this);
    private void Close_Button_Click(object sender, RoutedEventArgs e)
    {
        CancellationTokenSource?.Cancel();
        exitFlyout.Hide();
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
    TcpListener? listener = null;
    private async Task BackgroundTask()
    {
        if (CancellationTokenSource != null)
            return;
        CancellationTokenSource = new();
        listener?.Dispose();

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
            return;
        }
        catch (Exception ex)
        {
            _ = this.DispatcherQueue.TryEnqueue(() => ring1.IsActive = false);
            await ShowErrorMessage(ex.Message, "连接到服务器时发生错误", 2);
            EndTask();
            return;
        }
        if (serverStream == null || serverClient == null)
        {
            EndTask();
            _ = this.DispatcherQueue.TryEnqueue(() => statusIcon1.Visibility = Visibility.Collapsed);
            serverClient?.Dispose();
            await ShowErrorMessage("", "与服务器的连接无效", 2);
            return;
        }

        /// 等待游戏连接
        CancellationTokenSource? udpToken = new();
        // 组播广播
        _ = Task.Run(async () =>
        {
            try
            {
                using UdpClient udpClient = new();
                byte[] data = Encoding.UTF8.GetBytes($"[MOTD]{OnlineData.DisplayName} - MC 联机工具[/MOTD][AD]{OnlineData.LocalPort}[/AD]");
                IPEndPoint endPoint = new(IPAddress.Loopback, 4445);
                while (!udpToken.IsCancellationRequested)
                {
                    await udpClient.SendAsync(data, data.Length, endPoint);
                    await Task.Delay(1500, udpToken.Token);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _ = this.DispatcherQueue.TryEnqueue(() => ring2.IsActive = false);
                await ShowErrorMessage(ex.Message, "发送组播时错误", 3);
            }
        }, udpToken.Token);
        TcpClient? gameClient = null;
        NetworkStream? gameStream = null;
        try
        {
            listener = new(IPAddress.Loopback, OnlineData.LocalPort);
            listener.Start();
            gameClient = await listener.AcceptTcpClientAsync(CancellationTokenSource.Token);
            // 游戏已连接，与世界端握手
            gameStream = gameClient.GetStream();
            await serverStream.WriteAsync(Encoding.UTF8.GetBytes("OK"));
            listener.Dispose();
            udpToken.Cancel();
        }
        catch (OperationCanceledException)
        {
            udpToken.Cancel();
            EndTask();
            await ShowCancelMessage(3);
            udpToken = null;
            return;
        }
        catch (Exception ex)
        {
            _ = this.DispatcherQueue.TryEnqueue(() => ring2.IsActive = false);
            await ShowErrorMessage(ex.Message, "等待本地游戏时发生错误 ", 3);
            EndTask();
            listener?.Dispose();
            return;
        }

        // 成功连接，UI 示意
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
        // 转发开始
        var task1 = Forward(serverStream, gameStream, CancellationTokenSource.Token);
        var task2 = Forward(gameStream, serverStream, CancellationTokenSource.Token);
        await Task.WhenAny(task1, task2);
        // 结束
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

            retryBu.IsEnabled = cancelBu.IsEnabled = true;
            retryBu.Content = "开始";
            cancelBu.Content = "取消";
            statusIcon1.Glyph = statusIcon2.Glyph = "\uE712";

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
        listener?.Dispose();
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
