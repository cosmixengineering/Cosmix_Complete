import { useState } from 'react';
import { Send } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useHrAction } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { writeResult } from '../../schemas/contracts';
import type { CompensationState } from '../../types/requests';
import { paymentMethods, wageBases } from '../../types/hiring';
import { EnumField, Field, FormSection, LongField, MoneyField } from '../hiring/FormFields';
import { ActionMessage } from '../Feedback';

export function SalaryRequestForm({
  current,
  onSaved,
}: {
  current: CompensationState;
  onSaved: () => void;
}) {
  const [data, setData] = useState({
    baseAmount: current.effective?.terms.baseAmount ?? null,
    grossAmount: current.effective?.terms.grossAmount ?? null,
    currency: current.effective?.terms.currency ?? '',
    wageBasis: current.effective?.terms.wageBasis ?? null,
    paymentMethod: current.effective?.terms.paymentMethod ?? null,
    effectiveFrom: '',
    reason: '',
  });
  const [baseline] = useState(JSON.stringify(data));
  const [sent, setSent] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const action = useHrAction();
  useDirtyForm(!sent && baseline !== JSON.stringify(data));
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        setError(null);
        if (
          data.baseAmount === null ||
          data.grossAmount === null ||
          data.grossAmount < data.baseAmount ||
          !data.reason.trim()
        ) {
          setError('Gross salary must be at least the base salary, and a reason is required.');
          return;
        }
        void action
          .run(
            `employees/${current.employeeId}/compensation/requests`,
            {
              ...data,
              reason: data.reason.trim(),
              expectedVersion: current.employeeVersion,
              expectedCompensationId: current.latestRecordId,
            },
            'POST',
            writeResult,
          )
          .then((r) => {
            if (r) {
              setSent(true);
              onSaved();
            }
          });
      }}
    >
      <fieldset disabled={action.pending || sent}>
        <FormSection title="Proposed salary">
          <MoneyField
            label="Base amount"
            required
            value={data.baseAmount}
            onChange={(v) => setData({ ...data, baseAmount: v })}
          />
          <MoneyField
            label="Gross amount"
            required
            value={data.grossAmount}
            onChange={(v) => setData({ ...data, grossAmount: v })}
          />
          <Field
            label="Currency"
            required
            max={3}
            pattern="[A-Z]{3}"
            value={data.currency}
            onChange={(v) => setData({ ...data, currency: v?.toUpperCase() ?? '' })}
          />
          <EnumField
            label="Wage basis"
            required
            options={wageBases}
            value={data.wageBasis}
            onChange={(v) => setData({ ...data, wageBasis: v })}
          />
          <EnumField
            label="Payment method"
            required
            options={paymentMethods}
            value={data.paymentMethod}
            onChange={(v) => setData({ ...data, paymentMethod: v })}
          />
          <TextInput
            label="Effective from"
            type="date"
            required
            min={current.asOf}
            value={data.effectiveFrom}
            onChange={(e) => setData({ ...data, effectiveFrom: e.target.value })}
          />
          <LongField
            label="Salary change reason"
            required
            max={1000}
            value={data.reason}
            onChange={(v) => setData({ ...data, reason: v ?? '' })}
          />
        </FormSection>
        <div className="hr-actions">
          <Button type="submit">
            <Send size={16} />
            Submit salary request
          </Button>
        </div>
      </fieldset>
      {error && (
        <p className="hr-message hr-message--error" role="alert">
          {error}
        </p>
      )}
      <ActionMessage
        action={{
          error: action.error,
          success: sent ? 'Salary request submitted. Current compensation has not changed.' : null,
        }}
      />
    </form>
  );
}
