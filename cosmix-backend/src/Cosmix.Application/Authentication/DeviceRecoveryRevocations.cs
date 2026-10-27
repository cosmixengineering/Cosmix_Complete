using Cosmix.Domain.Authentication;

namespace Cosmix.Application.Authentication;

public static class DeviceRecoveryRevocations
{
    /// <summary>Apply a reviewed replacement to every live device and session; historical rows stay intact.</summary>
    public static void Apply(IReadOnlyList<RegisteredDevice> activeDevices, IReadOnlyList<UserSession> sessions,
        DateTimeOffset now)
    {
        foreach (var device in activeDevices) device.Revoke("device-recovery", now);
        foreach (var session in sessions)
            if (!session.RevokedAt.HasValue) session.Revoke("device-recovery", now);
    }
}
