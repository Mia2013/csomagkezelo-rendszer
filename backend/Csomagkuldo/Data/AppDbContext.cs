using Csomagkuldo.Models;
using Csomagkuldo.Enums;
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
        public DbSet<CourierCar> CourierCars { get; set; } = null!;
        public DbSet<ShippingFee> ShippingFees { get; set; } = null!;

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            configurationBuilder.Properties<Enum>().HaveConversion<string>();
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Package>().Property(p => p.TrackingNumber).HasMaxLength(36);
            modelBuilder.Entity<Package>().HasIndex(p => p.TrackingNumber).IsUnique();
            modelBuilder.Entity<Package>().Property(p => p.SenderPhoneNumber).HasMaxLength(30);
            modelBuilder.Entity<Package>().Property(p => p.RecipientPhoneNumber).HasMaxLength(30);
            modelBuilder.Entity<Package>().Property(p => p.RecipientEmail).HasMaxLength(254);
            modelBuilder.Entity<Package>().Property(p => p.PickupSlot).HasMaxLength(11);
            modelBuilder.Entity<Package>().ToTable(table =>
                table.HasCheckConstraint("CK_Packages_ShippingCost", "[ShippingCost] IS NULL OR [ShippingCost] >= 0"));
            modelBuilder.Entity<Package>()
                .HasOne(package => package.ShippingFee)
                .WithMany(fee => fee.Packages)
                .HasForeignKey(package => package.ShippingFeeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ShippingFee>().Property(fee => fee.Type).HasMaxLength(10);
            modelBuilder.Entity<ShippingFee>().Property(fee => fee.MaxSize).HasMaxLength(1);
            modelBuilder.Entity<ShippingFee>().HasIndex(fee => fee.Type).IsUnique();
            modelBuilder.Entity<ShippingFee>().HasIndex(fee => fee.MaxSize).IsUnique();
            modelBuilder.Entity<ShippingFee>().ToTable(table =>
            {
                table.HasCheckConstraint("CK_ShippingFees_Prices", "[RegisteredPrice] >= 0 AND [UnregisteredPrice] >= 0");
                table.HasCheckConstraint("CK_ShippingFees_MaxSize", "[MaxSize] IN ('S', 'M', 'L')");
            });
            modelBuilder.Entity<ShippingFee>().HasData(
                new ShippingFee { Id = 1, Type = ShippingFeeType.Small, MaxSize = "S", RegisteredPrice = 1000, UnregisteredPrice = 1000 },
                new ShippingFee { Id = 2, Type = ShippingFeeType.Medium, MaxSize = "M", RegisteredPrice = 2000, UnregisteredPrice = 2000 },
                new ShippingFee { Id = 3, Type = ShippingFeeType.Large, MaxSize = "L", RegisteredPrice = 4000, UnregisteredPrice = 4000 });

            modelBuilder.Entity<CourierCar>().Property(car => car.LicensePlate).HasMaxLength(20);
            modelBuilder.Entity<CourierCar>().Property(car => car.Manufacturer).HasMaxLength(100);
            modelBuilder.Entity<CourierCar>().Property(car => car.Model).HasMaxLength(100);
            modelBuilder.Entity<CourierCar>().HasIndex(car => car.LicensePlate).IsUnique();
            modelBuilder.Entity<CourierCar>().HasIndex(car => car.CourierId).IsUnique();
            modelBuilder.Entity<CourierCar>().ToTable(table =>
                table.HasCheckConstraint("CK_CourierCars_Capacity", "[Capacity] > 0"));
            modelBuilder.Entity<CourierCar>()
                .HasOne(car => car.Courier)
                .WithOne(user => user.CourierCar)
                .HasForeignKey<CourierCar>(car => car.CourierId)
                .OnDelete(DeleteBehavior.Restrict);

            // A futár feladatai maradjanak meg törlési kísérletnél.
            modelBuilder.Entity<CourierTask>()
                .HasOne(task => task.Courier)
                .WithMany(user => user.CourierTasks)
                .HasForeignKey(task => task.CourierId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
