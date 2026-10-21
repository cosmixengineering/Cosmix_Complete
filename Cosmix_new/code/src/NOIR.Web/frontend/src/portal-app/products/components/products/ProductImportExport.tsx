/**
 * ProductImportExport — Product-specific import/export using shared ImportExportDropdown.
 *
 * Handles domain-specific CSV parsing (variants, images, dynamic attributes)
 * while delegating UI to the shared component.
 */
import { useCallback } from 'react'
import { useTranslation } from 'react-i18next'
import { ImportExportDropdown, type ImportResult } from '@uikit'
import { bulkImportProducts, exportProducts, exportProductsFile, type ImportProductDto } from '@/services/products'
import { escapeCSV, parseCSV, downloadCsv } from '@/lib/csv'

interface ProductImportExportProps {
  totalCount?: number
  onImportComplete?: () => void
}

// Base CSV export fields
const BASE_EXPORT_FIELDS = [
  'name', 'slug', 'sku', 'barcode', 'basePrice', 'currency', 'status',
  'categoryName', 'brand', 'shortDescription', 'variantName', 'variantPrice',
  'compareAtPrice', 'stock', 'images',
]

export const ProductImportExport = ({
  totalCount,
  onImportComplete,
}: ProductImportExportProps) => {
  const { t } = useTranslation('common')

  const handleExportCsv = useCallback(async () => {
    const result = await exportProducts({
      includeAttributes: true,
      includeImages: true,
    })

    const allHeaders = [...BASE_EXPORT_FIELDS]
    result.attributeColumns.forEach(attrCode => {
      allHeaders.push(`attr_${attrCode}`)
    })

    const dataRows = result.rows.map((row) => {
      const values = [
        escapeCSV(row.name), escapeCSV(row.slug), escapeCSV(row.sku || ''),
        escapeCSV(row.barcode || ''), String(row.basePrice), escapeCSV(row.currency),
        escapeCSV(row.status), escapeCSV(row.categoryName || ''), escapeCSV(row.brand || ''),
        escapeCSV(row.shortDescription || ''), escapeCSV(row.variantName || ''),
        row.variantPrice != null ? String(row.variantPrice) : '',
        row.compareAtPrice != null ? String(row.compareAtPrice) : '',
        String(row.stock), escapeCSV(row.images || ''),
      ]
      result.attributeColumns.forEach(attrCode => {
        values.push(escapeCSV(row.attributes[attrCode] || ''))
      })
      return values.join(',')
    })

    const csvContent = [allHeaders.join(','), ...dataRows].join('\n')
    downloadCsv(csvContent, `products-export-${new Date().toISOString().split('T')[0]}.csv`)
  }, [])

  const handleExportExcel = useCallback(async () => {
    await exportProductsFile({ format: 'Excel', includeAttributes: true, includeImages: true })
  }, [])

  const handleImport = useCallback(async (file: File): Promise<ImportResult> => {
    const text = await file.text()
    const { headers, rows } = parseCSV(text)

    if (rows.length === 0) {
      throw new Error(t('importExport.invalidFile'))
    }

    const lowerHeaders = headers.map(h => h.toLowerCase())
    const requiredFields = ['name', 'baseprice']
    const missingFields = requiredFields.filter(f => !lowerHeaders.includes(f))
    if (missingFields.length > 0) {
      throw new Error(t('importExport.missingFields', { fields: missingFields.join(', ') }))
    }

    // Find attribute columns (start with attr_)
    const attrColumnIndexes: { index: number; code: string }[] = []
    headers.forEach((header, index) => {
      if (header.toLowerCase().startsWith('attr_')) {
        attrColumnIndexes.push({ index, code: header.substring(5).toLowerCase() })
      }
    })

    const columnMap = new Map<string, number>()
    lowerHeaders.forEach((h, i) => columnMap.set(h, i))

    const products: ImportProductDto[] = rows.map(row => {
      const getValue = (field: string): string | undefined => {
        const index = columnMap.get(field.toLowerCase())
        return index !== undefined ? row[index]?.trim() || undefined : undefined
      }
      const getNumberValue = (field: string): number | undefined => {
        const val = getValue(field)
        if (!val) return undefined
        const num = parseFloat(val)
        return isNaN(num) ? undefined : num
      }

      const attributes: Record<string, string> = {}
      attrColumnIndexes.forEach(({ index, code }) => {
        const value = row[index]?.trim()
        if (value) attributes[code] = value
      })

      return {
        name: getValue('name') || '',
        slug: getValue('slug'),
        basePrice: getNumberValue('baseprice') || 0,
        currency: getValue('currency'),
        shortDescription: getValue('shortdescription'),
        sku: getValue('sku'),
        barcode: getValue('barcode'),
        categoryName: getValue('categoryname'),
        brand: getValue('brand'),
        stock: getNumberValue('stock') ? Math.floor(getNumberValue('stock')!) : undefined,
        variantName: getValue('variantname'),
        variantPrice: getNumberValue('variantprice'),
        compareAtPrice: getNumberValue('compareatprice'),
        images: getValue('images'),
        attributes: Object.keys(attributes).length > 0 ? attributes : undefined,
      }
    })

    return await bulkImportProducts(products)
  }, [t])

  const handleDownloadTemplate = useCallback(() => {
    const headers = [...BASE_EXPORT_FIELDS, 'attr_color', 'attr_material'].join(',')
    const exampleRows = [
      'Example Product,example-product,SKU-001,,29990000,VND,Draft,Electronics,Apple,Short description,Default,29990000,,100,https://example.com/img1.jpg|https://example.com/img2.jpg,Silver,Aluminum',
      'Example Product,example-product,SKU-002,,29990000,VND,Draft,Electronics,Apple,,128GB Variant,32990000,34990000,50,,Silver,Aluminum',
    ].join('\n')
    downloadCsv([headers, exampleRows].join('\n'), 'products-import-template.csv')
  }, [])

  return (
    <ImportExportDropdown
      onExportCsv={handleExportCsv}
      onExportExcel={handleExportExcel}
      onImport={handleImport}
      onDownloadTemplate={handleDownloadTemplate}
      totalCount={totalCount}
      entityLabel={t('products.title', 'Products')}
      onImportComplete={onImportComplete}
    />
  )
}
