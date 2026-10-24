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
public class WarehouseOperationsTests
{
    private static TestApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private List<User> _users = null!;
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
        _users = new List<User>
        {
            new User { UserName = "courier", FirstName = "Bela", LastName = "Toth", Role = UserRole.Courier },
            new User { UserName = "other", FirstName = "Anna", LastName = "Kiss", Role = UserRole.Courier },
            new User { UserName = "inactive", FirstName = "Adam", LastName = "Barna", Role = UserRole.Courier, IsActive = false },
            new User { UserName = "customer", Role = UserRole.Customer },
            new User { UserName = "warehouse", Role = UserRole.WarehouseOperator },
            new User { UserName = "admin", Role = UserRole.Admin }
        };
        foreach (var user in _users)
        {
            user.PasswordHash = "private-test-password-hash";
            user.SecurityStamp = "private-test-security-stamp";
        }
        db.Users.AddRange(_users);
        await db.SaveChangesAsync();
        _courierId = _users[0].Id;
        _otherCourierId = _users[1].Id;
        db.CourierCars.AddRange(
            new CourierCar { LicensePlate = "TEST-001", Capacity = 100, CourierId = _courierId },
            new CourierCar { LicensePlate = "TEST-002", Capacity = 100, CourierId = _otherCourierId });
        await db.SaveChangesAsync();
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", _courierId.ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private async Task<Package> AddPackage(PackageStatus status, int? courierId = null, DateTime? createdAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var customer = _users.Single(user => user.Role == UserRole.Customer);
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(),
            SenderId = customer.Id, RecipientId = customer.Id,
            SenderName = "Teszt Feladó", SenderAddress = "Veszprém, Teszt utca 1.",
            RecipientName = "Teszt Címzett", RecipientAddress = "Pápa, Teszt utca 2.",
            SenderPhoneNumber = "+36301234567", RecipientPhoneNumber = "+36201234567",
            RecipientEmail = "recipient@example.test", PickupSlot = "09:00-12:00",
            Value = 1000, Weight = 500, Size = "S", PickupDate = DateTime.Today.AddDays(1),
            CurrentStatus = status, CourierId = courierId, CreatedAt = createdAt ?? DateTime.UtcNow
        };
        package.StatusHistory.Add(new PackageStatusHistory { Status = status, CourierId = courierId });
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private async Task AddTask(Package package, CourierTaskStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CourierTasks.Add(new CourierTask
        {
            PackageId = package.Id, CourierId = _courierId,
            TaskType = CourierTaskType.Pickup, Status = status
        });
        await db.SaveChangesAsync();
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task WarehouseListIncludesWaitingAndAssignedPackages(string role)
    {
        var waiting = await AddPackage(PackageStatus.AtWarehouse);
        var assigned = await AddPackage(PackageStatus.AssignedForDelivery, _courierId);
        await AddPackage(PackageStatus.PreBooked);
        await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        await AddPackage(PackageStatus.OutForDelivery, _courierId);
        await AddPackage(PackageStatus.Delivered);
        await AddPackage(PackageStatus.FailedDelivery, _courierId);
        SignIn(role);

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/warehouse");
        Assert.AreEqual(2, list.GetArrayLength());
        Assert.AreEqual(waiting.Id, list[0].GetProperty("id").GetInt32());
        Assert.AreEqual(assigned.Id, list[1].GetProperty("id").GetInt32());
        Assert.AreEqual("AssignedForDelivery", list[1].GetProperty("currentStatus").GetString());
        Assert.AreEqual(_courierId, list[1].GetProperty("courierId").GetInt32());
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task PickupListOnlyContainsUnassignedBookings(string role)
    {
        var expected = await AddPackage(PackageStatus.PreBooked);
        await AddPackage(PackageStatus.PreBooked, _courierId);
        var conflicting = await AddPackage(PackageStatus.PreBooked);
        await AddTask(conflicting, CourierTaskStatus.Assigned);
        await AddPackage(PackageStatus.AssignedForPickup, _courierId);
        await AddPackage(PackageStatus.AtWarehouse);
        SignIn(role);

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/unassignedPrebooked");
        Assert.AreEqual(1, list.GetArrayLength());
        Assert.AreEqual(expected.Id, list[0].GetProperty("id").GetInt32());
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task UnassignedWarehouseListAllowsCompletedPickupButExcludesActiveTasks(string role)
    {
        var expected = await AddPackage(PackageStatus.AtWarehouse);
        await AddTask(expected, CourierTaskStatus.Completed);
        await AddPackage(PackageStatus.AssignedForDelivery, _courierId);
        await AddPackage(PackageStatus.AtWarehouse, _courierId);
        var conflicting = await AddPackage(PackageStatus.AtWarehouse);
        await AddTask(conflicting, CourierTaskStatus.InProgress);
        await AddPackage(PackageStatus.PreBooked);
        SignIn(role);

        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/unassignedWarehouse");
        Assert.AreEqual(1, list.GetArrayLength());
        Assert.AreEqual(expected.Id, list[0].GetProperty("id").GetInt32());
    }

    [DataTestMethod]
    [DataRow("unassignedPrebooked")]
    [DataRow("warehouse")]
    [DataRow("unassignedWarehouse")]
    [DataRow("assigned")]
    public async Task EmptyPackageListsReturnEmptyArrays(string endpoint)
    {
        SignIn("WarehouseOperator");
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{endpoint}");
        Assert.AreEqual(JsonValueKind.Array, list.ValueKind);
        Assert.AreEqual(0, list.GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("unassignedPrebooked", PackageStatus.PreBooked)]
    [DataRow("warehouse", PackageStatus.AtWarehouse)]
    [DataRow("unassignedWarehouse", PackageStatus.AtWarehouse)]
    public async Task PackageListsReturnSafeDtosAndUtcCreationTimes(string endpoint, PackageStatus status)
    {
        var expected = await AddPackage(status);
        SignIn("WarehouseOperator");
        var response = await _client.GetAsync($"/api/Package/{endpoint}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = list[0];
        Assert.AreEqual(expected.TrackingNumber, item.GetProperty("trackingNumber").GetString());
        Assert.AreEqual(expected.SenderName, item.GetProperty("senderName").GetString());
        Assert.AreEqual(expected.SenderAddress, item.GetProperty("senderAddress").GetString());
        Assert.AreEqual(expected.RecipientName, item.GetProperty("recipientName").GetString());
        Assert.AreEqual(expected.RecipientAddress, item.GetProperty("recipientAddress").GetString());
        Assert.AreEqual(expected.Size, item.GetProperty("size").GetString());
        Assert.AreEqual(expected.Weight, item.GetProperty("weight").GetInt32());
        Assert.AreEqual(expected.PickupSlot, item.GetProperty("pickupSlot").GetString());
        Assert.AreEqual(expected.PickupDate, item.GetProperty("pickupDate").GetDateTime());
        Assert.IsTrue(item.GetProperty("createdAt").GetString()!.EndsWith("Z"));
        foreach (string name in new[] { "sender", "recipient", "courier", "courierTasks", "statusHistory", "passwordHash", "securityStamp" })
            Assert.IsFalse(item.TryGetProperty(name, out _), name);
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("private-test-"));
    }

    [DataTestMethod]
    [DataRow("unassignedPrebooked", PackageStatus.PreBooked)]
    [DataRow("warehouse", PackageStatus.AtWarehouse)]
    [DataRow("unassignedWarehouse", PackageStatus.AtWarehouse)]
    public async Task PackageListsUseCreationTimeThenIdOrder(string endpoint, PackageStatus status)
    {
        var time = new DateTime(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
        var newest = await AddPackage(status, createdAt: time.AddHours(1));
        var older = await AddPackage(status, createdAt: time);
        var sameTime = await AddPackage(status, createdAt: time);
        SignIn("WarehouseOperator");
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{endpoint}");
        Assert.AreEqual(3, list.GetArrayLength());
        Assert.AreEqual(older.Id, list[0].GetProperty("id").GetInt32());
        Assert.AreEqual(sameTime.Id, list[1].GetProperty("id").GetInt32());
        Assert.AreEqual(newest.Id, list[2].GetProperty("id").GetInt32());
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task CourierListOnlyReturnsActiveCouriersWithoutAccountData(string role)
    {
        SignIn(role);
        var response = await _client.GetAsync("/api/User/couriers");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(2, list.GetArrayLength());
        Assert.AreEqual(_otherCourierId, list[0].GetProperty("id").GetInt32());
        Assert.AreEqual(_courierId, list[1].GetProperty("id").GetInt32());
        Assert.AreEqual("Anna", list[0].GetProperty("firstName").GetString());
        Assert.AreEqual("Kiss", list[0].GetProperty("lastName").GetString());
        foreach (var courier in list.EnumerateArray())
            Assert.AreEqual(3, courier.EnumerateObject().Count());
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("private-test-"));
    }

    [TestMethod]
    public async Task NoActiveCouriersReturnsEmptyArray()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var courier in await db.Users.Where(user => user.Role == UserRole.Courier).ToListAsync())
            courier.IsActive = false;
        await db.SaveChangesAsync();
        SignIn("WarehouseOperator");
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/User/couriers");
        Assert.AreEqual(JsonValueKind.Array, list.ValueKind);
        Assert.AreEqual(0, list.GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("/api/Package/unassignedPrebooked", "Customer")]
    [DataRow("/api/Package/unassignedPrebooked", "Courier")]
    [DataRow("/api/Package/warehouse", "Customer")]
    [DataRow("/api/Package/warehouse", "Courier")]
    [DataRow("/api/Package/unassignedWarehouse", "Customer")]
    [DataRow("/api/Package/unassignedWarehouse", "Courier")]
    [DataRow("/api/User/couriers", "Customer")]
    [DataRow("/api/User/couriers", "Courier")]
    [DataRow("/api/Package/assigned", "Customer")]
    [DataRow("/api/Package/assigned", "Courier")]
    public async Task CustomerAndCourierCannotReadWarehouseLists(string url, string role)
    {
        SignIn(role);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await _client.GetAsync(url)).StatusCode);
    }

    [DataTestMethod]
    [DataRow("/api/Package/unassignedPrebooked")]
    [DataRow("/api/Package/warehouse")]
    [DataRow("/api/Package/unassignedWarehouse")]
    [DataRow("/api/User/couriers")]
    [DataRow("/api/Package/assigned")]
    public async Task AnonymousCannotReadWarehouseLists(string url)
    {
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await _client.GetAsync(url)).StatusCode);
    }

    [DataTestMethod]
    [DataRow("assignforpickup", "unassignedPrebooked", PackageStatus.PreBooked, PackageStatus.AssignedForPickup, CourierTaskType.Pickup, "WarehouseOperator")]
    [DataRow("assignforpickup", "unassignedPrebooked", PackageStatus.PreBooked, PackageStatus.AssignedForPickup, CourierTaskType.Pickup, "Admin")]
    [DataRow("assignfordelivery", "unassignedWarehouse", PackageStatus.AtWarehouse, PackageStatus.AssignedForDelivery, CourierTaskType.Delivery, "WarehouseOperator")]
    [DataRow("assignfordelivery", "unassignedWarehouse", PackageStatus.AtWarehouse, PackageStatus.AssignedForDelivery, CourierTaskType.Delivery, "Admin")]
    public async Task ManualAssignmentRemovesPackageFromUnassignedList(string endpoint, string listEndpoint,
        PackageStatus before, PackageStatus after, CourierTaskType type, string role)
    {
        var package = await AddPackage(before);
        SignIn(role);
        Assert.AreEqual(1, (await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{listEndpoint}")).GetArrayLength());
        var couriers = await _client.GetFromJsonAsync<JsonElement>("/api/User/couriers");
        int selectedCourierId = couriers[0].GetProperty("id").GetInt32();
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = selectedCourierId });
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(0, (await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{listEndpoint}")).GetArrayLength());
        var warehouse = await _client.GetFromJsonAsync<JsonElement>("/api/Package/warehouse");
        Assert.AreEqual(type == CourierTaskType.Delivery ? 1 : 0, warehouse.GetArrayLength());
        if (type == CourierTaskType.Delivery)
        {
            Assert.AreEqual("AssignedForDelivery", warehouse[0].GetProperty("currentStatus").GetString());
            Assert.AreEqual(selectedCourierId, warehouse[0].GetProperty("courierId").GetInt32());
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Packages.SingleAsync();
        var task = await db.CourierTasks.SingleAsync();
        var history = await db.PackageStatuses.OrderByDescending(item => item.Id).FirstAsync();
        Assert.AreEqual(after, saved.CurrentStatus);
        Assert.AreEqual(selectedCourierId, saved.CourierId);
        Assert.AreEqual(selectedCourierId, task.CourierId);
        Assert.AreEqual(type, task.TaskType);
        Assert.AreEqual(CourierTaskStatus.Assigned, task.Status);
        Assert.IsFalse(task.IsLocked);
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(after, history.Status);
        Assert.AreEqual(selectedCourierId, history.CourierId);
        Assert.AreEqual(task.AssignmentTime, history.ChangedAt);
    }

    [DataTestMethod]
    [DataRow("assignforpickup", "{}")]
    [DataRow("assignforpickup", "{\"courierId\":0}")]
    [DataRow("assignforpickup", "{\"courierId\":-1}")]
    [DataRow("assignfordelivery", "{}")]
    [DataRow("assignfordelivery", "{\"courierId\":0}")]
    [DataRow("assignfordelivery", "{\"courierId\":-1}")]
    public async Task MissingOrNonPositiveCourierIdDoesNotWriteAnything(string endpoint, string body)
    {
        var status = endpoint == "assignforpickup" ? PackageStatus.PreBooked : PackageStatus.AtWarehouse;
        var package = await AddPackage(status);
        SignIn("WarehouseOperator");
        var response = await _client.PutAsync($"/api/Package/{package.Id}/{endpoint}", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoAssignment(status);
    }

    [DataTestMethod]
    [DataRow("assignforpickup", UserRole.Customer)]
    [DataRow("assignforpickup", UserRole.WarehouseOperator)]
    [DataRow("assignforpickup", UserRole.Admin)]
    [DataRow("assignfordelivery", UserRole.Customer)]
    [DataRow("assignfordelivery", UserRole.WarehouseOperator)]
    [DataRow("assignfordelivery", UserRole.Admin)]
    public async Task NonCourierRolesCannotBeSelectedForManualAssignment(string endpoint, UserRole role)
    {
        var status = endpoint == "assignforpickup" ? PackageStatus.PreBooked : PackageStatus.AtWarehouse;
        var package = await AddPackage(status);
        int targetId = _users.Single(user => user.Role == role).Id;
        SignIn("WarehouseOperator");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = targetId });
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        await AssertNoAssignment(status);
    }

    private async Task AssertNoAssignment(PackageStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(0, await db.CourierTasks.CountAsync());
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync());
        var package = await db.Packages.SingleAsync();
        Assert.AreEqual(status, package.CurrentStatus);
        Assert.IsNull(package.CourierId);
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task AssignedListContainsOnlyCourierLinkedOngoingPackages(string role)
    {
        foreach (PackageStatus status in Enum.GetValues<PackageStatus>())
            await AddPackage(status, _courierId);
        await AddPackage(PackageStatus.AssignedForPickup);
        SignIn(role);
        var response = await _client.GetAsync("/api/Package/assigned");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(5, list.GetArrayLength());
        CollectionAssert.AreEquivalent(new[] { "AssignedForPickup", "InTransitToWarehouse", "AssignedForDelivery", "OutForDelivery", "FailedDelivery" },
            list.EnumerateArray().Select(item => item.GetProperty("currentStatus").GetString()).ToArray());
        foreach (var item in list.EnumerateArray())
        {
            Assert.AreEqual(_courierId, item.GetProperty("courierId").GetInt32());
            Assert.IsTrue(item.GetProperty("createdAt").GetString()!.EndsWith("Z"));
            Assert.IsFalse(item.TryGetProperty("courier", out _));
            Assert.IsFalse(item.GetProperty("canReassign").GetBoolean());
            Assert.IsFalse(item.GetProperty("canReceive").GetBoolean());
        }
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("private-test-"));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(9, await db.Packages.CountAsync());
        Assert.AreEqual(9, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(0, await db.CourierTasks.CountAsync());
    }

    [DataTestMethod]
    [DataRow(PackageStatus.AssignedForPickup, CourierTaskType.Pickup, CourierTaskStatus.Assigned, false, true, false)]
    [DataRow(PackageStatus.AssignedForPickup, CourierTaskType.Pickup, CourierTaskStatus.InProgress, true, false, false)]
    [DataRow(PackageStatus.AssignedForDelivery, CourierTaskType.Delivery, CourierTaskStatus.Assigned, false, true, false)]
    [DataRow(PackageStatus.AssignedForDelivery, CourierTaskType.Delivery, CourierTaskStatus.Assigned, true, false, false)]
    [DataRow(PackageStatus.InTransitToWarehouse, CourierTaskType.Pickup, CourierTaskStatus.InProgress, true, false, true)]
    [DataRow(PackageStatus.InTransitToWarehouse, CourierTaskType.Delivery, CourierTaskStatus.InProgress, true, false, false)]
    [DataRow(PackageStatus.InTransitToWarehouse, CourierTaskType.Pickup, CourierTaskStatus.Assigned, false, false, false)]
    [DataRow(PackageStatus.OutForDelivery, CourierTaskType.Delivery, CourierTaskStatus.InProgress, true, false, false)]
    [DataRow(PackageStatus.AssignedForDelivery, CourierTaskType.Pickup, CourierTaskStatus.Assigned, false, false, false)]
    public async Task WarehouseActionFlagsFollowActualTask(PackageStatus packageStatus, CourierTaskType type,
        CourierTaskStatus taskStatus, bool locked, bool canReassign, bool canReceive)
    {
        var package = await AddPackage(packageStatus, _courierId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CourierTasks.Add(new CourierTask
        {
            PackageId = package.Id, CourierId = _courierId, TaskType = type, Status = taskStatus, IsLocked = locked
        });
        await db.SaveChangesAsync();
        SignIn("WarehouseOperator");
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/assigned");
        Assert.AreEqual(canReassign, list[0].GetProperty("canReassign").GetBoolean());
        Assert.AreEqual(canReceive, list[0].GetProperty("canReceive").GetBoolean());
        if (packageStatus == PackageStatus.AssignedForDelivery)
        {
            var warehouse = await _client.GetFromJsonAsync<JsonElement>("/api/Package/warehouse");
            Assert.AreEqual(canReassign, warehouse[0].GetProperty("canReassign").GetBoolean());
        }
    }

    [DataTestMethod]
    [DataRow(0, false)]
    [DataRow(2, false)]
    [DataRow(1, true)]
    public async Task FlagsRequireOneActiveTaskAndIgnoreCompletedHistory(int activeTasks, bool expected)
    {
        var package = await AddPackage(PackageStatus.AssignedForDelivery, _courierId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CourierTasks.Add(new CourierTask
        {
            PackageId = package.Id, CourierId = _courierId, TaskType = CourierTaskType.Pickup,
            Status = CourierTaskStatus.Completed, IsLocked = true
        });
        for (int index = 0; index < activeTasks; index++)
            db.CourierTasks.Add(new CourierTask
            {
                PackageId = package.Id, CourierId = _courierId, TaskType = CourierTaskType.Delivery,
                Status = CourierTaskStatus.Assigned
            });
        await db.SaveChangesAsync();
        SignIn("WarehouseOperator");
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/assigned");
        Assert.AreEqual(expected, list[0].GetProperty("canReassign").GetBoolean());
        Assert.IsFalse(list[0].GetProperty("canReceive").GetBoolean());
    }

    [TestMethod]
    public async Task MismatchingTaskCourierDoesNotOfferReceipt()
    {
        var package = await AddPackage(PackageStatus.InTransitToWarehouse, _courierId);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CourierTasks.Add(new CourierTask
        {
            PackageId = package.Id, CourierId = _otherCourierId, TaskType = CourierTaskType.Pickup,
            Status = CourierTaskStatus.InProgress, IsLocked = true
        });
        await db.SaveChangesAsync();
        SignIn("WarehouseOperator");
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/assigned");
        Assert.IsFalse(list[0].GetProperty("canReceive").GetBoolean());
        Assert.IsFalse(list[0].GetProperty("canReassign").GetBoolean());
    }
}
