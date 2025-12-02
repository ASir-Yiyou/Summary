using Microsoft.EntityFrameworkCore;
using Summary.Domain.Entities;
using Summary.Domain.Interfaces;

namespace Summary.Common.EFCore.DbContexts
{
    public class AppDbContext : BaseTestDbContext<Guid, Guid>
    {
        private readonly ITestSession<Guid, Guid> _currentUser;

        public AppDbContext(DbContextOptions dbContextOptions,
            ITestSession<Guid, Guid> session) : base(dbContextOptions, session)
        {
            _currentUser = session;
        }

        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Role> Roles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Group>()
                .HasOne(g => g.Parent)
                .WithMany(g => g.Children)
                .HasForeignKey(g => g.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<UserRole>().HasKey(ur => new { ur.UserId, ur.RoleId });
            modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.Resource, rp.Action });
        }
    }
}
