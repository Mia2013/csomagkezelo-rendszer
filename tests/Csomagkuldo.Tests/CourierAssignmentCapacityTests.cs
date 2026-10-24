using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Csomagkuldo.Data;
using Csomagkuldo.Enums;
using Csomagkuldo.Helpers;
using Csomagkuldo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csomagkuldo.Tests;

[TestClass]
[DoNotParallelize]
public class CourierAssignmentCapacityTests
{
    private static TestApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private int _courierId;
    private int _otherCourierId;

    [ClassInitialize]
    public static async Task Initialize(TestContext unused)
    {
        _factory = new TestApplicationFactory();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        await db.Database.MigrateAsync();
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        if (_factory == null) return;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            _factory.CheckTestDatabase(db);
            await db.Database.EnsureDeletedAsync();
        }
        await _factory.DisposeAsync();
    }

    [TestInitialize]
    public async Task Reset()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CourierCars; DELETE FROM CourierTasks; DELETE FROM PackageStatuses; DELETE FROM Packages; DELETE FROM AspNetUsers;");
        var courier = new User { UserName = "courier", Role = UserRole.Courier };
        var other = new User { UserName = "other", Role = UserRole.Courier };
        db.Users.AddRange(courier, other);
        await db.SaveChangesAsync();
        _courierId = courier.Id;
        _otherCourierId = other.Id;
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        SignIn();
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role = "WarehouseOperator")
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", _courierId.ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private async Task<CourierCar> AddCar(int capacity, int? courierId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var car = new CourierCar
        {
            Capacity = capacity, CourierId = courierId, LicensePlate = Guid.NewGuid().ToString("N").Substring(0, 10),
            Manufacturer = "Ford", Model = "Transit", Year = 2020
        };
        db.CourierCars.Add(car);
        await db.SaveChangesAsync();
        return car;
    }

    private async Task<Package> AddCandidate(string operation, string size = "M")
    {
        bool delivery = operation == "assignfordelivery" || operation == "reassigndelivery";
        bool reassign = operation.StartsWith("reassign");
        var status = delivery ? PackageStatus.AtWarehouse : PackageStatus.PreBooked;
        if (reassign) status = delivery ? PackageStatus.AssignedForDelivery : PackageStatus.AssignedForPickup;
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), Size = size, CurrentStatus = status,
            CourierId = reassign ? _otherCourierId : null
        };
        package.StatusHistory.Add(new PackageStatusHistory { Status = status, CourierId = package.CourierId });
        if (reassign)
            package.CourierTasks.Add(new CourierTask
            {
                CourierId = _otherCourierId, TaskType = delivery ? CourierTaskType.Delivery : CourierTaskType.Pickup
            });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private async Task AddLoad(string size, CourierTaskType type = CourierTaskType.Pickup,
        CourierTaskStatus status = CourierTaskStatus.Assigned, int? courierId = null)
    {
        int ownerId = courierId ?? _courierId;
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), Size = size, CourierId = ownerId,
            CurrentStatus = type == CourierTaskType.Pickup ? PackageStatus.AssignedForPickup : PackageStatus.AssignedForDelivery
        };
        if (status == CourierTaskStatus.InProgress)
            package.CurrentStatus = type == CourierTaskType.Pickup ? PackageStatus.InTransitToWarehouse : PackageStatus.FailedDelivery;
        if (status == CourierTaskStatus.Completed)
        {
            package.CurrentStatus = type == CourierTaskType.Pickup ? PackageStatus.AtWarehouse : PackageStatus.Delivered;
            package.CourierId = null;
        }
        package.CourierTasks.Add(new CourierTask
        {
            CourierId = ownerId, TaskType = type, Status = status, IsLocked = status != CourierTaskStatus.Assigned
        });
        package.StatusHistory.Add(new PackageStatusHistory { Status = package.CurrentStatus, CourierId = ownerId });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Packages.Add(package);
        await db.SaveChangesAsync();
    }

    private Task<HttpResponseMessage> Assign(Package package, string operation, int? courierId = null)
    {
        string endpoint = operation.StartsWith("reassign") ? "reassigncourier" : operation;
        return _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = courierId ?? _courierId });
    }

    private async Task AssertUnchanged(Package package)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = (await db.Packages.FindAsync(package.Id))!;
        Assert.AreEqual(package.CurrentStatus, saved.CurrentStatus);
        Assert.AreEqual(package.CourierId, saved.CourierId);
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync(item => item.PackageId == package.Id));
        var tasks = await db.CourierTasks.Where(task => task.PackageId == package.Id).ToListAsync();
        Assert.AreEqual(package.CourierTasks.Count, tasks.Count);
        if (tasks.Count == 1)
        {
            Assert.AreEqual(package.CourierTasks[0].Id, tasks[0].Id);
            Assert.AreEqual(package.CourierId, tasks[0].CourierId);
            Assert.AreEqual(CourierTaskStatus.Assigned, tasks[0].Status);
            Assert.IsFalse(tasks[0].IsLocked);
        }
    }

    private async Task AssertValidAllocation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int used = (await CourierCapacityHelper.GetUsedSpace(db, _courierId))!.Value;
        var car = await db.CourierCars.SingleOrDefaultAsync(car => car.CourierId == _courierId);
        if (car == null) Assert.AreEqual(0, used);
        else Assert.IsTrue(used <= car.Capacity);
        Assert.IsFalse(await db.CourierTasks.Where(task => task.Status != CourierTaskStatus.Completed)
            .GroupBy(task => task.PackageId).AnyAsync(group => group.Count() > 1));
        var assigned = await db.Packages.Where(package => package.CourierId == _courierId).ToListAsync();
        foreach (var package in assigned)
            Assert.AreEqual(1, await db.CourierTasks.CountAsync(task => task.PackageId == package.Id &&
                task.CourierId == _courierId && task.Status != CourierTaskStatus.Completed));
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    [DataRow("reassignpickup")]
    [DataRow("reassigndelivery")]
    public async Task CourierWithoutCarCannotReceivePackage(string operation)
    {
        var package = await AddCandidate(operation);
        var response = await Assign(package, operation);
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "nincs autó");
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    [DataRow("reassignpickup")]
    [DataRow("reassigndelivery")]
    public async Task NewPackageCanExactlyFillSpaceLeftByMixedActiveTasks(string operation)
    {
        await AddCar(6, _courierId);
        await AddLoad("S");
        await AddLoad("M", CourierTaskType.Delivery, CourierTaskStatus.InProgress);
        await AddLoad("L", status: CourierTaskStatus.Completed);
        await AddLoad("L", courierId: _otherCourierId);
        var package = await AddCandidate(operation, "L");
        Assert.AreEqual(HttpStatusCode.OK, (await Assign(package, operation)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(6, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
        Assert.AreEqual(_courierId, (await db.Packages.FindAsync(package.Id))!.CourierId);
        Assert.AreEqual(1, await db.CourierTasks.CountAsync(task => task.PackageId == package.Id));
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync(item => item.PackageId == package.Id));
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    [DataRow("reassignpickup")]
    [DataRow("reassigndelivery")]
    public async Task ExistingReservationsCannotBeOverfilled(string operation)
    {
        await AddCar(3, _courierId);
        await AddLoad("M");
        var package = await AddCandidate(operation, "M");
        Assert.AreEqual(HttpStatusCode.Conflict, (await Assign(package, operation)).StatusCode);
        await AssertUnchanged(package);
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup", false)]
    [DataRow("assignfordelivery", false)]
    [DataRow("reassignpickup", false)]
    [DataRow("reassigndelivery", false)]
    [DataRow("assignforpickup", true)]
    [DataRow("assignfordelivery", true)]
    [DataRow("reassignpickup", true)]
    [DataRow("reassigndelivery", true)]
    public async Task UnknownSizeInCandidateOrExistingLoadStopsAssignment(string operation, bool existing)
    {
        await AddCar(6, _courierId);
        if (existing) await AddLoad("XL");
        var package = await AddCandidate(operation, existing ? "S" : "XL");
        var response = await Assign(package, operation);
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "méret");
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("reassignpickup")]
    [DataRow("reassigndelivery")]
    public async Task ReassignmentReleasesOldSpaceAndDoesNotDuplicateTask(string operation)
    {
        await AddCar(3, _courierId);
        await AddCar(2, _otherCourierId);
        await AddLoad("S");
        var package = await AddCandidate(operation, "M");
        int oldTaskId = package.CourierTasks[0].Id;
        Assert.AreEqual(HttpStatusCode.OK, (await Assign(package, operation)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, (await Assign(package, operation)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(0, await CourierCapacityHelper.GetUsedSpace(db, _otherCourierId));
        Assert.AreEqual(3, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
        var task = await db.CourierTasks.SingleAsync(task => task.PackageId == package.Id);
        Assert.AreEqual(oldTaskId, task.Id);
        Assert.AreEqual(_courierId, task.CourierId);
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync(item => item.PackageId == package.Id));
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup", "assignforpickup")]
    [DataRow("assignfordelivery", "assignfordelivery")]
    [DataRow("assignforpickup", "assignfordelivery")]
    [DataRow("reassignpickup", "reassigndelivery")]
    [DataRow("assignforpickup", "reassignpickup")]
    [DataRow("assignfordelivery", "reassigndelivery")]
    public async Task ConcurrentRequestsCannotTakeTheSameLastSpace(string firstOperation, string secondOperation)
    {
        await AddCar(3, _courierId);
        await AddLoad("M");
        var first = await AddCandidate(firstOperation, "S");
        var second = await AddCandidate(secondOperation, "S");
        var responses = await Task.WhenAll(Assign(first, firstOperation), Assign(second, secondOperation));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await AssertUnchanged(responses[0].StatusCode == HttpStatusCode.Conflict ? first : second);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(3, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
        Assert.AreEqual(4, await db.PackageStatuses.CountAsync());
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    public async Task ConcurrentAssignmentsOfSamePackageLeaveOneTaskAndOneHistoryEntry(string operation)
    {
        await AddCar(3, _courierId);
        await AddCar(3, _otherCourierId);
        var package = await AddCandidate(operation, "L");
        var responses = await Task.WhenAll(Assign(package, operation), Assign(package, operation, _otherCourierId));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.CourierTasks.SingleAsync();
        Assert.AreEqual(task.CourierId, (await db.Packages.SingleAsync()).CourierId);
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    [DataRow("reassignpickup")]
    [DataRow("reassigndelivery")]
    public async Task CapacityReductionCannotRaceWithPackageAssignment(string operation)
    {
        var car = await AddCar(3, _courierId);
        var package = await AddCandidate(operation, "L");
        SignIn("Admin");
        var responses = await Task.WhenAll(
            Assign(package, operation),
            _client.PutAsJsonAsync($"/api/CourierCar/{car.Id}", new
            {
                capacity = 2, licensePlate = car.LicensePlate, manufacturer = car.Manufacturer,
                model = car.Model, year = car.Year
            }));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    public async Task SmallerReplacementCarCannotRaceWithPackageAssignment(string operation)
    {
        await AddCar(3, _courierId);
        var replacement = await AddCar(2);
        var package = await AddCandidate(operation, "L");
        SignIn("Admin");
        var responses = await Task.WhenAll(
            Assign(package, operation),
            _client.PutAsJsonAsync($"/api/CourierCar/{replacement.Id}/assigncourier", new { courierId = _courierId }));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup", "unassign")]
    [DataRow("assignfordelivery", "unassign")]
    [DataRow("assignforpickup", "delete")]
    [DataRow("assignfordelivery", "delete")]
    public async Task CarCannotDisappearWhileReceivingNewPackage(string operation, string removal)
    {
        var car = await AddCar(3, _courierId);
        var package = await AddCandidate(operation, "L");
        SignIn("Admin");
        Task<HttpResponseMessage> remove = removal == "delete"
            ? _client.DeleteAsync($"/api/CourierCar/{car.Id}")
            : _client.PutAsync($"/api/CourierCar/{car.Id}/unassigncourier", null);
        var responses = await Task.WhenAll(Assign(package, operation), remove);
        Assert.AreEqual(1, responses.Count(response => response.IsSuccessStatusCode));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        await AssertValidAllocation();
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    [DataRow("reassignpickup")]
    [DataRow("reassigndelivery")]
    public async Task FailedHistorySaveDoesNotLeavePartialAssignmentOrReservedSpace(string operation)
    {
        await AddCar(6, _courierId);
        var package = await AddCandidate(operation);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE PackageStatuses WITH NOCHECK ADD CONSTRAINT CK_Test_RejectNewHistory CHECK (Id < 0);");
        try
        {
            var response = await Assign(package, operation);
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("CK_Test_RejectNewHistory"));
            await AssertUnchanged(package);
            Assert.AreEqual(0, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE PackageStatuses DROP CONSTRAINT CK_Test_RejectNewHistory;");
        }
        Assert.AreEqual(HttpStatusCode.OK, (await Assign(package, operation)).StatusCode);
        await AssertValidAllocation();
    }
}
