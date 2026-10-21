import React, { useState } from 'react';
import { Text, View } from 'react-native';
import { InfoRow, StatusBadge } from './ServiceControls';
import { Button, Notice } from './Controls';
import type { DataRecord, ServiceKey } from '../types/selfService';
import {
  amount,
  fraction,
  numeric,
  record,
  records,
  status,
  text,
} from '../utils/serviceValues';
import { layout } from '../theme/theme';

const list = (value: unknown) => (Array.isArray(value) ? records(value) : []);
const number = (value: unknown) =>
  numeric(value)?.toLocaleString('en-PK') ?? 'Unavailable';
export function ServiceSummary({
  service,
  root,
}: {
  service: ServiceKey;
  root: DataRecord;
}): React.JSX.Element | null {
  const [reveal, setReveal] = useState(false);
  if (service === 'loans') {
    return (
      <View style={layout.card}>
        <Text style={layout.heading}>Your borrowing summary</Text>
        <InfoRow
          label="Outstanding balance"
          value={amount(root.currentOutstandingBalance, root.currency)}
        />
        <InfoRow
          label="Recorded repayments"
          value={amount(root.totalRepaidAmount, root.currency)}
        />
        <InfoRow
          label="Active agreements"
          value={number(root.activeAgreementsCount)}
        />
        <Notice
          tone="info"
          message="Requests need company approval. Repayment records and balances are shown separately from request status."
        />
      </View>
    );
  }
  if (service === 'resignation') {
    return (
      <View style={layout.card}>
        <InfoRow
          label="Current employment status"
          value={text(root.currentStatus)}
        />
        <Text style={layout.body}>
          {root.canSubmit === true
            ? 'You can submit a resignation request for independent HR review.'
            : 'A new resignation request is currently unavailable. Contact HR about your employment record or an outstanding request.'}
        </Text>
      </View>
    );
  }
  if (service === 'requests') {
    return (
      <View style={layout.card}>
        <Text style={layout.heading}>Pending with your company</Text>
        {[
          ['Leave', 'pendingLeaveRequestsCount'],
          ['Advance / loan', 'pendingAdvanceLoanRequestsCount'],
          ['Allowances', 'pendingAllowanceClaimsCount'],
          ['Fuel', 'pendingFuelClaimsCount'],
          [
            'Attendance corrections',
            'pendingAttendanceCorrectionRequestsCount',
          ],
        ].map(([label, key]) => (
          <InfoRow key={key} label={label} value={number(root[key])} />
        ))}
        <InfoRow
          label="Total in these categories"
          value={number(root.totalPendingRequestsCount)}
        />
        <Text style={layout.muted}>
          Resignation, overtime and encashment have their own request lists.
        </Text>
      </View>
    );
  }
  if (service === 'shift') {
    return (
      <View style={layout.card}>
        <Text style={layout.heading}>Your dated shift</Text>
        <InfoRow label="Schedule" value={status(root.status)} />
        <InfoRow label="Business date" value={text(root.businessDate)} />
        <InfoRow label="Time zone" value={text(root.timeZone)} />
        <InfoRow label="Starts" value={text(root.startsAt)} />
        <InfoRow label="Ends" value={text(root.endsAt)} />
        <InfoRow
          label="Scheduled minutes"
          value={number(root.configuredScheduledMinutes)}
        />
        {root.holiday ? (
          <InfoRow label="Holiday" value={text(record(root.holiday).name)} />
        ) : null}
      </View>
    );
  }
  if (service === 'calendar') {
    return (
      <View style={layout.card}>
        <Text style={layout.heading}>Monthly assessment</Text>
        <InfoRow label="Calculation status" value={status(root.status)} />
        {[
          ['Present', 'presentDays'],
          ['Absent', 'absentDays'],
          ['Leave', 'leaveDays'],
          ['Holidays', 'holidayDays'],
          ['Rest days', 'restDays'],
          ['Late', 'lateDays'],
          ['Worked minutes', 'totalWorkedMinutes'],
          ['Overtime minutes', 'totalOvertimeMinutes'],
        ].map(([label, key]) => (
          <InfoRow key={key} label={label} value={number(root[key])} />
        ))}
        <Text style={layout.muted}>
          Unavailable values have not been supplied or calculated. Attendance
          assessments are separate from payroll settlement.
        </Text>
      </View>
    );
  }
  if (service === 'leaves') {
    const overview = root.overview ? record(root.overview) : null;
    return (
      <View style={layout.card}>
        <Text style={layout.heading}>Leave balances</Text>
        {!overview ? (
          <Text style={layout.muted}>
            The summary is unavailable or needs additional company access. Check
            an exact policy balance in the application form.
          </Text>
        ) : (
          <>
            <InfoRow label="As of" value={text(overview.asOf)} />
            {list(overview.balances).map((row, index) => {
              const balance = row.balance ? record(row.balance) : null;
              return (
                <View key={index} style={layout.stack6}>
                  <Text style={layout.label}>{text(row.leaveTypeName)}</Text>
                  <StatusBadge value={row.status} />
                  {balance ? (
                    <>
                      <InfoRow
                        label="Annual remaining"
                        value={
                          balance.annualBalance
                            ? fraction(
                                record(balance.annualBalance).remainingDays,
                              )
                            : 'Unavailable'
                        }
                      />
                      <InfoRow
                        label="Type remaining"
                        value={
                          balance.typeBalance
                            ? fraction(
                                record(balance.typeBalance).remainingDays,
                              )
                            : 'Not specified by this policy'
                        }
                      />
                    </>
                  ) : (
                    <Text style={layout.muted}>
                      No confirmed numeric balance supplied.
                    </Text>
                  )}
                </View>
              );
            })}
          </>
        )}
      </View>
    );
  }
  if (service === 'profile') {
    const assignment = root.activeAssignment
      ? record(root.activeAssignment)
      : null;
    const identity = text(root.identityDocumentNumber, '');
    const mask = identity ? `•••• ${identity.slice(-4)}` : 'Unavailable';
    return (
      <>
        <View style={layout.card}>
          <Text style={layout.heading}>
            {text(root.preferredName, text(root.fullLegalName))}
          </Text>
          <InfoRow
            label="Employee number"
            value={number(root.employeeNumber)}
          />
          <InfoRow label="Joined" value={text(root.joinedOn)} />
          <InfoRow
            label="Identity document"
            value={reveal ? text(root.identityDocumentNumber) : mask}
          />
          <Button
            title={reveal ? 'Hide identity number' : 'Show identity number'}
            secondary
            onPress={() => setReveal(value => !value)}
          />
          <InfoRow label="Company phone" value={text(root.companyPhone)} />
          <InfoRow label="Official email" value={text(root.officialEmail)} />
          <InfoRow
            label="Assignment availability"
            value={text(root.assignmentStatus)}
          />
          {assignment
            ? [
                'departmentName',
                'designationTitle',
                'staffCategoryName',
                'employmentTypeName',
                'branchName',
                'siteName',
                'reportingManagerName',
              ].map(key => (
                <InfoRow
                  key={key}
                  label={key.replace(/([A-Z])/g, ' $1')}
                  value={text(assignment[key])}
                />
              ))
            : null}
        </View>
        <View style={layout.card}>
          <Text style={layout.heading}>Documents</Text>
          {list(root.documents).length === 0 ? (
            <Text style={layout.muted}>No documents available.</Text>
          ) : (
            list(root.documents).map((row, index) => (
              <View key={index} style={layout.stack6}>
                <Text style={layout.label}>{text(row.originalFileName)}</Text>
                <InfoRow
                  label="Verification"
                  value={status(row.verificationStatus)}
                />
                <InfoRow label="Scan" value={status(row.scanStatus)} />
                <InfoRow
                  label="Expiry"
                  value={text(row.expiresOn, 'Not supplied')}
                />
              </View>
            ))
          )}
          <Text style={layout.muted}>
            Contact HR for document changes or copies.
          </Text>
        </View>
        <View style={layout.card}>
          <Text style={layout.heading}>Assigned assets & SIMs</Text>
          {list(root.assignedAssets).map((row, index) => (
            <InfoRow
              key={`asset-${index}`}
              label={text(row.assetReference)}
              value={`${
                row.isActive === true ? 'Assigned' : 'Returned'
              } · ${text(row.issuedAt)}`}
            />
          ))}
          {list(root.assignedSims).map((row, index) => (
            <InfoRow
              key={`sim-${index}`}
              label={text(row.phoneNumber)}
              value={`${text(row.simReference)} · ${status(row.status)}`}
            />
          ))}
          {!list(root.assignedAssets).length &&
          !list(root.assignedSims).length ? (
            <Text style={layout.muted}>No assigned items available.</Text>
          ) : null}
        </View>
      </>
    );
  }
  if (service === 'finances') {
    const compensation = root.currentCompensation
      ? record(root.currentCompensation)
      : null;
    return (
      <>
        <View style={layout.card}>
          <Text style={layout.heading}>Current compensation</Text>
          {compensation ? (
            <>
              <InfoRow
                label="Base"
                value={amount(compensation.baseAmount, compensation.currency)}
              />
              <InfoRow
                label="Gross"
                value={amount(compensation.grossAmount, compensation.currency)}
              />
              <InfoRow
                label="Wage basis"
                value={status(compensation.wageBasis)}
              />
              <InfoRow
                label="Effective from"
                value={text(compensation.effectiveFrom)}
              />
            </>
          ) : (
            <Text style={layout.muted}>No compensation record available.</Text>
          )}
        </View>
        <View style={layout.card}>
          <Text style={layout.heading}>Active agreements</Text>
          {list(root.activeAgreements).map((row, index) => (
            <View key={index} style={layout.stack6}>
              <Text style={layout.label}>{text(row.agreementNumber)}</Text>
              <InfoRow
                label="Balance"
                value={amount(row.remainingBalance, row.currency)}
              />
              <InfoRow label="Next due" value={text(row.nextDueDate)} />
            </View>
          ))}
          {!list(root.activeAgreements).length ? (
            <Text style={layout.muted}>No active agreements available.</Text>
          ) : null}
        </View>
        <View style={layout.card}>
          <Text style={layout.heading}>Recent repayments</Text>
          {list(root.recentRepayments).map((row, index) => (
            <InfoRow
              key={index}
              label={`${text(row.entryDate)} · ${text(row.agreementNumber)}`}
              value={amount(row.amount, row.currency)}
            />
          ))}
          {!list(root.recentRepayments).length ? (
            <Text style={layout.muted}>No recorded repayments.</Text>
          ) : null}
        </View>
        <View style={layout.card}>
          <Text style={layout.heading}>Payroll summaries</Text>
          {list(root.recentPayrollSummaries).map((row, index) => (
            <View key={index} style={layout.stack6}>
              <Text style={layout.label}>{text(row.batchReference)}</Text>
              <InfoRow
                label="Period"
                value={`${text(row.periodStart)} – ${text(row.periodEnd)}`}
              />
              <InfoRow
                label="Net payable"
                value={amount(row.netPayable, row.currency)}
              />
            </View>
          ))}
          {!list(root.recentPayrollSummaries).length ? (
            <Text style={layout.muted}>No payroll summary available.</Text>
          ) : null}
          <Notice
            tone="info"
            message="A payroll summary or handoff is not proof of payment. Contact Accounts for settlement confirmation."
          />
        </View>
      </>
    );
  }
  if (service === 'vehicles') {
    return (
      <View style={layout.card}>
        <Text style={layout.heading}>Your assigned vehicles</Text>
        {list(root.custody).map((row, index) => (
          <InfoRow
            key={index}
            label={text(row.registrationNumber)}
            value={`${
              row.isActive === true ? 'Current assignment' : 'Returned'
            } · ${text(row.issuedAt)}`}
          />
        ))}
        {!list(root.custody).length ? (
          <Text style={layout.muted}>No vehicle assignments available.</Text>
        ) : null}
      </View>
    );
  }
  return null;
}
