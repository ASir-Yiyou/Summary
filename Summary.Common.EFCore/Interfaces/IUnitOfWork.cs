using Summary.Domain.Entities;

namespace Summary.Common.EFCore.Interfaces
{
    public interface IUnitOfWork : IAsyncDisposable
    {
        // 为每个实体定义一个仓储属性
        // 如果有自定义仓储，这里就用自定义接口，如 IBillRepository
        IRepository<Bill> Bills { get; }

        IRepository<Custom> Customs { get; }
        IRepository<TimeLine> TimeLines { get; }
        IRepository<Milestone> Milestones { get; }

        // 提交所有更改到数据库
        Task<int> CommitAsync(CancellationToken cancellationToken = default);
    }
}