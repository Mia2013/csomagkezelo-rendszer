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
public class PackageApiTests
{
    private static TestApplicationFactory _factory = null!;
    private HttpClient _client = null!;

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
        await db.Database.ExecuteSqlRawAsync("DELETE FROM PackageStatuses; DELETE FROM Packages; DELETE FROM AspNetUsers;");
        db.Users.Add(new User
        {
            UserName = "recipient", Email = "recipient@example.test",
            Address = "Pápa, Minta utca 2."
        });
        await db.SaveChangesAsync();
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private static Dictionary<string, object?> Booking()
    {
        return new()
        {
            ["senderName"] = "  Teszt Feladó  ",
            ["senderAddress"] = "Veszprém, Teszt utca 1.",
            ["senderPhoneNumber"] = "+36301234567",
            ["recipientName"] = "Teszt Címzett",
            ["recipientAddress"] = "Pápa, Minta utca 2.",
            ["recipientPhoneNumber"] = "+36201234567",
            ["recipientEmail"] = "recipient@example.test",
            ["value"] = 15000,
            ["weight"] = 1200,
            ["size"] = "M",
            ["pickupDate"] = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"),
            ["pickupSlot"] = "09:00-12:00"
        };
    }

    private async Task<string> Create(Dictionary<string, object?>? data = null)
    {
        var response = await _client.PostAsJsonAsync("/api/Package", data ?? Booking());
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.AreEqual("PreBooked", json.GetProperty("status").GetString());
        string number = json.GetProperty("trackingNumber").GetString()!;
        Assert.IsTrue(Guid.TryParseExact(number, "D", out _));
        Assert.IsTrue(response.Headers.Location!.ToString().Contains("trackingNumber=" + number));
        return number;
    }

    [TestMethod]
    public async Task GuestBookingCreatesPackageAndFirstHistory()
    {
        string number = await Create();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.Packages.Include(p => p.StatusHistory).SingleAsync();
        Assert.AreEqual(number, package.TrackingNumber);
        Assert.AreEqual("Teszt Feladó", package.SenderName);
        Assert.AreEqual("recipient@example.test", package.RecipientEmail);
        Assert.AreEqual("09:00-12:00", package.PickupSlot);
        Assert.AreEqual("+36301234567", package.SenderPhoneNumber);
        Assert.IsNull(package.SenderId);
        Assert.AreEqual((await db.Users.SingleAsync(user => user.UserName == "recipient")).Id, package.RecipientId);
        Assert.IsNull(package.CourierId);
        Assert.AreEqual(1, package.StatusHistory.Count);
        var history = package.StatusHistory.Single();
        Assert.AreEqual(package.Id, history.PackageId);
        Assert.AreEqual(PackageStatus.PreBooked, history.Status);
        Assert.AreEqual(package.CreatedAt, history.ChangedAt);
        Assert.IsNull(history.CourierId);
        Assert.AreEqual(0, await db.CourierTasks.CountAsync());
        Assert.AreEqual(0, await db.WarehouseLogs.CountAsync());
    }

    [TestMethod]
    public async Task InternalFieldsCannotBeSetByTheClient()
    {
        var data = Booking();
        data["id"] = 999999;
        data["senderId"] = 999999;
        data["recipientId"] = 999999;
        data["courierId"] = 999999;
        data["currentStatus"] = "Delivered";
        data["trackingNumber"] = Guid.NewGuid().ToString();
        data["createdAt"] = "2000-01-01T00:00:00Z";
        data["statusHistory"] = new[] { new { status = "Delivered" } };
        string number = await Create(data);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.Packages.Include(p => p.StatusHistory).SingleAsync();
        Assert.AreNotEqual(data["trackingNumber"], number);
        Assert.IsNull(package.SenderId);
        Assert.IsNull(package.CourierId);
        Assert.AreEqual((await db.Users.SingleAsync(user => user.UserName == "recipient")).Id, package.RecipientId);
        Assert.AreEqual(PackageStatus.PreBooked, package.CurrentStatus);
        Assert.AreEqual(1, package.StatusHistory.Count);
        Assert.AreEqual(DateTime.UtcNow.Year, package.CreatedAt.Year);
    }

    [DataTestMethod]
    [DataRow("senderName", "   ")]
    [DataRow("recipientAddress", "")]
    [DataRow("recipientEmail", "invalid")]
    [DataRow("senderPhoneNumber", "abc")]
    [DataRow("recipientPhoneNumber", "")]
    [DataRow("pickupSlot", "00:00-23:00")]
    [DataRow("size", "XL")]
    [DataRow("pickupDate", "2000-01-01")]
    [DataRow("weight", 0)]
    [DataRow("weight", 30001)]
    [DataRow("value", -1)]
    public async Task InvalidBookingDoesNotWriteAnything(string field, object value)
    {
        var data = Booking();
        data[field] = value;
        var response = await _client.PostAsJsonAsync("/api/Package", data);
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(0, await db.Packages.CountAsync());
        Assert.AreEqual(0, await db.PackageStatuses.CountAsync());
    }

    [DataTestMethod]
    [DataRow("value")]
    [DataRow("weight")]
    [DataRow("pickupDate")]
    [DataRow("senderPhoneNumber")]
    public async Task MissingRequiredFieldIsRejected(string field)
    {
        var data = Booking();
        data.Remove(field);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/Package", data)).StatusCode);
    }

