import type { CompensationTerms } from '../../types/requests';
import { paymentMethods, wageBases } from '../../types/hiring';
export function CompensationView({ terms }: { terms: CompensationTerms }) {
  const amount = (n: number) =>
    n.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) +
    ' ' +
    terms.currency;
  return (
    <dl className="hr-dl">
      <div>
        <dt>Base amount</dt>
        <dd>{amount(terms.baseAmount)}</dd>
      </div>
      <div>
        <dt>Gross amount</dt>
        <dd>{amount(terms.grossAmount)}</dd>
      </div>
      <div>
        <dt>Wage basis</dt>
        <dd>{wageBases[terms.wageBasis - 1]}</dd>
      </div>
      <div>
        <dt>Payment method</dt>
        <dd>{paymentMethods[terms.paymentMethod - 1]}</dd>
      </div>
    </dl>
  );
}
