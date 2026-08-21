using Serilog;

Console.Title = "MC 联机工具 - 服务器端";
Console.WriteLine("MC 联机工具");
Console.WriteLine("Copyright (c) 2026 ShiT0Code");
Console.WriteLine("Licensed under the MIT License. See LICENSE file for details.");
Console.WriteLine("This software uses third-party libraries. See THIRD-PARTY-LICENSE.txt for details.");

if (args.Length > 0 && int.TryParse(args[0], out int port))
{
    Worker.Port = port;
    Console.WriteLine($"使用端口：{port}");
}

var builder = Host.CreateDefaultBuilder();

builder.UseSystemd();

builder.UseSerilog((context, config) =>
{
    config.ReadFrom.Configuration(context.Configuration);
});

builder.ConfigureServices(services =>
{
    services.AddHostedService<Worker>();
});

var host = builder.Build();
await host.RunAsync();