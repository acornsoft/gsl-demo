namespace Gsl.Functions.RegE;

/// <summary>
/// Business-day calendar: Monday-Friday, excluding US federal holidays (5 U.S.C. 6103).
/// Reg E defines a business day as any day the institution is open to the public for
/// substantially all business functions (12 CFR 1005.2(d)). For this synthetic demo we
/// model that as Mon-Fri minus federal holidays. Holidays falling on Saturday are observed
/// on Friday and on Sunday are observed on Monday (OPM rule).
/// </summary>
public static class BusinessCalendar
{
    public static bool IsBusinessDay(DateOnly d) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsFederalHoliday(d);

    public static bool IsFederalHoliday(DateOnly d)
    {
        // Check this year and next year (Jan 1 of next year can be observed on Dec 31).
        return FederalHolidays(d.Year).Contains(d) || FederalHolidays(d.Year + 1).Contains(d);
    }

    public static HashSet<DateOnly> FederalHolidays(int year) => new()
    {
        Observed(new DateOnly(year, 1, 1)),                 // New Year's Day
        NthWeekday(year, 1, DayOfWeek.Monday, 3),           // Birthday of Martin Luther King, Jr.
        NthWeekday(year, 2, DayOfWeek.Monday, 3),           // Washington's Birthday
        LastWeekday(year, 5, DayOfWeek.Monday),             // Memorial Day
        Observed(new DateOnly(year, 6, 19)),                // Juneteenth
        Observed(new DateOnly(year, 7, 4)),                 // Independence Day
        NthWeekday(year, 9, DayOfWeek.Monday, 1),           // Labor Day
        NthWeekday(year, 10, DayOfWeek.Monday, 2),          // Columbus Day
        Observed(new DateOnly(year, 11, 11)),               // Veterans Day
        NthWeekday(year, 11, DayOfWeek.Thursday, 4),        // Thanksgiving Day
        Observed(new DateOnly(year, 12, 25)),               // Christmas Day
    };

    /// <summary>Adds n business days, counting from the day after <paramref name="start"/>.</summary>
    public static DateOnly AddBusinessDays(DateOnly start, int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n));
        var d = start;
        var added = 0;
        while (added < n)
        {
            d = d.AddDays(1);
            if (IsBusinessDay(d)) added++;
        }
        return d;
    }

    private static DateOnly Observed(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(-1),
        DayOfWeek.Sunday => d.AddDays(1),
        _ => d,
    };

    private static DateOnly NthWeekday(int year, int month, DayOfWeek dow, int n)
    {
        var first = new DateOnly(year, month, 1);
        var offset = ((int)dow - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(offset + 7 * (n - 1));
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek dow)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var offset = ((int)last.DayOfWeek - (int)dow + 7) % 7;
        return last.AddDays(-offset);
    }
}
