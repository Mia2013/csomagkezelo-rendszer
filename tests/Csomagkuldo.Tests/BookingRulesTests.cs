using Csomagkuldo.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Csomagkuldo.Tests;

[TestClass]
public class BookingRulesTests
{
    [DataTestMethod]
    [DataRow("2026-10-03T21:59:00Z", "2026-10-04", true)]
    [DataRow("2026-10-03T22:01:00Z", "2026-10-04", false)]
    [DataRow("2026-10-03T22:01:00Z", "2026-10-05", true)]
    [DataRow("2026-01-03T23:01:00Z", "2026-01-04", false)]
    [DataRow("2026-01-03T23:01:00Z", "2026-01-05", true)]
    [DataRow("2026-03-28T23:30:00Z", "2026-03-29", false)]
    public void PickupUsesBudapestCalendarDay(string utc, string pickup, bool expected)
    {
        DateTime now = DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        Assert.AreEqual(expected, BookingRules.IsValidPickupDate(DateOnly.Parse(pickup), now));
    }
}
