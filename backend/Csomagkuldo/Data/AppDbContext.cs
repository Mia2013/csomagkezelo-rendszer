using Csomagkuldo.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Csomagkuldo.Data
{
    public class AppDbContext : IdentityUserContext<User, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        // Users táblát már tartalmazza az "IdentityUserContext<User, int>"
        public DbSet<CourierTask> CourierTasks { get; set; } = null!;
        public DbSet<Package> Packages { get; set; } = null!;
        public DbSet<PackageStatusHistory> PackageStatuses { get; set; } = null!;
        public DbSet<WarehouseLog> WarehouseLogs { get; set; } = null!;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<Enum>().HaveConversion<string>();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //értelmesen töröljön
            modelBuilder.Entity<CourierTask>()
                .HasOne(task => task.Courier)
                .WithMany(user => user.CourierTasks)
                .HasForeignKey(task => task.CourierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
