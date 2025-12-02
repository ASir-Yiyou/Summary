using Summary.Common.EFCore.DbContexts;
using Summary.Common.EFCore.Interfaces;
using Summary.Common.EFCore.Repositories;
using Summary.Domain.Entities;

namespace Summary.Common.EFCore
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly MyTestDbContext _context;

        private readonly Lazy<IRepository<Bill>> _billRepository;
        private readonly Lazy<IRepository<Custom>> _customRepository;
        private readonly Lazy<IRepository<TimeLine>> _timeLineRepository;
        private readonly Lazy<IRepository<Milestone>> _milestoneRepository;

        public UnitOfWork(MyTestDbContext context)
        {
            _context = context;

            _billRepository = new Lazy<IRepository<Bill>>(() => new Repository<Bill>(_context));
            _customRepository = new Lazy<IRepository<Custom>>(() => new Repository<Custom>(_context));
            _timeLineRepository = new Lazy<IRepository<TimeLine>>(() => new Repository<TimeLine>(_context));
            _milestoneRepository = new Lazy<IRepository<Milestone>>(() => new Repository<Milestone>(_context));
        }

        // 实现接口的属性，返回 Lazy<T> 的 Value
        public IRepository<Bill> Bills => _billRepository.Value;

        public IRepository<Custom> Customs => _customRepository.Value;
        public IRepository<TimeLine> TimeLines => _timeLineRepository.Value;
        public IRepository<Milestone> Milestones => _milestoneRepository.Value;

        public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
        }
    }
}