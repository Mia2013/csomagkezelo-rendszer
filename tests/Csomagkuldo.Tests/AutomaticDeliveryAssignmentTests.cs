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
public class AutomaticDeliveryAssignmentTests
{
    private const string Url = "/api/Package/autoassignfordelivery";
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
        var sender = new User { UserName = "sender", PasswordHash = "private-password-hash" };
        var recipient = new User { UserName = "recipient", Email = "recipient@example.test", Address = "Pápa, Minta utca 2." };
        db.Users.AddRange(sender, recipient);
        await db.SaveChangesAsync();
        _senderId = sender.Id;
        _recipientId = recipient.Id;
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        SignIn();
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role = "WarehouseOperator", int? id = null)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", (id ?? _senderId).ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
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

    private async Task<Package> AddPackage(string size = "S", PackageStatus status = PackageStatus.AtWarehouse,
        int? courierId = null, DateTime? createdAt = null)
    {
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), Size = size, CurrentStatus = status, CourierId = courierId,
            SenderId = _senderId, RecipientId = _recipientId, SenderName = "private-sender",
            RecipientEmail = "recipient@example.test", RecipientAddress = "Pápa, Minta utca 2.",
            PickupDate = DateTime.UtcNow.Date.AddDays(2), PickupSlot = "09:00-12:00",
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        package.StatusHistory.Add(new PackageStatusHistory { Status = status, CourierId = courierId });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private async Task AddTask(Package package, int courierId, CourierTaskType type = CourierTaskType.Delivery,
        CourierTaskStatus status = CourierTaskStatus.Assigned)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.CourierTasks.Add(new CourierTask
        {
            PackageId = package.Id, CourierId = courierId, TaskType = type, Status = status,
            IsLocked = status != CourierTaskStatus.Assigned
        });
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> ReadResult(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual(2, result.EnumerateObject().Count());
        Assert.AreEqual(JsonValueKind.Array, result.GetProperty("assigned").ValueKind);
        Assert.AreEqual(JsonValueKind.Array, result.GetProperty("unassigned").ValueKind);
        return result;
    }

    private async Task AssertAssigned(Package package, int courierId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = (await db.Packages.FindAsync(package.Id))!;
        Assert.AreEqual(PackageStatus.AssignedForDelivery, saved.CurrentStatus);
        Assert.AreEqual(courierId, saved.CourierId);
        var task = await db.CourierTasks.SingleAsync(task => task.PackageId == package.Id && task.Status != CourierTaskStatus.Completed);
        Assert.AreEqual(courierId, task.CourierId);
        Assert.AreEqual(CourierTaskType.Delivery, task.TaskType);
        Assert.AreEqual(CourierTaskStatus.Assigned, task.Status);
        Assert.IsFalse(task.IsLocked);
        var history = await db.PackageStatuses.Where(item => item.PackageId == package.Id).OrderBy(item => item.Id).ToListAsync();
        Assert.AreEqual(2, history.Count);
        Assert.AreEqual(PackageStatus.AtWarehouse, history[0].Status);
        Assert.AreEqual(PackageStatus.AssignedForDelivery, history[1].Status);
        Assert.AreEqual(courierId, history[1].CourierId);
        Assert.AreEqual(task.AssignmentTime, history[1].ChangedAt);
    }

    private async Task AssertPending(Package package)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = (await db.Packages.FindAsync(package.Id))!;
        Assert.AreEqual(PackageStatus.AtWarehouse, saved.CurrentStatus);
        Assert.IsNull(saved.CourierId);
        Assert.AreEqual(package.PickupDate, saved.PickupDate);
        Assert.AreEqual(package.PickupSlot, saved.PickupSlot);
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync(item => item.PackageId == package.Id));
        Assert.AreEqual(0, await db.CourierTasks.CountAsync(task => task.PackageId == package.Id && task.Status != CourierTaskStatus.Completed));
    }

    private async Task AssertValidCapacityAndTasks()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cars = await db.CourierCars.Where(car => car.CourierId != null).ToListAsync();
        foreach (var car in cars)
        {
            int? used = await CourierCapacityHelper.GetUsedSpace(db, car.CourierId!.Value);
            Assert.IsNotNull(used);
            Assert.IsTrue(used.Value <= car.Capacity);
        }
        Assert.IsFalse(await db.CourierTasks.Where(task => task.Status != CourierTaskStatus.Completed)
            .GroupBy(task => task.PackageId).AnyAsync(group => group.Count() > 1));
        Assert.IsFalse(await db.CourierTasks.AnyAsync(task => task.Status != CourierTaskStatus.Completed &&
            task.Package.CourierId != task.CourierId));
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task AuthorizedUserCanAssignMixedSizesAndCourierCanSeeDeliveryTasks(string role)
    {
        var courier = await AddCourier(6);
        var packages = new List<Package> { await AddPackage("S"), await AddPackage("M"), await AddPackage("L") };
        SignIn(role);
        var response = await _client.PostAsync(Url, null);
        var result = await ReadResult(response);
        Assert.AreEqual(3, result.GetProperty("assigned").GetArrayLength());
        Assert.AreEqual(0, result.GetProperty("unassigned").GetArrayLength());
        for (int i = 0; i < packages.Count; i++)
        {
            var item = result.GetProperty("assigned")[i];
            Assert.AreEqual(packages[i].Id, item.GetProperty("packageId").GetInt32());
            Assert.AreEqual(packages[i].TrackingNumber, item.GetProperty("trackingNumber").GetString());
            Assert.AreEqual(courier.Id, item.GetProperty("courierId").GetInt32());
            Assert.AreEqual(3, item.EnumerateObject().Count());
            await AssertAssigned(packages[i], courier.Id);
        }
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("private-"));
        SignIn("Courier", courier.Id);
        var tasks = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{courier.Id}/deliverytasks");
        Assert.AreEqual(3, tasks.GetArrayLength());
        var tracking = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + packages[0].TrackingNumber);
        Assert.AreEqual("AssignedForDelivery", tracking.GetProperty("status").GetString());
        await AssertValidCapacityAndTasks();
    }

    [DataTestMethod]
    [DataRow("anonymous", 401)]
    [DataRow("Customer", 403)]
    [DataRow("Courier", 403)]
    public async Task UnauthorizedCallerCannotStartBulkAssignment(string role, int expected)
    {
        await AddCourier();
        var package = await AddPackage();
        if (role == "anonymous") _client.DefaultRequestHeaders.Authorization = null;
        else SignIn(role);
        Assert.AreEqual(expected, (int)(await _client.PostAsync(Url, null)).StatusCode);
        await AssertPending(package);
    }

    [TestMethod]
    public async Task EmptyWarehouseReturnsTwoEmptyLists()
    {
        var result = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(0, result.GetProperty("assigned").GetArrayLength());
        Assert.AreEqual(0, result.GetProperty("unassigned").GetArrayLength());
    }

    [TestMethod]
    public async Task OldLargePackageDoesNotBlockYoungerFittingPackages()
    {
        var first = await AddCourier(2);
        var second = await AddCourier(1);
        var time = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        var small = await AddPackage("S", createdAt: time.AddHours(2));
        var medium = await AddPackage("M", createdAt: time.AddHours(1));
        var large = await AddPackage("L", createdAt: time);
        var result = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(2, result.GetProperty("assigned").GetArrayLength());
        Assert.AreEqual(medium.Id, result.GetProperty("assigned")[0].GetProperty("packageId").GetInt32());
        Assert.AreEqual(small.Id, result.GetProperty("assigned")[1].GetProperty("packageId").GetInt32());
        Assert.AreEqual(large.Id, result.GetProperty("unassigned")[0].GetProperty("packageId").GetInt32());
        await AssertAssigned(medium, first.Id);
        await AssertAssigned(small, second.Id);
        await AssertPending(large);
        await AssertValidCapacityAndTasks();
    }

    [TestMethod]
    public async Task EqualCreationTimesUsePackageIdOrder()
    {
        var courier = await AddCourier(3);
        var time = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
        var first = await AddPackage("L", createdAt: time);
        var second = await AddPackage("S", createdAt: time);
        var result = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(first.Id, result.GetProperty("assigned")[0].GetProperty("packageId").GetInt32());
        await AssertAssigned(first, courier.Id);
        await AssertPending(second);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task CourierChoiceUsesActualFreeSpaceAndThenCourierId(bool tie)
    {
        var first = await AddCourier(null);
        var second = await AddCourier(null);
        await AddCar(second.Id, tie ? 3 : 5);
        await AddCar(first.Id, tie ? 3 : 8);
        if (!tie)
        {
            var pickup = await AddPackage("L", PackageStatus.AssignedForPickup, first.Id);
            await AddTask(pickup, first.Id, CourierTaskType.Pickup);
            var delivery = await AddPackage("L", PackageStatus.OutForDelivery, first.Id);
            await AddTask(delivery, first.Id, status: CourierTaskStatus.InProgress);
            var otherLoad = await AddPackage("S", PackageStatus.AssignedForDelivery, second.Id);
            await AddTask(otherLoad, second.Id);
        }
        var package = await AddPackage("M");
        var result = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(1, result.GetProperty("assigned").GetArrayLength());
        await AssertAssigned(package, tie ? first.Id : second.Id);
        await AssertValidCapacityAndTasks();
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
    public async Task NoEligibleCourierKeepsPackagePendingWithReason(string situation)
    {
        if (situation == "no_car") await AddCourier(null);
        if (situation == "inactive") await AddCourier(6, active: false);
        if (situation == "non_courier") await AddCourier(6, role: UserRole.Customer);
        if (situation == "too_small") await AddCourier(1);
        if (situation == "full" || situation == "unknown_load" || situation == "overallocated")
        {
            var courier = await AddCourier(situation == "overallocated" ? 1 : 3);
            var load = await AddPackage(situation == "unknown_load" ? "XL" : "L", PackageStatus.AssignedForPickup, courier.Id);
            await AddTask(load, courier.Id, CourierTaskType.Pickup);
        }
        var package = await AddPackage("M");
        var result = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(0, result.GetProperty("assigned").GetArrayLength());
        var pending = result.GetProperty("unassigned");
        Assert.AreEqual(1, pending.GetArrayLength());
        Assert.AreEqual(package.Id, pending[0].GetProperty("packageId").GetInt32());
        Assert.AreEqual(package.TrackingNumber, pending[0].GetProperty("trackingNumber").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(pending[0].GetProperty("reason").GetString()));
        Assert.AreEqual(3, pending[0].EnumerateObject().Count());
        await AssertPending(package);
    }

    [TestMethod]
    public async Task InvalidPackageSizeIsReportedWithoutBlockingValidPackage()
    {
        var courier = await AddCourier(1);
        var invalid = await AddPackage("XL");
        var valid = await AddPackage("S");
        var result = await ReadResult(await _client.PostAsync(Url, null));
        StringAssert.Contains(result.GetProperty("unassigned")[0].GetProperty("reason").GetString()!, "méret");
        await AssertPending(invalid);
        await AssertAssigned(valid, courier.Id);
    }

    [TestMethod]
    public async Task OnlyUnassignedWarehousePackagesWithoutActiveTaskAreProcessed()
    {
        var courier = await AddCourier();
        foreach (var status in Enum.GetValues<PackageStatus>())
            if (status != PackageStatus.AtWarehouse) await AddPackage(status: status);
        await AddPackage(courierId: courier.Id);
        var conflict = await AddPackage();
        await AddTask(conflict, courier.Id, CourierTaskType.Pickup, CourierTaskStatus.InProgress);
        var eligible = await AddPackage();
        await AddTask(eligible, courier.Id, CourierTaskType.Pickup, CourierTaskStatus.Completed);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var before = await db.Packages.AsNoTracking().Where(package => package.Id != eligible.Id).OrderBy(package => package.Id)
            .Select(package => new { package.Id, package.CurrentStatus, package.CourierId }).ToListAsync();
        var result = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(1, result.GetProperty("assigned").GetArrayLength());
        Assert.AreEqual(0, result.GetProperty("unassigned").GetArrayLength());
        Assert.AreEqual(eligible.Id, result.GetProperty("assigned")[0].GetProperty("packageId").GetInt32());
        await AssertAssigned(eligible, courier.Id);
        var after = await db.Packages.AsNoTracking().Where(package => package.Id != eligible.Id).OrderBy(package => package.Id)
            .Select(package => new { package.Id, package.CurrentStatus, package.CourierId }).ToListAsync();
        Assert.AreEqual(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        Assert.AreEqual(1, await db.CourierTasks.CountAsync(task => task.PackageId == eligible.Id && task.Status == CourierTaskStatus.Completed));
    }

    [TestMethod]
    public async Task RepeatedRunDoesNotDuplicateTasksOrHistoryForPartiallyAssignedStock()
    {
        var courier = await AddCourier(2);
        var first = await AddPackage("S");
        var second = await AddPackage("M");
        await ReadResult(await _client.PostAsync(Url, null));
        var repeated = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(0, repeated.GetProperty("assigned").GetArrayLength());
        Assert.AreEqual(second.Id, repeated.GetProperty("unassigned")[0].GetProperty("packageId").GetInt32());
        await AssertAssigned(first, courier.Id);
        await AssertPending(second);
        await AssertValidCapacityAndTasks();
    }

    [TestMethod]
    public async Task AssignmentKeepsPhysicalWarehouseStockAndLogUnchanged()
    {
        await AddCourier();
        var first = await AddPackage("M");
        var second = await AddPackage("S");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var log = new WarehouseLog { Date = DateTime.UtcNow.AddHours(-1), PackageCount = 2, UsedCapacity = 3, Capacity = 1000 };
        db.WarehouseLogs.Add(log);
        await db.SaveChangesAsync();
        await ReadResult(await _client.PostAsync(Url, null));
        var stock = await _client.GetFromJsonAsync<JsonElement>("/api/Package/warehouse");
        Assert.AreEqual(2, stock.GetArrayLength());
        CollectionAssert.AreEquivalent(new[] { first.Id, second.Id }, stock.EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray());
        var stored = await db.Packages.AsNoTracking().Where(package => package.CurrentStatus == PackageStatus.AtWarehouse ||
            package.CurrentStatus == PackageStatus.AssignedForDelivery).Select(package => package.Size).ToListAsync();
        Assert.AreEqual(3, stored.Sum(PackageSizeHelper.GetSpace));
        db.ChangeTracker.Clear();
        var savedLog = await db.WarehouseLogs.SingleAsync();
        Assert.AreEqual(log.Id, savedLog.Id);
        Assert.AreEqual(log.Date, savedLog.Date);
        Assert.AreEqual(2, savedLog.PackageCount);
        Assert.AreEqual(3, savedLog.UsedCapacity);
        Assert.AreEqual(1000, savedLog.Capacity);
    }

    [TestMethod]
    public async Task TwoWarehouseOperatorsCannotAssignSamePackagesTwice()
    {
        await AddCourier(6);
        await AddPackage("S");
        await AddPackage("M");
        await AddPackage("L");
        var responses = await Task.WhenAll(_client.PostAsync(Url, null), _client.PostAsync(Url, null));
        var ids = new List<int>();
        foreach (var response in responses)
        {
            var result = await ReadResult(response);
            ids.AddRange(result.GetProperty("assigned").EnumerateArray().Select(item => item.GetProperty("packageId").GetInt32()));
        }
        Assert.AreEqual(3, ids.Count);
        Assert.AreEqual(3, ids.Distinct().Count());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(3, await db.CourierTasks.CountAsync());
        Assert.AreEqual(6, await db.PackageStatuses.CountAsync());
        await AssertValidCapacityAndTasks();
    }

    [TestMethod]
    public async Task ManualAndBulkDeliveryAssignmentCannotDuplicateSamePackage()
    {
        var courier = await AddCourier(1);
        var package = await AddPackage();
        var responses = await Task.WhenAll(_client.PostAsync(Url, null),
            _client.PutAsJsonAsync($"/api/Package/{package.Id}/assignfordelivery", new { courierId = courier.Id }));
        var bulk = await ReadResult(responses[0]);
        Assert.IsTrue(responses[1].StatusCode == HttpStatusCode.OK || responses[1].StatusCode == HttpStatusCode.Conflict);
        int manualCount = responses[1].StatusCode == HttpStatusCode.OK ? 1 : 0;
        Assert.AreEqual(1, manualCount + bulk.GetProperty("assigned").GetArrayLength());
        await AssertAssigned(package, courier.Id);
        await AssertValidCapacityAndTasks();
    }

    [TestMethod]
    public async Task BulkDeliveryAndManualPickupCannotTakeSameLastSpace()
    {
        var courier = await AddCourier(1);
        var delivery = await AddPackage();
        var pickup = await AddPackage(status: PackageStatus.PreBooked);
        var responses = await Task.WhenAll(_client.PostAsync(Url, null),
            _client.PutAsJsonAsync($"/api/Package/{pickup.Id}/assignforpickup", new { courierId = courier.Id }));
        var bulk = await ReadResult(responses[0]);
        Assert.IsTrue(responses[1].StatusCode == HttpStatusCode.OK || responses[1].StatusCode == HttpStatusCode.Conflict);
        bool manualWon = responses[1].StatusCode == HttpStatusCode.OK;
        Assert.AreEqual(manualWon ? 0 : 1, bulk.GetProperty("assigned").GetArrayLength());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
        Assert.AreEqual(3, await db.PackageStatuses.CountAsync());
        if (manualWon) await AssertPending(delivery);
        else await AssertAssigned(delivery, courier.Id);
        await AssertValidCapacityAndTasks();
    }

    [TestMethod]
    public async Task BulkDeliveryAndNewBookingShareCapacityProtection()
    {
        await AddCourier(1);
        await AddPackage();
        var booking = new
        {
            senderName = "Feladó", senderAddress = "Veszprém, Teszt utca 1.", senderPhoneNumber = "+36301234567",
            recipientName = "Címzett", recipientAddress = "Pápa, Minta utca 2.", recipientPhoneNumber = "+36201234567",
            recipientEmail = "recipient@example.test", size = "S", weight = 1000, value = 0,
            pickupDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"), pickupSlot = "09:00-12:00"
        };
        var responses = await Task.WhenAll(_client.PostAsync(Url, null), _client.PostAsJsonAsync("/api/Package", booking));
        var bulk = await ReadResult(responses[0]);
        Assert.AreEqual(HttpStatusCode.Created, responses[1].StatusCode);
        var created = await responses[1].Content.ReadFromJsonAsync<JsonElement>();
        bool pickupWon = created.GetProperty("status").GetString() == "AssignedForPickup";
        Assert.AreEqual(pickupWon ? 0 : 1, bulk.GetProperty("assigned").GetArrayLength());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(2, await db.Packages.CountAsync());
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
        Assert.AreEqual(3, await db.PackageStatuses.CountAsync());
        await AssertValidCapacityAndTasks();
    }

    [TestMethod]
    public async Task ConcurrentCapacityReductionCannotOverfillCar()
    {
        var courier = await AddCourier(null);
        var car = await AddCar(courier.Id, 3);
        await AddPackage("L");
        SignIn("Admin");
        var responses = await Task.WhenAll(_client.PostAsync(Url, null),
            _client.PutAsJsonAsync($"/api/CourierCar/{car.Id}", new
            {
                capacity = 2, licensePlate = car.LicensePlate, manufacturer = car.Manufacturer, model = car.Model, year = car.Year
            }));
        await ReadResult(responses[0]);
        Assert.IsTrue(responses[1].StatusCode == HttpStatusCode.OK || responses[1].StatusCode == HttpStatusCode.Conflict);
        await AssertValidCapacityAndTasks();
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task FailureOnSecondPackageRollsBackEarlierPartialSave(bool historyFailure)
    {
        var courier = await AddCourier();
        var first = await AddPackage("S");
        var second = await AddPackage("M");
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        string addConstraint = historyFailure
            ? string.Format("ALTER TABLE PackageStatuses ADD CONSTRAINT CK_Test_RejectSecondDelivery CHECK (Status <> 'AssignedForDelivery' OR PackageId <> {0});", second.Id)
            : string.Format("ALTER TABLE CourierTasks ADD CONSTRAINT CK_Test_RejectSecondDelivery CHECK (PackageId <> {0});", second.Id);
        string dropConstraint = historyFailure
            ? "ALTER TABLE PackageStatuses DROP CONSTRAINT CK_Test_RejectSecondDelivery;"
            : "ALTER TABLE CourierTasks DROP CONSTRAINT CK_Test_RejectSecondDelivery;";
        await db.Database.ExecuteSqlRawAsync(addConstraint);
        try
        {
            var response = await _client.PostAsync(Url, null);
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("CK_Test_RejectSecondDelivery"));
            await AssertPending(first);
            await AssertPending(second);
            Assert.AreEqual(0, await db.CourierTasks.CountAsync());
            Assert.AreEqual(0, await CourierCapacityHelper.GetUsedSpace(db, courier.Id));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(dropConstraint);
        }
        var retried = await ReadResult(await _client.PostAsync(Url, null));
        Assert.AreEqual(2, retried.GetProperty("assigned").GetArrayLength());
        await AssertAssigned(first, courier.Id);
        await AssertAssigned(second, courier.Id);
        await AssertValidCapacityAndTasks();
    }
}
