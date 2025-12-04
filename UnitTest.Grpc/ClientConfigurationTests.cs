using Grpc.Core;
using Grpc.Shared.Contracts.Test;
using Microsoft.Extensions.DependencyInjection;
using Summary.Common.Core.Extensions;
using Summary.Common.Model;

namespace UnitTest.Grpc
{
    public class ClientConfigurationTests
    {
        private ServiceProvider _provider;

        public ClientConfigurationTests()
        {
            var services = new ServiceCollection();

            // 2. [虚拟配置]：在内存中模拟 appsettings.json
            services.Configure<AppAccessBasicConfiguration>(config =>
            {
                config.UseHttps = true;
                config.CertificatePath = "D:\\test.pfx";      // 使用 Fixture 生成的证书
                config.CertificatePassWord = "123456";
            });

            services.AddGrpcClient<DeviceControl.DeviceControlClient>(o =>
            {
                o.Address = new Uri("https://localhost:7089");
            })
            .ConfigurePrimaryHttpsMessageHandler();

            _provider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task GetStatus_Should_Return_Running()
        {
            var _client = _provider.GetRequiredService<DeviceControl.DeviceControlClient>();
            var request = new StatusRequest { DeviceId = 101 };

            var response = await _client.GetStatusAsync(request);

            Assert.Equal("Running", response.Status);
            Assert.True(response.UptimeSeconds > 0);
        }

        // --- 测试 2: 服务端流 ---
        [Fact]
        public async Task MonitorTemperature_Should_Receive_Stream_Data()
        {
            var _client = _provider.GetRequiredService<DeviceControl.DeviceControlClient>();
            var request = new MonitorRequest { DeviceId = 101 };
            using var call = _client.MonitorTemperature(request);

            var receivedData = new List<TemperatureData>();

            // 读取流
            await foreach (var data in call.ResponseStream.ReadAllAsync())
            {
                receivedData.Add(data);
            }

            // 断言：服务端逻辑写了循环5次
            Assert.Equal(5, receivedData.Count);
            Assert.All(receivedData, d => Assert.True(d.Value > 0));
        }

        // --- 测试 3: 客户端流 ---
        [Fact]
        public async Task UploadLogs_Should_Return_Success()
        {
            var _client = _provider.GetRequiredService<DeviceControl.DeviceControlClient>();
            using var call = _client.UploadLogs();

            // 发送 3 条日志
            await call.RequestStream.WriteAsync(new LogEntry { Content = "Log 1" });
            await call.RequestStream.WriteAsync(new LogEntry { Content = "Log 2" });
            await call.RequestStream.WriteAsync(new LogEntry { Content = "Log 3" });

            await call.RequestStream.CompleteAsync(); // 告诉服务端我发完了

            var response = await call;

            Assert.True(response.Success);
            Assert.Equal(3, response.Count);
        }

        // --- 测试 4: 双向流 ---
        [Fact]
        public async Task LiveConsole_Should_Ping_Pong()
        {
            var _client = _provider.GetRequiredService<DeviceControl.DeviceControlClient>();
            using var call = _client.LiveConsole();

            // 1. 开启接收任务
            var responseList = new List<string>();
            var readTask = Task.Run(async () =>
            {
                await foreach (var resp in call.ResponseStream.ReadAllAsync())
                {
                    responseList.Add(resp.ResultText);
                }
            });

            // 2. 发送指令
            await call.RequestStream.WriteAsync(new ConsoleCommand { CommandText = "ping" });
            await call.RequestStream.WriteAsync(new ConsoleCommand { CommandText = "test" });

            // 稍等一下让服务端处理
            await Task.Delay(500);

            await call.RequestStream.CompleteAsync();
            await readTask;

            // 3. 断言
            Assert.Contains(responseList, r => r.Contains("pong")); // ping -> pong
            Assert.Contains(responseList, r => r.Contains("Executed: test"));
        }
    }
}