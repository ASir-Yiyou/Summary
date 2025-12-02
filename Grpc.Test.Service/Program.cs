using Grpc.Test.Service.Services;
using Summary.Common.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = false;
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});
builder.WebHost.UseKestrelbyConfig(builder.Configuration);
var app = builder.Build();
app.MapGrpcService<DeviceService>();

app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
