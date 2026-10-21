namespace Cosmix.Domain.HR.Leaves;

public enum LeaveLedgerEntryType { Entitlement = 1, Reservation = 2, Utilization = 3, Release = 4, Adjustment = 5, Encashment = 6 }
public enum LeavePayTreatment { PendingReview = 0, Paid = 1, Unpaid = 2, DoesNotReduceRegularLeave = 3 }
public enum LeaveKind { Other = 0, Sick = 1, Casual = 2, Exam = 3, Emergency = 4, SiteInjuryRest = 5, PersonalTravel = 6 }
