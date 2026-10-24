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
public class MyPackageApiTests
{
    private static TestApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private int _userId;
    private int _otherUserId;
    private int _thirdUserId;
    private const string RecipientAddress = "Pápa, B utca 2.";

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
        var first = new User { UserName = "user-a", Email = "a@example.test", Address = "Veszprém, A utca 1.", PasswordHash = "private-hash" };
        var second = new User { UserName = "user-b", Email = "b@example.test", Address = RecipientAddress, PasswordHash = "private-hash" };
        var third = new User { UserName = "user-c", Email = "c@example.test", Address = "Ajka, C utca 3.", PasswordHash = "private-hash" };
        db.Users.AddRange(first, second, third);
        await db.SaveChangesAsync();
        _userId = first.Id;
        _otherUserId = second.Id;
        _thirdUserId = third.Id;
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(int? id, string role = "Customer", bool expired = false, string? claimId = null)
    {
        var claims = new List<Claim> { new Claim("role", role) };
        if (id != null || claimId != null) claims.Add(new Claim("id", claimId ?? id!.Value.ToString()));
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend", claims,
            expires: expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private async Task<Package> AddPackage(int? senderId, int? recipientId,
        PackageStatus status = PackageStatus.PreBooked, DateTime? createdAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), SenderId = senderId, RecipientId = recipientId,
            SenderName = "Felhasználó A", SenderAddress = "Veszprém, A utca 1.",
            RecipientName = "Felhasználó A", RecipientAddress = "Veszprém, A utca 1.", RecipientEmail = "a@example.test",
            Size = "M", Weight = 1200, Value = 15000, CurrentStatus = status,
            PickupDate = DateTime.Today.AddDays(1), PickupSlot = "09:00-12:00", CreatedAt = createdAt ?? DateTime.UtcNow
        };
        package.StatusHistory.Add(new PackageStatusHistory { Status = status });
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package;
    }

    private static Dictionary<string, object?> Booking()
    {
        return new Dictionary<string, object?>
        {
            ["senderName"] = "Felhasználó A", ["senderAddress"] = "Veszprém, A utca 1.", ["senderPhoneNumber"] = "+36301234567",
            ["recipientName"] = "Felhasználó B", ["recipientAddress"] = RecipientAddress,
            ["recipientPhoneNumber"] = "+36201234567", ["recipientEmail"] = "b@example.test",
            ["value"] = 15000, ["weight"] = 1200, ["size"] = "M",
            ["pickupDate"] = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"), ["pickupSlot"] = "09:00-12:00"
        };
    }

    [DataTestMethod]
    [DataRow("sent", false)]
    [DataRow("sent", true)]
    [DataRow("received", false)]
    [DataRow("received", true)]
    public async Task ListsOnlyContainPackagesOwnedInTheRequestedDirection(string direction, bool history)
    {
        var status = history ? PackageStatus.Delivered : PackageStatus.PreBooked;
        Package expected;
        if (direction == "sent")
        {
            expected = await AddPackage(_userId, _otherUserId, status);
            await AddPackage(_otherUserId, _thirdUserId, status);
            await AddPackage(_thirdUserId, _userId, status);
        }
        else
        {
            expected = await AddPackage(_otherUserId, _userId, status);
            await AddPackage(_thirdUserId, _otherUserId, status);
            await AddPackage(_userId, _thirdUserId, status);
        }
        SignIn(_userId);
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}?history={history}");
        Assert.AreEqual(1, list.GetArrayLength());
        Assert.AreEqual(expected.Id, list[0].GetProperty("id").GetInt32());
    }

    [DataTestMethod]
    [DataRow("sent")]
    [DataRow("received")]
    public async Task DeliveredIsHistoryButFailedDeliveryRemainsCurrent(string direction)
    {
        foreach (PackageStatus status in Enum.GetValues<PackageStatus>())
            await AddPackage(_userId, _otherUserId, status);
        SignIn(direction == "sent" ? _userId : _otherUserId);
        var current = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}");
        Assert.AreEqual(7, current.GetArrayLength());
        var statuses = current.EnumerateArray().Select(item => item.GetProperty("currentStatus").GetString()).ToArray();
        CollectionAssert.DoesNotContain(statuses, "Delivered");
        CollectionAssert.Contains(statuses, "FailedDelivery");
        var previous = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}?history=true");
        Assert.AreEqual(1, previous.GetArrayLength());
        Assert.AreEqual("Delivered", previous[0].GetProperty("currentStatus").GetString());
    }

    [DataTestMethod]
    [DataRow("sent", false)]
    [DataRow("sent", true)]
    [DataRow("received", false)]
    [DataRow("received", true)]
    public async Task EmptyListsAreEmptyArrays(string direction, bool history)
    {
        SignIn(_userId);
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}?history={history}");
        Assert.AreEqual(JsonValueKind.Array, list.ValueKind);
        Assert.AreEqual(0, list.GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("sent", "anonymous")]
    [DataRow("received", "anonymous")]
    [DataRow("sent", "invalid")]
    [DataRow("received", "invalid")]
    [DataRow("sent", "expired")]
    [DataRow("received", "expired")]
    [DataRow("sent", "missingId")]
    [DataRow("received", "missingId")]
    [DataRow("sent", "nonNumericId")]
    [DataRow("received", "nonNumericId")]
    [DataRow("sent", "missingUser")]
    [DataRow("received", "missingUser")]
    public async Task InvalidAuthenticationCannotReadOwnLists(string direction, string kind)
    {
        if (kind == "invalid") _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        else if (kind == "expired") SignIn(_userId, expired: true);
        else if (kind == "missingId") SignIn(null);
        else if (kind == "nonNumericId") SignIn(null, claimId: "abc");
        else if (kind == "missingUser") SignIn(int.MaxValue);
        Assert.AreEqual(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/Package/mine/{direction}")).StatusCode);
    }

    [DataTestMethod]
    [DataRow("sent", "Customer")]
    [DataRow("received", "Customer")]
    [DataRow("sent", "Admin")]
    [DataRow("received", "Admin")]
    public async Task QueryUserIdsCannotSelectAnotherPersonsPackages(string direction, string role)
    {
        await AddPackage(_otherUserId, _thirdUserId);
        await AddPackage(_thirdUserId, _otherUserId);
        SignIn(_userId, role);
        var list = await _client.GetFromJsonAsync<JsonElement>(
            $"/api/Package/mine/{direction}?userId={_otherUserId}&senderId={_otherUserId}&recipientId={_otherUserId}");
        Assert.AreEqual(0, list.GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("sent", false)]
    [DataRow("sent", true)]
    [DataRow("received", false)]
    [DataRow("received", true)]
    public async Task ListsUseNewestFirstWithIdAsTieBreaker(string direction, bool history)
    {
        var status = history ? PackageStatus.Delivered : PackageStatus.PreBooked;
        var time = new DateTime(2026, 10, 23, 8, 0, 0, DateTimeKind.Utc);
        var older = await AddPackage(_userId, _otherUserId, status, time);
        var firstNew = await AddPackage(_userId, _otherUserId, status, time.AddHours(1));
        var lastNew = await AddPackage(_userId, _otherUserId, status, time.AddHours(1));
        SignIn(direction == "sent" ? _userId : _otherUserId);
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}?history={history}");
        CollectionAssert.AreEqual(new[] { lastNew.Id, firstNew.Id, older.Id },
            list.EnumerateArray().Select(item => item.GetProperty("id").GetInt32()).ToArray());
    }

    [DataTestMethod]
    [DataRow("sent", false)]
    [DataRow("sent", true)]
    [DataRow("received", false)]
    [DataRow("received", true)]
    public async Task ListsUseLimitedDtosWithUtcCreationTime(string direction, bool history)
    {
        var package = await AddPackage(_userId, _otherUserId, history ? PackageStatus.Delivered : PackageStatus.PreBooked);
        SignIn(direction == "sent" ? _userId : _otherUserId);
        var response = await _client.GetAsync($"/api/Package/mine/{direction}?history={history}");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = list[0];
        Assert.AreEqual(package.TrackingNumber, item.GetProperty("trackingNumber").GetString());
        Assert.AreEqual(package.SenderAddress, item.GetProperty("senderAddress").GetString());
        Assert.AreEqual(package.RecipientAddress, item.GetProperty("recipientAddress").GetString());
        Assert.AreEqual(package.Size, item.GetProperty("size").GetString());
        Assert.AreEqual(package.Weight, item.GetProperty("weight").GetInt32());
        Assert.AreEqual(package.PickupDate, item.GetProperty("pickupDate").GetDateTime());
        Assert.IsTrue(item.GetProperty("createdAt").GetString()!.EndsWith("Z"));
        foreach (string name in new[] { "sender", "recipient", "courier", "courierId", "courierTasks", "statusHistory", "passwordHash", "securityStamp" })
            Assert.IsFalse(item.TryGetProperty(name, out _), name);
        Assert.IsFalse((await response.Content.ReadAsStringAsync()).Contains("private-hash"));
    }

    [DataTestMethod]
    [DataRow("sent")]
    [DataRow("received")]
    public async Task UnlinkedLegacyPackageIsNotClaimedByContactDetails(string direction)
    {
        var package = await AddPackage(null, null);
        SignIn(_userId);
        Assert.AreEqual(0, (await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}")).GetArrayLength());
        _client.DefaultRequestHeaders.Authorization = null;
        var tracking = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + package.TrackingNumber);
        Assert.AreEqual("PreBooked", tracking.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task UserCanBeSenderAndRecipientOfTheSamePackage()
    {
        var package = await AddPackage(_userId, _userId);
        SignIn(_userId);
        foreach (string direction in new[] { "sent", "received" })
        {
            var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}");
            Assert.AreEqual(1, list.GetArrayLength());
            Assert.AreEqual(package.Id, list[0].GetProperty("id").GetInt32());
        }
    }

    [TestMethod]
    public async Task BookingUsesAuthenticatedSenderAndResolvedRecipient()
    {
        var booking = Booking();
        booking["senderId"] = _thirdUserId;
        booking["recipientId"] = _thirdUserId;
        SignIn(_userId);
        Assert.AreEqual(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/Package", booking)).StatusCode);
        Assert.AreEqual(1, (await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/sent")).GetArrayLength());
        Assert.AreEqual(0, (await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/received")).GetArrayLength());
        SignIn(_otherUserId);
        Assert.AreEqual(1, (await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/received")).GetArrayLength());
        SignIn(_thirdUserId);
        Assert.AreEqual(0, (await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/received")).GetArrayLength());
        using var scope = _factory.Services.CreateScope();
        var package = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Packages.SingleAsync();
        Assert.AreEqual(_userId, package.SenderId);
        Assert.AreEqual(_otherUserId, package.RecipientId);
    }

    [TestMethod]
    public async Task GuestBookingAppearsForRecipientWithoutCreatingSenderOwnership()
    {
        Assert.AreEqual(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/Package", Booking())).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var package = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Packages.SingleAsync();
        Assert.IsNull(package.SenderId);
        Assert.AreEqual(_otherUserId, package.RecipientId);
        SignIn(_otherUserId);
        Assert.AreEqual(1, (await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/received")).GetArrayLength());
        SignIn(_userId);
        Assert.AreEqual(0, (await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/sent")).GetArrayLength());
    }

    [DataTestMethod]
    [DataRow("unknownEmail")]
    [DataRow("wrongAddress")]
    [DataRow("duplicate")]
    public async Task BookingRequiresOneExistingRecipientWithMatchingAddress(string kind)
    {
        var booking = Booking();
        if (kind == "unknownEmail") booking["recipientEmail"] = "unknown@example.test";
        if (kind == "wrongAddress") booking["recipientAddress"] = "Budapest, Másik utca 9.";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (kind == "duplicate")
        {
            db.Users.Add(new User { UserName = "duplicate", Email = "b@example.test", Address = RecipientAddress });
            await db.SaveChangesAsync();
        }
        Assert.AreEqual(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/Package", booking)).StatusCode);
        Assert.AreEqual(0, await db.Packages.CountAsync());
        Assert.AreEqual(0, await db.PackageStatuses.CountAsync());
    }

    [TestMethod]
    public async Task RecipientLookupIgnoresCaseAndOuterAddressWhitespace()
    {
        var booking = Booking();
        booking["recipientEmail"] = "B@EXAMPLE.TEST";
        booking["recipientAddress"] = "  " + RecipientAddress.ToUpperInvariant() + "  ";
        Assert.AreEqual(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/Package", booking)).StatusCode);
        using var scope = _factory.Services.CreateScope();
        Assert.AreEqual(_otherUserId, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Packages.SingleAsync()).RecipientId);
    }

    [TestMethod]
    public async Task StoredRecipientIdSurvivesProfileContactChanges()
    {
        Assert.AreEqual(HttpStatusCode.Created, (await _client.PostAsJsonAsync("/api/Package", Booking())).StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recipient = (await db.Users.FindAsync(_otherUserId))!;
        recipient.Email = "changed@example.test";
        recipient.Address = "Új cím";
        await db.SaveChangesAsync();
        SignIn(_otherUserId);
        Assert.AreEqual(1, (await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/received")).GetArrayLength());
    }
}
