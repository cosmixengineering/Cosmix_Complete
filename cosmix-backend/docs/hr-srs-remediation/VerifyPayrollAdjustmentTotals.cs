using System.Reflection;
using Cosmix.Application.HR.PayrollHandoff;

// Actual Finance total projection; synthetic money only, no SQL or payroll writes.
var method = typeof(PayrollHandoffService).GetMethod("Totals", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new Exception("Finance totals projection missing");
var checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
PayrollHandoffTotals Totals(params (string Component, decimal Amount)[] lines) =>
    (PayrollHandoffTotals)method.Invoke(null, [lines])!;
var baseline = Totals(("Overtime", 200m), ("Allowance", 300m), ("FuelClaim", 50m),
    ("AttendanceDeduction", -80m), ("LoanRepayment", -100m));
Check(baseline.OvertimeTotal == 200m && baseline.AllowanceTotal == 350m &&
    baseline.AttendanceDeductionTotal == -180m && baseline.NetAmount == 370m,
    "existing overtime, allowance and principal deductions retain signed totals");
var adjusted = Totals(("Overtime", 200m), ("OtherApprovedAdjustment", 100m),
    ("OtherApprovedAdjustment", -20m));
Check(adjusted.NetAmount == 280m, "Finance net must include both approved credits and recoveries exactly once");
Check(adjusted.OvertimeTotal == 200m && adjusted.AttendanceDeductionTotal == 0m,
    "adjustments do not rewrite original ordinary deductions or OT totals");
Check(Totals(("OtherApprovedAdjustment", -20.25m)).NetAmount == -20.25m,
    "two-decimal recovery remains signed, not whole-rupee OT rounding");
Check(Totals().NetAmount == 0, "an explicitly empty line collection totals zero");
Console.WriteLine($"Payroll adjustment totals: {checks}/{checks} passed. No SQL, host, settlement or payment.");
