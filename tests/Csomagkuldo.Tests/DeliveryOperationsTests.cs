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
public class DeliveryOperationsTests
{
    private static TestApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private int _courierId;
    private int _otherCourierId;
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
        var courier = new User { UserName = "courier", Role = UserRole.Courier };
        var otherCourier = new User { UserName = "othercourier", Role = UserRole.Courier };
        var sender = new User { UserName = "sender", PasswordHash = "private-password-hash" };
        var recipient = new User { UserName = "recipient", Email = "recipient@example.test", Address = "Pápa, Teszt utca 2." };
        db.Users.AddRange(courier, otherCourier, sender, recipient);
        await db.SaveChangesAsync();
        _courierId = courier.Id;
        _otherCourierId = otherCourier.Id;
        _senderId = sender.Id;
        _recipientId = recipient.Id;
        db.CourierCars.AddRange(
            new CourierCar { LicensePlate = "TEST-001", Capacity = 10, CourierId = _courierId },
            new CourierCar { LicensePlate = "TEST-002", Capacity = 10, CourierId = _otherCourierId });
        await db.SaveChangesAsync();
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        SignIn();
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(string role = "Courier", int? id = null, HttpClient? client = null)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", (id ?? _courierId).ToString()), new Claim("role", role) },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        (client ?? _client).DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private async Task<Package> AddPackage(PackageStatus status = PackageStatus.AssignedForDelivery, bool addTask = true,
        CourierTaskType type = CourierTaskType.Delivery, CourierTaskStatus? taskStatus = null,
        bool? locked = null, string size = "M", int? cost = 2000, int? courierId = null)
    {
        int owner = courierId ?? _courierId;
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), CurrentStatus = status, CourierId = owner,
            SenderId = _senderId, SenderName = "Teszt Feladó", SenderAddress = "Veszprém, Teszt utca 1.",
            SenderPhoneNumber = "+36301234567", RecipientId = _recipientId, RecipientName = "Teszt Címzett",
            RecipientAddress = "Pápa, Teszt utca 2.", RecipientPhoneNumber = "+36201234567",
            RecipientEmail = "recipient@example.test", Size = size, Weight = 500, Value = 99999,
            ShippingCost = cost, ShippingFeeId = cost == null ? null : 2,
            PickupDate = DateTime.UtcNow.Date.AddDays(2), PickupSlot = "09:00-12:00"
        };
        package.StatusHistory.Add(new PackageStatusHistory { Status = status, CourierId = owner });
        if (addTask)
        {
            var progress = taskStatus ?? (status == PackageStatus.OutForDelivery ? CourierTaskStatus.InProgress : CourierTaskStatus.Assigned);
            package.CourierTasks.Add(new CourierTask
            {
                CourierId = owner, TaskType = type, Status = progress, IsLocked = locked ?? progress != CourierTaskStatus.Assigned
            });
        }
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private Task<HttpResponseMessage> Send(Package package, string endpoint, int? courierId = null, bool paid = true)
    {
        return _client.PutAsJsonAsync($"/api/Package/{package.Id}/{endpoint}", new { courierId = courierId ?? _courierId, isPaid = paid });
    }

    private static async Task AssertOk(HttpResponseMessage response)
    {
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task AssertUnchanged(Package original)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Packages.SingleAsync(p => p.Id == original.Id);
        Assert.AreEqual(original.CurrentStatus, saved.CurrentStatus);
        Assert.AreEqual(original.CourierId, saved.CourierId);
        Assert.AreEqual(original.IsPaid, saved.IsPaid);
        Assert.AreEqual(original.ShippingCost, saved.ShippingCost);
        Assert.AreEqual(1, await db.PackageStatuses.CountAsync(p => p.PackageId == original.Id));
        Assert.AreEqual(0, await db.WarehouseLogs.CountAsync());
        var tasks = await db.CourierTasks.Where(t => t.PackageId == original.Id).OrderBy(t => t.Id).ToListAsync();
        Assert.AreEqual(original.CourierTasks.Count, tasks.Count);
        for (int i = 0; i < tasks.Count; i++)
        {
            Assert.AreEqual(original.CourierTasks[i].Status, tasks[i].Status);
            Assert.AreEqual(original.CourierTasks[i].IsLocked, tasks[i].IsLocked);
            Assert.AreEqual(original.CourierTasks[i].CourierId, tasks[i].CourierId);
        }
    }

    [DataTestMethod]
    [DataRow("pickuptasks", PackageStatus.AssignedForPickup, CourierTaskType.Pickup)]
    [DataRow("deliverytasks", PackageStatus.AssignedForDelivery, CourierTaskType.Delivery)]
    public async Task TaskListsContainNecessaryPackageDataOnly(string endpoint, PackageStatus status, CourierTaskType type)
    {
        var package = await AddPackage(status, type: type);
        var result = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_courierId}/{endpoint}");
        Assert.AreEqual(1, result.GetArrayLength());
        var task = result[0];
        Assert.AreEqual(package.SenderName, task.GetProperty("senderName").GetString());
        Assert.AreEqual(package.SenderAddress, task.GetProperty("senderAddress").GetString());
        Assert.AreEqual(package.SenderPhoneNumber, task.GetProperty("senderPhoneNumber").GetString());
        Assert.AreEqual(package.RecipientName, task.GetProperty("recipientName").GetString());
        Assert.AreEqual(package.RecipientAddress, task.GetProperty("recipientAddress").GetString());
        Assert.AreEqual(package.RecipientPhoneNumber, task.GetProperty("recipientPhoneNumber").GetString());
        Assert.AreEqual(package.Size, task.GetProperty("size").GetString());
        Assert.AreEqual(package.Weight, task.GetProperty("weight").GetInt32());
        Assert.AreEqual(package.PickupDate, task.GetProperty("pickupDate").GetDateTime());
        Assert.AreEqual(package.PickupSlot, task.GetProperty("pickupSlot").GetString());
        Assert.AreEqual(2000, task.GetProperty("shippingCost").GetInt32());
        Assert.IsFalse(task.GetProperty("isPaid").GetBoolean());
        Assert.IsFalse(task.TryGetProperty("sender", out _));
        Assert.IsFalse(task.TryGetProperty("recipient", out _));
        Assert.IsFalse(task.TryGetProperty("passwordHash", out _));
        Assert.IsFalse(task.TryGetProperty("value", out _));
    }

    [DataTestMethod]
    [DataRow("pickuptasks")]
    [DataRow("deliverytasks")]
    public async Task NonCourierDatabaseUserCannotBeQueriedAsCourier(string endpoint)
    {
        SignIn("Admin");
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.GetAsync($"/api/Package/{_senderId}/{endpoint}")).StatusCode);
    }

    [TestMethod]
    public async Task StartLocksTaskAndLogsActualRemainingWarehouseStock()
    {
        var package = await AddPackage();
        await AddPackage(PackageStatus.AtWarehouse, addTask: false, size: "L");
        await AddPackage(courierId: _otherCourierId, size: "S");
        await AddPackage(PackageStatus.OutForDelivery, courierId: _otherCourierId);
        await AssertOk(await Send(package, "startdelivery"));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Packages.SingleAsync(p => p.Id == package.Id);
        Assert.AreEqual(PackageStatus.OutForDelivery, saved.CurrentStatus);
        Assert.AreEqual(_courierId, saved.CourierId);
        Assert.IsFalse(saved.IsPaid);
        var task = await db.CourierTasks.SingleAsync(t => t.PackageId == package.Id);
        Assert.AreEqual(CourierTaskStatus.InProgress, task.Status);
        Assert.IsTrue(task.IsLocked);
        Assert.AreEqual(2, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
        var log = await db.WarehouseLogs.SingleAsync();
        Assert.AreEqual(2, log.PackageCount);
        Assert.AreEqual(4, log.UsedCapacity);
        Assert.AreEqual(1000, log.Capacity);
        var history = await db.PackageStatuses.Where(h => h.PackageId == package.Id).OrderBy(h => h.Id).LastAsync();
        Assert.AreEqual(PackageStatus.OutForDelivery, history.Status);
        Assert.AreEqual(_courierId, history.CourierId);
        Assert.AreEqual(log.Date, history.ChangedAt);
        var deliveries = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_courierId}/deliverytasks");
        Assert.AreEqual(1, deliveries.GetArrayLength());
        Assert.AreEqual("InProgress", deliveries[0].GetProperty("status").GetString());
        Assert.AreEqual("OutForDelivery", deliveries[0].GetProperty("currentStatus").GetString());
        SignIn("WarehouseOperator");
        var warehouse = await _client.GetFromJsonAsync<JsonElement>("/api/Package/warehouse");
        Assert.AreEqual(2, warehouse.GetArrayLength());
        Assert.IsFalse(warehouse.EnumerateArray().Any(p => p.GetProperty("id").GetInt32() == package.Id));
    }

    [DataTestMethod]
    [DataRow("S", 1000)]
    [DataRow("M", 2000)]
    [DataRow("L", 4000)]
    [DataRow("S", 0)]
    public async Task CompletionRecordsStoredFeeAndReleasesOnlyCompletedTask(string size, int cost)
    {
        var package = await AddPackage(PackageStatus.OutForDelivery, size: size, cost: cost);
        await AddPackage(size: "S");
        var response = await _client.PutAsJsonAsync($"/api/Package/{package.Id}/completedelivery",
            new { courierId = _courierId, isPaid = true, shippingCost = 1, value = 1, shippingFeeId = 1 });
        await AssertOk(response);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Packages.SingleAsync(p => p.Id == package.Id);
        Assert.AreEqual(PackageStatus.Delivered, saved.CurrentStatus);
        Assert.IsTrue(saved.IsPaid);
        Assert.IsNull(saved.CourierId);
        Assert.AreEqual(cost, saved.ShippingCost);
        Assert.AreEqual(99999, saved.Value);
        var task = await db.CourierTasks.SingleAsync(t => t.PackageId == package.Id);
        Assert.AreEqual(CourierTaskStatus.Completed, task.Status);
        Assert.IsTrue(task.IsLocked);
        Assert.AreEqual(_courierId, task.CourierId);
        Assert.AreEqual(1, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
        Assert.AreEqual(0, await db.WarehouseLogs.CountAsync());
        var history = await db.PackageStatuses.Where(h => h.PackageId == package.Id).OrderBy(h => h.Id).LastAsync();
        Assert.AreEqual(PackageStatus.Delivered, history.Status);
        Assert.AreEqual(_courierId, history.CourierId);
        var deliveries = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/{_courierId}/deliverytasks");
        Assert.AreEqual(1, deliveries.GetArrayLength());
        Assert.IsFalse(deliveries.EnumerateArray().Any(t => t.GetProperty("packageId").GetInt32() == package.Id));
        var tracking = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + package.TrackingNumber);
        Assert.AreEqual("Delivered", tracking.GetProperty("status").GetString());
        Assert.IsTrue(tracking.GetProperty("changedAt").GetString()!.EndsWith("Z"));
        Assert.IsFalse(tracking.TryGetProperty("shippingCost", out _));
        SignIn("Customer", _recipientId);
        var received = await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/received?history=true");
        Assert.AreEqual(1, received.GetArrayLength());
        Assert.AreEqual(cost, received[0].GetProperty("shippingCost").GetInt32());
        Assert.IsTrue(received[0].GetProperty("isPaid").GetBoolean());
    }

    [DataTestMethod]
    [DataRow("startdelivery", "Customer")]
    [DataRow("startdelivery", "WarehouseOperator")]
    [DataRow("completedelivery", "Customer")]
    [DataRow("completedelivery", "WarehouseOperator")]
    [DataRow("startdelivery", "anonymous")]
    [DataRow("completedelivery", "anonymous")]
    public async Task WrongRoleCannotModifyDelivery(string endpoint, string role)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        if (role == "anonymous") _client.DefaultRequestHeaders.Authorization = null;
        else SignIn(role);
        Assert.AreEqual(role == "anonymous" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden, (await Send(package, endpoint)).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery", false)]
    [DataRow("startdelivery", true)]
    [DataRow("completedelivery", false)]
    [DataRow("completedelivery", true)]
    public async Task OtherCourierCannotModifyTaskWithEitherId(string endpoint, bool victimId)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        SignIn(id: _otherCourierId);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await Send(package, endpoint, victimId ? _courierId : _otherCourierId)).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery", PackageStatus.PreBooked)]
    [DataRow("startdelivery", PackageStatus.AtWarehouse)]
    [DataRow("startdelivery", PackageStatus.OutForDelivery)]
    [DataRow("startdelivery", PackageStatus.Delivered)]
    [DataRow("startdelivery", PackageStatus.FailedDelivery)]
    [DataRow("completedelivery", PackageStatus.AssignedForDelivery)]
    [DataRow("completedelivery", PackageStatus.AtWarehouse)]
    [DataRow("completedelivery", PackageStatus.InTransitToWarehouse)]
    [DataRow("completedelivery", PackageStatus.Delivered)]
    [DataRow("completedelivery", PackageStatus.FailedDelivery)]
    public async Task InvalidPackageStateDoesNotChangeAnything(string endpoint, PackageStatus status)
    {
        var package = await AddPackage(status, taskStatus: endpoint == "startdelivery" ? CourierTaskStatus.Assigned : CourierTaskStatus.InProgress);
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, endpoint)).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery", "package")]
    [DataRow("startdelivery", "courier")]
    [DataRow("startdelivery", "task")]
    [DataRow("completedelivery", "package")]
    [DataRow("completedelivery", "courier")]
    [DataRow("completedelivery", "task")]
    public async Task MissingDataReturnsNotFound(string endpoint, string missing)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery,
            addTask: missing != "task");
        SignIn("Admin");
        int id = missing == "package" ? int.MaxValue : package.Id;
        var response = await _client.PutAsJsonAsync($"/api/Package/{id}/{endpoint}",
            new { courierId = missing == "courier" ? int.MaxValue : _courierId, isPaid = true });
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery")]
    [DataRow("completedelivery")]
    public async Task PickupTaskCannotBeUsedForDelivery(string endpoint)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery,
            type: CourierTaskType.Pickup);
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, endpoint)).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery", CourierTaskStatus.Assigned, true)]
    [DataRow("startdelivery", CourierTaskStatus.InProgress, true)]
    [DataRow("completedelivery", CourierTaskStatus.InProgress, false)]
    [DataRow("completedelivery", CourierTaskStatus.Assigned, false)]
    public async Task InvalidTaskStateOrLockPreventsTransition(string endpoint, CourierTaskStatus status, bool locked)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery,
            taskStatus: status, locked: locked);
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, endpoint)).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery")]
    [DataRow("completedelivery")]
    public async Task DuplicateActiveTasksPreventTransition(string endpoint)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        var extra = new CourierTask { PackageId = package.Id, CourierId = _courierId, TaskType = CourierTaskType.Delivery };
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CourierTasks.Add(extra);
            await db.SaveChangesAsync();
        }
        package.CourierTasks.Add(extra);
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, endpoint)).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery")]
    [DataRow("completedelivery")]
    public async Task MismatchingPackageCourierPreventsTransition(string endpoint)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Packages.FindAsync(package.Id))!.CourierId = _otherCourierId;
            await db.SaveChangesAsync();
        }
        package.CourierId = _otherCourierId;
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, endpoint)).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingOrFalsePaymentConfirmationCannotComplete(bool omitField)
    {
        var package = await AddPackage(PackageStatus.OutForDelivery);
        var data = new Dictionary<string, object> { ["courierId"] = _courierId };
        if (!omitField) data["isPaid"] = false;
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/Package/{package.Id}/completedelivery", data)).StatusCode);
        await AssertUnchanged(package);
    }

    [TestMethod]
    public async Task UnknownHistoricalFeeIsNotGuessedOnCompletion()
    {
        var package = await AddPackage(PackageStatus.OutForDelivery, cost: null);
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, "completedelivery")).StatusCode);
        await AssertUnchanged(package);
    }

    [TestMethod]
    public async Task AlreadyRecordedPaymentCannotBeRecordedAgain()
    {
        var package = await AddPackage(PackageStatus.OutForDelivery);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Packages.FindAsync(package.Id))!.IsPaid = true;
            await db.SaveChangesAsync();
        }
        package.IsPaid = true;
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, "completedelivery")).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("startdelivery")]
    [DataRow("completedelivery")]
    public async Task RepeatedRequestDoesNotDuplicateHistoryOrPayment(string endpoint)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        await AssertOk(await Send(package, endpoint));
        var repeated = await Send(package, endpoint);
        Assert.AreEqual(endpoint == "startdelivery" ? HttpStatusCode.Conflict : HttpStatusCode.NotFound, repeated.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(endpoint == "startdelivery" ? 1 : 0, await db.WarehouseLogs.CountAsync());
        var saved = await db.Packages.SingleAsync();
        Assert.AreEqual(endpoint == "completedelivery", saved.IsPaid);
        Assert.AreEqual(endpoint == "startdelivery" ? PackageStatus.OutForDelivery : PackageStatus.Delivered, saved.CurrentStatus);
    }

    [DataTestMethod]
    [DataRow("startdelivery", false)]
    [DataRow("startdelivery", true)]
    [DataRow("completedelivery", true)]
    public async Task FailedSaveRollsBackPackageTaskPaymentHistoryAndLog(string endpoint, bool historyFailure)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        string add = historyFailure
            ? "ALTER TABLE PackageStatuses ADD CONSTRAINT CK_Test_RejectDelivery CHECK (Status NOT IN ('OutForDelivery', 'Delivered'));"
            : "ALTER TABLE WarehouseLogs ADD CONSTRAINT CK_Test_RejectDelivery CHECK (PackageCount < 0);";
        string drop = historyFailure
            ? "ALTER TABLE PackageStatuses DROP CONSTRAINT CK_Test_RejectDelivery;"
            : "ALTER TABLE WarehouseLogs DROP CONSTRAINT CK_Test_RejectDelivery;";
        if (endpoint == "completedelivery") add = "ALTER TABLE PackageStatuses ADD CONSTRAINT CK_Test_RejectDelivery CHECK (Status <> 'Delivered');";
        await db.Database.ExecuteSqlRawAsync(add);
        try
        {
            var response = await Send(package, endpoint);
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("CK_Test_RejectDelivery"));
            await AssertUnchanged(package);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(drop);
        }
        await AssertOk(await Send(package, endpoint));
    }

    [DataTestMethod]
    [DataRow("startdelivery")]
    [DataRow("completedelivery")]
    public async Task ConcurrentRequestsCanTransitionPackageOnlyOnce(string endpoint)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        var responses = await Task.WhenAll(Send(package, endpoint), Send(package, endpoint));
        Assert.AreEqual(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.IsTrue(responses.All(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.NotFound));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
        Assert.AreEqual(endpoint == "startdelivery" ? 1 : 0, await db.WarehouseLogs.CountAsync());
        Assert.AreEqual(endpoint == "startdelivery" ? 2 : 0, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
        var saved = await db.Packages.SingleAsync();
        Assert.AreEqual(endpoint == "completedelivery", saved.IsPaid);
        var task = await db.CourierTasks.SingleAsync();
        Assert.AreEqual(endpoint == "startdelivery" ? CourierTaskStatus.InProgress : CourierTaskStatus.Completed, task.Status);
        Assert.IsTrue(task.IsLocked);
    }

    [TestMethod]
    public async Task ConcurrentDeparturesKeepWarehouseSnapshotsConsistent()
    {
        var first = await AddPackage();
        var second = await AddPackage();
        var responses = await Task.WhenAll(Send(first, "startdelivery"), Send(second, "startdelivery"));
        Assert.IsTrue(responses.Any(r => r.StatusCode == HttpStatusCode.OK));
        for (int i = 0; i < responses.Length; i++)
        {
            if (responses[i].StatusCode == HttpStatusCode.OK) continue;
            Assert.AreEqual(HttpStatusCode.Conflict, responses[i].StatusCode);
            await AssertOk(await Send(i == 0 ? first : second, "startdelivery"));
        }
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logs = await db.WarehouseLogs.OrderBy(log => log.Id).ToListAsync();
        Assert.AreEqual(2, logs.Count);
        Assert.AreEqual(1, logs[0].PackageCount);
        Assert.AreEqual(2, logs[0].UsedCapacity);
        Assert.AreEqual(0, logs[1].PackageCount);
        Assert.AreEqual(0, logs[1].UsedCapacity);
        Assert.AreEqual(4, await CourierCapacityHelper.GetUsedSpace(db, _courierId));
    }

    [TestMethod]
    public async Task ConcurrentStartAndReassignmentCannotMoveStartedDelivery()
    {
        var package = await AddPackage();
        using var warehouse = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        SignIn("WarehouseOperator", client: warehouse);
        var responses = await Task.WhenAll(Send(package, "startdelivery"), warehouse.PutAsJsonAsync(
            $"/api/Package/{package.Id}/reassigncourier", new { courierId = _otherCourierId }));
        Assert.AreEqual(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.IsTrue(responses.All(r => r.StatusCode == HttpStatusCode.OK || r.StatusCode == HttpStatusCode.Conflict || r.StatusCode == HttpStatusCode.Forbidden));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var task = await db.CourierTasks.SingleAsync();
        var saved = await db.Packages.SingleAsync();
        Assert.AreEqual(task.CourierId, saved.CourierId);
        Assert.AreEqual(task.IsLocked ? _courierId : _otherCourierId, task.CourierId);
        Assert.AreEqual(task.IsLocked ? PackageStatus.OutForDelivery : PackageStatus.AssignedForDelivery, saved.CurrentStatus);
        Assert.AreEqual(task.IsLocked ? 1 : 0, await db.WarehouseLogs.CountAsync());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task BookedPackageCanCompletePickupWarehouseAndPaidDelivery(bool guest)
    {
        if (guest) _client.DefaultRequestHeaders.Authorization = null;
        else SignIn("Customer", _senderId);
        var booking = await _client.PostAsJsonAsync("/api/Package", new
        {
            senderName = "Feladó", senderAddress = "Veszprém, Teszt utca 1.", senderPhoneNumber = "+36301234567",
            recipientName = "Címzett", recipientAddress = "Pápa, Teszt utca 2.", recipientPhoneNumber = "+36201234567",
            recipientEmail = "recipient@example.test", size = "M", weight = 500, value = 99999,
            pickupDate = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"), pickupSlot = "09:00-12:00"
        });
        Assert.AreEqual(HttpStatusCode.Created, booking.StatusCode, await booking.Content.ReadAsStringAsync());
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.Packages.SingleAsync();
        SignIn(id: package.CourierId);
        await AssertOk(await Send(package, "startpickup", package.CourierId));
        await AssertOk(await Send(package, "pickuppackage", package.CourierId));
        SignIn("WarehouseOperator");
        await AssertOk(await Send(package, "receivetowarehouse", package.CourierId));
        await AssertOk(await Send(package, "assignfordelivery", _otherCourierId));
        SignIn(id: _otherCourierId);
        await AssertOk(await Send(package, "startdelivery", _otherCourierId));
        await AssertOk(await Send(package, "completedelivery", _otherCourierId));
        db.ChangeTracker.Clear();
        var saved = await db.Packages.SingleAsync();
        Assert.AreEqual(PackageStatus.Delivered, saved.CurrentStatus);
        Assert.AreEqual(guest ? null : (int?)_senderId, saved.SenderId);
        Assert.IsTrue(saved.IsPaid);
        Assert.AreEqual(2000, saved.ShippingCost);
        Assert.AreEqual(2, await db.CourierTasks.CountAsync(t => t.Status == CourierTaskStatus.Completed));
        Assert.AreEqual(0, await CourierCapacityHelper.GetUsedSpace(db, _otherCourierId));
        var logs = await db.WarehouseLogs.OrderBy(log => log.Id).ToListAsync();
        Assert.AreEqual(2, logs.Count);
        Assert.AreEqual(2, logs[0].UsedCapacity);
        Assert.AreEqual(0, logs[1].UsedCapacity);
        var tracking = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + package.TrackingNumber);
        CollectionAssert.AreEqual(new[] { "PreBooked", "AssignedForPickup", "InTransitToWarehouse", "AtWarehouse", "AssignedForDelivery", "OutForDelivery", "Delivered" },
            tracking.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("status").GetString()).ToArray());
    }
    [DataTestMethod]
    [DataRow("startdelivery")]
    [DataRow("completedelivery")]
    public async Task AdminCanRecordOperationForAssignedCourier(string endpoint)
    {
        var package = await AddPackage(endpoint == "startdelivery" ? PackageStatus.AssignedForDelivery : PackageStatus.OutForDelivery);
        SignIn("Admin", _senderId);
        await AssertOk(await Send(package, endpoint));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var history = await db.PackageStatuses.OrderBy(h => h.Id).LastAsync();
        Assert.AreEqual(_courierId, history.CourierId);
        Assert.AreEqual(endpoint == "startdelivery" ? PackageStatus.OutForDelivery : PackageStatus.Delivered, history.Status);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task InvalidWarehouseSizePreventsInaccurateDepartureLog(bool otherPackage)
    {
        var package = await AddPackage(size: otherPackage ? "M" : "XL");
        if (otherPackage) await AddPackage(PackageStatus.AtWarehouse, addTask: false, size: "XL");
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, "startdelivery")).StatusCode);
        await AssertUnchanged(package);
    }

    [DataTestMethod]
    [DataRow("WarehouseOperator")]
    [DataRow("Admin")]
    public async Task StartedDeliveryCannotBeReassignedUsingPreDepartureEndpoint(string role)
    {
        var package = await AddPackage();
        await AssertOk(await Send(package, "startdelivery"));
        SignIn(role);
        Assert.AreEqual(HttpStatusCode.Conflict, (await Send(package, "reassigncourier", _otherCourierId)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(_courierId, (await db.Packages.SingleAsync()).CourierId);
        Assert.IsTrue((await db.CourierTasks.SingleAsync()).IsLocked);
        Assert.AreEqual(2, await db.PackageStatuses.CountAsync());
    }
}
