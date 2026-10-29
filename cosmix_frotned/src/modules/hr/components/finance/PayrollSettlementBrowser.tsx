import { useState } from 'react';
import { Button } from '@/components/ui/Button';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import { payrollSettlementBatchPage } from '../../schemas/payroll-settlement';
import { FinanceQueryState } from './FinanceQueryState';
import { FinanceNotice, HasMorePager } from './FinanceChrome';
import { PayrollSelect } from './PayrollFields';
import { PayrollSettlementReports } from './PayrollSettlementReports';

export function PayrollSettlementBrowser() {
  const access = useFinanceAccess();
  const allowed = access.can('payroll.reports.read');
  const [page, setPage] = useState(1);
  const [selected, setSelected] = useState('');
  const batches = useFinanceQuery(
    `/hr/payroll-settlement?page=${page}&pageSize=25`,
    payrollSettlementBatchPage,
    allowed,
  );
  if (!allowed) return <FinanceNotice>Payroll report read access is required.</FinanceNotice>;
  const items = !batches.isError ? (batches.data?.items ?? []) : [];
  const batch = items.find((item) => item.batchId === selected);
  function turnPage(next: number) {
    setSelected('');
    setPage(next);
  }
  return (
    <>
      <section className="payroll-section-card">
        <h2>Payroll reports</h2>
        <p>Select a frozen salary run to review its register and payment reconciliation.</p>
        <FinanceQueryState query={batches}>
          <PayrollSelect
            label="Frozen salary run"
            value={batch?.batchId ?? ''}
            onChange={setSelected}
            options={items.map((item) => ({
              value: item.batchId,
              label: `${item.batchReference} · ${item.periodStart} · ${item.currency}`,
            }))}
          />
          {!items.length && <FinanceNotice>No frozen salary runs on this page.</FinanceNotice>}
          <HasMorePager
            page={page}
            hasMore={batches.data?.hasMore ?? false}
            busy={batches.isFetching}
            onPrevious={() => turnPage(page - 1)}
            onNext={() => turnPage(page + 1)}
          />
        </FinanceQueryState>
        <Button
          variant="secondary"
          disabled={batches.isFetching}
          onClick={() => void batches.refetch()}
        >
          Refresh salary runs
        </Button>
      </section>
      {batch && (
        <PayrollSettlementReports
          key={batch.batchId}
          batchId={batch.batchId}
          allowed={allowed}
          exportAllowed={access.can('payroll.reports.export')}
        />
      )}
    </>
  );
}
