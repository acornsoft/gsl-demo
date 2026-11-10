namespace Gsl.Functions.RegE;

public sealed record RegEDeadlineRequest(DateOnly NoticeDate, bool NewAccount, bool PosOrForeign);

public sealed record RegEDeadlines(
    DateOnly NoticeDate,
    bool NewAccount,
    bool PosOrForeign,
    int InvestigationBusinessDays,
    DateOnly InvestigationDueDate,
    DateOnly ProvisionalCreditDueDate,
    int ExtendedInvestigationCalendarDays,
    DateOnly ExtendedInvestigationDueDate,
    IReadOnlyList<string> Citations,
    string Note);

/// <summary>
/// Regulation E error-resolution timelines, 12 CFR 1005.11(c).
/// Synthetic demo logic for the fictional Gotham Savings and Loan. Not legal advice.
/// </summary>
public static class RegEDeadlineCalculator
{
    public static RegEDeadlines Calculate(RegEDeadlineRequest r)
    {
        // 12 CFR 1005.11(c)(1): investigate and determine whether an error occurred within
        // 10 business days of receiving notice of error.
        // 12 CFR 1005.11(c)(3)(i): 20 business days if the notice involves an electronic fund
        // transfer to or from the account within 30 days after the first deposit (new account).
        var businessDays = r.NewAccount ? 20 : 10;
        var investigationDue = BusinessCalendar.AddBusinessDays(r.NoticeDate, businessDays);

        // 12 CFR 1005.11(c)(2)(i): if the institution can't finish within that window, it may take
        // up to 45 days, provided it provisionally credits the account (amount of the alleged
        // error, including interest where applicable) within the same 10 (or 20) business days.
        var provisionalCreditDue = investigationDue;

        // 12 CFR 1005.11(c)(3)(ii): 90 days instead of 45 for transfers that were not initiated
        // within a state, resulted from a point-of-sale debit card transaction, or occurred
        // within 30 days after the first deposit to the account (new account).
        var calendarDays = (r.NewAccount || r.PosOrForeign) ? 90 : 45;
        var extendedDue = r.NoticeDate.AddDays(calendarDays);

        var citations = new List<string>
        {
            r.NewAccount ? "12 CFR 1005.11(c)(3)(i)" : "12 CFR 1005.11(c)(1)",
            "12 CFR 1005.11(c)(2)(i)",
        };
        if (calendarDays == 90) citations.Add("12 CFR 1005.11(c)(3)(ii)");

        return new RegEDeadlines(
            r.NoticeDate, r.NewAccount, r.PosOrForeign,
            businessDays, investigationDue, provisionalCreditDue,
            calendarDays, extendedDue, citations,
            "Business days = Mon-Fri excluding US federal holidays (demo model of 12 CFR 1005.2(d)). " +
            "Synthetic demo data only; not legal advice.");
    }
}
