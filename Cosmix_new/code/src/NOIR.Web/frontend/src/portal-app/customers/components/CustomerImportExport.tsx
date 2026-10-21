/**
 * CustomerImportExport — Customer-specific import/export using shared ImportExportDropdown.
 */
import { useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { ImportExportDropdown, type ImportResult } from '@uikit'
import { exportCustomers, bulkImportCustomers, type ImportCustomerDto } from '@/services/customers'
import { parseCSV, downloadCsv } from '@/lib/csv'

export interface CustomerImportExportProps {
  totalCount?: number
  onImportComplete?: () => void
}

const REQUIRED_HEADERS = ['email', 'firstname', 'lastname']
const TEMPLATE_HEADERS = 'email,firstName,lastName,phone,tags'
const TEMPLATE_ROWS = [
  'john@example.com,John,Doe,+84123456789,"vip,newsletter"',
  'jane@example.com,Jane,Smith,,returning',
].join('\n')

export const CustomerImportExport = ({
  totalCount,
  onImportComplete,
}: CustomerImportExportProps) => {
  const { t } = useTranslation('common')

  const handleExportCsv = useCallback(async () => {
    await exportCustomers({ format: 'CSV' })
  }, [])

  const handleExportExcel = useCallback(async () => {
    await exportCustomers({ format: 'Excel' })
  }, [])

  const handleImport = useCallback(async (file: File): Promise<ImportResult> => {
    const text = await file.text()
    const { headers, rows } = parseCSV(text)

    if (rows.length === 0) {
      throw new Error(t('importExport.invalidFile'))
    }

    const lowerHeaders = headers.map(h => h.toLowerCase())
    const missingFields = REQUIRED_HEADERS.filter(f => !lowerHeaders.includes(f))
    if (missingFields.length > 0) {
      throw new Error(t('importExport.missingFields', { fields: missingFields.join(', ') }))
    }

    const columnMap = new Map<string, number>()
    lowerHeaders.forEach((h, i) => columnMap.set(h, i))

    const customers: ImportCustomerDto[] = rows.map(row => {
      const getValue = (field: string): string | undefined => {
        const index = columnMap.get(field.toLowerCase())
        return index !== undefined ? row[index]?.trim() || undefined : undefined
      }
      return {
        email: getValue('email') || '',
        firstName: getValue('firstname') || '',
        lastName: getValue('lastname') || '',
        phone: getValue('phone'),
        tags: getValue('tags'),
      }
    })

    return await bulkImportCustomers(customers)
  }, [t])

  const handleDownloadTemplate = useCallback(() => {
    downloadCsv([TEMPLATE_HEADERS, TEMPLATE_ROWS].join('\n'), 'customers-import-template.csv')
  }, [])

  return (
    <ImportExportDropdown
      onExportCsv={handleExportCsv}
      onExportExcel={handleExportExcel}
      onImport={handleImport}
      onDownloadTemplate={handleDownloadTemplate}
      totalCount={totalCount}
      entityLabel={t('customers.title', 'Customers')}
      onImportComplete={onImportComplete}
    />
  )
}
