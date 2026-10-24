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
public class AutomaticPickupAssignmentTests
{
    private static TestApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private int _senderId;
    private int _recipientId;

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
        var sender = new User { UserName = "sender", Email = "sender@example.test", Address = "Veszprém, Teszt utca 1." };
        var recipient = new User { UserName = "recipient", Email = "recipient@example.test", Address = "Pápa, Minta utca 2." };
        db.Users.AddRange(sender, recipient);
        await db.SaveChangesAsync();
        _senderId = sender.Id;
        _recipientId = recipient.Id;
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role, int id, bool expired = false)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", id.ToString()), new Claim("role", role) },
            expires: expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static Dictionary<string, object?> Booking(string size = "M")
    {
        return new Dictionary<string, object?>
        {
            ["senderName"] = "Teszt Feladó", ["senderAddress"] = "Veszprém, Teszt utca 1.",
            ["senderPhoneNumber"] = "+36301234567", ["recipientName"] = "Teszt Címzett",
            ["recipientAddress"] = "Pápa, Minta utca 2.", ["recipientPhoneNumber"] = "+36201234567",
            ["recipientEmail"] = "recipient@example.test", ["value"] = 15000, ["weight"] = 1200,
            ["size"] = size, ["pickupDate"] = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"),
            ["pickupSlot"] = "09:00-12:00"
        };
    }

    private async Task<User> AddCourier(int? capacity = 6, bool active = true, UserRole role = UserRole.Courier)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var courier = new User { UserName = Guid.NewGuid().ToString(), Role = role, IsActive = active };
        db.Users.Add(courier);
        await db.SaveChangesAsync();
        if (capacity != null) await AddCar(courier.Id, capacity.Value);
        return courier;
    }

    private async Task<CourierCar> AddCar(int courierId, int capacity)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var car = new CourierCar
        {
            CourierId = courierId, Capacity = capacity, LicensePlate = Guid.NewGuid().ToString("N").Substring(0, 10),
            Manufacturer = "Ford", Model = "Transit", Year = 2020
        };
        db.CourierCars.Add(car);
        await db.SaveChangesAsync();
        return car;
    }

    private async Task AddLoad(int courierId, string size, CourierTaskType type = CourierTaskType.Pickup,
        CourierTaskStatus status = CourierTaskStatus.Assigned)
    {
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), Size = size,
            CourierId = status == CourierTaskStatus.Completed ? null : courierId,
            CurrentStatus = PackageStatus.AssignedForPickup
        };
        if (status == CourierTaskStatus.Completed) package.CurrentStatus = PackageStatus.AtWarehouse;
        else if (type == CourierTaskType.Delivery) package.CurrentStatus = PackageStatus.AssignedForDelivery;
        package.CourierTasks.Add(new CourierTask
        {
            CourierId = courierId, TaskType = type, Status = status, IsLocked = status == CourierTaskStatus.InProgress
        });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Packages.Add(package);
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> ReadCreated(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        string number = result.GetProperty("trackingNumber").GetString()!;
        Assert.IsTrue(Guid.TryParse(number, out _));
        StringAssert.Contains(response.Headers.Location!.ToString(), "trackingNumber=" + number);
        Assert.AreEqual(5, result.EnumerateObject().Count());
        Assert.IsFalse(result.GetProperty("isPaid").GetBoolean());
        return result;
    }

    private async Task<Package> AssertBooking(JsonElement result, int? courierId)
    {
        string number = result.GetProperty("trackingNumber").GetString()!;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.Packages.Include(package => package.CourierTasks).Include(package => package.StatusHistory)
            .SingleAsync(package => package.TrackingNumber == number);
        var expected = courierId == null ? PackageStatus.PreBooked : PackageStatus.AssignedForPickup;
        Assert.AreEqual(expected.ToString(), result.GetProperty("status").GetString());
        Assert.AreEqual(expected, package.CurrentStatus);
        Assert.AreEqual(courierId, package.CourierId);
        Assert.AreEqual(_recipientId, package.RecipientId);
        Assert.IsFalse(package.IsPaid);
        var history = package.StatusHistory.OrderBy(item => item.ChangedAt).ThenBy(item => item.Id).ToList();
        Assert.AreEqual(PackageStatus.PreBooked, history[0].Status);
        Assert.IsNull(history[0].CourierId);
        Assert.AreEqual(package.CreatedAt, history[0].ChangedAt);
        Assert.AreEqual(courierId == null ? 1 : 2, history.Count);
        Assert.AreEqual(courierId == null ? 0 : 1, package.CourierTasks.Count);
        if (courierId != null)
        {
            var task = package.CourierTasks.Single();
            Assert.AreEqual(courierId, task.CourierId);
            Assert.AreEqual(CourierTaskType.Pickup, task.TaskType);
            Assert.AreEqual(CourierTaskStatus.Assigned, task.Status);
            Assert.IsFalse(task.IsLocked);
            Assert.AreEqual(PackageStatus.AssignedForPickup, history[1].Status);
            Assert.AreEqual(courierId, history[1].CourierId);
            Assert.AreEqual(task.AssignmentTime, history[1].ChangedAt);
        }
        Assert.AreEqual(0, await db.WarehouseLogs.CountAsync());
        return package;
    }

    [DataTestMethod]
    [DataRow("S", 1)]
    [DataRow("M", 2)]
    [DataRow("L", 3)]
    public async Task SingleCourierReceivesFittingPackageWithBothHistoryEvents(string size, int capacity)
    {
        var courier = await AddCourier(capacity);
        var result = await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking(size)));
        var package = await AssertBooking(result, courier.Id);
        Assert.IsNull(package.SenderId);
        var tracking = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + package.TrackingNumber);
        Assert.AreEqual("AssignedForPickup", tracking.GetProperty("status").GetString());
        Assert.AreEqual("PreBooked", tracking.GetProperty("history")[0].GetProperty("status").GetString());
        Assert.AreEqual(2, tracking.GetProperty("history").GetArrayLength());
        Assert.IsTrue(tracking.GetProperty("changedAt").GetString()!.EndsWith("Z"));
        Assert.IsFalse(tracking.TryGetProperty("courierId", out _));
        Assert.IsFalse(tracking.TryGetProperty("recipientEmail", out _));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SenderComesFromTokenWhileInternalFieldsCannotOverrideAutomaticAssignment(bool signedIn)
    {
        var courier = await AddCourier();
        if (signedIn) SignIn("Customer", _senderId);
        var data = Booking();
        data["senderId"] = _recipientId;
        data["recipientId"] = courier.Id;
        data["courierId"] = int.MaxValue;
        data["currentStatus"] = "Delivered";
        data["isPaid"] = true;
        var result = await ReadCreated(await _client.PostAsJsonAsync("/api/Package", data));
        var package = await AssertBooking(result, courier.Id);
        Assert.AreEqual(signedIn ? _senderId : (int?)null, package.SenderId);
    }

    [TestMethod]
    public async Task MostFreeSpaceWinsRatherThanLargestCar()
    {
        var larger = await AddCourier(8);
        await AddLoad(larger.Id, "L");
        await AddLoad(larger.Id, "L", CourierTaskType.Delivery, CourierTaskStatus.InProgress);
        var moreFree = await AddCourier(5);
        await AddLoad(moreFree.Id, "S");
        await AddLoad(moreFree.Id, "L", status: CourierTaskStatus.Completed);
        var result = await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking()));
        await AssertBooking(result, moreFree.Id);
    }

    [TestMethod]
    public async Task TieUsesSmallerCourierIdEvenWhenCarsWereAddedInReverseOrder()
    {
        var first = await AddCourier(null);
        var second = await AddCourier(null);
        await AddCar(second.Id, 4);
        await AddCar(first.Id, 4);
        var result = await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking()));
        await AssertBooking(result, first.Id);
    }

    [TestMethod]
    public async Task InvalidCandidatesAreSkippedInFavorOfEligibleCourier()
    {
        await AddCourier(100, active: false);
        await AddCourier(100, role: UserRole.Customer);
        await AddCourier(null);
        var unknownLoad = await AddCourier(100);
        await AddLoad(unknownLoad.Id, "XL");
        var overallocated = await AddCourier(1);
        await AddLoad(overallocated.Id, "L");
        var eligible = await AddCourier(2);
        var result = await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking()));
        await AssertBooking(result, eligible.Id);
    }

    [DataTestMethod]
    [DataRow("no_courier")]
    [DataRow("no_car")]
    [DataRow("inactive")]
    [DataRow("non_courier")]
    [DataRow("full")]
    [DataRow("too_small")]
    [DataRow("unknown_load")]
    [DataRow("overallocated")]
    public async Task NoSuitableCourierKeepsSuccessfulBookingUnassignedAndDatesUnchanged(string situation)
    {
        if (situation == "no_car") await AddCourier(null);
        if (situation == "inactive") await AddCourier(6, active: false);
        if (situation == "non_courier") await AddCourier(6, role: UserRole.Customer);
        if (situation == "too_small") await AddCourier(1);
        if (situation == "full" || situation == "unknown_load" || situation == "overallocated")
        {
            var courier = await AddCourier(situation == "overallocated" ? 1 : 3);
            await AddLoad(courier.Id, situation == "unknown_load" ? "XL" : "L");
        }
        var data = Booking();
        var result = await ReadCreated(await _client.PostAsJsonAsync("/api/Package", data));
        var package = await AssertBooking(result, null);
        Assert.AreEqual(data["pickupDate"], package.PickupDate.ToString("yyyy-MM-dd"));
        Assert.AreEqual(data["pickupSlot"], package.PickupSlot);
        SignIn("WarehouseOperator", _senderId);
        var pending = await _client.GetFromJsonAsync<JsonElement>("/api/Package/unassignedPrebooked");
        Assert.AreEqual(1, pending.GetArrayLength());
        Assert.AreEqual(package.TrackingNumber, pending[0].GetProperty("trackingNumber").GetString());
    }

    [TestMethod]
    public async Task NextBookingUsesUpdatedFreeSpace()
    {
        var first = await AddCourier(4);
        var second = await AddCourier(3);
        await AssertBooking(await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking())), first.Id);
        await AssertBooking(await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking())), second.Id);
    }

    [DataTestMethod]
    [DataRow(2)]
    [DataRow(3)]
    public async Task ParallelBookingsArePreservedWithoutOverbookingLastSpace(int count)
    {
        var courier = await AddCourier(1);
        var requests = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < count; i++) requests.Add(_client.PostAsJsonAsync("/api/Package", Booking("S")));
        var responses = await Task.WhenAll(requests);
        var numbers = new List<string>();
        foreach (var response in responses)
        {
            var result = await ReadCreated(response);
            numbers.Add(result.GetProperty("trackingNumber").GetString()!);
        }
        Assert.AreEqual(count, numbers.Distinct().Count());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(count, await db.Packages.CountAsync());
        Assert.AreEqual(1, await db.Packages.CountAsync(package => package.CurrentStatus == PackageStatus.AssignedForPickup));
        Assert.AreEqual(count - 1, await db.Packages.CountAsync(package => package.CurrentStatus == PackageStatus.PreBooked && package.CourierId == null));
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
        Assert.AreEqual(count + 1, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(1, await CourierCapacityHelper.GetUsedSpace(db, courier.Id));
    }

    [TestMethod]
    public async Task AutomaticAndManualAssignmentShareLastSpaceProtection()
    {
        var courier = await AddCourier(1);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var manual = new Package { TrackingNumber = Guid.NewGuid().ToString(), Size = "S" };
        manual.StatusHistory.Add(new PackageStatusHistory { Status = PackageStatus.PreBooked });
        db.Packages.Add(manual);
        await db.SaveChangesAsync();
        SignIn("WarehouseOperator", _senderId);
        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/Package", Booking("S")),
            _client.PutAsJsonAsync($"/api/Package/{manual.Id}/assignforpickup", new { courierId = courier.Id }));
        var created = await ReadCreated(responses[0]);
        Assert.IsTrue(responses[1].StatusCode == HttpStatusCode.OK || responses[1].StatusCode == HttpStatusCode.Conflict);
        db.ChangeTracker.Clear();
        Assert.AreEqual(2, await db.Packages.CountAsync());
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
        Assert.AreEqual(1, await CourierCapacityHelper.GetUsedSpace(db, courier.Id));
        Assert.AreEqual(3, await db.PackageStatuses.CountAsync());
        bool manualWon = responses[1].StatusCode == HttpStatusCode.OK;
        Assert.AreEqual(manualWon ? "PreBooked" : "AssignedForPickup", created.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task AutomaticAssignmentCannotOverfillConcurrentlyReducedCar()
    {
        var courier = await AddCourier(null);
        var car = await AddCar(courier.Id, 3);
        SignIn("Admin", _senderId);
        var responses = await Task.WhenAll(
            _client.PostAsJsonAsync("/api/Package", Booking("L")),
            _client.PutAsJsonAsync($"/api/CourierCar/{car.Id}", new
            {
                capacity = 2, licensePlate = car.LicensePlate, manufacturer = car.Manufacturer, model = car.Model, year = car.Year
            }));
        await ReadCreated(responses[0]);
        Assert.IsTrue(responses[1].StatusCode == HttpStatusCode.OK || responses[1].StatusCode == HttpStatusCode.Conflict);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var savedCar = await db.CourierCars.SingleAsync();
        int used = (await CourierCapacityHelper.GetUsedSpace(db, courier.Id))!.Value;
        Assert.IsTrue(used <= savedCar.Capacity);
        Assert.AreEqual(1, await db.Packages.CountAsync());
    }

    [DataTestMethod]
    [DataRow("size", 400)]
    [DataRow("recipient", 400)]
    [DataRow("token", 401)]
    public async Task InvalidBookingDoesNotCreateAutomaticTask(string invalid, int expected)
    {
        await AddCourier();
        var data = Booking();
        if (invalid == "size") data["size"] = "XL";
        if (invalid == "recipient") data["recipientEmail"] = "unknown@example.test";
        if (invalid == "token") SignIn("Customer", _senderId, expired: true);
        Assert.AreEqual(expected, (int)(await _client.PostAsJsonAsync("/api/Package", data)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(0, await db.Packages.CountAsync());
        Assert.AreEqual(0, await db.CourierTasks.CountAsync());
        Assert.AreEqual(0, await db.PackageStatuses.CountAsync());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailedTaskOrHistorySaveRollsBackEntireBooking(bool historyFailure)
    {
        var courier = await AddCourier();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        string addConstraint = historyFailure
            ? "ALTER TABLE PackageStatuses ADD CONSTRAINT CK_Test_RejectAutomaticHistory CHECK (Status = 'PreBooked');"
            : "ALTER TABLE CourierTasks ADD CONSTRAINT CK_Test_RejectAutomaticTask CHECK (PackageId < 0);";
        string dropConstraint = historyFailure
            ? "ALTER TABLE PackageStatuses DROP CONSTRAINT CK_Test_RejectAutomaticHistory;"
            : "ALTER TABLE CourierTasks DROP CONSTRAINT CK_Test_RejectAutomaticTask;";
        await db.Database.ExecuteSqlRawAsync(addConstraint);
        try
        {
            var response = await _client.PostAsJsonAsync("/api/Package", Booking());
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("CK_Test_RejectAutomatic"));
            Assert.AreEqual(0, await db.Packages.CountAsync());
            Assert.AreEqual(0, await db.CourierTasks.CountAsync());
            Assert.AreEqual(0, await db.PackageStatuses.CountAsync());
            Assert.AreEqual(0, await CourierCapacityHelper.GetUsedSpace(db, courier.Id));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(dropConstraint);
        }
        await AssertBooking(await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking())), courier.Id);
        Assert.AreEqual(1, await db.Packages.CountAsync());
    }

    [TestMethod]
    public async Task AutomaticallyAssignedPackageCanReachWarehouseThroughExistingOperations()
    {
        var courier = await AddCourier(2);
        var created = await ReadCreated(await _client.PostAsJsonAsync("/api/Package", Booking()));
        var package = await AssertBooking(created, courier.Id);
        SignIn("Courier", courier.Id);
        var tasks = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{courier.Id}/pickuptasks");
        Assert.AreEqual(package.Id, tasks[0].GetProperty("packageId").GetInt32());
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/startpickup", new { courierId = courier.Id })).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/pickuppackage", new { courierId = courier.Id })).StatusCode);
        SignIn("WarehouseOperator", _senderId);
        Assert.AreEqual(HttpStatusCode.OK, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/receivetowarehouse", new { courierId = courier.Id })).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Packages.SingleAsync();
        Assert.AreEqual(PackageStatus.AtWarehouse, saved.CurrentStatus);
        Assert.IsNull(saved.CourierId);
        Assert.AreEqual(CourierTaskStatus.Completed, (await db.CourierTasks.SingleAsync()).Status);
        Assert.AreEqual(4, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(0, await CourierCapacityHelper.GetUsedSpace(db, courier.Id));
        Assert.AreEqual(2, (await db.WarehouseLogs.SingleAsync()).UsedCapacity);
    }
}
