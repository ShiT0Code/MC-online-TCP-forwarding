using System.Net.Sockets;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    public static int Port { get; set; } = 25568;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("\n\n服务开始运行，监听端口 {Port}，时间: {time}", Port, DateTimeOffset.Now);
        using TcpListener listener = new(System.Net.IPAddress.Any, Port);
        listener.Start();
        uint groupID = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            groupID++;
            logger.LogInformation("开始一个新循环，等待第一组客户端，组别第 {groupID} 组", groupID);
            TcpClient? client1 = null;
            TcpClient? client2 = null;
            try
            {
                client1 = await listener.AcceptTcpClientAsync(stoppingToken);
                logger.LogInformation("{groupID} 组第一个客户端连接到服务器", groupID);
                if (!client1.Connected)
                {
                    client1.Dispose();
                    logger.LogWarning("{groupID} 组第一个客户端已立刻断开，跳过本组", groupID);
                    continue;
                }

                client2 = await listener.AcceptTcpClientAsync(stoppingToken);
                logger.LogInformation("{groupID} 组第二个客户端连接到服务器", groupID);
                if (!client2.Connected)
                {
                    client1.Dispose();
                    client2.Dispose();
                    logger.LogWarning("{groupID} 组第二个客户端已立刻断开，跳过本组", groupID);
                    continue;
                }

                _ = RunSessionAsync(client1, client2, groupID, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("{groupID} 组监听循环收到停止信号，退出", groupID);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError("{groupID} 组接受TCP连接时时出现错误：{ex}", groupID, ex);
                client1?.Dispose();
                client2?.Dispose();
            }
        }
        logger.LogInformation("TcpListener已停止\n\n");
    }

    private async Task RunSessionAsync(TcpClient client1, TcpClient client2, uint groupID, CancellationToken serviceStoppingToken)
    {
        using var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(serviceStoppingToken);
        var token = sessionCts.Token;

        try
        {
            await Run(client1, client2, groupID, token);
        }
        catch (Exception ex)
        {
            logger.LogError("{groupID} 组转发会话任务发生未捕获异常 {ex}", groupID, ex);
        }
    }

    private async Task Run(TcpClient client1, TcpClient client2, uint groupID, CancellationToken token)
    {
        try
        {
            NetworkStream stream1 = client1.GetStream();
            NetworkStream stream2 = client2.GetStream();

            logger.LogInformation("{groupID} 组开始转发", groupID);
            var task1 = Forward(stream1, stream2, groupID, 1, token);
            var task2 = Forward(stream2, stream1, groupID, 2, token);
            // 任意一端结束，立刻关闭整个隧道
            await Task.WhenAny(task1, task2);
        }
        catch (Exception ex)
        {
            logger.LogError("{groupID} 组在获取流时错误: {ex}", groupID, ex);
        }
        finally
        {
            client1?.Dispose();
            client2?.Dispose();
            logger.LogWarning("{groupID} 组停止转发", groupID);
        }
    }

    private async Task Forward(NetworkStream fromStream, NetworkStream toStream, uint groupID, int way, CancellationToken token)
    {
        try
        {
            await fromStream.CopyToAsync(toStream, 4096, token);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            logger.LogError("{groupID} 组 {way} 方向出现错误: {ex}", groupID, way, ex.Message);
        }
    }
}