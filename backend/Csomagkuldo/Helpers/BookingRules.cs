namespace Csomagkuldo.Helpers;

public static class BookingRules
{
    public static bool IsValidPickupDate(DateOnly pickupDate, DateTime utcNow)
    {
        TimeZoneInfo timeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Budapest");
        DateTime localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone);
        return pickupDate > DateOnly.FromDateTime(localNow);
    }
}
