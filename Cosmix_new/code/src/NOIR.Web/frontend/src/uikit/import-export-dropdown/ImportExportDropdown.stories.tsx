import type { Meta, StoryObj } from 'storybook'
import { ImportExportDropdown } from './ImportExportDropdown'

const sleep = (ms: number) => new Promise((r) => setTimeout(r, ms))

const meta = {
  title: 'UIKit/ImportExportDropdown',
  component: ImportExportDropdown,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof ImportExportDropdown>

export default meta
type Story = StoryObj<typeof meta>

/** Full import/export dropdown with all 4 options — Products/Customers pattern */
export const FullImportExport: Story = {
  args: {
    onExportCsv: async () => {
      await sleep(1000)
    },
    onExportExcel: async () => {
      await sleep(1000)
    },
    onImport: async () => {
      await sleep(1500)
      return { success: 42, errors: [{ row: 3, message: 'Invalid email format' }, { row: 7, message: 'Missing required field: name' }] }
    },
    onDownloadTemplate: () => {},
    totalCount: 156,
    entityLabel: 'Products',
  },
}

/** Export-only dropdown — Orders/Reports pattern */
export const ExportOnly: Story = {
  args: {
    onExportCsv: async () => {
      await sleep(1000)
    },
    onExportExcel: async () => {
      await sleep(1000)
    },
  },
}

/** Export-only with item count badge */
export const ExportOnlyWithCount: Story = {
  args: {
    onExportCsv: async () => {
      await sleep(1000)
    },
    onExportExcel: async () => {
      await sleep(1000)
    },
    totalCount: 89,
  },
}

/** Import succeeds with no errors */
export const ImportSuccess: Story = {
  args: {
    onExportExcel: async () => {
      await sleep(1000)
    },
    onImport: async () => {
      await sleep(1500)
      return { success: 25, errors: [] }
    },
    onDownloadTemplate: () => {},
    entityLabel: 'Customers',
  },
}

/** Import with partial errors */
export const ImportWithErrors: Story = {
  args: {
    onExportCsv: async () => {
      await sleep(1000)
    },
    onExportExcel: async () => {
      await sleep(1000)
    },
    onImport: async () => {
      await sleep(1500)
      return {
        success: 18,
        errors: [
          { row: 2, message: 'Duplicate email: john@example.com' },
          { row: 5, message: 'Invalid phone number format' },
          { row: 8, message: 'Category not found: Unknown' },
          { row: 12, message: 'Price must be a positive number' },
          { row: 15, message: 'Missing required field: lastName' },
        ],
      }
    },
    onDownloadTemplate: () => {},
    totalCount: 42,
    entityLabel: 'Employees',
  },
}

/** Disabled state */
export const Disabled: Story = {
  args: {
    onExportCsv: async () => {},
    onExportExcel: async () => {},
    disabled: true,
  },
}
