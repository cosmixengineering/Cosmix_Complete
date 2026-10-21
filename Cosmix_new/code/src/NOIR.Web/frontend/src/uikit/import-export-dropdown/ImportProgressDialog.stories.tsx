import type { Meta, StoryObj } from 'storybook'
import { ImportProgressDialog } from './ImportProgressDialog'

const meta = {
  title: 'UIKit/ImportProgressDialog',
  component: ImportProgressDialog,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
  },
} satisfies Meta<typeof ImportProgressDialog>

export default meta
type Story = StoryObj<typeof meta>

/** Import in progress with 50% completion */
export const InProgress: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    isImporting: true,
    progress: 50,
    result: null,
    entityLabel: 'Products',
  },
}

/** Completed with all successes */
export const AllSuccess: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    isImporting: false,
    progress: 100,
    result: { success: 25, errors: [] },
    entityLabel: 'Customers',
  },
}

/** Completed with some errors */
export const WithErrors: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    isImporting: false,
    progress: 100,
    result: {
      success: 18,
      errors: [
        { row: 2, message: 'Duplicate email: john@example.com' },
        { row: 5, message: 'Invalid phone number format' },
        { row: 8, message: 'Category not found: Unknown' },
        { row: 12, message: 'Price must be a positive number' },
        { row: 15, message: 'Missing required field: lastName' },
      ],
    },
    entityLabel: 'Employees',
  },
}

/** Completed with all errors (zero success) */
export const AllErrors: Story = {
  args: {
    open: true,
    onOpenChange: () => {},
    isImporting: false,
    progress: 100,
    result: {
      success: 0,
      errors: [
        { row: 1, message: 'Missing required field: email' },
        { row: 2, message: 'Missing required field: email' },
        { row: 3, message: 'Invalid data format' },
      ],
    },
  },
}
