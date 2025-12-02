using Grpc.Core;
using Grpc.Shared.Contracts.Test;

namespace Grpc.Test.Service.Services
{
    public class DeviceService : DeviceControl.DeviceControlBase
    {
        private readonly ILogger<DeviceService> _logger;

        public DeviceService(ILogger<DeviceService> logger)
        {
            _logger = logger;
        }

        // 1. 单向调用 (Unary)
        public override Task<StatusResponse> GetStatus(StatusRequest request, ServerCallContext context)
        {
            _logger.LogWarning($"收到状态查询: DeviceID={request.DeviceId}");

            return Task.FromResult(new StatusResponse
            {
                Status = "Running",
                UptimeSeconds = 3600
            });
        }

        // 2. 服务端流 (Server Streaming)
        public override async Task MonitorTemperature(
            MonitorRequest request,
            IServerStreamWriter<TemperatureData> responseStream,
            ServerCallContext context)
        {
            _logger.LogWarning($"开始推送温度数据: DeviceID={request.DeviceId}");

            var random = new Random();

            // 模拟发送 5 次数据
            for (int i = 0; i < 5; i++)
            {
                // 如果客户端取消了连接，停止发送
                if (context.CancellationToken.IsCancellationRequested) break;

                await responseStream.WriteAsync(new TemperatureData
                {
                    Timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    Value = 20 + random.NextDouble() * 5 // 20-25度随机
                });

                // 模拟间隔
                await Task.Delay(1000);
            }
            _logger.LogWarning("温度推送结束");
        }

        // 3. 客户端流 (Client Streaming)
        public override async Task<UploadResult> UploadLogs(
            IAsyncStreamReader<LogEntry> requestStream,
            ServerCallContext context)
        {
            int count = 0;
            _logger.LogWarning("开始接收日志...");

            // 循环读取客户端发来的流
            while (await requestStream.MoveNext())
            {
                var log = requestStream.Current;
                _logger.LogWarning($"[Server接收日志] {log.Level}: {log.Content}");
                count++;
            }

            return new UploadResult
            {
                Success = true,
                Count = count,
                Message = "LogWarning"
            };
        }

        // 4. 双向流 (Bidirectional Streaming)
        public override async Task LiveConsole(
            IAsyncStreamReader<ConsoleCommand> requestStream,
            IServerStreamWriter<ConsoleOutput> responseStream,
            ServerCallContext context)
        {
            // 只要客户端还在发，我就一直听
            while (await requestStream.MoveNext())
            {
                var cmd = requestStream.Current.CommandText;
                _logger.LogWarning($"[收到指令] {cmd}");

                // 模拟处理逻辑：收到 "ping" 回复 "pong"
                string result = cmd.ToLower() == "ping" ? "pong" : $"Executed: {cmd}";

                // 立即回复给客户端
                await responseStream.WriteAsync(new ConsoleOutput
                {
                    ResultText = $"> Server: {result}"
                });
            }
        }
    }
}
