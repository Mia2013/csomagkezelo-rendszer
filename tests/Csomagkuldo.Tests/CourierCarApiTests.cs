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
public class CourierCarApiTests
{
    private static TestApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private int _courierId;
    private int _otherCourierId;
    private int _inactiveCourierId;
    private int _customerId;

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
        var first = new User { UserName = "courier", Role = UserRole.Courier, PasswordHash = "private-hash" };
        var second = new User { UserName = "other", Role = UserRole.Courier };
        var inactive = new User { UserName = "inactive", Role = UserRole.Courier, IsActive = false };
        var customer = new User { UserName = "customer", Role = UserRole.Customer };
        db.Users.AddRange(first, second, inactive, customer);
        await db.SaveChangesAsync();
        _courierId = first.Id;
        _otherCourierId = second.Id;
        _inactiveCourierId = inactive.Id;
        _customerId = customer.Id;
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role = "Admin", int? id = null)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", (id ?? _courierId).ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static Dictionary<string, object?> CarData(int capacity = 6, string plate = "ABC-123")
    {
        return new Dictionary<string, object?>
        {
            ["capacity"] = capacity, ["licensePlate"] = plate,
            ["manufacturer"] = "  Ford  ", ["model"] = "  Transit  ", ["year"] = 2020
        };
    }

    private async Task<CourierCar> AddCar(int capacity = 6, int? courierId = null, string? plate = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var car = new CourierCar
        {
            Capacity = capacity, CourierId = courierId, LicensePlate = plate ?? Guid.NewGuid().ToString("N")[..10],
            Manufacturer = "Ford", Model = "Transit", Year = 2020
        };
        db.CourierCars.Add(car);
        await db.SaveChangesAsync();
        return car;
    }

    private async Task<CourierTask> AddTask(string size, CourierTaskStatus status = CourierTaskStatus.Assigned,
        CourierTaskType type = CourierTaskType.Pickup, int? courierId = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = new CourierTask
        {
            CourierId = courierId ?? _courierId, TaskType = type, Status = status,
            Package = new Package
            {
                TrackingNumber = Guid.NewGuid().ToString(), Size = size,
                CurrentStatus = type == CourierTaskType.Pickup ? PackageStatus.AssignedForPickup : PackageStatus.AssignedForDelivery
            }
        };
        db.CourierTasks.Add(task);
        await db.SaveChangesAsync();
        return task;
    }

    [TestMethod]
    public async Task AdminCanCreateReadUpdateAndDeleteUnassignedCar()
    {
        SignIn();
        var data = CarData(6, " abc-123 ");
        data["courierId"] = _courierId;
        var response = await _client.PostAsJsonAsync("/api/CourierCar", data);
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var car = await response.Content.ReadFromJsonAsync<JsonElement>();
        int id = car.GetProperty("id").GetInt32();
        Assert.AreEqual("ABC-123", car.GetProperty("licensePlate").GetString());
        Assert.AreEqual("Ford", car.GetProperty("manufacturer").GetString());
        Assert.AreEqual("Transit", car.GetProperty("model").GetString());
        Assert.AreEqual(JsonValueKind.Null, car.GetProperty("courierId").ValueKind);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.GetAsync(response.Headers.Location)).StatusCode);
        var update = await _client.PutAsJsonAsync($"/api/CourierCar/{id}", CarData(9, "NEW-456"));
        Assert.AreEqual(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(9, updated.GetProperty("capacity").GetInt32());
        Assert.AreEqual("NEW-456", updated.GetProperty("licensePlate").GetString());
        Assert.AreEqual(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/CourierCar/{id}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/CourierCar/{id}")).StatusCode);
    }

    [DataTestMethod]
    [DataRow("Admin")]
    [DataRow("WarehouseOperator")]
    public async Task CarListsUseDtosAndAllowMultipleUnassignedCars(string role)
    {
        var first = await AddCar(courierId: _courierId);
        await AddCar();
        await AddCar();
        SignIn(role);
        var response = await _client.GetAsync("/api/CourierCar");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(3, list.GetArrayLength());
        Assert.AreEqual(first.Id, list[0].GetProperty("id").GetInt32());
        Assert.IsFalse(list[0].TryGetProperty("courier", out _));
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("private-hash"));
    }

    [TestMethod]
    public async Task EmptyCarListReturnsEmptyArray()
    {
        SignIn();
        Assert.AreEqual(0, (await _client.GetFromJsonAsync<JsonElement>("/api/CourierCar")).GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("capacity", 0)]
    [DataRow("capacity", -1)]
    [DataRow("licensePlate", "")]
    [DataRow("manufacturer", "")]
    [DataRow("model", "")]
    [DataRow("year", 0)]
    public async Task InvalidCarDataDoesNotCreateCar(string field, object value)
    {
        SignIn();
        var data = CarData();
        data[field] = value;
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/CourierCar", data)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourierCars.CountAsync());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DuplicatePlateIsRejectedOnCreateAndUpdate(bool update)
    {
        await AddCar(plate: "ABC-123");
        SignIn();
        HttpResponseMessage response;
        if (update)
        {
            var second = await AddCar(plate: "DEF-456");
            response = await _client.PutAsJsonAsync($"/api/CourierCar/{second.Id}", CarData(6, " abc-123 "));
        }
        else response = await _client.PostAsJsonAsync("/api/CourierCar", CarData(6, " abc-123 "));
        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
    }

    [DataTestMethod]
    [DataRow("get")]
    [DataRow("update")]
    [DataRow("assign")]
    [DataRow("unassign")]
    [DataRow("delete")]
    public async Task MissingCarReturnsNotFound(string operation)
    {
        SignIn();
        var response = await SendOperation(operation, int.MaxValue);
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> SendOperation(string operation, int id)
    {
        if (operation == "get") return _client.GetAsync($"/api/CourierCar/{id}");
        if (operation == "create") return _client.PostAsJsonAsync("/api/CourierCar", CarData());
        if (operation == "update") return _client.PutAsJsonAsync($"/api/CourierCar/{id}", CarData());
        if (operation == "assign") return _client.PutAsJsonAsync($"/api/CourierCar/{id}/assigncourier", new { courierId = _courierId });
        if (operation == "unassign") return _client.PutAsync($"/api/CourierCar/{id}/unassigncourier", null);
        return _client.DeleteAsync($"/api/CourierCar/{id}");
    }

    [DataTestMethod]
    [DataRow("create", "WarehouseOperator")]
    [DataRow("update", "WarehouseOperator")]
    [DataRow("assign", "WarehouseOperator")]
    [DataRow("unassign", "WarehouseOperator")]
    [DataRow("delete", "WarehouseOperator")]
    [DataRow("create", "Courier")]
    [DataRow("update", "Courier")]
    [DataRow("assign", "Courier")]
    [DataRow("unassign", "Courier")]
    [DataRow("delete", "Courier")]
    [DataRow("create", "Customer")]
    [DataRow("update", "Customer")]
    [DataRow("assign", "Customer")]
    [DataRow("unassign", "Customer")]
    [DataRow("delete", "Customer")]
    public async Task OnlyAdminCanModifyCars(string operation, string role)
    {
        var car = await AddCar();
        SignIn(role);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await SendOperation(operation, car.Id)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourierCars.SingleAsync();
        Assert.AreEqual(car.Id, saved.Id);
        Assert.IsNull(saved.CourierId);
    }

    [DataTestMethod]
    [DataRow("create")]
    [DataRow("update")]
    [DataRow("assign")]
    [DataRow("unassign")]
    [DataRow("delete")]
    [DataRow("get")]
    public async Task CarOperationsRequireLogin(string operation)
    {
        var car = await AddCar();
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await SendOperation(operation, car.Id)).StatusCode);
    }

    [DataTestMethod]
    [DataRow("Admin")]
    [DataRow("WarehouseOperator")]
    [DataRow("Courier")]
    public async Task CapacityCombinesPickupAndDeliveryButExcludesCompletedAndOtherCouriers(string role)
    {
        await AddCar(6, _courierId);
        await AddTask("S");
        await AddTask("M", CourierTaskStatus.InProgress, CourierTaskType.Delivery);
        await AddTask("L", CourierTaskStatus.Completed);
        await AddTask("L", courierId: _otherCourierId);
        SignIn(role);
        var data = await _client.GetFromJsonAsync<JsonElement>($"/api/CourierCar/courier/{_courierId}/capacity");
        Assert.AreEqual(6, data.GetProperty("capacity").GetInt32());
        Assert.AreEqual(3, data.GetProperty("usedCapacity").GetInt32());
        Assert.AreEqual(3, data.GetProperty("freeCapacity").GetInt32());
    }

    [TestMethod]
    public async Task ExactlyFullCarHasZeroFreeSpace()
    {
        await AddCar(6, _courierId);
        await AddTask("S");
        await AddTask("M");
        await AddTask("L", CourierTaskStatus.InProgress, CourierTaskType.Delivery);
        SignIn();
        var data = await _client.GetFromJsonAsync<JsonElement>($"/api/CourierCar/courier/{_courierId}/capacity");
        Assert.AreEqual(6, data.GetProperty("usedCapacity").GetInt32());
        Assert.AreEqual(0, data.GetProperty("freeCapacity").GetInt32());
    }

    [TestMethod]
    public async Task SamePackageIsNotCountedTwiceInLegacyDuplicateTasks()
    {
        await AddCar(6, _courierId);
        var task = await AddTask("M");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CourierTasks.Add(new CourierTask { PackageId = task.PackageId, CourierId = _courierId, TaskType = CourierTaskType.Delivery });
        await db.SaveChangesAsync();
        SignIn();
        var data = await _client.GetFromJsonAsync<JsonElement>($"/api/CourierCar/courier/{_courierId}/capacity");
        Assert.AreEqual(2, data.GetProperty("usedCapacity").GetInt32());
    }

    [DataTestMethod]
    [DataRow("Customer")]
    [DataRow("otherCourier")]
    [DataRow("anonymous")]
    public async Task CapacityAccessIsRestricted(string role)
    {
        await AddCar(6, _courierId);
        if (role == "otherCourier") SignIn("Courier", _otherCourierId);
        else if (role != "anonymous") SignIn(role);
        var expected = role == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
        Assert.AreEqual(expected, (await _client.GetAsync($"/api/CourierCar/courier/{_courierId}/capacity")).StatusCode);
    }

    [TestMethod]
    public async Task CourierWithoutCarHasNoAvailableCapacityResponse()
    {
        SignIn();
        Assert.AreEqual(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/CourierCar/courier/{_courierId}/capacity")).StatusCode);
    }

    [DataTestMethod]
    [DataRow("XL", 6)]
    [DataRow("L", 2)]
    public async Task InvalidOrOverallocatedLoadDoesNotReturnMisleadingFreeSpace(string size, int capacity)
    {
        await AddCar(capacity, _courierId);
        await AddTask(size);
        SignIn();
        Assert.AreEqual(HttpStatusCode.Conflict, (await _client.GetAsync($"/api/CourierCar/courier/{_courierId}/capacity")).StatusCode);
    }

    [DataTestMethod]
    [DataRow("customer")]
    [DataRow("inactive")]
    [DataRow("missing")]
    public async Task AssignmentRequiresAnExistingActiveCourier(string kind)
    {
        var car = await AddCar();
        int targetId = kind == "customer" ? _customerId : kind == "inactive" ? _inactiveCourierId : int.MaxValue;
        SignIn();
        var response = await _client.PutAsJsonAsync($"/api/CourierCar/{car.Id}/assigncourier", new { courierId = targetId });
        Assert.AreEqual(kind == "missing" ? HttpStatusCode.NotFound : HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.IsNull((await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourierCars.SingleAsync()).CourierId);
    }

    [TestMethod]
    public async Task InactiveCourierExistingCarCanStillBeInspected()
    {
        await AddCar(6, _inactiveCourierId);
        SignIn();
        var data = await _client.GetFromJsonAsync<JsonElement>($"/api/CourierCar/courier/{_inactiveCourierId}/capacity");
        Assert.IsFalse(data.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(6, data.GetProperty("freeCapacity").GetInt32());
    }

    [DataTestMethod]
    [DataRow(2, HttpStatusCode.Conflict)]
    [DataRow(3, HttpStatusCode.OK)]
    public async Task FirstCarMustFitAlreadyAssignedPackages(int capacity, HttpStatusCode expected)
    {
        var car = await AddCar(capacity);
        await AddTask("L");
        SignIn();
        Assert.AreEqual(expected, (await _client.PutAsJsonAsync($"/api/CourierCar/{car.Id}/assigncourier", new { courierId = _courierId })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(expected == HttpStatusCode.OK ? _courierId : (int?)null, (await db.CourierCars.SingleAsync()).CourierId);
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
    }

    [DataTestMethod]
    [DataRow(4, HttpStatusCode.Conflict)]
    [DataRow(5, HttpStatusCode.OK)]
    public async Task CarSwapKeepsOneCarAndMustFitExistingLoad(int newCapacity, HttpStatusCode expected)
    {
        var oldCar = await AddCar(6, _courierId);
        var replacement = await AddCar(newCapacity);
        await AddTask("M");
        await AddTask("L", CourierTaskStatus.InProgress, CourierTaskType.Delivery);
        SignIn();
        Assert.AreEqual(expected, (await _client.PutAsJsonAsync($"/api/CourierCar/{replacement.Id}/assigncourier", new { courierId = _courierId })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await db.CourierCars.CountAsync(car => car.CourierId == _courierId));
        var assigned = await db.CourierCars.SingleAsync(car => car.CourierId == _courierId);
        Assert.AreEqual(expected == HttpStatusCode.OK ? replacement.Id : oldCar.Id, assigned.Id);
        Assert.AreEqual(2, await db.CourierTasks.CountAsync(task => task.CourierId == _courierId));
    }

    [DataTestMethod]
    [DataRow(2, HttpStatusCode.Conflict)]
    [DataRow(3, HttpStatusCode.OK)]
    [DataRow(8, HttpStatusCode.OK)]
    public async Task CapacityUpdateCannotLeaveLessSpaceThanAllocated(int capacity, HttpStatusCode expected)
    {
        var car = await AddCar(6, _courierId, "ABC-123");
        await AddTask("S");
        await AddTask("M");
        SignIn();
        Assert.AreEqual(expected, (await _client.PutAsJsonAsync($"/api/CourierCar/{car.Id}", CarData(capacity))).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourierCars.SingleAsync();
        Assert.AreEqual(expected == HttpStatusCode.OK ? capacity : 6, saved.Capacity);
        Assert.AreEqual(_courierId, saved.CourierId);
    }

    [DataTestMethod]
    [DataRow("unassign")]
    [DataRow("delete")]
    public async Task ActiveCourierCannotLoseCarWithoutReplacement(string operation)
    {
        var car = await AddCar(6, _courierId);
        await AddTask("S");
        SignIn();
        Assert.AreEqual(HttpStatusCode.Conflict, (await SendOperation(operation, car.Id)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.AreEqual(_courierId, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourierCars.SingleAsync()).CourierId);
    }

    [TestMethod]
    public async Task CompletedTasksAllowUnassigningAndDeletingCar()
    {
        var car = await AddCar(6, _courierId);
        await AddTask("L", CourierTaskStatus.Completed);
        SignIn();
        Assert.AreEqual(HttpStatusCode.OK, (await SendOperation("unassign", car.Id)).StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, (await SendOperation("delete", car.Id)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(4, await db.Users.CountAsync());
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
    }

    [TestMethod]
    public async Task AnotherActiveCouriersCarCannotBeTaken()
    {
        var car = await AddCar(6, _otherCourierId);
        await AddTask("M", courierId: _otherCourierId);
        SignIn();
        Assert.AreEqual(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync($"/api/CourierCar/{car.Id}/assigncourier", new { courierId = _courierId })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.AreEqual(_otherCourierId, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourierCars.SingleAsync()).CourierId);
    }

    [TestMethod]
    public async Task ConcurrentAssignmentsStillLeaveOnlyOneCarForCourier()
    {
        var first = await AddCar();
        var second = await AddCar();
        SignIn();
        var responses = await Task.WhenAll(
            _client.PutAsJsonAsync($"/api/CourierCar/{first.Id}/assigncourier", new { courierId = _courierId }),
            _client.PutAsJsonAsync($"/api/CourierCar/{second.Id}/assigncourier", new { courierId = _courierId }));
        Assert.IsTrue(responses.Any(response => response.StatusCode == HttpStatusCode.OK));
        Assert.IsTrue(responses.All(response => response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Conflict));
        using var scope = _factory.Services.CreateScope();
        Assert.AreEqual(1, await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourierCars.CountAsync(car => car.CourierId == _courierId));
    }

    [TestMethod]
    public async Task FailedReplacementAssignmentRestoresOldCar()
    {
        var oldCar = await AddCar(6, _courierId, "OLD-001");
        var replacement = await AddCar(6, plate: "NEW-001");
        await AddTask("M");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE CourierCars ADD CONSTRAINT CK_Test_RejectReplacement CHECK (LicensePlate <> 'NEW-001' OR CourierId IS NULL);");
        try
        {
            SignIn();
            var response = await _client.PutAsJsonAsync($"/api/CourierCar/{replacement.Id}/assigncourier", new { courierId = _courierId });
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("CK_Test_RejectReplacement"));
            Assert.AreEqual(_courierId, (await db.CourierCars.FindAsync(oldCar.Id))!.CourierId);
            Assert.IsNull((await db.CourierCars.FindAsync(replacement.Id))!.CourierId);
            Assert.AreEqual(1, await db.CourierTasks.CountAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE CourierCars DROP CONSTRAINT CK_Test_RejectReplacement;");
        }
    }
}
