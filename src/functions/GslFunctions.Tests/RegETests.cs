using Gsl.Functions.Messaging;
using Gsl.Functions.RegE;
using Xunit;

namespace Gsl.Functions.Tests;

public class BusinessCalendarTests
{
    [Theory]
    [InlineData("2026-11-26")] // Thanksgiving
    [InlineData("2026-11-11")] // Veterans Day (Wed)
    [InlineData("2026-12-25")] // Christmas (Fri)
    [InlineData("2027-01-01")] // New Year's (Fri)
    [InlineData("2027-01-18")] // MLK Day
    [InlineData("2027-07-05")] // Independence Day observed (Jul 4 is Sun)
    [InlineData("2027-12-24")] // Christmas observed (Dec 25 is Sat)
    [InlineData("2027-12-31")] // New Year's 2028 observed (Jan 1 is Sat)
    [InlineData("2026-05-25")] // Memorial Day
    [InlineData("2026-06-19")] // Juneteenth
    public void FederalHolidaysAreNotBusinessDays(string date) =>
        Assert.False(BusinessCalendar.IsBusinessDay(DateOnly.Parse(date)));

    [Fact]
    public void WeekendsAreNotBusinessDays()
    {
        Assert.False(BusinessCalendar.IsBusinessDay(new DateOnly(2026, 11, 14))); // Sat
        Assert.False(BusinessCalendar.IsBusinessDay(new DateOnly(2026, 11, 15))); // Sun
        Assert.True(BusinessCalendar.IsBusinessDay(new DateOnly(2026, 11, 16)));  // Mon
    }

    [Fact]
    public void AddBusinessDaysSkipsWeekendAndThanksgiving()
    {
        // Notice Mon 2026-11-16. Business days: 17,18,19,20, 23,24,25, (26 holiday), 27, 30, Dec 1.
        Assert.Equal(new DateOnly(2026, 12, 1), BusinessCalendar.AddBusinessDays(new DateOnly(2026, 11, 16), 10));
    }
}

public class RegEDeadlineCalculatorTests
{
    [Fact]
    public void StandardAccount_10BusinessDays_45CalendarDays()
    {
        // Notice Mon 2026-11-09: 10,(11 Veterans Day skipped),12,13,16,17,18,19,20,23,24 -> Tue 2026-11-24.
        var r = RegEDeadlineCalculator.Calculate(new(new DateOnly(2026, 11, 9), false, false));
        Assert.Equal(10, r.InvestigationBusinessDays);
        Assert.Equal(new DateOnly(2026, 11, 24), r.InvestigationDueDate);
        Assert.Equal(r.InvestigationDueDate, r.ProvisionalCreditDueDate);
        Assert.Equal(45, r.ExtendedInvestigationCalendarDays);
        Assert.Equal(new DateOnly(2026, 12, 24), r.ExtendedInvestigationDueDate);
        Assert.Contains("12 CFR 1005.11(c)(1)", r.Citations);
        Assert.DoesNotContain("12 CFR 1005.11(c)(3)(ii)", r.Citations);
    }

    [Fact]
    public void NewAccount_20BusinessDays_90CalendarDays()
    {
        var r = RegEDeadlineCalculator.Calculate(new(new DateOnly(2026, 11, 9), true, false));
        Assert.Equal(20, r.InvestigationBusinessDays);
        // 11/10,12,13,16-20,23,24,25,27,30 = 13; Dec 1,2,3,4,7,8,9 = 20.
        Assert.Equal(new DateOnly(2026, 12, 9), r.InvestigationDueDate);
        Assert.Equal(90, r.ExtendedInvestigationCalendarDays);
        Assert.Equal(new DateOnly(2027, 2, 7), r.ExtendedInvestigationDueDate);
        Assert.Contains("12 CFR 1005.11(c)(3)(i)", r.Citations);
        Assert.Contains("12 CFR 1005.11(c)(3)(ii)", r.Citations);
    }

    [Fact]
    public void PosOrForeign_10BusinessDays_90CalendarDays()
    {
        var r = RegEDeadlineCalculator.Calculate(new(new DateOnly(2026, 11, 9), false, true));
        Assert.Equal(10, r.InvestigationBusinessDays);
        Assert.Equal(90, r.ExtendedInvestigationCalendarDays);
    }
}

public class RegERequestParserTests
{
    [Theory]
    [InlineData(null, "required")]
    [InlineData("", "required")]
    [InlineData("not json", "not valid JSON")]
    [InlineData("[]", "JSON object")]
    [InlineData("{}", "noticeDate is required")]
    [InlineData("{\"noticeDate\":20261109}", "noticeDate is required")]
    [InlineData("{\"noticeDate\":\"11/09/2026\"}", "not a valid date")]
    [InlineData("{\"noticeDate\":\"2026-02-30\"}", "not a valid date")]
    [InlineData("{\"noticeDate\":\"2026-11-09\",\"newAccount\":\"yes\"}", "newAccount must be a boolean")]
    [InlineData("{\"noticeDate\":\"2026-11-09\",\"posOrForeign\":1}", "posOrForeign must be a boolean")]
    public void BadInputGivesClearError(string? body, string expected)
    {
        var (req, err) = RegERequestParser.Parse(body);
        Assert.Null(req);
        Assert.Contains(expected, err);
    }

    [Fact]
    public void ValidInputParses()
    {
        var (req, err) = RegERequestParser.Parse("{\"noticeDate\":\"2026-11-09\",\"newAccount\":true}");
        Assert.Null(err);
        Assert.Equal(new RegEDeadlineRequest(new DateOnly(2026, 11, 9), true, false), req);
    }
}

public class PublisherTests
{
    [Fact]
    public async Task SkipsCleanlyWhenNotConfigured()
    {
        Environment.SetEnvironmentVariable("ServiceBusConnection__fullyQualifiedNamespace", null);
        await using var p = ServiceBusDisputeEventPublisher.FromEnvironment();
        Assert.False(p.Enabled);
        Assert.Equal("skipped", await p.PublishAsync(new { x = 1 }));
    }
}
