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
public class ShippingFeeApiTests
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
        await db.Database.ExecuteSqlRawAsync("DELETE FROM CourierCars; DELETE FROM CourierTasks; DELETE FROM PackageStatuses; DELETE FROM Packages; DELETE FROM AspNetUsers;");
        foreach (var fee in await db.ShippingFees.ToListAsync())
        {
            int price = fee.MaxSize == "S" ? 1000 : fee.MaxSize == "M" ? 2000 : 4000;
            fee.RegisteredPrice = price;
            fee.UnregisteredPrice = price;
        }
        var sender = new User { UserName = "sender" };
        var recipient = new User { UserName = "recipient", Email = "recipient@example.test", Address = "Pápa, Minta utca 2." };
        db.Users.AddRange(sender, recipient);
        await db.SaveChangesAsync();
        _senderId = sender.Id;
        _recipientId = recipient.Id;
        _client = _factory.CreateClient(new() { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
    }

    [TestCleanup]
    public void CloseClient() => _client.Dispose();

    private void SignIn(int id)
    {
        var token = new JwtSecurityToken("Csomagkuldo", "CsomagkuldoFrontend",
            new[] { new Claim("id", id.ToString()), new Claim("role", "Customer") },
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestApplicationFactory.TestKey)), SecurityAlgorithms.HmacSha256));
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static Dictionary<string, object?> Booking(string size = "M")
    {
        return new Dictionary<string, object?>
        {
            ["senderName"] = "Feladó", ["senderAddress"] = "Veszprém, Teszt utca 1.", ["senderPhoneNumber"] = "+36301234567",
            ["recipientName"] = "Címzett", ["recipientAddress"] = "Pápa, Minta utca 2.",
            ["recipientEmail"] = "recipient@example.test", ["recipientPhoneNumber"] = "+36201234567",
            ["size"] = size, ["weight"] = 1000, ["value"] = 15000,
            ["pickupDate"] = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd"), ["pickupSlot"] = "09:00-12:00"
        };
    }

    private async Task<JsonElement> Create(Dictionary<string, object?>? data = null)
    {
        var response = await _client.PostAsJsonAsync("/api/Package", data ?? Booking());
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [DataTestMethod]
    [DataRow("S", 1000, false)]
    [DataRow("M", 2000, false)]
    [DataRow("L", 4000, false)]
    [DataRow("S", 1000, true)]
    [DataRow("M", 2000, true)]
    [DataRow("L", 4000, true)]
    public async Task BookingStoresTariffAndAgreedPriceForGuestAndRegisteredSender(string size, int expected, bool registered)
    {
        if (registered) SignIn(_senderId);
        var result = await Create(Booking(size));
        Assert.AreEqual(expected, result.GetProperty("shippingCost").GetInt32());
        Assert.IsFalse(result.GetProperty("isPaid").GetBoolean());
        string number = result.GetProperty("trackingNumber").GetString()!;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var package = await db.Packages.Include(package => package.ShippingFee).SingleAsync();
        Assert.AreEqual(number, package.TrackingNumber);
        Assert.AreEqual(expected, package.ShippingCost);
        Assert.AreEqual(size, package.ShippingFee!.MaxSize);
        Assert.AreEqual(package.ShippingFee.Id, package.ShippingFeeId);
        Assert.AreEqual(registered ? _senderId : (int?)null, package.SenderId);
        Assert.AreEqual(_recipientId, package.RecipientId);
        Assert.IsFalse(package.IsPaid);
        var tracking = await _client.GetFromJsonAsync<JsonElement>("/api/Package/track?trackingNumber=" + number);
        Assert.IsFalse(tracking.TryGetProperty("shippingCost", out _));
        Assert.IsFalse(tracking.TryGetProperty("shippingFee", out _));
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PriceCategoryUsesTokenRatherThanClientSuppliedSenderId(bool registered)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fee = await db.ShippingFees.SingleAsync(fee => fee.MaxSize == "M");
        fee.RegisteredPrice = 1500;
        fee.UnregisteredPrice = 2500;
        await db.SaveChangesAsync();
        if (registered) SignIn(_senderId);
        var data = Booking();
        data["senderId"] = _recipientId;
        var result = await Create(data);
        Assert.AreEqual(registered ? 1500 : 2500, result.GetProperty("shippingCost").GetInt32());
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task LaterTariffChangeDoesNotRepriceEarlierBookingOrOwnList(bool registered)
    {
        if (registered) SignIn(_senderId);
        var first = await Create();
        string oldNumber = first.GetProperty("trackingNumber").GetString()!;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fee = await db.ShippingFees.SingleAsync(fee => fee.MaxSize == "M");
        fee.RegisteredPrice = 2700;
        fee.UnregisteredPrice = 3200;
        await db.SaveChangesAsync();
        var second = await Create();
        Assert.AreEqual(registered ? 2700 : 3200, second.GetProperty("shippingCost").GetInt32());
        Assert.AreEqual(2000, (await db.Packages.SingleAsync(package => package.TrackingNumber == oldNumber)).ShippingCost);
        SignIn(_recipientId);
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/received");
        var oldItem = list.EnumerateArray().Single(item => item.GetProperty("trackingNumber").GetString() == oldNumber);
        Assert.AreEqual(2000, oldItem.GetProperty("shippingCost").GetInt32());
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(int.MaxValue)]
    public async Task PackageValueAndInjectedPaymentOrTariffFieldsCannotChangePrice(int value)
    {
        var data = Booking("M");
        data["value"] = value;
        data["shippingFeeId"] = 1;
        data["shippingCost"] = 1;
        data["registeredPrice"] = 0;
        data["isPaid"] = true;
        var result = await Create(data);
        Assert.AreEqual(2000, result.GetProperty("shippingCost").GetInt32());
        using var scope = _factory.Services.CreateScope();
        var package = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Packages.Include(package => package.ShippingFee).SingleAsync();
        Assert.AreEqual(value, package.Value);
        Assert.AreEqual("M", package.ShippingFee!.MaxSize);
        Assert.AreEqual(2000, package.ShippingCost);
        Assert.IsFalse(package.IsPaid);
    }

    [DataTestMethod]
    [DataRow("sent", false)]
    [DataRow("received", false)]
    [DataRow("sent", true)]
    [DataRow("received", true)]
    public async Task OwnListsReturnStoredPriceAndPaymentFlag(string direction, bool history)
    {
        SignIn(_senderId);
        await Create(Booking("S"));
        if (history)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var package = await db.Packages.SingleAsync();
            // Tesztadat: egy már lezárt, kifizetett csomag. Új fizetési végpontot nem készítünk.
            package.CurrentStatus = PackageStatus.Delivered;
            package.IsPaid = true;
            await db.SaveChangesAsync();
        }
        SignIn(direction == "sent" ? _senderId : _recipientId);
        var list = await _client.GetFromJsonAsync<JsonElement>($"/api/Package/mine/{direction}?history={history}");
        Assert.AreEqual(1, list.GetArrayLength());
        Assert.AreEqual(1000, list[0].GetProperty("shippingCost").GetInt32());
        Assert.AreEqual(history, list[0].GetProperty("isPaid").GetBoolean());
        Assert.IsFalse(list[0].TryGetProperty("shippingFee", out _));
    }

    [TestMethod]
    public async Task HistoricalUnknownFeeRemainsNullOnOwnList()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Packages.Add(new Package
        {
            TrackingNumber = Guid.NewGuid().ToString(), Size = "S", SenderId = _senderId, RecipientId = _recipientId
        });
        await db.SaveChangesAsync();
        SignIn(_senderId);
        var list = await _client.GetFromJsonAsync<JsonElement>("/api/Package/mine/sent");
        Assert.AreEqual(JsonValueKind.Null, list[0].GetProperty("shippingCost").ValueKind);
        Assert.IsNull((await db.Packages.SingleAsync()).ShippingFeeId);
    }

    [TestMethod]
    public async Task AutomaticPickupAssignmentAlsoPreservesTheFee()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var courier = new User { UserName = "courier", Role = UserRole.Courier };
        db.Users.Add(courier);
        await db.SaveChangesAsync();
        db.CourierCars.Add(new CourierCar { CourierId = courier.Id, Capacity = 6, LicensePlate = "TEST-001" });
        await db.SaveChangesAsync();
        var result = await Create();
        Assert.AreEqual("AssignedForPickup", result.GetProperty("status").GetString());
        Assert.AreEqual(2000, result.GetProperty("shippingCost").GetInt32());
        var package = await db.Packages.AsNoTracking().SingleAsync();
        Assert.IsNotNull(package.ShippingFeeId);
        Assert.AreEqual(2000, package.ShippingCost);
        Assert.AreEqual(courier.Id, package.CourierId);
        Assert.AreEqual(1, await db.CourierTasks.CountAsync());
    }

    [TestMethod]
    public async Task MissingTariffDoesNotCreateUnpricedPackageOrHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fee = await db.ShippingFees.SingleAsync(fee => fee.MaxSize == "M");
        db.ShippingFees.Remove(fee);
        await db.SaveChangesAsync();
        try
        {
            var response = await _client.PostAsJsonAsync("/api/Package", Booking());
            Assert.AreEqual(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.AreEqual(0, await db.Packages.CountAsync());
            Assert.AreEqual(0, await db.PackageStatuses.CountAsync());
            Assert.AreEqual(0, await db.CourierTasks.CountAsync());
        }
        finally
        {
            db.ShippingFees.Add(new ShippingFee
            {
                Type = ShippingFeeType.Medium, MaxSize = "M", RegisteredPrice = 2000, UnregisteredPrice = 2000
            });
            await db.SaveChangesAsync();
        }
    }
}
