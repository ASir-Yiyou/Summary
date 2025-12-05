using Projects;

var builder = DistributedApplication.CreateBuilder(args);

// 1. 定义后端服务
// 这里的名字 "auth", "order" 会自动成为服务发现的 DNS 名称
var auth = builder.AddProject<AuthenticationServer>("auth").WithReplicas(2);
var demo = builder.AddProject<Summary_DemoService>("order").WithReplicas(2);
var demo1 = builder.AddProject<Summary_DemoService1>("product");

// 2. 定义网关
// WithReference 表示网关依赖于上面三个服务
// WaitFor 表示：尽量等待它们准备好 (Aspire 的 WaitFor 主要是注入配置，不完全阻塞进程，但在 Dashboard 上逻辑清晰)
var gateway = builder.AddProject<ApiGateway>("gateway")
    .WithReference(auth)
    .WithReference(demo)
    .WithReference(demo1);


// 启动
builder.Build().Run();
