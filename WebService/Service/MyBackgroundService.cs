using OpenIddict.Abstractions;

namespace AuthenticationServer.Service
{
    public class MyBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        public MyBackgroundService(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var tokenManager = scope.ServiceProvider.GetRequiredService<IOpenIddictTokenManager>();
                    var authManager = scope.ServiceProvider.GetRequiredService<IOpenIddictAuthorizationManager>();

                    await tokenManager.PruneAsync(DateTimeOffset.UtcNow.AddDays(-7), stoppingToken);
                    await authManager.PruneAsync(DateTimeOffset.UtcNow.AddDays(-7), stoppingToken);
                }
                catch { /* Log Error */ }

                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}
