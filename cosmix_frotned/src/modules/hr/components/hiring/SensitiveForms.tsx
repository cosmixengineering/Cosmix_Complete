import { useState } from 'react';
import { Save, Trash2, Plus } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction, useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import {
  documentsSchema,
  financialSchema,
  medicalSchema,
  sectionResponse,
} from '../../schemas/hiring';
import { writeResult } from '../../schemas/contracts';
import type { Financial, Medical, Profile } from '../../types/hiring';
import { paymentMethods, wageBases } from '../../types/hiring';
import { ActionMessage, Denied, QueryState } from '../Feedback';
import {
  BankFields,
  DocumentChoice,
  EnumField,
  Field,
  FormSection,
  LongField,
  MoneyField,
} from './FormFields';

export function SensitiveSection({
  employeeId,
  section,
  editable,
  profile,
  onSaved,
}: {
  employeeId: string;
  section: 'financial' | 'medical';
  editable: boolean;
  profile: Profile;
  onSaved: () => void;
}) {
  const access = useHrAccess();
  const permission = `hr.employees.${section}.read`;
  const financial = useHrQuery(
    `employees/${employeeId}/financial`,
    sectionResponse(financialSchema),
    ['hr.employees.read', permission],
    section === 'financial',
  );
  const medical = useHrQuery(
    `employees/${employeeId}/medical`,
    sectionResponse(medicalSchema),
    ['hr.employees.read', permission],
    section === 'medical',
  );
  if (!access.can('hr.employees.read', permission)) return <Denied />;
  const canEdit =
    editable && access.can('hr.employees.drafts.manage', `hr.employees.${section}.manage`);
  return section === 'financial' ? (
    <QueryState query={financial}>
      {financial.data && (
        <FinancialForm
          key={financial.data.version}
          employeeId={employeeId}
          initial={financial.data.data}
          version={financial.data.version}
          editable={canEdit}
          profile={profile}
          onSaved={() => {
            void financial.refetch();
            onSaved();
          }}
        />
      )}
    </QueryState>
  ) : (
    <QueryState query={medical}>
      {medical.data && (
        <MedicalForm
          key={medical.data.version}
          employeeId={employeeId}
          initial={medical.data.data}
          version={medical.data.version}
          editable={canEdit}
          onSaved={() => {
            void medical.refetch();
            onSaved();
          }}
        />
      )}
    </QueryState>
  );
}
function FinancialForm({
  employeeId,
  initial,
  version,
  editable,
  profile,
  onSaved,
}: {
  employeeId: string;
  initial: Financial | null;
  version: string;
  editable: boolean;
  profile: Profile;
  onSaved: () => void;
}) {
  const [data, setData] = useState<Financial>(
    initial ?? {
      baseSalary: null,
      grossSalary: null,
      currency: null,
      wageBasis: null,
      paymentMethod: null,
      effectiveFrom: null,
      bank: null,
      previousSalaries: [],
    },
  );
  const [baseline, setBaseline] = useState(JSON.stringify(data));
  const action = useHrAction();
  useDirtyForm(editable && JSON.stringify(data) !== baseline);
  const set = <K extends keyof Financial>(key: K, value: Financial[K]) =>
    setData((p) => ({ ...p, [key]: value }));
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void (async () => {
          const r = await action.run(
            `employees/${employeeId}/financial`,
            { expectedVersion: version, data },
            'PUT',
            writeResult,
          );
          if (r) {
            setBaseline(JSON.stringify(data));
            onSaved();
          }
        })();
      }}
    >
      <fieldset disabled={!editable || action.pending}>
        <FormSection title="Proposed compensation">
          <MoneyField
            label="Base salary"
            value={data.baseSalary}
            onChange={(v) => set('baseSalary', v)}
          />
          <MoneyField
            label="Gross salary"
            value={data.grossSalary}
            onChange={(v) => set('grossSalary', v)}
          />
          <Field
            label="Currency"
            max={3}
            pattern="[A-Z]{3}"
            value={data.currency}
            onChange={(v) => set('currency', v?.toUpperCase() ?? null)}
          />
          <EnumField
            label="Wage basis"
            options={wageBases}
            value={data.wageBasis}
            onChange={(v) => set('wageBasis', v)}
          />
          <EnumField
            label="Payment method"
            options={paymentMethods}
            value={data.paymentMethod}
            onChange={(v) => set('paymentMethod', v)}
          />
          <Field
            label="Compensation effective from"
            type="date"
            value={data.effectiveFrom}
            onChange={(v) => set('effectiveFrom', v)}
          />
        </FormSection>
        <BankFields value={data.bank} onChange={(v) => set('bank', v)} />
        {data.bank && editable && (
          <Button variant="ghost" onClick={() => set('bank', null)}>
            Clear proposed bank details
          </Button>
        )}
        <section className="hr-form-section">
          <h2>Previous salaries</h2>
          {data.previousSalaries.map((row, i) => {
            const update = (patch: Partial<typeof row>) =>
              set(
                'previousSalaries',
                data.previousSalaries.map((r, n) => (n === i ? { ...r, ...patch } : r)),
              );
            const options = profile.previousEmployment.map((p) => ({
              value: p.entryId,
              label: p.employerName ?? 'Unnamed employer',
            }));
            if (row.employmentEntryId && !options.some((o) => o.value === row.employmentEntryId))
              options.push({
                value: row.employmentEntryId,
                label: 'Unavailable previous employment',
              });
            return (
              <div className="hr-repeat" key={i}>
                <div className="hr-repeat-heading">
                  <h3>Salary {i + 1}</h3>
                  <Button
                    variant="ghost"
                    title="Remove previous salary"
                    aria-label={`Remove salary ${i + 1}`}
                    onClick={() =>
                      set(
                        'previousSalaries',
                        data.previousSalaries.filter((_, n) => n !== i),
                      )
                    }
                  >
                    <Trash2 size={16} />
                  </Button>
                </div>
                <div className="hr-form-grid">
                  <SelectInput
                    label="Previous employer"
                    required
                    value={row.employmentEntryId}
                    options={[{ value: '', label: 'Select employer' }, ...options]}
                    onChange={(e) => update({ employmentEntryId: e.target.value })}
                  />
                  <MoneyField
                    label="Previous salary amount"
                    value={row.amount}
                    onChange={(v) => update({ amount: v })}
                  />
                  <Field
                    label="Previous salary currency"
                    max={3}
                    pattern="[A-Z]{3}"
                    value={row.currency}
                    onChange={(v) => update({ currency: v?.toUpperCase() ?? null })}
                  />
                </div>
              </div>
            );
          })}
          {editable && (
            <Button
              variant="secondary"
              disabled={data.previousSalaries.length >= 100 || !profile.previousEmployment.length}
              onClick={() =>
                set('previousSalaries', [
                  ...data.previousSalaries,
                  { employmentEntryId: '', amount: null, currency: null },
                ])
              }
            >
              <Plus size={16} />
              Add previous salary
            </Button>
          )}
        </section>
      </fieldset>
      <ActionMessage action={{ error: action.error, success: null }} />
      {editable && (
        <div className="hr-actions hr-actions--sticky">
          <Button type="submit" variant="success" disabled={action.pending}>
            <Save size={16} />
            Save financial details
          </Button>
        </div>
      )}
    </form>
  );
}
function MedicalForm({
  employeeId,
  initial,
  version,
  editable,
  onSaved,
}: {
  employeeId: string;
  initial: Medical | null;
  version: string;
  editable: boolean;
  onSaved: () => void;
}) {
  const [data, setData] = useState<Medical>(
    initial ?? { details: null, authorizationReference: null, supportingDocumentId: null },
  );
  const [baseline, setBaseline] = useState(JSON.stringify(data));
  const action = useHrAction();
  const documents = useHrQuery(`employees/${employeeId}/documents?medical=true`, documentsSchema, [
    'hr.employees.read',
    'hr.employees.documents.read',
    'hr.employees.medical.read',
  ]);
  useDirtyForm(editable && JSON.stringify(data) !== baseline);
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        void (async () => {
          const r = await action.run(
            `employees/${employeeId}/medical`,
            { expectedVersion: version, data },
            'PUT',
            writeResult,
          );
          if (r) {
            setBaseline(JSON.stringify(data));
            onSaved();
          }
        })();
      }}
    >
      <fieldset disabled={!editable || action.pending}>
        <FormSection title="Private medical information">
          <LongField
            label="Medical details"
            max={10000}
            value={data.details}
            onChange={(v) => setData((p) => ({ ...p, details: v }))}
          />
          <Field
            label="Medical authorization reference"
            max={500}
            value={data.authorizationReference}
            onChange={(v) => setData((p) => ({ ...p, authorizationReference: v }))}
          />
          <DocumentChoice
            label="Private medical evidence"
            documents={documents.data ?? []}
            value={data.supportingDocumentId}
            onChange={(v) => setData((p) => ({ ...p, supportingDocumentId: v }))}
          />
        </FormSection>
      </fieldset>
      <ActionMessage action={{ error: action.error, success: null }} />
      {editable && (
        <div className="hr-actions hr-actions--sticky">
          <Button type="submit" variant="success" disabled={action.pending}>
            <Save size={16} />
            Save medical details
          </Button>
        </div>
      )}
    </form>
  );
}
