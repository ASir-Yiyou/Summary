using Microsoft.EntityFrameworkCore;
using Summary.Common.EFCore.DbContexts;
using Summary.Common.EFCore.Repositories;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

namespace UnitTest.EFCore
{
    public class BillRepositoryIntegrationTests
    {
        private readonly MyTestDbContext _dbContext;
        private readonly Repository<Bill> _billRepository;
        private readonly DbContextOptions<MyTestDbContext> options;

        public BillRepositoryIntegrationTests()
        {
            options = new DbContextOptionsBuilder<MyTestDbContext>()
               .UseNpgsql("Host=localhost;Database=postgres;User ID=postgres;Password=1023")//实际链接
               .Options;

            var session = new TestSession();

            _dbContext = new MyTestDbContext(options, session);

            _billRepository = new Repository<Bill>(_dbContext);
        }

        [Fact]
        public async Task AddAsync_ShouldAddBillToDatabase()
        {
            var newBill = new Bill { Amount = 100m, Description = "测试", Account = Guid.NewGuid().ToString() };
            await _billRepository.AddAsync(newBill);

            await _dbContext.SaveChangesAsync();

            var savedBill = await _dbContext.Bills.FindAsync(newBill.Id);

            Assert.NotNull(savedBill);
            Assert.Equal(100m, savedBill.Amount);
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnCorrectBill()
        {
            var existingBill = new Bill { Id = Guid.NewGuid(), Amount = 250m };
            _dbContext.Bills.Add(existingBill);
            await _dbContext.SaveChangesAsync();

            var foundBill = await _billRepository.GetByIdAsync(existingBill.Id);

            Assert.NotNull(foundBill);
            Assert.Equal(250m, foundBill.Amount);
        }

        [Fact]
        public async Task SoftDeleteAsync_ShouldReturnNull()
        {
            var existingBill = new Bill { Id = Guid.NewGuid(), Amount = 250m, Description = "软删" };
            _billRepository.Add(existingBill);
            await _dbContext.SaveChangesAsync();

            var frt = await _billRepository.GetByIdAsync(existingBill.Id);

            Assert.NotNull(frt);
            Assert.Equal(250m, frt.Amount);

            _billRepository.Remove(frt);
            await _dbContext.SaveChangesAsync();

            var sec = await _billRepository.FindAsync(t => t.Id == frt.Id);
            Assert.Empty(sec);

            var trd = await _billRepository.GetByIdAsync(existingBill.Id);
            Assert.NotNull(trd);

            var fourth = await _billRepository.GetByIdWithoutTrackerAsync(existingBill.Id);
            Assert.Null(fourth);
        }

        [Fact]
        public async Task FilterTenant_ShouldReturnNull()
        {
            var existingBill = new Bill { Id = Guid.NewGuid(), Amount = 250m, Description = "过滤" };
            _billRepository.Add(existingBill);
            await _dbContext.SaveChangesAsync();
            var session = new TestSession() { TenantId = Guid.NewGuid().ToString() };
            var dbContext = new MyTestDbContext(options, session);
            var billRepository = new Repository<Bill>(dbContext);
            IEnumerable<Bill> sec = await billRepository.FindAsync(t => t.Id == existingBill.Id);
            Assert.True(0 == sec.Count());
        }
    }
}