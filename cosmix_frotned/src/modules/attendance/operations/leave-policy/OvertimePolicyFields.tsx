import { TextInput } from '@/components/forms/TextInput';
import { rounding, salaryDivisor, type OvertimePay } from './contracts';
import { Choice } from './Ui';

export function OvertimePolicyFields({
  value,
  onChange,
}: {
  value: OvertimePay | null | undefined;
  onChange: (value: OvertimePay | undefined) => void;
}) {
  function update<K extends keyof OvertimePay>(key: K, setting: OvertimePay[K]) {
    if (!value) return;
    const next = { ...value, [key]: setting };
    if (key === 'salaryDivisor') next.fixedSalaryDivisorDays = null;
    onChange(next);
  }
  return (
    <section className="attendance-section">
      <h3>Overtime pay</h3>
      <label className="attendance-check">
        <input
          type="checkbox"
          checked={value != null}
          onChange={(event) =>
            onChange(
              event.target.checked
                ? {
                    standardDutySeconds: null,
                    salaryBasis: null,
                    salaryDivisor: null,
                    fixedSalaryDivisorDays: null,
                    rateMultiplier: null,
                    rounding: null,
                    breakTreatment: null,
                  }
                : undefined,
            )
          }
        />
        Configure overtime
      </label>
      <p>
        New overtime calculations require an explicitly configured published policy. Recorded awards
        retain their original evidence. Eligibility is configured separately.
      </p>
      {value && (
        <div className="attendance-grid">
          <TextInput
            label="Overtime standard duty seconds"
            type="number"
            min={60}
            max={86400}
            step={1}
            required
            value={value.standardDutySeconds ?? ''}
            onChange={(event) =>
              update(
                'standardDutySeconds',
                event.target.value === '' ? null : Number(event.target.value),
              )
            }
          />
          <Choice
            label="Overtime salary basis"
            value={value.salaryBasis ?? ''}
            options={['base_salary']}
            onChange={(setting) =>
              update('salaryBasis', setting === 'base_salary' ? setting : null)
            }
          />
          <Choice
            label="Overtime salary divisor"
            value={value.salaryDivisor ?? ''}
            options={salaryDivisor}
            onChange={(setting) =>
              update(
                'salaryDivisor',
                setting === 'actual_calendar_month_days' || setting === 'fixed_days'
                  ? setting
                  : null,
              )
            }
          />
          {value.salaryDivisor === 'fixed_days' && (
            <TextInput
              label="Overtime fixed salary divisor days"
              type="number"
              min={1}
              max={366}
              step={1}
              required
              value={value.fixedSalaryDivisorDays ?? ''}
              onChange={(event) =>
                update(
                  'fixedSalaryDivisorDays',
                  event.target.value === '' ? null : Number(event.target.value),
                )
              }
            />
          )}
          <TextInput
            label="Overtime rate multiplier (greater than zero)"
            type="number"
            min={0.0001}
            max={100}
            step={0.0001}
            required
            value={value.rateMultiplier ?? ''}
            onChange={(event) =>
              update(
                'rateMultiplier',
                event.target.value === '' ? null : Number(event.target.value),
              )
            }
          />
          <Choice
            label="Overtime rounding"
            value={value.rounding ?? ''}
            options={rounding}
            onChange={(setting) =>
              update('rounding', rounding.find((option) => option === setting) ?? null)
            }
          />
          <Choice
            label="Overtime break treatment"
            value={value.breakTreatment ?? ''}
            options={['count_all_breaks']}
            onChange={(setting) =>
              update('breakTreatment', setting === 'count_all_breaks' ? setting : null)
            }
          />
        </div>
      )}
    </section>
  );
}
