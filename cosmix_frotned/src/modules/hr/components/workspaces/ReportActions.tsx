import { useState } from 'react';
import { Download, Printer } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { SelectInput } from '@/components/forms/SelectInput';

export function ReportActions() {
  const [format, setFormat] = useState('xlsx');
  return (
    <div className="hr-report-actions">
      <SelectInput
        label="Export format"
        value={format}
        options={[
          { value: 'xlsx', label: 'XLSX' },
          { value: 'pdf', label: 'PDF' },
          { value: 'csv', label: 'CSV' },
        ]}
        onChange={(event) => setFormat(event.target.value)}
      />
      <span title="An authorized report job and verified download are required">
        <Button variant="secondary" disabled>
          <Download size={16} />
          Export
        </Button>
      </span>
      <span title="A generated, permission-filtered report is required">
        <Button variant="ghost" aria-label="Print report" disabled>
          <Printer size={17} />
        </Button>
      </span>
      <span className="muted">No generated report available</span>
    </div>
  );
}
