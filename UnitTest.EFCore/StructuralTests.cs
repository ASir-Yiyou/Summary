using Microsoft.EntityFrameworkCore;

namespace UnitTest.EFCore
{
    public record /*struct*/ StatusOption //不建议用struct，虽然可以使用ComplexProperty存但是语义会发生改变，在实现查询的时候并不方便
    {
        public required string Status { get; init; }
        public string? CustomStatus { get; init; }
    }

    public class Ticket
    {
        public int Id { get; set; }
        public required string Subject { get; set; }
        public StatusOption Status { get; set; } =new StatusOption { Status = "Unknown", CustomStatus = null };
        public DateTime CreatedOn { get; set; }
    }
    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<Ticket> Tickets { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Ticket>(entity =>
            {
                entity.OwnsOne/*ComplexProperty*/(e => e.Status, statusBuilder =>
                {
                    statusBuilder.Property(s => s.Status).HasColumnName("Status");
                    statusBuilder.Property(s => s.CustomStatus).HasColumnName("CustomStatus");
                });
            });
        }
    }
    public class StructuralTests
    {
        private TestDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new TestDbContext(options);
        }

        [Fact]
        public async Task AddAndGetTicket_ShouldPreserveStatusValue()
        {
            await using var context = CreateContext();

            var initialStatus = new StatusOption { Status = "Open", CustomStatus = null };
            var newTicket = new Ticket
            {
                Subject = "My first ticket",
                Status = initialStatus,
                CreatedOn = DateTime.UtcNow
            };

            context.Tickets.Add(newTicket);
            await context.SaveChangesAsync();

            var savedTicket = await context.Tickets.FindAsync(newTicket.Id);

            Assert.NotNull(savedTicket);
            Assert.Equal("My first ticket", savedTicket.Subject);

            Assert.Equal(initialStatus, savedTicket.Status);
        }

        [Fact]
        public async Task UpdateTicketStatus_ShouldCorrectlySaveChanges()
        {
            await using var content = CreateContext();
            var initialStatus = new StatusOption { Status = "Open", CustomStatus = null };
            var ticket = new Ticket { Subject = "Ticket to update", Status = initialStatus, CreatedOn = DateTime.UtcNow };
            content.Tickets.Add(ticket);
            await content.SaveChangesAsync();

            var ticketToUpdate = await content.Tickets.FindAsync(ticket.Id);
            Assert.NotNull(ticketToUpdate);

            var newStatus = ticketToUpdate.Status with { Status = "Closed", CustomStatus = "Resolved" };

            ticketToUpdate.Status = newStatus;

            content.SaveChanges();

            var updatedTicket = await content.Tickets.FindAsync(ticket.Id);

            Assert.NotNull(updatedTicket);
            Assert.NotEqual(initialStatus, updatedTicket.Status);
            Assert.Equal(newStatus, updatedTicket.Status);
            Assert.Equal("Closed", updatedTicket.Status.Status);
            Assert.Equal("Resolved", updatedTicket.Status.CustomStatus);
        }

        [Fact]
        public async Task QueryByStatus_ShouldReturnOnlyStatusEqual()
        {
            await using var content = CreateContext();
            //var frtStatus = new StatusOption { Status = "Open", CustomStatus = null };
            //var secStatus = new StatusOption { Status = "Close", CustomStatus = "Done" };

            for (int i = 0; i < 10; i++)
            {
                //不能使用var ticket = new Ticket { Subject = $"Ticket to Open{i}", Status = frtStatus, CreatedOn = DateTime.UtcNow };这种方式循环插入，这样之后最后一个值会被正常赋值
                var ticket = new Ticket { Subject = $"Ticket to Open{i}", Status = new StatusOption { Status = "Open", CustomStatus = null }, CreatedOn = DateTime.UtcNow };
                content.Tickets.Add(ticket);
            }

            for (int i = 0; i < 10; i++)
            {
                var ticket = new Ticket { Subject = $"Ticket to Close{i}", Status = new StatusOption { Status = "Close", CustomStatus = "Done" }, CreatedOn = DateTime.UtcNow };
                content.Tickets.Add(ticket);
            }

            await content.SaveChangesAsync();

            var rlt = content.Tickets.ToList();

            var list = await content.Tickets.Where(t => t.Status.Status == "Open").ToListAsync();
            Assert.Equal(10, list.Count);

            //var list1 = await content.Tickets.Where(t => t.Status == frtStatus).ToListAsync();不能这样写没有Id是不能做判断的
            var list1 = await content.Tickets.Where(t => t.Status.Status == "Close" && t.Status.CustomStatus == "Done").ToListAsync();
            Assert.Equal(10, list1.Count);
        }
    }
}
