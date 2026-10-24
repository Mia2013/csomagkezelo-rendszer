using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
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
public class WarehouseReceiptTests
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
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CourierCars; DELETE FROM WarehouseLogs; DELETE FROM CourierTasks; DELETE FROM PackageStatuses; DELETE FROM Packages; DELETE FROM AspNetUsers;");
        var courier = new User { UserName = "courier", Role = UserRole.Courier };
        var other = new User { UserName = "other", Role = UserRole.Courier };
        db.Users.AddRange(courier, other,
            new User { UserName = "inactive", Role = UserRole.Courier, IsActive = false },
            new User { UserName = "customer", Role = UserRole.Customer });
        await db.SaveChangesAsync();
        _courierId = courier.Id;
        _otherCourierId = other.Id;
        db.CourierCars.AddRange(
            new CourierCar { LicensePlate = "TEST-001", Capacity = 100, CourierId = _courierId },
            new CourierCar { LicensePlate = "TEST-002", Capacity = 100, CourierId = _otherCourierId });
        await db.SaveChangesAsync();
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role, int? id = null, HttpClient? client = null)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", (id ?? _courierId).ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        (client ?? _client).DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private async Task<Package> AddPackage(PackageStatus status, int? courierId = null, string size = "S")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), Size = size, Weight = 500,
            PickupDate = DateTime.Today.AddDays(1), CurrentStatus = status, CourierId = courierId
        };
        package.StatusHistory.Add(new PackageStatusHistory { Status = status, CourierId = courierId });
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private async Task<CourierTask> AddTask(Package package, CourierTaskStatus status,
        CourierTaskType type = CourierTaskType.Pickup, bool? locked = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = new CourierTask
        {
            PackageId = package.Id, CourierId = package.CourierId ?? _courierId,
            Status = status, TaskType = type, IsLocked = locked ?? status != CourierTaskStatus.Assigned
        };
        db.CourierTasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    private async Task AddStock(int count, string size = "L", PackageStatus status = PackageStatus.AtWarehouse)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        for (int i = 0; i < count; i++)
            db.Packages.Add(new Package { TrackingNumber = Guid.NewGuid().ToString(), Size = size, CurrentStatus = status });
        await db.SaveChangesAsync();
    }

    private async Task AssertDenied(Package package, CourierTask? task)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = (await db.Packages.FindAsync(package.Id))!;
        Assert.AreEqual(package.CurrentStatus, saved.CurrentStatus);
        Assert.AreEqual(package.CourierId, saved.CourierId);
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync(item => item.PackageId == package.Id));
        Assert.AreEqual(0, await db.WarehouseLogs.CountAsync());
        Assert.AreEqual(task == null ? 0 : 1, await db.CourierTasks.CountAsync());
        if (task != null)
        {
            var savedTask = (await db.CourierTasks.FindAsync(task.Id))!;
            Assert.AreEqual(task.CourierId, savedTask.CourierId);
            Assert.AreEqual(task.Status, savedTask.Status);
            Assert.AreEqual(task.IsLocked, savedTask.IsLocked);
        }
    }

    [DataTestMethod]
    [DataRow("S", 1)]
    [DataRow("M", 2)]
    [DataRow("L", 3)]
    [DataRow("XL", 0)]
    public void PackageSizesHaveAgreedSpace(string size, int expected)
    {
        Assert.AreEqual(expected, PackageSizeHelper.GetSpace(size));
    }

    [DataTestMethod]
    [DataRow("Courier")]
    [DataRow("Admin")]
    public async Task StartLocksTaskWithoutPretendingPackageWasPickedUp(string role)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, CourierTaskStatus.Assigned);
        SignIn(role);
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/startpickup", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(PackageStatus.AssignedForPickup, (await db.Packages.FindAsync(package.Id))!.CurrentStatus);
        var savedTask = (await db.CourierTasks.FindAsync(task.Id))!;
        Assert.AreEqual(CourierTaskStatus.InProgress, savedTask.Status);
        Assert.IsTrue(savedTask.IsLocked);
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_courierId}/pickuptasks");
        Assert.AreEqual(1, list.GetArrayLength());
        Assert.AreEqual("InProgress", list[0].GetProperty("status").GetString());
        Assert.IsTrue(list[0].GetProperty("isLocked").GetBoolean());
    }

    [DataTestMethod]
    [DataRow(CourierTaskStatus.Assigned, false, HttpStatusCode.NotFound)]
    [DataRow(CourierTaskStatus.InProgress, false, HttpStatusCode.Conflict)]
    public async Task PickupRequiresStartedLockedTask(CourierTaskStatus status, bool locked, HttpStatusCode expected)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, status, locked: locked);
        SignIn("Courier");
        Assert.AreEqual(expected, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/pickuppackage", new { courierId = _courierId })).StatusCode);
        await AssertDenied(package, task);
    }

    [TestMethod]
    public async Task StartedButNotPickedUpPackageCannotBeReceived()
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, CourierTaskStatus.InProgress);
        SignIn("WarehouseOperator");
        Assert.AreEqual(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/receivetowarehouse", new { courierId = _courierId })).StatusCode);
        await AssertDenied(package, task);
    }

    [DataTestMethod]
    [DataRow("receivetowarehouse", "Courier")]
    [DataRow("receivetowarehouse", "Customer")]
    [DataRow("startpickup", "WarehouseOperator")]
    [DataRow("startpickup", "Customer")]
    [DataRow("reassigncourier", "Courier")]
    [DataRow("reassigncourier", "Customer")]
    public async Task WrongRoleCannotPerformOperation(string endpoint, string role)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, CourierTaskStatus.Assigned);
        SignIn(role);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId })).StatusCode);
        await AssertDenied(package, task);
    }

    [DataTestMethod]
    [DataRow("startpickup")]
    [DataRow("pickuppackage")]
    public async Task OtherCourierCannotStartOrPickup(string endpoint)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, CourierTaskStatus.Assigned);
        SignIn("Courier", _otherCourierId);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId })).StatusCode);
        await AssertDenied(package, task);
    }

    [DataTestMethod]
    [DataRow(PackageStatus.PreBooked)]
    [DataRow(PackageStatus.AtWarehouse)]
    [DataRow(PackageStatus.AssignedForDelivery)]
    public async Task StartRejectsWrongPackageState(PackageStatus status)
    {
        var package = await AddPackage(status, _courierId);
        var task = await AddTask(package, CourierTaskStatus.Assigned);
        SignIn("Courier");
        Assert.AreEqual(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/startpickup", new { courierId = _courierId })).StatusCode);
        await AssertDenied(package, task);
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task ReceiptLogsActualCountAndSpaceIncludingAssignedPackages(string role)
    {
        await AddStock(1, "S");
        await AddStock(1, "M", PackageStatus.AssignedForDelivery);
        await AddStock(1, "L", PackageStatus.OutForDelivery);
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId, "L");
        var task = await AddTask(package, CourierTaskStatus.InProgress);
        SignIn(role);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/receivetowarehouse", new { courierId = _courierId })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.WarehouseLogs.SingleAsync();
        Assert.AreEqual(3, log.PackageCount);
        Assert.AreEqual(6, log.UsedCapacity);
        Assert.AreEqual(1000, log.Capacity);
        Assert.AreEqual(log.Date, (await db.PackageStatuses.OrderByDescending(item => item.Id).FirstAsync()).ChangedAt);
        Assert.AreEqual(CourierTaskStatus.Completed, (await db.CourierTasks.FindAsync(task.Id))!.Status);
        Assert.AreEqual(PackageStatus.AtWarehouse, (await db.Packages.FindAsync(package.Id))!.CurrentStatus);
        Assert.IsNull((await db.Packages.FindAsync(package.Id))!.CourierId);
    }

    [DataTestMethod]
    [DataRow("S", HttpStatusCode.OK)]
    [DataRow("M", HttpStatusCode.Conflict)]
    public async Task WarehouseCapacityIsRespected(string size, HttpStatusCode expected)
    {
        await AddStock(333); // 999 helyegység.
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId, size);
        var task = await AddTask(package, CourierTaskStatus.InProgress);
        SignIn("WarehouseOperator");
        Assert.AreEqual(expected, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/receivetowarehouse", new { courierId = _courierId })).StatusCode);
        if (expected == HttpStatusCode.Conflict) await AssertDenied(package, task);
        else
        {
            using var scope = _factory.Services.CreateScope();
            var log = await scope.ServiceProvider.GetRequiredService<AppDbContext>().WarehouseLogs.SingleAsync();
            Assert.AreEqual(334, log.PackageCount);
            Assert.AreEqual(1000, log.UsedCapacity);
        }
    }

    [DataTestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task UnknownSizeDoesNotProduceFalseOccupancy(bool incomingIsInvalid)
    {
        if (!incomingIsInvalid) await AddStock(1, "XL");
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId, incomingIsInvalid ? "XL" : "S");
        var task = await AddTask(package, CourierTaskStatus.InProgress);
        SignIn("WarehouseOperator");
        Assert.AreEqual(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/receivetowarehouse", new { courierId = _courierId })).StatusCode);
        await AssertDenied(package, task);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RepeatedReceiptDoesNotDuplicateStockOrLog(bool parallel)
    {
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        await AddTask(package, CourierTaskStatus.InProgress);
        SignIn("WarehouseOperator");
        string url = $"/api/Package/{package.Id}/receivetowarehouse";
        HttpResponseMessage[] responses;
        if (parallel)
            responses = await Task.WhenAll(_client.PutAsJsonAsync(url, new { courierId = _courierId }), _client.PutAsJsonAsync(url, new { courierId = _courierId }));
        else
            responses = new[] { await _client.PutAsJsonAsync(url, new { courierId = _courierId }), await _client.PutAsJsonAsync(url, new { courierId = _courierId }) };
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.IsTrue(responses.All(response => response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.Conflict));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await db.WarehouseLogs.CountAsync());
        Assert.AreEqual(1, (await db.WarehouseLogs.SingleAsync()).PackageCount);
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
    }

    [TestMethod]
    public async Task ConcurrentReceiptsCannotOverfillLastWarehouseSpace()
    {
        await AddStock(333);
        var first = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        var second = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        await AddTask(first, CourierTaskStatus.InProgress);
        await AddTask(second, CourierTaskStatus.InProgress);
        SignIn("WarehouseOperator");
        var responses = await Task.WhenAll(
            _client.PutAsJsonAsync($"/api/Package/{first.Id}/receivetowarehouse", new { courierId = _courierId }),
            _client.PutAsJsonAsync($"/api/Package/{second.Id}/receivetowarehouse", new { courierId = _courierId }));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = await db.WarehouseLogs.SingleAsync();
        Assert.AreEqual(1000, log.UsedCapacity);
        Assert.AreEqual(334, log.PackageCount);
        Assert.AreEqual(1, await db.CourierTasks.CountAsync(task => task.Status == CourierTaskStatus.Completed));
    }

    [DataTestMethod]
    [DataRow(CourierTaskType.Pickup, PackageStatus.AssignedForPickup, "WarehouseOperator")]
    [DataRow(CourierTaskType.Pickup, PackageStatus.AssignedForPickup, "Admin")]
    [DataRow(CourierTaskType.Delivery, PackageStatus.AssignedForDelivery, "WarehouseOperator")]
    [DataRow(CourierTaskType.Delivery, PackageStatus.AssignedForDelivery, "Admin")]
    public async Task BeforeDepartureExistingTaskCanChangeCourier(CourierTaskType type, PackageStatus status, string role)
    {
        var package = await AddPackage(status, _courierId);
        var task = await AddTask(package, CourierTaskStatus.Assigned, type);
        SignIn(role);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/reassigncourier", new { courierId = _otherCourierId })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.CourierTasks.SingleAsync();
        Assert.AreEqual(task.Id, saved.Id);
        Assert.AreEqual(_otherCourierId, saved.CourierId);
        Assert.AreEqual(_otherCourierId, (await db.Packages.SingleAsync()).CourierId);
        Assert.AreEqual(status, (await db.Packages.SingleAsync()).CurrentStatus);
        Assert.AreEqual(CourierTaskStatus.Assigned, saved.Status);
        Assert.IsFalse(saved.IsLocked);
        var history = await db.PackageStatuses.OrderBy(item => item.Id).ToListAsync();
        Assert.AreEqual(2, history.Count);
        Assert.AreEqual(_courierId, history[0].CourierId);
        Assert.AreEqual(_otherCourierId, history[1].CourierId);
        if (type == CourierTaskType.Pickup)
        {
            SignIn("Courier");
            Assert.AreEqual(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/startpickup", new { courierId = _courierId })).StatusCode);
            SignIn("Courier", _otherCourierId);
            Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/startpickup", new { courierId = _otherCourierId })).StatusCode);
        }
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task AfterDepartureReassignmentIsRejected(string role)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, CourierTaskStatus.Assigned);
        SignIn("Courier");
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/startpickup", new { courierId = _courierId })).StatusCode);
        task.Status = CourierTaskStatus.InProgress;
        task.IsLocked = true;
        SignIn(role);
        Assert.AreEqual(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/reassigncourier", new { courierId = _otherCourierId })).StatusCode);
        await AssertDenied(package, task);
    }

    [DataTestMethod]
    [DataRow("inactive")]
    [DataRow("customer")]
    public async Task ReassignmentRequiresActiveCourier(string userName)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, CourierTaskStatus.Assigned);
        using var scope = _factory.Services.CreateScope();
        int targetId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(user => user.UserName == userName).Select(user => user.Id).SingleAsync();
        SignIn("WarehouseOperator");
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/reassigncourier", new { courierId = targetId })).StatusCode);
        await AssertDenied(package, task);
    }

    [TestMethod]
    public async Task StartingAndReassigningConcurrentlyCannotMoveStartedTask()
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        await AddTask(package, CourierTaskStatus.Assigned);
        SignIn("Courier");
        using var warehouseClient = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        SignIn("WarehouseOperator", client: warehouseClient);
        var responses = await Task.WhenAll(
            _client.PutAsJsonAsync($"/api/Package/{package.Id}/startpickup", new { courierId = _courierId }),
            warehouseClient.PutAsJsonAsync($"/api/Package/{package.Id}/reassigncourier", new { courierId = _otherCourierId }));
        Assert.AreEqual(1, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        Assert.IsTrue(responses.All(response => response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict || response.StatusCode == HttpStatusCode.NotFound));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.CourierTasks.SingleAsync();
        Assert.AreEqual(task.CourierId, (await db.Packages.SingleAsync()).CourierId);
        if (task.IsLocked) Assert.AreEqual(_courierId, task.CourierId);
        else Assert.AreEqual(_otherCourierId, task.CourierId);
    }

    [TestMethod]
    public async Task FailedLogInsertRollsBackReceiptAndHistory()
    {
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        var task = await AddTask(package, CourierTaskStatus.InProgress);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE WarehouseLogs ADD CONSTRAINT CK_Test_RejectReceipt CHECK (PackageCount < 0);");
        try
        {
            SignIn("WarehouseOperator");
            var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/receivetowarehouse", new { courierId = _courierId });
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("CK_Test_RejectReceipt"));
            await AssertDenied(package, task);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE WarehouseLogs DROP CONSTRAINT CK_Test_RejectReceipt;");
        }
    }
}
