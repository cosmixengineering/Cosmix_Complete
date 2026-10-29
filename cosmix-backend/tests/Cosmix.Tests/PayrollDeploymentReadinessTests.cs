using Cosmix.Api.Health;
using Xunit;

namespace Cosmix.Tests;

public sealed class PayrollDeploymentReadinessTests
{
    private static PayrollDeploymentReadinessCheck.ConfigurationSnapshot Ready =>
        new(true, true, true, true, true, true, true, true, true, true, true);

    [Fact]
    public void Production_rejects_local_sms_even_when_sender_is_configured()
    {
        Assert.Contains("sms_login_recovery_unavailable",
            PayrollDeploymentReadinessCheck.EvaluateConfiguration(Ready with { ExternalSmsSelected = false }));
    }

    [Fact]
    public void Disabled_authentication_storage_and_jobs_remain_visible()
    {
        var issues = PayrollDeploymentReadinessCheck.EvaluateConfiguration(Ready with
        { MobileEnabled = false, WebEnabled = false, PrivateFilesConfigured = false, JobsEnabled = false });
        Assert.Equal(4, issues.Count);
        Assert.Contains("mobile_authentication_unavailable", issues);
        Assert.Contains("web_authentication_unavailable", issues);
        Assert.Contains("private_evidence_storage_unavailable", issues);
        Assert.Contains("report_notification_dispatch_unavailable", issues);
    }

    [Fact]
    public void Configured_metadata_is_not_a_provider_delivery_or_business_signoff()
    {
        Assert.Empty(PayrollDeploymentReadinessCheck.EvaluateConfiguration(Ready));
        Assert.Contains("otp_crypto_unavailable",
            PayrollDeploymentReadinessCheck.EvaluateConfiguration(Ready with { OtpConfigured = false }));
    }
}
