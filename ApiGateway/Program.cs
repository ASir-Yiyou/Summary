var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSwaggerGen();
builder.Services.AddServiceDiscovery();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();
var app = builder.Build();
app.UseSwagger();
app.UseGatewayRedLogging();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/auth/swagger/v1/swagger.json", "Auth Service");
    options.SwaggerEndpoint("/order/swagger/v1/swagger.json", "Order Service");
    options.SwaggerEndpoint("/product/swagger/v1/swagger.json", "Product Service");

    // 1. 开启 Token 持久化 (刷新页面Token还在)
    options.ConfigObject.PersistAuthorization = true;

    // 2. 配置 OAuth Client (让 Order/Product 页面也能自动填 ClientId)
    // 这些配置是全局的，对所有下拉框里的服务都生效
    options.OAuthClientId("swagger_user_client");
    options.OAuthAppName("Swagger UI");
    options.OAuthScopes("api", "offline_access");
    options.OAuthUsePkce();
});
app.MapReverseProxy();
app.Run();
