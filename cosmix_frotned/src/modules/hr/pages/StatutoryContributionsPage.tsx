import { EmployeeReference } from '@/components/employees/EmployeeReference';
import { useState } from 'react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { request } from '@/services/api/http-client';
import { Denied, ActionMessage } from '../components/Feedback';
import { FinanceQueryState } from '../components/finance/FinanceQueryState';
import {
  FinanceNotice,
  FinanceTabs,
  TextAreaField,
  HasMorePager,
} from '../components/finance/FinanceChrome';
import { PayrollEmployeePicker, PayrollSelect } from '../components/finance/PayrollFields';
import { money } from '../components/finance/labels';
import { useFinanceAccess } from '../hooks/use-finance-access';
import { useFinanceAction, useFinanceQuery } from '../hooks/use-finance-queries';
import { useHrLifetime } from '../hooks/use-hr-lifetime';
import { financeError } from '../api/finance-messages';
import {
  accountingSetup,
  contribution,
  contributionPage,
  obligation,
  registration,
  coverage,
  statutorySchemeCode,
} from '../schemas/payroll-accounting';
import '@/styles/payroll-handoff.css';
const schemes = ['EOBI', 'PESSI', 'SESSI'].map((value) => ({ value, label: value }));
export function StatutoryContributionsPage() {
  const access = useFinanceAccess();
  const [tab, setTab] = useState('entries');
  if (!access.can('statutory.contributions.manage')) return <Denied />;
  return (
    <div className="payroll-handoff-container">
      <header className="payroll-header-banner">
        <h1>Statutory contributions</h1>
        <p>Actual employee contributions, registration and approved applicability.</p>
        <FinanceTabs
          tabs={[
            { id: 'entries', label: 'Monthly contributions' },
            { id: 'registration', label: 'Registration and applicability' },
          ]}
          active={tab}
          onChange={setTab}
          label="Statutory views"
        />
      </header>
      {tab === 'entries' ? <ContributionEntries /> : <StatutoryRegistrations />}
    </div>
  );
}
function ContributionEntries() {
  const access = useFinanceAccess();
  const action = useFinanceAction();
  const lifetime = useHrLifetime();
  const [page, setPage] = useState(1);
  const [selectedId, setSelectedId] = useState('');
  const [scheme, setScheme] = useState('');
  const [month, setMonth] = useState('');
  const [currency, setCurrency] = useState('');
  const [employerRegistration, setEmployerRegistration] = useState('');
  const [notes, setNotes] = useState('');
  const [reason, setReason] = useState('');
  const [employeeId, setEmployeeId] = useState('');
  const [employeeAmount, setEmployeeAmount] = useState('');
  const [employerAmount, setEmployerAmount] = useState('');
  const [evidence, setEvidence] = useState('');
  const [sha, setSha] = useState('');
  const [entries, setEntries] = useState<
    {
      employeeId: string;
      employeeContributionAmount: number;
      employerContributionAmount: number;
      evidenceReference: string;
      evidenceSha256: string;
    }[]
  >([]);
  const [obligationId, setObligationId] = useState('');
  const [obligationPage, setObligationPage] = useState(1);
  const [profileId, setProfileId] = useState('');
  const [postingDate, setPostingDate] = useState('');
  const [downloadError, setDownloadError] = useState('');
  const [downloading, setDownloading] = useState(false);
  const list = useFinanceQuery(
    `/statutory-contributions?page=${page}&pageSize=25`,
    contributionPage,
    true,
  );
  const detail = useFinanceQuery(
    `/statutory-contributions/${selectedId}`,
    contribution,
    true,
    Boolean(selectedId),
  );
  const setup = useFinanceQuery(
    '/payroll-accounting/setup',
    accountingSetup,
    access.can('payroll.accounting.accrue'),
  );
  const obligations = useFinanceQuery(
    `/payroll-accounting/obligations?page=${obligationPage}&pageSize=100`,
    z.array(obligation),
    access.can('payroll.accounting.accrue'),
  );
  const item = !detail.isError && !detail.isFetching ? detail.data : undefined;
  async function save() {
    const result = await action.run(
      '/statutory-contributions',
      {
        scheme: statutorySchemeCode(scheme),
        contributionMonth: `${month}-01`,
        currency,
        employerRegistrationNumber: employerRegistration,
        notes,
        entries,
        reason,
      },
      'POST',
      contribution,
    );
    if (result) {
      setSelectedId(result.id);
      setEntries([]);
      void list.refetch();
    }
  }
  async function decide(post: boolean) {
    if (!item?.version) return;
    const result = await action.run(
      `/statutory-contributions/${item.id}/${post ? 'post' : 'approve'}`,
      post
        ? {
            obligationId,
            postingProfileId: profileId,
            postingDate,
            expectedVersion: item.version,
            reason,
          }
        : { expectedVersion: item.version, reason },
      'POST',
      contribution,
    );
    if (result) {
      void detail.refetch();
      void list.refetch();
    }
  }
  async function exportSchedule() {
    if (!item || downloading) return;
    const signal = lifetime();
    setDownloading(true);
    setDownloadError('');
    try {
      const blob = await request(`/statutory-contributions/${item.id}/export`, {
        signal,
        responseType: 'blob',
      });
      signal.throwIfAborted();
      if (!(blob instanceof Blob)) throw new Error('Invalid export');
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `statutory-${item.id}.csv`;
      link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (failure) {
      if (!signal.aborted) setDownloadError(financeError(failure));
    } finally {
      if (!signal.aborted) setDownloading(false);
    }
  }
  return (
    <section className="payroll-section-card">
      <h2>Monthly contribution entries</h2>
      <FinanceNotice>
        Enter the authorized amount and evidence for each employee, including explicit zero
        contributions where applicable. Totals and journal amounts come from the saved employee
        lines.
      </FinanceNotice>
      <TextAreaField
        label="Creation, approval or posting reason"
        value={reason}
        onChange={setReason}
        required
      />
      <FinanceQueryState query={list}>
        {list.data?.items.map((row) => (
          <article key={row.id} className="payroll-batch-card">
            <Button variant="secondary" onClick={() => setSelectedId(row.id)}>
              {row.scheme} · {row.contributionMonth}
            </Button>
            <p>
              {row.state} · {money(row.totalContributionAmount, row.currency)}
            </p>
          </article>
        ))}
        <HasMorePager
          page={page}
          hasMore={page * 25 < (list.data?.totalCount ?? 0)}
          busy={list.isFetching}
          onPrevious={() => setPage(page - 1)}
          onNext={() => setPage(page + 1)}
        />
      </FinanceQueryState>
      {selectedId && (
        <FinanceQueryState query={detail}>
          {item && (
            <>
              <h3>
                {item.scheme} · {item.state}
              </h3>
              <p>
                Employee total {money(item.employeeContributionAmount, item.currency)} · Employer
                total {money(item.employerContributionAmount, item.currency)}
              </p>
              <div className="payroll-table-wrapper">
                <table className="payroll-table">
                  <thead>
                    <tr>
                      <th>Employee / registration</th>
                      <th>Employee amount</th>
                      <th>Employer amount</th>
                      <th>Evidence</th>
                    </tr>
                  </thead>
                  <tbody>
                    {item.entries?.map((line) => (
                      <tr key={line.id}>
                        <td>
                          <EmployeeReference employeeId={line.employeeId} />
                          <br />
                          {line.registrationNumber}
                        </td>
                        <td>{money(line.employeeContributionAmount, item.currency)}</td>
                        <td>{money(line.employerContributionAmount, item.currency)}</td>
                        <td>
                          {line.evidenceReference}
                          <br />
                          {line.evidenceSha256}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <Button
                variant="secondary"
                disabled={downloading}
                onClick={() => void exportSchedule()}
              >
                Export actual schedule CSV
              </Button>
              {item.postingJournalEntryId && (
                <p>
                  Posted journal: <code>{item.postingJournalEntryId}</code> · Obligation{' '}
                  {item.obligationId}
                </p>
              )}
              {item.postedEmployeeCount !== undefined && (
                <FinanceNotice>
                  {item.postedEmployeeCount} of {item.entries?.length ?? 0} employee contributions
                  posted.
                  {item.state === 'Approved' &&
                    ' Post each remaining payroll group; this month stays open until every employee entry is posted.'}
                </FinanceNotice>
              )}
              {item.state === 'Draft' &&
                item.createdByUserId !== access.user?.account?.accountId &&
                access.can('statutory.contributions.approve') && (
                  <Button
                    disabled={action.pending || !reason.trim() || !item.version}
                    onClick={() => void decide(false)}
                  >
                    Independently approve entries
                  </Button>
                )}
              {item.state === 'Approved' && access.can('payroll.accounting.accrue') && (
                <FinanceQueryState query={setup}>
                  <FinanceQueryState query={obligations}>
                    <form
                      onSubmit={(event) => {
                        event.preventDefault();
                        void decide(true);
                      }}
                    >
                      <PayrollSelect
                        label="Accrued salary obligation"
                        value={obligationId}
                        onChange={setObligationId}
                        options={(obligations.data ?? [])
                          .filter(
                            (row) =>
                              row.currency === item.currency &&
                              !(item.postedObligationIds ?? []).includes(row.id),
                          )
                          .map((row) => ({
                            value: row.id,
                            label: `${row.batchId} · ${money(row.totalNetAmount, row.currency)}`,
                          }))}
                      />
                      <PayrollSelect
                        label="Approved posting profile"
                        value={profileId}
                        onChange={setProfileId}
                        options={(setup.data?.profiles ?? [])
                          .filter(
                            (row) => row.state === 'Approved' && row.currency === item.currency,
                          )
                          .map((row) => ({ value: row.id, label: row.name }))}
                      />
                      <HasMorePager
                        page={obligationPage}
                        hasMore={obligations.data?.length === 100}
                        busy={obligations.isFetching}
                        onPrevious={() => {
                          setObligationPage(obligationPage - 1);
                          setObligationId('');
                        }}
                        onNext={() => {
                          setObligationPage(obligationPage + 1);
                          setObligationId('');
                        }}
                      />
                      <TextInput
                        label="Posting date"
                        type="date"
                        required
                        value={postingDate}
                        onChange={(event) => setPostingDate(event.target.value)}
                      />
                      <Button type="submit" disabled={action.pending || !reason.trim()}>
                        Post linked statutory journal
                      </Button>
                    </form>
                  </FinanceQueryState>
                </FinanceQueryState>
              )}
            </>
          )}
        </FinanceQueryState>
      )}
      <h3>Create contribution draft</h3>
      <fieldset disabled={action.pending}>
        <div className="hr-form-grid">
          <PayrollSelect label="Scheme" value={scheme} onChange={setScheme} options={schemes} />
          <TextInput
            label="Contribution month"
            type="month"
            value={month}
            onChange={(event) => setMonth(event.target.value)}
            required
          />
          <TextInput
            label="Currency"
            required
            pattern="[A-Z]{3}"
            value={currency}
            onChange={(event) => setCurrency(event.target.value.toUpperCase())}
          />
          <TextInput
            label="Employer registration number"
            required
            value={employerRegistration}
            onChange={(event) => setEmployerRegistration(event.target.value)}
          />
        </div>
        <TextAreaField label="Notes" value={notes} onChange={setNotes} />
        <form
          onSubmit={(event) => {
            event.preventDefault();
            setEntries((rows) => [
              ...rows,
              {
                employeeId,
                employeeContributionAmount: Number(employeeAmount),
                employerContributionAmount: Number(employerAmount),
                evidenceReference: evidence,
                evidenceSha256: sha,
              },
            ]);
            setEmployeeId('');
            setEmployeeAmount('');
            setEmployerAmount('');
            setEvidence('');
            setSha('');
          }}
        >
          <PayrollEmployeePicker value={employeeId} onChange={setEmployeeId} />
          <div className="hr-form-grid">
            <TextInput
              label="Employee actual contribution"
              required
              type="number"
              min="0"
              step="0.01"
              value={employeeAmount}
              onChange={(event) => setEmployeeAmount(event.target.value)}
            />
            <TextInput
              label="Employer actual contribution"
              required
              type="number"
              min="0"
              step="0.01"
              value={employerAmount}
              onChange={(event) => setEmployerAmount(event.target.value)}
            />
            <TextInput
              label="Evidence reference"
              required
              value={evidence}
              onChange={(event) => setEvidence(event.target.value)}
            />
            <TextInput
              label="Evidence SHA256"
              required
              pattern="[a-fA-F0-9]{64}"
              value={sha}
              onChange={(event) => setSha(event.target.value)}
            />
          </div>
          <Button
            type="submit"
            disabled={!employeeId || entries.some((row) => row.employeeId === employeeId)}
          >
            Add employee entry
          </Button>
        </form>
        {entries.map((line, index) => (
          <p key={line.employeeId}>
            <EmployeeReference employeeId={line.employeeId} />: employee{' '}
            {line.employeeContributionAmount} · employer {line.employerContributionAmount}{' '}
            <Button
              variant="secondary"
              onClick={() => setEntries((rows) => rows.filter((_, i) => index !== i))}
            >
              Remove unsaved entry
            </Button>
          </p>
        ))}
        <Button
          disabled={
            !entries.length ||
            !reason.trim() ||
            !scheme ||
            !month ||
            !currency ||
            !employerRegistration.trim()
          }
          onClick={() => void save()}
        >
          Save actual entries as draft
        </Button>
      </fieldset>
      {downloadError && <p role="alert">{downloadError}</p>}
      <ActionMessage action={action} />
    </section>
  );
}

function StatutoryRegistrations() {
  const access = useFinanceAccess();
  const action = useFinanceAction();
  const [employeeId, setEmployeeId] = useState('');
  const [reason, setReason] = useState('');
  const [scheme, setScheme] = useState('');
  const [number, setNumber] = useState('');
  const [registeredAt, setRegisteredAt] = useState('');
  const [start, setStart] = useState('');
  const [end, setEnd] = useState('');
  const [selectedSchemes, setSchemes] = useState<string[]>([]);
  const [explicitCoverage, setExplicitCoverage] = useState(false);
  const [evidence, setEvidence] = useState('');
  const [sha, setSha] = useState('');
  const registrations = useFinanceQuery(
    `/employees/${employeeId}/statutory-registrations`,
    z.array(registration),
    true,
    Boolean(employeeId),
  );
  const coverages = useFinanceQuery(
    `/employees/${employeeId}/statutory-coverage`,
    z.array(coverage),
    true,
    Boolean(employeeId),
  );
  async function register() {
    const result = await action.run(
      `/employees/${employeeId}/statutory-registrations`,
      { scheme: statutorySchemeCode(scheme), registrationNumber: number, registeredAt, reason },
      'POST',
      registration,
    );
    if (result) void registrations.refetch();
  }
  async function saveCoverage() {
    const result = await action.run(
      `/employees/${employeeId}/statutory-coverage`,
      {
        effectiveFrom: start,
        effectiveToExclusive: end || null,
        schemes: selectedSchemes.map(statutorySchemeCode),
        evidenceReference: evidence,
        evidenceSha256: sha,
        reason,
      },
      'POST',
      coverage,
    );
    if (result) void coverages.refetch();
  }
  async function approve(id: string, expectedVersion: string) {
    const result = await action.run(
      `/employees/${employeeId}/statutory-coverage/${id}/approve`,
      { expectedVersion, reason },
      'POST',
      coverage,
    );
    if (result) void coverages.refetch();
  }
  return (
    <section className="payroll-section-card">
      <h2>Employee registration and applicability</h2>
      <PayrollEmployeePicker value={employeeId} onChange={setEmployeeId} />
      <TextAreaField label="Reason" required value={reason} onChange={setReason} />
      {employeeId && (
        <>
          <FinanceQueryState query={registrations}>
            {registrations.data?.map((row) => (
              <p key={row.id}>
                {row.scheme} · {row.registrationNumber} · {row.registeredAt}
              </p>
            ))}
          </FinanceQueryState>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void register();
            }}
          >
            <fieldset disabled={action.pending}>
              <PayrollSelect label="Scheme" value={scheme} onChange={setScheme} options={schemes} />
              <TextInput
                label="Registration number"
                required
                value={number}
                onChange={(event) => setNumber(event.target.value)}
              />
              <TextInput
                label="Registered on"
                type="date"
                required
                value={registeredAt}
                onChange={(event) => setRegisteredAt(event.target.value)}
              />
              <Button type="submit" disabled={!reason.trim()}>
                Record registration
              </Button>
            </fieldset>
          </form>
          <FinanceQueryState query={coverages}>
            <h3>Applicability decisions</h3>
            {coverages.data?.map((row) => (
              <article key={row.id} className="payroll-batch-card">
                <p>
                  {row.effectiveFrom} → {row.effectiveToExclusive ?? 'Ongoing'} ·{' '}
                  {row.schemes.length ? row.schemes.join(', ') : 'Explicitly no applicable scheme'}{' '}
                  · {row.state}
                </p>
                <p>{row.evidenceReference}</p>
                {row.state === 'Draft' &&
                  row.createdByUserId !== access.user?.account?.accountId &&
                  access.can('statutory.contributions.approve') && (
                    <Button
                      disabled={action.pending || !reason.trim()}
                      onClick={() => void approve(row.id, row.version)}
                    >
                      Approve applicability
                    </Button>
                  )}
              </article>
            ))}
          </FinanceQueryState>
          <form
            onSubmit={(event) => {
              event.preventDefault();
              void saveCoverage();
            }}
          >
            <h3>Propose applicability</h3>
            <fieldset disabled={action.pending}>
              <TextInput
                label="Effective from"
                type="date"
                required
                value={start}
                onChange={(event) => setStart(event.target.value)}
              />
              <TextInput
                label="Effective end (exclusive)"
                type="date"
                value={end}
                onChange={(event) => setEnd(event.target.value)}
              />
              {schemes.map((item) => (
                <label key={item.value}>
                  <input
                    type="checkbox"
                    checked={selectedSchemes.includes(item.value)}
                    onChange={(event) =>
                      setSchemes((values) =>
                        event.target.checked
                          ? [...values, item.value]
                          : values.filter((value) => value !== item.value),
                      )
                    }
                  />{' '}
                  {item.label}
                </label>
              ))}
              <label>
                <input
                  type="checkbox"
                  required
                  checked={explicitCoverage}
                  onChange={(event) => setExplicitCoverage(event.target.checked)}
                />{' '}
                I have explicitly checked applicability, including any no-scheme decision.
              </label>
              <TextInput
                label="Evidence reference"
                required
                value={evidence}
                onChange={(event) => setEvidence(event.target.value)}
              />
              <TextInput
                label="Evidence SHA256"
                required
                pattern="[a-fA-F0-9]{64}"
                value={sha}
                onChange={(event) => setSha(event.target.value)}
              />
              <Button type="submit" disabled={!reason.trim() || !explicitCoverage}>
                Submit applicability for approval
              </Button>
            </fieldset>
          </form>
        </>
      )}
      <ActionMessage action={action} />
    </section>
  );
}
