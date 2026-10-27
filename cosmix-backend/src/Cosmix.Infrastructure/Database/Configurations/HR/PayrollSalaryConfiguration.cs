using Cosmix.Domain.HR.PayrollSalary;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Cosmix.Infrastructure.Database.Configurations.HR;
public sealed class PayrollSalaryComponentConfiguration : IEntityTypeConfiguration<PayrollSalaryComponent>
{
    public void Configure(EntityTypeBuilder<PayrollSalaryComponent> b)
    {
        b.ToTable("PayrollSalaryComponents","hr",t=>{
            t.HasCheckConstraint("CK_PayrollSalaryComponents_Amounts","[Amount] >= 0 AND [Revision] > 0 AND [Kind] BETWEEN 1 AND 4");
            t.HasCheckConstraint("CK_PayrollSalaryComponents_Dates","[EffectiveToExclusive] IS NULL OR [EffectiveToExclusive] > [EffectiveFrom]");});
        b.HasKey(x=>x.Id);b.Property(x=>x.Code).HasMaxLength(40);b.Property(x=>x.Currency).HasMaxLength(3);
        b.Property(x=>x.Amount).HasPrecision(18,2);b.Property(x=>x.RecordedBy).HasMaxLength(200);
        b.HasOne(x=>x.Employee).WithMany().HasForeignKey(x=>x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x=>x.ApprovalRequest).WithMany().HasForeignKey(x=>x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x=>new{x.EmployeeId,x.Code,x.Revision}).IsUnique();b.HasIndex(x=>x.ApprovalRequestId).IsUnique();
    }
}
public sealed class PayrollSalaryPolicyConfiguration : IEntityTypeConfiguration<PayrollSalaryPolicy>
{
    public void Configure(EntityTypeBuilder<PayrollSalaryPolicy> b)
    {
        b.ToTable("PayrollSalaryPolicies","hr");b.HasKey(x=>x.Id);b.Property(x=>x.RulesJson).HasMaxLength(32000);
        b.Property(x=>x.RecordedBy).HasMaxLength(200);b.HasIndex(x=>x.Revision).IsUnique();
        b.HasOne(x=>x.ApprovalRequest).WithMany().HasForeignKey(x=>x.ApprovalRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
