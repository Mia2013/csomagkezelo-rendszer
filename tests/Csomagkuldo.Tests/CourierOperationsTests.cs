using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Csomagkuldo.Data;
using Csomagkuldo.Enums;
using Csomagkuldo.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csomagkuldo.Tests;

[TestClass]
[DoNotParallelize]
public class CourierOperationsTests
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
        var otherCourier = new User { UserName = "othercourier", Role = UserRole.Courier };
        db.Users.AddRange(courier, otherCourier);
        await db.SaveChangesAsync();
        _courierId = courier.Id;
        _otherCourierId = otherCourier.Id;
        db.CourierCars.AddRange(
            new CourierCar { LicensePlate = "TEST-001", Capacity = 100, CourierId = _courierId },
            new CourierCar { LicensePlate = "TEST-002", Capacity = 100, CourierId = _otherCourierId });
        await db.SaveChangesAsync();
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role, int id = 0)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", id.ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private async Task<Package> AddPackage(PackageStatus status, int? courierId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(),
            SenderName = "Teszt Feladó", SenderAddress = "Veszprém, Teszt utca 1.",
            RecipientName = "Teszt Címzett", RecipientAddress = "Pápa, Teszt utca 2.",
            Value = 1000, Weight = 500, Size = "S", PickupDate = DateTime.Today.AddDays(1),
            CurrentStatus = status, CourierId = courierId
        };
        package.StatusHistory.Add(new PackageStatusHistory { Status = status, CourierId = courierId });
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private async Task<CourierTask> AddTask(Package package, CourierTaskType type, CourierTaskStatus status, int courierId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = new CourierTask
        {
            PackageId = package.Id, CourierId = courierId, TaskType = type, Status = status,
            IsLocked = status != CourierTaskStatus.Assigned
        };
        db.CourierTasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    [DataTestMethod]
    [DataRow("pickuptasks", CourierTaskType.Pickup, PackageStatus.AssignedForPickup)]
    [DataRow("deliverytasks", CourierTaskType.Delivery, PackageStatus.AssignedForDelivery)]
    public async Task TaskListsReturnActualTasks(string endpoint, CourierTaskType type, PackageStatus status)
    {
        var package = await AddPackage(status, _courierId);
        var expected = await AddTask(package, type, CourierTaskStatus.Assigned, _courierId);
        var other = await AddPackage(status, _otherCourierId);
        await AddTask(other, type, CourierTaskStatus.Assigned, _otherCourierId);
        var completed = await AddPackage(status, _courierId);
        await AddTask(completed, type, CourierTaskStatus.Completed, _courierId);
        var wrongType = await AddPackage(status, _courierId);
        await AddTask(wrongType, type == CourierTaskType.Pickup ? CourierTaskType.Delivery : CourierTaskType.Pickup,
            CourierTaskStatus.Assigned, _courierId);
        SignIn("Courier", _courierId);

        var response = await _client.GetAsync($"/api/Package/{_courierId}/{endpoint}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(JsonValueKind.Array, json.ValueKind);
        Assert.AreEqual(1, json.GetArrayLength());
        Assert.AreEqual(expected.Id, json[0].GetProperty("id").GetInt32());
        Assert.AreEqual(package.Id, json[0].GetProperty("packageId").GetInt32());
        Assert.AreEqual(package.TrackingNumber, json[0].GetProperty("trackingNumber").GetString());
        Assert.AreEqual(type.ToString(), json[0].GetProperty("taskType").GetString());
        Assert.IsTrue(json[0].GetProperty("assignmentTime").GetString()!.EndsWith("Z"));
        Assert.IsFalse(json[0].TryGetProperty("courier", out _));
        Assert.IsFalse(json[0].TryGetProperty("package", out _));
    }

    [DataTestMethod]
    [DataRow("pickuptasks")]
    [DataRow("deliverytasks")]
    public async Task EmptyTaskListIsAnEmptyArray(string endpoint)
    {
        SignIn("Courier", _courierId);
        var json = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_courierId}/{endpoint}");
        Assert.AreEqual(JsonValueKind.Array, json.ValueKind);
        Assert.AreEqual(0, json.GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("pickuptasks")]
    [DataRow("deliverytasks")]
    public async Task MissingCourierReturnsNotFound(string endpoint)
    {
        SignIn("Admin");
        Assert.AreEqual(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/Package/{int.MaxValue}/{endpoint}")).StatusCode);
    }

    [DataTestMethod]
    [DataRow("pickuptasks")]
    [DataRow("deliverytasks")]
    public async Task CourierCannotReadAnotherCouriersTasks(string endpoint)
    {
        SignIn("Courier", _otherCourierId);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/Package/{_courierId}/{endpoint}")).StatusCode);
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task WarehouseCanReadUnassignedPackages(string role)
    {
        var expected = await AddPackage(PackageStatus.PreBooked);
        await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        await AddPackage(PackageStatus.PreBooked, _courierId);
        SignIn(role);
        var json = await _client.GetFromJsonAsync<JsonElement>("/api/Package/unassignedPrebooked");
        Assert.AreEqual(1, json.GetArrayLength());
        Assert.AreEqual(expected.Id, json[0].GetProperty("id").GetInt32());
    }

    [TestMethod]
    public async Task EmptyUnassignedPackagesIsAnEmptyArray()
    {
        SignIn("WarehouseOperator");
        var json = await _client.GetFromJsonAsync<JsonElement>("/api/Package/unassignedPrebooked");
        Assert.AreEqual(JsonValueKind.Array, json.ValueKind);
        Assert.AreEqual(0, json.GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("assignforpickup", CourierTaskType.Pickup, PackageStatus.PreBooked, PackageStatus.AssignedForPickup)]
    [DataRow("assignfordelivery", CourierTaskType.Delivery, PackageStatus.AtWarehouse, PackageStatus.AssignedForDelivery)]
    public async Task AssignmentSavesCorrectTaskAndCourier(string endpoint, CourierTaskType type, PackageStatus before, PackageStatus after)
    {
        var package = await AddPackage(before);
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Packages.SingleAsync();
        var task = await db.CourierTasks.SingleAsync();
        var history = await db.PackageStatuses.OrderByDescending(item => item.Id).FirstAsync();
        Assert.AreEqual(_courierId, saved.CourierId);
        Assert.AreEqual(after, saved.CurrentStatus);
        Assert.AreEqual(type, task.TaskType);
        Assert.AreEqual(_courierId, task.CourierId);
        Assert.AreEqual(CourierTaskStatus.Assigned, task.Status);
        Assert.IsFalse(task.IsLocked);
        Assert.AreEqual(after, history.Status);
        Assert.AreEqual(_courierId, history.CourierId);
        Assert.AreEqual(task.AssignmentTime, history.ChangedAt);
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
    }

    [DataTestMethod]
    [DataRow("assignforpickup", true)]
    [DataRow("assignforpickup", false)]
    [DataRow("assignfordelivery", true)]
    [DataRow("assignfordelivery", false)]
    public async Task MissingAssignmentDataDoesNotWriteAnything(string endpoint, bool missingPackage)
    {
        var package = await AddPackage(PackageStatus.PreBooked);
        int packageId = missingPackage ? int.MaxValue : package.Id;
        int courierId = missingPackage ? _courierId : int.MaxValue;
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{packageId}/{endpoint}", new { courierId });
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(0, await db.CourierTasks.CountAsync());
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
        Assert.IsNull((await db.Packages.SingleAsync()).CourierId);
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    public async Task CustomerCannotBeAssignedAsCourier(string endpoint)
    {
        var package = await AddPackage(PackageStatus.PreBooked);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = new User { UserName = "customer", Role = UserRole.Customer };
        db.Users.Add(customer);
        await db.SaveChangesAsync();
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = customer.Id });
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual(0, await db.CourierTasks.CountAsync());
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    public async Task AssignmentCannotCreateSecondActiveTask(string endpoint)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        await AddTask(package, CourierTaskType.Pickup, CourierTaskStatus.Assigned, _courierId);
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _otherCourierId });
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(_courierId, (await db.Packages.SingleAsync()).CourierId);
    }

    [TestMethod]
    public async Task PickupUpdatesTheStartedTaskNotAnOldCompletedTask()
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var oldTask = await AddTask(package, CourierTaskType.Pickup, CourierTaskStatus.Completed, _courierId);
        var activeTask = await AddTask(package, CourierTaskType.Pickup, CourierTaskStatus.Assigned, _courierId);
        SignIn("Courier", _courierId);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/Package/{package.Id}/startpickup", new { courierId = _courierId })).StatusCode);
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/pickuppackage", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(CourierTaskStatus.Completed, (await db.CourierTasks.FindAsync(oldTask.Id))!.Status);
        Assert.AreEqual(CourierTaskStatus.InProgress, (await db.CourierTasks.FindAsync(activeTask.Id))!.Status);
        Assert.IsTrue((await db.CourierTasks.FindAsync(activeTask.Id))!.IsLocked);
        Assert.AreEqual(PackageStatus.InTransitToWarehouse, (await db.Packages.SingleAsync()).CurrentStatus);
        var history = await db.PackageStatuses.OrderByDescending(item => item.Id).FirstAsync();
        Assert.AreEqual(PackageStatus.InTransitToWarehouse, history.Status);
        Assert.AreEqual(_courierId, history.CourierId);
    }

    [TestMethod]
    public async Task WarehouseReceiptFindsTaskByCourierIdAndReleasesPackage()
    {
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        var task = await AddTask(package, CourierTaskType.Pickup, CourierTaskStatus.InProgress, _courierId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // A két azonosító szándékosan különbözzön: ez volt a hibás keresés esete.
        if (task.Id == _courierId)
        {
            _courierId = _otherCourierId;
            (await db.CourierTasks.FindAsync(task.Id))!.CourierId = _courierId;
            (await db.Packages.FindAsync(package.Id))!.CourierId = _courierId;
            await db.SaveChangesAsync();
        }
        Assert.AreNotEqual(task.Id, _courierId);
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/receivetowarehouse", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        db.ChangeTracker.Clear();
        Assert.AreEqual(CourierTaskStatus.Completed, (await db.CourierTasks.FindAsync(task.Id))!.Status);
        Assert.IsTrue((await db.CourierTasks.FindAsync(task.Id))!.IsLocked);
        var saved = await db.Packages.SingleAsync();
        Assert.AreEqual(PackageStatus.AtWarehouse, saved.CurrentStatus);
        Assert.IsNull(saved.CourierId);
        var history = await db.PackageStatuses.OrderByDescending(item => item.Id).FirstAsync();
        Assert.AreEqual(PackageStatus.AtWarehouse, history.Status);
        Assert.AreEqual(_courierId, history.CourierId);
    }

    [DataTestMethod]
    [DataRow("pickuppackage", "package")]
    [DataRow("pickuppackage", "courier")]
    [DataRow("pickuppackage", "task")]
    [DataRow("receivetowarehouse", "package")]
    [DataRow("receivetowarehouse", "courier")]
    [DataRow("receivetowarehouse", "task")]
    public async Task MissingPickupOrReceiptDataDoesNotChangePackage(string endpoint, string missing)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        int packageId = missing == "package" ? int.MaxValue : package.Id;
        int courierId = missing == "courier" ? int.MaxValue : _courierId;
        SignIn("Admin");
        var response = await _client.PutAsJsonAsync($"/api/Package/{packageId}/{endpoint}", new { courierId });
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(PackageStatus.AssignedForPickup, (await db.Packages.SingleAsync()).CurrentStatus);
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(0, await db.CourierTasks.CountAsync());
    }

    [DataTestMethod]
    [DataRow("pickuppackage", CourierTaskStatus.Assigned)]
    [DataRow("receivetowarehouse", CourierTaskStatus.InProgress)]
    public async Task DeliveryTaskCannotBeUsedAsPickupTask(string endpoint, CourierTaskStatus status)
    {
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        var task = await AddTask(package, CourierTaskType.Delivery, status, _courierId);
        SignIn("Admin");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(status, (await db.CourierTasks.FindAsync(task.Id))!.Status);
        Assert.AreEqual(PackageStatus.InTransitToWarehouse, (await db.Packages.SingleAsync()).CurrentStatus);
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
    }

    [DataTestMethod]
    [DataRow("pickuppackage")]
    [DataRow("receivetowarehouse")]
    public async Task CourierCannotModifyAnotherCouriersTask(string endpoint)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        SignIn("Courier", _otherCourierId);
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [TestMethod]
    public async Task PickupWarehouseAndDeliveryAssignmentWorkTogether()
    {
        var package = await AddPackage(PackageStatus.PreBooked);
        SignIn("WarehouseOperator");
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/Package/{package.Id}/assignforpickup", new { courierId = _courierId })).StatusCode);

        SignIn("Courier", _courierId);
        var pickups = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_courierId}/pickuptasks");
        Assert.AreEqual(1, pickups.GetArrayLength());
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/Package/{package.Id}/startpickup", new { courierId = _courierId })).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/Package/{package.Id}/pickuppackage", new { courierId = _courierId })).StatusCode);

        SignIn("WarehouseOperator");
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/Package/{package.Id}/receivetowarehouse", new { courierId = _courierId })).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(
            $"/api/Package/{package.Id}/assignfordelivery", new { courierId = _otherCourierId })).StatusCode);

        SignIn("Courier", _otherCourierId);
        var deliveries = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_otherCourierId}/deliverytasks");
        Assert.AreEqual(1, deliveries.GetArrayLength());
        Assert.AreEqual("Delivery", deliveries[0].GetProperty("taskType").GetString());
        var tracking = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + package.TrackingNumber);
        Assert.AreEqual("AssignedForDelivery", tracking.GetProperty("status").GetString());
        Assert.AreEqual(5, tracking.GetProperty("history").GetArrayLength());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(_otherCourierId, (await db.Packages.SingleAsync()).CourierId);
        Assert.AreEqual(1, await db.CourierTasks.CountAsync(task => task.Status == CourierTaskStatus.Completed));
        Assert.AreEqual(1, await db.CourierTasks.CountAsync(task => task.Status == CourierTaskStatus.Assigned));
    }

    private async Task AssertUnchanged(Package package, CourierTask? task = null, int historyCount = 1)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Packages.SingleAsync();
        Assert.AreEqual(package.CurrentStatus, saved.CurrentStatus);
        Assert.AreEqual(package.CourierId, saved.CourierId);
        Assert.AreEqual(historyCount, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(task == null ? 0 : 1, await db.CourierTasks.CountAsync());
        if (task != null)
        {
            var savedTask = await db.CourierTasks.SingleAsync();
            Assert.AreEqual(task.Id, savedTask.Id);
            Assert.AreEqual(task.CourierId, savedTask.CourierId);
            Assert.AreEqual(task.Status, savedTask.Status);
            Assert.AreEqual(task.TaskType, savedTask.TaskType);
            Assert.AreEqual(task.IsLocked, savedTask.IsLocked);
        }
    }

    [DataTestMethod]
    [DataRow("assignforpickup", PackageStatus.PreBooked)]
    [DataRow("assignfordelivery", PackageStatus.AtWarehouse)]
    public async Task InactiveCourierCannotReceiveNewTask(string endpoint, PackageStatus status)
    {
        var package = await AddPackage(status);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Users.FindAsync(_courierId))!.IsActive = false;
        await db.SaveChangesAsync();
        SignIn("WarehouseOperator");

        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("assignforpickup", PackageStatus.AtWarehouse)]
    [DataRow("assignforpickup", PackageStatus.Delivered)]
    [DataRow("assignforpickup", PackageStatus.InTransitToWarehouse)]
    [DataRow("assignfordelivery", PackageStatus.PreBooked)]
    [DataRow("assignfordelivery", PackageStatus.InTransitToWarehouse)]
    [DataRow("assignfordelivery", PackageStatus.Delivered)]
    public async Task AssignmentRejectsInvalidPackageState(string endpoint, PackageStatus status)
    {
        var package = await AddPackage(status);
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("assignforpickup", PackageStatus.PreBooked)]
    [DataRow("assignfordelivery", PackageStatus.AtWarehouse)]
    public async Task AssignmentRejectsExistingCourierLink(string endpoint, PackageStatus status)
    {
        var package = await AddPackage(status, _otherCourierId);
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("pickuppackage", PackageStatus.PreBooked, CourierTaskStatus.InProgress)]
    [DataRow("pickuppackage", PackageStatus.AtWarehouse, CourierTaskStatus.InProgress)]
    [DataRow("receivetowarehouse", PackageStatus.AssignedForPickup, CourierTaskStatus.InProgress)]
    [DataRow("receivetowarehouse", PackageStatus.Delivered, CourierTaskStatus.InProgress)]
    public async Task StatusChangeRejectsWrongPackageState(string endpoint, PackageStatus status, CourierTaskStatus taskStatus)
    {
        var package = await AddPackage(status, _courierId);
        var task = await AddTask(package, CourierTaskType.Pickup, taskStatus, _courierId);
        SignIn(endpoint == "receivetowarehouse" ? "WarehouseOperator" : "Courier", _courierId);
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        await AssertUnchanged(package, task);
    }

    [DataTestMethod]
    [DataRow("pickuppackage", PackageStatus.AssignedForPickup, CourierTaskStatus.InProgress)]
    [DataRow("receivetowarehouse", PackageStatus.InTransitToWarehouse, CourierTaskStatus.InProgress)]
    public async Task StatusChangeRejectsConflictingCourierLink(string endpoint, PackageStatus status, CourierTaskStatus taskStatus)
    {
        var package = await AddPackage(status, _otherCourierId);
        var task = await AddTask(package, CourierTaskType.Pickup, taskStatus, _courierId);
        SignIn(endpoint == "receivetowarehouse" ? "WarehouseOperator" : "Courier", _courierId);
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        await AssertUnchanged(package, task);
    }

    [DataTestMethod]
    [DataRow("assignforpickup", PackageStatus.PreBooked)]
    [DataRow("assignfordelivery", PackageStatus.AtWarehouse)]
    public async Task RepeatedAssignmentDoesNotDuplicateTaskOrHistory(string endpoint, PackageStatus status)
    {
        var package = await AddPackage(status);
        SignIn("WarehouseOperator");
        var url = $"/api/Package/{package.Id}/{endpoint}";
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(url, new { courierId = _courierId })).StatusCode);
        Assert.AreEqual(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync(url, new { courierId = _courierId })).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(_courierId, (await db.Packages.SingleAsync()).CourierId);
    }

    [DataTestMethod]
    [DataRow("pickuppackage", PackageStatus.AssignedForPickup, CourierTaskStatus.InProgress, PackageStatus.InTransitToWarehouse, CourierTaskStatus.InProgress)]
    [DataRow("receivetowarehouse", PackageStatus.InTransitToWarehouse, CourierTaskStatus.InProgress, PackageStatus.AtWarehouse, CourierTaskStatus.Completed)]
    public async Task RepeatedStatusChangeDoesNotAddHistory(string endpoint, PackageStatus before, CourierTaskStatus taskBefore,
        PackageStatus after, CourierTaskStatus taskAfter)
    {
        var package = await AddPackage(before, _courierId);
        var task = await AddTask(package, CourierTaskType.Pickup, taskBefore, _courierId);
        SignIn(endpoint == "receivetowarehouse" ? "WarehouseOperator" : "Courier", _courierId);
        var url = $"/api/Package/{package.Id}/{endpoint}";
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync(url, new { courierId = _courierId })).StatusCode);
        var repeatStatus = endpoint == "pickuppackage" ? HttpStatusCode.Conflict : HttpStatusCode.NotFound;
        Assert.AreEqual(repeatStatus, (await _client.PutAsJsonAsync(url, new { courierId = _courierId })).StatusCode);

        package.CurrentStatus = after;
        package.CourierId = after == PackageStatus.AtWarehouse ? null : _courierId;
        task.Status = taskAfter;
        task.IsLocked = true;
        await AssertUnchanged(package, task, 2);
    }

    [DataTestMethod]
    [DataRow("assignforpickup", "WarehouseOperator")]
    [DataRow("assignfordelivery", "WarehouseOperator")]
    [DataRow("assignforpickup", "Admin")]
    [DataRow("assignfordelivery", "Admin")]
    public async Task LockedTaskCannotBeReplacedByAnotherAssignment(string endpoint, string role)
    {
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        var task = await AddTask(package, CourierTaskType.Pickup, CourierTaskStatus.InProgress, _courierId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.CourierTasks.FindAsync(task.Id))!.IsLocked = true;
        await db.SaveChangesAsync();
        task.IsLocked = true;
        SignIn(role);

        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _otherCourierId });
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        await AssertUnchanged(package, task);
    }

    [DataTestMethod]
    [DataRow("pickuppackage", PackageStatus.AssignedForPickup, CourierTaskStatus.Assigned)]
    [DataRow("receivetowarehouse", PackageStatus.InTransitToWarehouse, CourierTaskStatus.InProgress)]
    public async Task CourierCannotUseOwnIdToChangeAnotherCouriersPackage(string endpoint, PackageStatus status, CourierTaskStatus taskStatus)
    {
        var package = await AddPackage(status, _otherCourierId);
        var task = await AddTask(package, CourierTaskType.Pickup, taskStatus, _otherCourierId);
        SignIn(endpoint == "receivetowarehouse" ? "WarehouseOperator" : "Courier", _courierId);
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnchanged(package, task);
    }

    [DataTestMethod]
    [DataRow("assignforpickup")]
    [DataRow("assignfordelivery")]
    [DataRow("pickuppackage")]
    [DataRow("receivetowarehouse")]
    public async Task CustomerCannotPerformWorkerOperations(string endpoint)
    {
        var package = await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        var task = await AddTask(package, CourierTaskType.Pickup, CourierTaskStatus.Assigned, _courierId);
        SignIn("Customer", _courierId);
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = _courierId });
        Assert.AreEqual(HttpStatusCode.Forbidden, response.StatusCode);
        await AssertUnchanged(package, task);
    }

    [DataTestMethod]
    [DataRow("pickuptasks")]
    [DataRow("deliverytasks")]
    public async Task CustomerCannotReadCourierLists(string endpoint)
    {
        SignIn("Customer", _courierId);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/Package/{_courierId}/{endpoint}")).StatusCode);
    }

    [DataTestMethod]
    [DataRow("pickuptasks", PackageStatus.AssignedForPickup, CourierTaskType.Pickup)]
    [DataRow("deliverytasks", PackageStatus.AssignedForDelivery, CourierTaskType.Delivery)]
    public async Task AdminCanReadAnotherCouriersTasks(string endpoint, PackageStatus status, CourierTaskType type)
    {
        var package = await AddPackage(status, _courierId);
        var task = await AddTask(package, type, CourierTaskStatus.Assigned, _courierId);
        SignIn("Admin");
        var json = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_courierId}/{endpoint}");
        Assert.AreEqual(1, json.GetArrayLength());
        Assert.AreEqual(task.Id, json[0].GetProperty("id").GetInt32());
        Assert.IsFalse(json[0].GetProperty("isLocked").GetBoolean());
    }
}
