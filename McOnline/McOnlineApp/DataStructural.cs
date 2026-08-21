namespace McOnlineApp;
public sealed class OnlineData
{
    public string Server { get; set; } = string.Empty;
    public int ServerPort { get; set; }
    public int LocalPort { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool EnableAutoReconnect { get; set; }
}
