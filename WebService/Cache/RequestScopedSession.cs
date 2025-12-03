using Summary.Domain.Interfaces;

namespace WebService.Cache
{
    public class RequestScopedSession : IDbSession<Guid, Guid>
    {
        // 默认值为空
        public Guid UserId { get; private set; } = Guid.Empty;
        public Guid TenantId { get; private set; } = Guid.Empty;

        public bool IsInitialized { get; private set; } = false;

        // 提供一个初始化方法供中间件调用
        public void Initialize(Guid userId, Guid tenantId)
        {
            UserId = userId;
            TenantId = tenantId;
            IsInitialized = true;
        }
    }
}
