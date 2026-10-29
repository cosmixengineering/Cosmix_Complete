#!/usr/bin/env python3
"""Offline exact employee/month comparison; no SQL, provider, transfer or sign-off.

See docs/payroll-acceptance/Readiness-and-Comparison-20261028.md. Inputs must be
independently normalized, approved CSV extracts. Output contains employee financial
data and belongs in restricted storage outside the repository.
"""
import argparse
import csv
import hashlib
import io
import json
import pathlib
import re
import sys
from datetime import datetime, timezone
from decimal import Decimal

KEY_FIELDS = ('employee_id', 'month', 'currency')
PAYROLL_FIELDS = KEY_FIELDS + ('gross', 'deductions', 'net', 'employee_contribution', 'employer_contribution', 'paid', 'unpaid')
ACCOUNTING_FIELDS = KEY_FIELDS + ('salary_expense', 'deduction_payable', 'employer_expense', 'contribution_payable', 'salary_payable', 'cash_paid')
MAX_BYTES = 32 * 1024 * 1024
MAX_ROWS = 100000


class InputError(ValueError):
    """Messages are bounded codes, source roles and line numbers, never raw input."""


def money(value, role, line, field):
    if not isinstance(value, str) or not re.fullmatch(r'(?:0|[1-9][0-9]{0,14})(?:\.[0-9]{1,2})?', value):
        raise InputError(f'{role}: row {line}: invalid_amount:{field}')
    return Decimal(value).quantize(Decimal('0.01'))


def source_bytes(path, role):
    try:
        with pathlib.Path(path).open('rb') as stream:
            data = stream.read(MAX_BYTES + 1)
    except OSError:
        raise InputError(f'{role}: source_unreadable') from None
    if not data or len(data) > MAX_BYTES:
        raise InputError(f'{role}: source_size_invalid')
    return data


