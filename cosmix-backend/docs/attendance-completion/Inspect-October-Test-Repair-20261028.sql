-- READ ONLY. Produces a bounded manifest; contains no repair/migration operation.
-- Must run on the separately verified existing Cosmix_Testing database.
SET NOCOUNT ON;
IF DB_NAME() <> N'Cosmix_Testing' THROW 51000, 'Wrong database.', 1;
;WITH B AS (
    SELECT DISTINCT PayrollHandoffBatchId Id FROM hr.PayrollHandoffLines
    WHERE AttendancePeriodLockEventId = '6E56463D-2802-478D-B917-2E8FD615F635'
), O AS (
    SELECT o.Id FROM hr.PayrollObligations o JOIN B ON B.Id = o.BatchId
), J AS (
    SELECT j.Id FROM hr.PayrollJournalEntries j JOIN O ON O.Id = j.ObligationId
), S AS (
    SELECT s.Id FROM hr.StatutoryContributions s
    WHERE s.ObligationId IN (SELECT Id FROM O) OR s.JournalEntryId IN (SELECT Id FROM J)
), RowsInPlan AS (
    SELECT 'hr.SalaryPayments' TableName, p.Id FROM hr.SalaryPayments p JOIN O ON O.Id = p.ObligationId
    UNION ALL SELECT 'hr.StatutoryContributions', Id FROM S
    UNION ALL SELECT 'hr.PayrollJournalEntries', Id FROM J
    UNION ALL SELECT 'hr.PayrollObligations', Id FROM O
    UNION ALL SELECT 'hr.PayrollHandoffLines', l.Id FROM hr.PayrollHandoffLines l JOIN B ON B.Id = l.PayrollHandoffBatchId
    UNION ALL SELECT 'hr.PayrollHandoffBatches', Id FROM B
    UNION ALL SELECT 'attendance.AttendancePeriodLockEvents', Id FROM attendance.AttendancePeriodLockEvents
        WHERE Id = '6E56463D-2802-478D-B917-2E8FD615F635' AND AttendancePeriodLockId = '4EB6E47C-91C6-482F-914E-D5060BD63D97'
    UNION ALL SELECT 'attendance.AttendancePeriodLocks', Id FROM attendance.AttendancePeriodLocks
        WHERE Id = '4EB6E47C-91C6-482F-914E-D5060BD63D97' AND PeriodStart = '20261001' AND PeriodEndExclusive = '20261101'
)
SELECT TableName, COUNT(*) AS PlannedRows,
    CONVERT(varchar(64), HASHBYTES('SHA2_256', STRING_AGG(CONVERT(nvarchar(max), Id), N'|')
        WITHIN GROUP (ORDER BY Id)), 2) AS IdSetSha256
FROM RowsInPlan GROUP BY TableName ORDER BY TableName;
