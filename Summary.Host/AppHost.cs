using Projects;

var builder = DistributedApplication.CreateBuilder(args);


var auth = builder.AddProject<Projects.AuthenticationServer>("auth")
    .WithReplicas(2); // 你的多副本配置


// 定义 Order 服务
var order = builder.AddProject<Projects.Summary_DemoService>("product")
    .WithReference(auth) // 1. 注入 Auth 服务发现 (为了后端验证 Token)
                         // 2. 注入网关的外部地址 (为了 Swagger UI 在浏览器里能跳转)
                         // 注意：在开发环境通常固定网关端口比较方便，或者通过配置读取
    .WithEnvironment("Identity:GatewayPublicUrl", "https://localhost:9000")
    .WithEnvironment("Identity:AuthInternalUrl", "https://auth")
    .WithReplicas(2);

// Product 服务同理
var product = builder.AddProject<Projects.Summary_DemoService1>("order")
    .WithReference(auth)
    .WithEnvironment("Identity:GatewayPublicUrl", "https://localhost:9000")
    .WithEnvironment("Identity:AuthInternalUrl", "https://auth");

// 2. 定义网关
// WithReference 表示网关依赖于上面三个服务
// WaitFor 表示：尽量等待它们准备好 (Aspire 的 WaitFor 主要是注入配置，不完全阻塞进程，但在 Dashboard 上逻辑清晰)
var gateway = builder.AddProject<ApiGateway>("gateway")
    .WithReference(auth)
    .WithReference(order)
    .WithReference(product);


// 启动
builder.Build().Run();
