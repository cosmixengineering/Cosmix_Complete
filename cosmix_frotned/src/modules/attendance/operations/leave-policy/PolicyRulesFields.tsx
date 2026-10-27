import { TextInput } from '@/components/forms/TextInput';
import type { PolicyRules } from './contracts';
import { rounding, salaryDivisor } from './contracts';
import { Choice } from './Ui';
import { human } from './inputs';
import { OvertimePolicyFields } from './OvertimePolicyFields';

type Group = Exclude<keyof PolicyRules, 'schemaVersion' | 'overtime'>;
type Field = { key: string; options?: readonly string[]; boolean?: boolean; integer?: boolean };
const groups: { group: Group; title: string; fields: Field[] }[] = [
  {
    group: 'arrival',
    title: 'Arrival thresholds (seconds after shift start)',
    fields: [
      { key: 'lateAfterShiftStartSeconds', integer: true },
      { key: 'lateThresholdComparison', options: ['strictly_after', 'at_or_after'] },
      { key: 'halfDayAfterShiftStartSeconds', integer: true },
      { key: 'hourlyAfterShiftStartSeconds', integer: true },
    ],
  },
  {
    group: 'paidTime',
    title: 'Paid time',
    fields: [
      { key: 'basis', options: ['actual_elapsed_duty', 'scheduled_window_overlap'] },
      { key: 'breakTreatment', options: ['count_all_breaks', 'deduct_scheduled_unpaid_break'] },
      { key: 'earlyExitTreatment', options: ['actual_worked_time', 'half_day_credit'] },
      { key: 'halfDayCreditSeconds', integer: true },
      {
        key: 'offDayTreatment',
        options: ['actual_worked_time', 'no_credit', 'scheduled_day_credit'],
      },
      {
        key: 'holidayTreatment',
        options: ['actual_worked_time', 'no_credit', 'scheduled_day_credit'],
      },
      { key: 'partialLeaveHalfDayTreatment', options: ['half_day_total_includes_paid_leave'] },
    ],
  },
  {
    group: 'lateDeduction',
    title: 'Late occurrence treatment',
    fields: [
      { key: 'enabled', boolean: true },
      { key: 'occurrenceThreshold', integer: true },
      {
        key: 'resetPeriod',
        options: ['calendar_month', 'calendar_year', 'fiscal_july_june', 'weekly'],
      },
      { key: 'countHalfDayArrival', boolean: true },
      { key: 'countHourlyArrival', boolean: true },
      { key: 'salaryBasis', options: ['base_salary', 'gross_salary'] },
      { key: 'salaryDivisor', options: salaryDivisor },
      { key: 'fixedSalaryDivisorDays', integer: true },
      { key: 'deductionKind', options: ['day_fraction', 'fixed_amount', 'hours'] },
      { key: 'deductionDaysPerThreshold' },
      { key: 'fixedAmountPkr' },
      { key: 'deductionHoursPerThreshold' },
      {
        key: 'weekStartsOn',
        options: ['sunday', 'monday', 'tuesday', 'wednesday', 'thursday', 'friday', 'saturday'],
      },
      { key: 'rounding', options: rounding.filter((v) => v.startsWith('two_decimal')) },
      { key: 'policyChangeCountingStrategy', options: ['restart_on_late_rule_change'] },
      { key: 'salaryEffectiveDateBasis', options: ['threshold_completion_date'] },
      { key: 'overlapTreatment', options: ['larger_of_ordinary_or_late'] },
    ],
  },
  {
    group: 'ordinaryPay',
    title: 'Ordinary attendance interpretation',
    fields: [
      { key: 'salaryBasis', options: ['base_salary'] },
      { key: 'salaryDivisor', options: salaryDivisor },
      { key: 'fixedSalaryDivisorDays', integer: true },
      { key: 'fullUnpaidDayDeductionDays' },
      { key: 'unpaidHalfDayDeductionDays' },
      { key: 'hourlyShortfallCalculation', options: ['daily_rate_over_scheduled_hours'] },
      { key: 'paidLeaveTreatment', options: ['no_deduction'] },
      { key: 'weeklyOffTreatment', options: ['no_deduction'] },
      { key: 'applicablePaidHolidayTreatment', options: ['no_deduction'] },
      { key: 'rounding', options: rounding.filter((v) => v.startsWith('two_decimal')) },
    ],
  },
];
export function PolicyRulesFields({
  rules,
  onChange,
}: {
  rules: PolicyRules;
  onChange: (rules: PolicyRules) => void;
}) {
  function update(group: Group, key: string, value: string | number | boolean | null) {
    const next = { ...rules, [group]: { ...rules[group], [key]: value } };
    if (group === 'lateDeduction') {
      if (key === 'deductionKind') {
        next.lateDeduction.deductionDaysPerThreshold = null;
        next.lateDeduction.fixedAmountPkr = null;
        next.lateDeduction.deductionHoursPerThreshold = null;
      }
      if (key === 'resetPeriod') next.lateDeduction.weekStartsOn = null;
      if (key === 'salaryDivisor') next.lateDeduction.fixedSalaryDivisorDays = null;
    }
    if (group === 'ordinaryPay' && key === 'salaryDivisor')
      next.ordinaryPay.fixedSalaryDivisorDays = null;
    onChange(next);
  }
  function applicable(group: Group, key: string) {
    const late = rules.lateDeduction;
    if (key === 'fixedSalaryDivisorDays')
      return (
        (group === 'lateDeduction' ? late.salaryDivisor : rules.ordinaryPay.salaryDivisor) ===
        'fixed_days'
      );
    if (group !== 'lateDeduction') return true;
    if (key === 'deductionDaysPerThreshold') return late.deductionKind === 'day_fraction';
    if (key === 'fixedAmountPkr') return late.deductionKind === 'fixed_amount';
    if (key === 'deductionHoursPerThreshold') return late.deductionKind === 'hours';
    if (key === 'weekStartsOn') return late.resetPeriod === 'weekly';
    return true;
  }
  return (
    <>
      {groups.map(({ group, title, fields }) => (
        <section className="attendance-section" key={group}>
          <h3>{title}</h3>
          <div className="attendance-grid">
            {fields
              .filter((f) => applicable(group, f.key))
              .map((field) => {
                const value = (rules[group] as Record<string, unknown>)[field.key];
                if (field.boolean)
                  return (
                    <Choice
                      key={field.key}
                      label={human(field.key)}
                      value={value == null ? '' : String(value)}
                      options={[
                        { value: 'true', label: 'Yes' },
                        { value: 'false', label: 'No' },
                      ]}
                      onChange={(v) => update(group, field.key, v === '' ? null : v === 'true')}
                    />
                  );
                if (field.options)
                  return (
                    <Choice
                      key={field.key}
                      label={human(field.key)}
                      value={value == null ? '' : String(value)}
                      options={field.options}
                      onChange={(v) => update(group, field.key, v || null)}
                    />
                  );
                return (
                  <TextInput
                    key={field.key}
                    label={human(field.key)}
                    type="number"
                    step={field.integer ? 1 : 'any'}
                    value={value === null ? '' : String(value)}
                    required
                    onChange={(e) =>
                      update(
                        group,
                        field.key,
                        e.target.value === '' ? null : Number(e.target.value),
                      )
                    }
                  />
                );
              })}
          </div>
        </section>
      ))}
      <OvertimePolicyFields
        value={rules.overtime}
        onChange={(overtime) => {
          const next = { ...rules };
          if (overtime === undefined) delete next.overtime;
          else next.overtime = overtime;
          onChange(next);
        }}
      />
    </>
  );
}
