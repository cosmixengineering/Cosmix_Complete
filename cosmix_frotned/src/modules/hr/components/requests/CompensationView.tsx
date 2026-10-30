import { Coins, Banknote, Calendar, CreditCard } from 'lucide-react';
import type { CompensationTerms } from '../../types/requests';
import { paymentMethods, wageBases } from '../../types/hiring';

export function CompensationView({ terms }: { terms: CompensationTerms }) {
  const currency = terms.currency || 'PKR';
  const formatAmount = (n: number) =>
    `${currency} ${n.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

  const wageBasisLabel = wageBases[terms.wageBasis - 1] ?? `Basis #${terms.wageBasis}`;
  const paymentMethodLabel =
    paymentMethods[terms.paymentMethod - 1] ?? `Method #${terms.paymentMethod}`;

  return (
    <div
      className="emp-kv-grid"
      style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(170px, 1fr))',
        gap: '8px',
        margin: '4px 0',
      }}
    >
      <div
        className="emp-kv-item"
        style={{
          background: '#f8fafc',
          padding: '8px 12px',
          borderRadius: '6px',
          border: '1px solid #e2e8f0',
        }}
      >
        <span
          className="emp-kv-label"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '5px',
            fontSize: '10px',
            fontWeight: 600,
            color: '#64748b',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            marginBottom: '3px',
          }}
        >
          <Coins size={12} style={{ color: '#2563eb' }} />
          Base Salary
        </span>
        <span
          className="emp-kv-value"
          style={{
            fontSize: '13.5px',
            fontWeight: 700,
            color: '#0f172a',
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
          }}
        >
          {formatAmount(terms.baseAmount)}
        </span>
      </div>

      <div
        className="emp-kv-item"
        style={{
          background: '#f8fafc',
          padding: '8px 12px',
          borderRadius: '6px',
          border: '1px solid #e2e8f0',
        }}
      >
        <span
          className="emp-kv-label"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '5px',
            fontSize: '10px',
            fontWeight: 600,
            color: '#64748b',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            marginBottom: '3px',
          }}
        >
          <Banknote size={12} style={{ color: '#16a34a' }} />
          Gross Salary
        </span>
        <span
          className="emp-kv-value"
          style={{
            fontSize: '13.5px',
            fontWeight: 700,
            color: '#0f172a',
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, monospace',
          }}
        >
          {formatAmount(terms.grossAmount)}
        </span>
      </div>

      <div
        className="emp-kv-item"
        style={{
          background: '#f8fafc',
          padding: '8px 12px',
          borderRadius: '6px',
          border: '1px solid #e2e8f0',
        }}
      >
        <span
          className="emp-kv-label"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '5px',
            fontSize: '10px',
            fontWeight: 600,
            color: '#64748b',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            marginBottom: '3px',
          }}
        >
          <Calendar size={12} style={{ color: '#0284c7' }} />
          Wage Basis
        </span>
        <span className="emp-kv-value">
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              fontSize: '11px',
              fontWeight: 600,
              padding: '2px 8px',
              borderRadius: '4px',
              background: '#eff6ff',
              color: '#1d4ed8',
              border: '1px solid #dbeafe',
            }}
          >
            {wageBasisLabel}
          </span>
        </span>
      </div>

      <div
        className="emp-kv-item"
        style={{
          background: '#f8fafc',
          padding: '8px 12px',
          borderRadius: '6px',
          border: '1px solid #e2e8f0',
        }}
      >
        <span
          className="emp-kv-label"
          style={{
            display: 'flex',
            alignItems: 'center',
            gap: '5px',
            fontSize: '10px',
            fontWeight: 600,
            color: '#64748b',
            textTransform: 'uppercase',
            letterSpacing: '0.04em',
            marginBottom: '3px',
          }}
        >
          <CreditCard size={12} style={{ color: '#7c3aed' }} />
          Payment Method
        </span>
        <span className="emp-kv-value">
          <span
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              fontSize: '11px',
              fontWeight: 600,
              padding: '2px 8px',
              borderRadius: '4px',
              background: '#f1f5f9',
              color: '#334155',
              border: '1px solid #e2e8f0',
            }}
          >
            {paymentMethodLabel}
          </span>
        </span>
      </div>
    </div>
  );
}
