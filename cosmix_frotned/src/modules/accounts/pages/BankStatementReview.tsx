import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useFinanceCommand } from '../hooks/use-finance-command';
import {
  bankProposalSchema,
  type BankWorkspace,
  type BankResolution,
} from '../schemas/bank-reconciliation';

export function BankStatementReview({
  data,
  manageable,
  onSaved,
}: {
  data: BankWorkspace;
  manageable: boolean;
  onSaved: () => void;
}) {
  const [resolutions, setResolutions] = useState<BankResolution[]>(
    data.proposal?.resolutions ?? [],
  );
  const [reason, setReason] = useState('');
  const [page, setPage] = useState(1);
  const command = useFinanceCommand();
  function change(value: BankResolution) {
    setResolutions((old) =>
      [...old.filter((x) => x.row !== value.row), value].sort((a, b) => a.row - b.row),
    );
  }
  return (
    <section aria-label="Statement matching proposal">
      <h4>Review statement transactions</h4>
      <p>
        Select the posted entries represented by each bank transaction. Bulk transfers may match
        several entries with the same total. Any different reference needs an explanation. Dates may
        differ by at most seven days.
      </p>
      <fieldset disabled={!manageable || command.pending || command.uncertain}>
        {data.rows.slice((page - 1) * 20, page * 20).map((row) => (
          <MatchRow
            key={row.row}
            row={row}
            data={data}
            value={
              resolutions.find((x) => x.row === row.row) ?? {
                row: row.row,
                journalIds: [],
                outsidePayrollReason: null,
                referenceDifferenceReason: null,
              }
            }
            onChange={change}
          />
        ))}
        <TextInput
          label="Proposal review reason"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          maxLength={1000}
        />
      </fieldset>
      <Button disabled={page === 1} onClick={() => setPage((p) => p - 1)}>
        Previous transactions
      </Button>
      <span>
        {' '}
        Page {page} of {Math.ceil(data.rows.length / 20)}{' '}
      </span>
      <Button disabled={page * 20 >= data.rows.length} onClick={() => setPage((p) => p + 1)}>
        Next transactions
      </Button>
      {manageable && (
        <Button
          disabled={!reason.trim() || command.pending}
          onClick={async () => {
            const result = await command.run(
              `/finance/bank-reconciliation/${data.statement.id}/proposal`,
              {
                expectedPreviousProposalId: data.proposal?.id ?? null,
                expectedLedgerFingerprint: data.ledgerFingerprint,
                resolutions: resolutions.filter(
                  (x) => x.journalIds.length || x.outsidePayrollReason?.trim(),
                ),
                reason,
              },
              bankProposalSchema,
            );
            if (result) onSaved();
          }}
        >
          {command.uncertain ? 'Retry saved proposal' : 'Save matching proposal'}
        </Button>
      )}
      {command.error && <p role="alert">{command.error}</p>}
    </section>
  );
}

function MatchRow({
  row,
  data,
  value,
  onChange,
}: {
  row: BankWorkspace['rows'][number];
  data: BankWorkspace;
  value: BankResolution;
  onChange: (value: BankResolution) => void;
}) {
  const [search, setSearch] = useState('');
  const outside = value.outsidePayrollReason !== null;
  const choices = data.ledger.filter(
    (x) =>
      value.journalIds.includes(x.journalId) ||
      (Math.sign(x.amount) === Math.sign(row.amount) &&
        x.reference.toLowerCase().includes(search.toLowerCase())),
  );
  const visible = [
    ...choices.filter((x) => value.journalIds.includes(x.journalId)),
    ...choices.filter((x) => !value.journalIds.includes(x.journalId)).slice(0, 100),
  ];
  return (
    <div className="hr-form-section">
      <p>
        <strong>
          Row {row.row}: {row.date} · {row.reference} · {row.amount.toFixed(2)}{' '}
          {data.statement.currency}
        </strong>
      </p>
      <label>
        <input
          type="checkbox"
          checked={outside}
          onChange={(e) =>
            onChange({
              ...value,
              journalIds: [],
              outsidePayrollReason: e.target.checked ? '' : null,
              referenceDifferenceReason: null,
            })
          }
        />{' '}
        Outside payroll
      </label>
      {outside ? (
        <TextInput
          label="Non-payroll classification reason"
          value={value.outsidePayrollReason ?? ''}
          onChange={(e) => onChange({ ...value, outsidePayrollReason: e.target.value })}
          maxLength={1000}
        />
      ) : (
        <>
          <TextInput
            label="Find posted payment reference"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <label>
            Matching payroll entries (Ctrl/Command to select several)
            <select
              multiple
              value={value.journalIds}
              onChange={(e) =>
                onChange({
                  ...value,
                  journalIds: Array.from(e.target.selectedOptions, (x) => x.value),
                })
              }
            >
              {visible.map((x) => (
                <option key={x.journalId} value={x.journalId}>
                  {x.date} · {x.reference} · {x.amount.toFixed(2)}
                </option>
              ))}
            </select>
          </label>
          {choices.length > visible.length && (
            <p>Showing the first 100 matches. Narrow the reference search.</p>
          )}
          <TextInput
            label="Different bank reference explanation (if applicable)"
            value={value.referenceDifferenceReason ?? ''}
            onChange={(e) =>
              onChange({ ...value, referenceDifferenceReason: e.target.value || null })
            }
            maxLength={1000}
          />
        </>
      )}
      <Button
        variant="secondary"
        onClick={() =>
          onChange({
            row: row.row,
            journalIds: [],
            outsidePayrollReason: null,
            referenceDifferenceReason: null,
          })
        }
      >
        Leave unmatched
      </Button>
    </div>
  );
}