def read_rows(path, fields, month, currency, role):
    data = source_bytes(path, role)
    try:
        reader = csv.DictReader(io.StringIO(data.decode('utf-8-sig'), newline=''), strict=True)
        if reader.fieldnames is None or len(reader.fieldnames) != len(fields) or set(reader.fieldnames) != set(fields):
            raise InputError(f'{role}: exact_headers_required')
        rows = {}
        for line, raw in enumerate(reader, 2):
            if line > MAX_ROWS + 1 or None in raw or any(v is None for v in raw.values()):
                raise InputError(f'{role}: row {line}: shape_or_limit_invalid')
            employee = raw['employee_id']
            if not re.fullmatch(r'[A-Za-z0-9][A-Za-z0-9_.-]{0,99}', employee):
                raise InputError(f'{role}: row {line}: invalid_employee_id')
            if raw['month'] != month or raw['currency'] != currency:
                raise InputError(f'{role}: row {line}: period_or_currency_mismatch')
            if employee in rows:
                raise InputError(f'{role}: row {line}: duplicate_employee')
            row = {f: money(raw[f], role, line, f) for f in fields if f not in KEY_FIELDS}
            if fields == PAYROLL_FIELDS:
                if (row['gross'] - row['deductions'] != row['net'] or
                        row['net'] - row['paid'] != row['unpaid'] or
                        row['employee_contribution'] > row['deductions']):
                    raise InputError(f'{role}: row {line}: payroll_equation_invalid')
            rows[employee] = row
        if not rows:
            raise InputError(f'{role}: empty_employee_set')
        return rows, {'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data), 'rows': len(rows)}
    except (UnicodeError, csv.Error):
        raise InputError(f'{role}: invalid_utf8_csv') from None


def amount(value):
    return format(value, '.2f')


def accounting_expected(payroll):
    return dict(salary_expense=payroll['gross'], deduction_payable=payroll['deductions']-payroll['employee_contribution'],
                employer_expense=payroll['employer_contribution'],
                contribution_payable=payroll['employee_contribution']+payroll['employer_contribution'],
                salary_payable=payroll['unpaid'], cash_paid=payroll['paid'])


def compare(reference, system, accounting, month, currency, evidence_kind):
    if not re.fullmatch(r'[0-9]{4}-(?:0[1-9]|1[0-2])', month) or month.startswith('0000'):
        raise InputError('invalid_month')
    if not re.fullmatch(r'[A-Z]{3}', currency) or evidence_kind not in ('Synthetic', 'Real'):
        raise InputError('invalid_currency_or_evidence_kind')
    paths = [pathlib.Path(p).resolve() for p in (reference, system, accounting)]
    if len(set(paths)) != 3 or any(a.exists() and b.exists() and a.samefile(b) for i,a in enumerate(paths) for b in paths[i+1:]):
        raise InputError('independent_input_files_required')
    datasets, sources = {}, {}
    for role, path, fields in zip(('reference', 'system', 'accounting'), paths, (PAYROLL_FIELDS, PAYROLL_FIELDS, ACCOUNTING_FIELDS)):
        datasets[role], sources[role] = read_rows(path, fields, month, currency, role)
    differences = []
    employees = sorted(set().union(*(set(x) for x in datasets.values())))

    def difference(employee, relation, field, expected, actual):
        if expected != actual:
            differences.append(dict(code='amount_mismatch', employee_id=employee, relation=relation, field=field,
                                    expected=amount(expected), actual=amount(actual), difference=amount(actual-expected)))

    for employee in employees:
        present = [role for role, rows in datasets.items() if employee in rows]
        if len(present) != 3:
            differences.append(dict(code='employee_membership_mismatch', employee_id=employee, present_in=present))
        if employee in datasets['reference'] and employee in datasets['system']:
            for field, expected in datasets['reference'][employee].items():
                difference(employee, 'reference_to_system', field, expected, datasets['system'][employee][field])
        for role in ('reference', 'system'):
            if employee in datasets[role] and employee in datasets['accounting']:
                for field, expected in accounting_expected(datasets[role][employee]).items():
                    difference(employee, role+'_to_accounting', field, expected, datasets['accounting'][employee][field])
    totals = {role: {field: amount(sum((r[field] for r in rows.values()), Decimal('0')))
                     for field in next(iter(rows.values()))} for role, rows in datasets.items()}
    return dict(schema_version=1, generated_at_utc=datetime.now(timezone.utc).isoformat(), month=month, currency=currency,
                evidence_kind=evidence_kind, status='DIFFERENCES' if differences else 'MATCH',
                company_acceptance_established=False, employee_count=len(employees), sources=sources,
                totals=totals, differences=differences,
                limitations=['Normalized inputs must be independently reviewed against their original sources.',
                             'Paid means confirmed/disbursed payments net of reversals; unconfirmed posted reservations are excluded at a consistent cutoff.',
                             'Contribution and deduction payables are pre-remittance balances for this month only.',
                             'No bank certification, external transfer, legal-rate approval or company sign-off is established.'])


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('reference', 'system', 'accounting', 'month', 'currency', 'output-directory'):
        parser.add_argument('--'+name, required=True)
    parser.add_argument('--evidence-kind', choices=('Synthetic', 'Real'), required=True)
    parser.add_argument('--provenance', action='append', default=[], help='Original source to hash; never parsed or copied.')
    args = parser.parse_args(argv)
    try:
        output = pathlib.Path(args.output_directory).resolve()
        repo = pathlib.Path(__file__).resolve().parents[2]
        if output == repo or repo in output.parents or output.exists():
            raise InputError('output_requires_new_directory_outside_repository')
        inputs = [pathlib.Path(x).resolve() for x in (args.reference, args.system, args.accounting, *args.provenance)]
        if any(output == p or output in p.parents for p in inputs):
            raise InputError('output_must_not_contain_inputs')
        result = compare(args.reference, args.system, args.accounting, args.month, args.currency, args.evidence_kind)
        result['original_source_hashes'] = []
        for i, path in enumerate(args.provenance):
            data = source_bytes(path, f'provenance_{i+1}')
            result['original_source_hashes'].append(dict(source_number=i+1, sha256=hashlib.sha256(data).hexdigest(), bytes=len(data)))
        result['tool_sha256'] = hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest()
        output.mkdir(parents=True, exist_ok=False)
        report = output / 'comparison.json'
        report.write_text(json.dumps(result, indent=2)+'\n', encoding='utf-8')
        with (output / 'differences.csv').open('w', encoding='utf-8', newline='') as stream:
            fields = ['code','employee_id','relation','field','expected','actual','difference','present_in']
            writer = csv.DictWriter(stream, fieldnames=fields)
            writer.writeheader()
            for row in result['differences']:
                writer.writerow({key: '|'.join(value) if isinstance(value,list) else value for key,value in row.items()})
        print(json.dumps({'status': result['status'], 'employee_count': result['employee_count'],
                          'difference_count': len(result['differences']), 'company_acceptance_established': False}))
        return 1 if result['differences'] else 0
    except InputError as error:
        print(json.dumps({'status':'INVALID_INPUT','code':str(error)}), file=sys.stderr)
        return 2
    except (OSError, ValueError):
        print('{"status":"ERROR","code":"comparison_io_failed"}', file=sys.stderr)
        return 2


if __name__ == '__main__':
    sys.exit(main())
