namespace Cosmix.Application.HR.PayrollAccounting;

public sealed class PayrollFinanceConflictException(string message) : InvalidOperationException(message);
public sealed class PayrollFinanceValidationException(string message) : ArgumentException(message);

/// <summary>Translate only explicitly invoked pure domain rules, never repository or storage failures.</summary>
public static class PayrollFinanceErrors
{
    public static void Run(Action rule) => Run(() => { rule(); return true; });
    public static T Run<T>(Func<T> rule)
    {
        try { return rule(); }
        catch (InvalidOperationException ex) { throw new PayrollFinanceConflictException(ex.Message); }
        catch (ArgumentException ex) { throw new PayrollFinanceValidationException(ex.Message); }
    }
}
