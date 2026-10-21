namespace Cosmix.Infrastructure.Authentication.Bootstrap;

/// <summary>Checked by persistence guards; only the local empty-database transaction can enter this scope.</summary>
public sealed class BootstrapWriteScope : IDisposable
{
    private static readonly AsyncLocal<bool> Current = new();
    public static bool IsActive => Current.Value;
    internal BootstrapWriteScope()
    {
        if (Current.Value) throw new InvalidOperationException("Nested bootstrap is forbidden.");
        Current.Value = true;
    }
    public void Dispose() => Current.Value = false;
}
