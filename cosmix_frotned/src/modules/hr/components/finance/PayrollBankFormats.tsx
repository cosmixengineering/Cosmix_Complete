import { useState } from 'react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceQuery } from '../../hooks/use-finance-queries';
import { bankFormat, bankTemplate, type BankFormat } from '../../schemas/payroll-bank';
import { prepareBankFormat, bankReviewText } from '../../utils/payroll-bank-form';
import { FinanceNotice, TextAreaField } from './FinanceChrome';
import { FinanceQueryState } from './FinanceQueryState';
import { PayrollSelect } from './PayrollFields';
import { ReviewedFinanceCommand } from './ReviewedFinanceCommand';

export function PayrollBankFormats({
  currency,
  formats,
  onChanged,
}: {
  currency: string;
  formats: BankFormat[];
  onChanged(): void;
}) {
  const access = useFinanceAccess();
  const templates = useFinanceQuery(
    '/payroll-accounting/bank/templates',
    z.array(bankTemplate),
    access.can('payroll.accounting.bank.export'),
  );
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [version, setVersion] = useState('1');
  const [proof, setProof] = useState('');
  const [reason, setReason] = useState('');
  const [codes, setCodes] = useState([{ bankName: '', bankCode: '' }]);
  const [names, setNames] = useState('');
  const template = templates.data?.find((item) => item.code === code);
  return (
    <details>
      <summary>Bank format configuration & independent review</summary>
      <FinanceNotice>
        The supplied HBL workbook structures start unapproved. Enter the bank-provided routing codes
        or reviewed bank-name list; a second authorized reviewer must approve the exact profile
        before export.
      </FinanceNotice>
      {access.can('payroll.accounting.configure') && (
        <FinanceQueryState query={templates}>
          <ReviewedFinanceCommand
            label="Create bank format"
            schema={bankFormat}
            prepare={() => {
              if (!template) throw Error();
              return {
                path: '/payroll-accounting/bank/formats',
                body: prepareBankFormat(
                  template,
                  name,
                  Number(version),
                  currency,
                  template.code === 'HBL_IBFT' ? codes : [],
                  template.code === 'HBL_INTERNAL' ? names.split('\n') : [],
                  proof,
                  reason,
                ),
                summary: `${name} · version ${version} · ${currency} · ${template.name}. The displayed rules and evidence will be retained for independent review.`,
              };
            }}
            onSaved={() => {
              setProof('');
              setReason('');
              onChanged();
            }}
          >
            {() => (
              <>
                <PayrollSelect
                  label="Supplied bank template"
                  value={code}
                  onChange={(value) => {
                    setCode(value);
                    setName(templates.data?.find((item) => item.code === value)?.name ?? '');
                    setCodes([{ bankName: '', bankCode: '' }]);
                    setNames('');
                  }}
                  options={(templates.data ?? []).map((item) => ({
                    value: item.code,
                    label: item.name,
                  }))}
                />
                <TextInput
                  label="Format profile name"
                  value={name}
                  onChange={(event) => setName(event.target.value)}
                  required
                />
                <TextInput
                  label="Format version"
                  type="number"
                  min="1"
                  step="1"
                  value={version}
                  onChange={(event) => setVersion(event.target.value)}
                  required
                />
                <p>Currency: {currency}</p>
                {template && (
                  <>
                    <p>
                      Workbook: {template.rules.worksheetName} · {template.rules.outputFormat}
                    </p>
                    <ol>
                      {template.rules.columns.map((column, index) => (
                        <li key={index}>
                          {column.header} · {column.field}
                          {column.literal !== null ? ` · ${column.literal}` : ''}
                        </li>
                      ))}
                    </ol>
                    <p>
                      Supplied template SHA-256: <code>{template.sourceTemplateSha256}</code>
                    </p>
                  </>
                )}
                {template?.code === 'HBL_IBFT' && (
                  <>
                    <h4>Exact bank routing codes</h4>
                    {codes.map((row, index) => (
                      <div key={index}>
                        <TextInput
                          label={`Bank name ${index + 1}`}
                          value={row.bankName}
                          onChange={(event) =>
                            setCodes((current) =>
                              current.map((item, i) =>
                                i === index ? { ...item, bankName: event.target.value } : item,
                              ),
                            )
                          }
                          required
                        />
                        <TextInput
                          label={`Bank code ${index + 1}`}
                          value={row.bankCode}
                          onChange={(event) =>
                            setCodes((current) =>
                              current.map((item, i) =>
                                i === index ? { ...item, bankCode: event.target.value } : item,
                              ),
                            )
                          }
                          required
                        />
                        {codes.length > 1 && (
                          <Button
                            type="button"
                            variant="secondary"
                            onClick={() =>
                              setCodes((current) => current.filter((_, i) => i !== index))
                            }
                          >
                            Remove bank {index + 1}
                          </Button>
                        )}
                      </div>
                    ))}
                    <Button
                      type="button"
                      variant="secondary"
                      onClick={() =>
                        setCodes((current) => [...current, { bankName: '', bankCode: '' }])
                      }
                    >
                      Add bank mapping
                    </Button>
                  </>
                )}
                {template?.code === 'HBL_INTERNAL' && (
                  <TextAreaField
                    label="Reviewed beneficiary bank names · one exact name per line"
                    value={names}
                    onChange={setNames}
                    required
                  />
                )}
                <TextAreaField
                  label="Supplied template and bank mapping evidence · maximum 8000 characters"
                  value={proof}
                  onChange={setProof}
                  required
                />
                <TextAreaField
                  label="Configuration reason · maximum 500 characters"
                  value={reason}
                  onChange={setReason}
                  required
                />
              </>
            )}
          </ReviewedFinanceCommand>
        </FinanceQueryState>
      )}
      {formats.map((format) => (
        <article className="payroll-batch-card" key={format.id}>
          <h4>
            {format.name} · version {format.formatVersion} · {format.currency}
          </h4>
          <p>
            {format.state} · {format.rules.outputFormat}
          </p>
          <details>
            <summary>Review exact format rules</summary>
            <p>
              Worksheet: {format.rules.worksheetName} · Date format: {format.rules.dateFormat}
            </p>
            <p>
              Header row: {format.rules.includeHeader ? 'Included' : 'Omitted'} · UTF-8 BOM:{' '}
              {format.rules.utf8Bom ? 'Included' : 'Omitted'} · Delimiter:{' '}
              <code>{JSON.stringify(format.rules.delimiter)}</code>
            </p>
            <ol>
              {format.rules.columns.map((column, index) => (
                <li key={index}>
                  {column.header} · {column.field}
                  {column.literal !== null ? ` · ${column.literal}` : ''}
                  {column.required ? ' · required' : ' · optional'}
                </li>
              ))}
            </ol>
            <ul>
              {format.rules.bankCodes?.map((row) => (
                <li key={row.bankName}>
                  {row.bankName}: {row.bankCode}
                </li>
              ))}
              {format.rules.beneficiaryBankNames?.map((bankName) => (
                <li key={bankName}>{bankName}</li>
              ))}
            </ul>
            <p>
              Template SHA-256: <code>{format.rules.templateSha256}</code>
            </p>
          </details>
          {format.state === 'Draft' &&
            access.can('payroll.accounting.configure.approve') &&
            Boolean(access.user?.account?.accountId) &&
            access.user?.account?.accountId.toLowerCase() !==
              format.createdByUserId.toLowerCase() && (
              <ApproveBankFormat format={format} onChanged={onChanged} />
            )}
        </article>
      ))}
    </details>
  );
}
function ApproveBankFormat({ format, onChanged }: { format: BankFormat; onChanged(): void }) {
  const [proof, setProof] = useState('');
  const [reason, setReason] = useState('');
  return (
    <ReviewedFinanceCommand
      label="Approve bank format"
      schema={bankFormat}
      prepare={() => ({
        path: `/payroll-accounting/bank/formats/${format.id}/approve`,
        body: { ...bankReviewText(reason, proof), expectedVersion: format.version },
        summary: `Approve ${format.name}, format version ${format.formatVersion}, currency ${format.currency}, current revision ${format.version}. This approves only the format; it does not send a bank payment.`,
      })}
      onSaved={() => {
        setProof('');
        setReason('');
        onChanged();
      }}
    >
      {() => (
        <>
          <TextAreaField
            label="Independent review evidence · maximum 8000 characters"
            value={proof}
            onChange={setProof}
            required
          />
          <TextAreaField
            label="Approval reason · maximum 500 characters"
            value={reason}
            onChange={setReason}
            required
          />
        </>
      )}
    </ReviewedFinanceCommand>
  );
}