    [TestMethod]
    public async Task TrackingReturnsOnlyPublicFieldsAndUtcTime()
    {
        string number = await Create();
        // Még egy korábban eltárolt hibás token sem akadályozhatja a nyilvános követést.
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        var response = await _client.GetAsync("/api/Package/track?trackingNumber=" + Uri.EscapeDataString(" " + number.ToUpperInvariant() + " "));
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        CollectionAssert.AreEquivalent(new[] { "trackingNumber", "status", "changedAt", "history" }, json.EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual("PreBooked", json.GetProperty("status").GetString());
        Assert.IsTrue(json.GetProperty("changedAt").GetString()!.EndsWith("Z"));
        var history = json.GetProperty("history").EnumerateArray().ToArray();
        Assert.AreEqual(1, history.Length);
        CollectionAssert.AreEquivalent(new[] { "status", "changedAt" }, history[0].EnumerateObject().Select(p => p.Name).ToArray());
        Assert.AreEqual("PreBooked", history[0].GetProperty("status").GetString());
        Assert.IsTrue(history[0].GetProperty("changedAt").GetString()!.EndsWith("Z"));
    }

    [TestMethod]
    public async Task TrackingReadsLatestHistoryWithStableTieBreak()
    {
        string number = await Create();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var package = await db.Packages.SingleAsync();
            DateTime time = package.CreatedAt.AddMinutes(1);
            db.PackageStatuses.Add(new() { PackageId = package.Id, Status = PackageStatus.AtWarehouse, ChangedAt = time });
            await db.SaveChangesAsync();
            db.PackageStatuses.Add(new() { PackageId = package.Id, Status = PackageStatus.Delivered, ChangedAt = time });
            await db.SaveChangesAsync();
        }
        var json = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + number);
        Assert.AreEqual("Delivered", json.GetProperty("status").GetString());
        CollectionAssert.AreEqual(new[] { "PreBooked", "AtWarehouse", "Delivered" },
            json.GetProperty("history").EnumerateArray().Select(item => item.GetProperty("status").GetString()).ToArray());
    }

    [DataTestMethod]
    [DataRow("", 400)]
    [DataRow("abc", 400)]
    [DataRow("00000000-0000-0000-0000-000000000000", 404)]
    public async Task TrackingHandlesInvalidAndUnknownCodes(string number, int expected)
    {
        Assert.AreEqual(expected, (int)(await _client.GetAsync("/api/Package/track?trackingNumber=" + number)).StatusCode);
    }

    [TestMethod]
    public async Task LoginTokenConnectsBookingToUser()
    {
        var registration = await _client.PostAsJsonAsync("/api/Auth/register", new
        {
            userName = "bookingtest",
            password = "TesztJelszo123!",
            email = "booking@example.test",
            firstName = "Teszt",
            lastName = "Feladó",
            address = "Teszt utca 1."
        });
        Assert.AreEqual(HttpStatusCode.OK, registration.StatusCode);
        var login = await _client.PostAsJsonAsync("/api/Auth/login", new { userName = "bookingtest", password = "TesztJelszo123!" });
        var json = await login.Content.ReadFromJsonAsync<JsonElement>();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.GetProperty("token").GetString());
        await Create();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual((await db.Users.SingleAsync(user => user.UserName == "bookingtest")).Id, (await db.Packages.SingleAsync()).SenderId);
    }

    [DataTestMethod]
    [DataRow("invalid")]
    [DataRow("expired")]
    [DataRow("missingUser")]
    public async Task InvalidAuthenticationCannotBecomeGuestBooking(string kind)
    {
        string token = "invalid";
        if (kind != "invalid")
        {
            var jwt = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
                new[] { new Claim("id", "999999") },
                expires: kind == "expired" ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
            token = new JwtSecurityTokenHandler().WriteToken(jwt);
        }
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await _client.PostAsJsonAsync("/api/Package", Booking())).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.AreEqual(0, await scope.ServiceProvider.GetRequiredService<AppDbContext>().Packages.CountAsync());
    }

    [TestMethod]
    public async Task ParallelBookingsReceiveDifferentNumbers()
    {
        string[] numbers = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Create()));
        Assert.AreEqual(5, numbers.Distinct().Count());
    }

    [TestMethod]
    public async Task DatabaseRejectsDuplicateTrackingNumber()
    {
        string number = await Create();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Packages.Add(new Package { TrackingNumber = number, PickupDate = DateTime.Today.AddDays(1) });
        await Assert.ThrowsExceptionAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [TestMethod]
    public async Task FailedHistoryInsertRollsBackPackageToo()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _factory.CheckTestDatabase(db);
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE PackageStatuses ADD CONSTRAINT CK_Test_RejectPreBooked CHECK (Status <> 'PreBooked');");
        try
        {
            var response = await _client.PostAsJsonAsync("/api/Package", Booking());
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("CK_Test_RejectPreBooked"));
            Assert.AreEqual(0, await db.Packages.CountAsync());
            Assert.AreEqual(0, await db.PackageStatuses.CountAsync());
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE PackageStatuses DROP CONSTRAINT CK_Test_RejectPreBooked;");
        }
    }

    [DataTestMethod]
    [DataRow("GET", "/api/Package")]
    [DataRow("GET", "/api/Package/1")]
    [DataRow("DELETE", "/api/Package/1")]
    [DataRow("PATCH", "/api/Package/1/delivered")]
    [DataRow("GET", "/api/User")]
    [DataRow("DELETE", "/api/User/1")]
    public async Task ManagementEndpointsAreNotPublic(string method, string url)
    {
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));
        Assert.IsTrue(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.Unauthorized);
    }
}
