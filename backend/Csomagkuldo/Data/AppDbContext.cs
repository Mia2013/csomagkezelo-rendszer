using Csomagkezelo.Models;
using Csomagkuldo.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using System.Security.AccessControl;

namespace Csomagkuldo.Data
{
    public class AppDbContext
    {
        public class AppDbContext : IdentityDbContext<ApplicationUser>
        {

            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) 
            {
            }

            public DbSet<User> Users { get; set; }
            public DbSet<CourierTask> CourierTasks { get; set; }
            public DbSet<Package> Packages { get; set; }
            public DbSet<PackageStatusHistory> PackageStatuses { get; set; }
            public DbSet<WarehouseLog> WareHouseLogs { get; set; }


            

            //ha kell valami előre amit be állítunk, ide betehetjük majd:
            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
            
            }
        }
}
