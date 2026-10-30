import { useState } from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import { Banknote, Send, X } from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { useHrAction } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { writeResult } from '../../schemas/contracts';
import type { CompensationState } from '../../types/requests';
import { paymentMethods, wageBases } from '../../types/hiring';
import { EnumField, Field, LongField, MoneyField } from '../hiring/FormFields';
import { ActionMessage } from '../Feedback';

export function SalaryRequestModal({
  open,
  onClose,
  current,
  employeeName,
  onSaved,
}: {
  open: boolean;
  onClose: () => void;
  current: CompensationState;
  employeeName?: string;
  onSaved: () => void;
}) {
  return (
    <DialogPrimitive.Root
      open={open}
      onOpenChange={(v) => {
        if (!v) onClose();
      }}
    >
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="org-modal-overlay" />
        <DialogPrimitive.Content
          className="org-modal-content org-modal-wide change-form-modal"
          aria-describedby={undefined}
        >
          <div className="org-modal-header">
            <div className="org-modal-header-left">
              <div className="org-modal-icon-badge">
                <Banknote size={19} />
              </div>
              <div className="org-modal-titles">
                <DialogPrimitive.Title className="org-modal-title">
                  Propose Salary Revision
                </DialogPrimitive.Title>
                <p className="org-modal-desc">
                  Submit a salary adjustment proposal for {employeeName || 'this employee'}
                </p>
              </div>
            </div>
            <button
              type="button"
              className="org-modal-close-btn"
              aria-label="Close dialog"
              onClick={onClose}
            >
              <X size={18} />
            </button>
          </div>

          <SalaryRequestForm
            current={current}
            onSaved={() => {
              onSaved();
              onClose();
            }}
            onCancel={onClose}
          />
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}

export function SalaryRequestForm({
  current,
  onSaved,
  onCancel,
}: {
  current: CompensationState;
  onSaved: () => void;
  onCancel?: () => void;
}) {
  const [data, setData] = useState({
    baseAmount: current.effective?.terms.baseAmount ?? null,
    grossAmount: current.effective?.terms.grossAmount ?? null,
    currency: current.effective?.terms.currency || 'PKR',
    wageBasis: current.effective?.terms.wageBasis ?? 1,
    paymentMethod: current.effective?.terms.paymentMethod ?? 1,
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
              currency: data.currency.trim() || 'PKR',
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
      <div className="org-modal-body">
        <fieldset
          disabled={action.pending || sent}
          style={{ border: 'none', padding: 0, margin: 0 }}
        >
          <div
            style={{
              display: 'grid',
              gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))',
              gap: '10px',
              marginBottom: '10px',
            }}
          >
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
          </div>

          <div>
            <LongField
              label="Salary change reason"
              required
              max={1000}
              value={data.reason}
              onChange={(v) => setData({ ...data, reason: v ?? '' })}
            />
          </div>
        </fieldset>

        {error && (
          <p className="hr-message hr-message--error" role="alert" style={{ marginTop: '10px' }}>
            {error}
          </p>
        )}
        <ActionMessage
          action={{
            error: action.error,
            success: sent ? 'Salary request submitted. Current compensation has not changed.' : null,
          }}
        />
      </div>

      <div className="org-modal-footer">
        {onCancel && (
          <button
            type="button"
            className="org-btn-cancel"
            disabled={action.pending}
            onClick={onCancel}
          >
            Cancel
          </button>
        )}
        <button
          type="submit"
          className="org-btn-submit"
          disabled={!data.reason?.trim() || action.pending}
        >
          <Send size={15} />
          Submit salary request
        </button>
      </div>
    </form>
  );
}
