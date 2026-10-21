namespace Cosmix.Domain.Authentication;

public enum AccountStatus { PendingAccess = 0, ReadyForInvitation = 1, Invited = 2, Active = 3, Suspended = 4, Disabled = 5 }
public enum AccessScopeKind { Self = 1, Employee = 2, Department = 3, Team = 4, Site = 5, Project = 6, All = 7 }
public enum InvitationDeliveryStatus { Pending = 0, Delivered = 1, Failed = 2 }
public enum MfaMethod { Totp = 1, RecoveryCode = 2, ApprovedChannel = 3 }
