import { useState } from 'react'
import type { Meta, StoryObj } from 'storybook'
import { VirtualList } from './VirtualList'

interface ListItem {
  id: number
  label: string
  description: string
  hasDetails?: boolean
}

const generateItems = (count: number): ListItem[] =>
  Array.from({ length: count }, (_, i) => ({
    id: i + 1,
    label: `Item ${i + 1}`,
    description: `This is the description for item number ${i + 1}.`,
  }))

const meta = {
  title: 'UIKit/VirtualList',
  component: VirtualList,
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div style={{ maxWidth: 500, padding: 16 }}>
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof VirtualList>

export default meta
type Story = StoryObj<typeof meta>

export const Default: Story = {
  args: {
    items: generateItems(1000),
    estimateSize: 60,
    height: 400,
    getItemKey: (item: ListItem) => item.id,
    renderItem: (item: ListItem) => (
      <div
        style={{
          padding: '12px 16px',
          borderBottom: '1px solid #e5e7eb',
        }}
      >
        <div style={{ fontWeight: 500 }}>{item.label}</div>
        <div style={{ fontSize: 13, color: '#6b7280' }}>{item.description}</div>
      </div>
    ),
  },
}

export const SmallList: Story = {
  args: {
    items: generateItems(20),
    estimateSize: 48,
    height: 300,
    getItemKey: (item: ListItem) => item.id,
    renderItem: (item: ListItem) => (
      <div
        style={{
          padding: '10px 16px',
          borderBottom: '1px solid #e5e7eb',
        }}
      >
        {item.label}
      </div>
    ),
  },
}

export const LargeList: Story = {
  args: {
    items: generateItems(10000),
    estimateSize: 40,
    height: 500,
    getItemKey: (item: ListItem) => item.id,
    renderItem: (item: ListItem, index: number) => (
      <div
        style={{
          padding: '8px 16px',
          borderBottom: '1px solid #e5e7eb',
          backgroundColor: index % 2 === 0 ? '#f9fafb' : 'white',
        }}
      >
        <span style={{ fontWeight: 500 }}>{item.label}</span>
        <span style={{ color: '#9ca3af', marginLeft: 8, fontSize: 12 }}>#{item.id}</span>
      </div>
    ),
  },
}

export const WithOverscan: Story = {
  args: {
    items: generateItems(500),
    estimateSize: 48,
    height: 300,
    overscan: 20,
    getItemKey: (item: ListItem) => item.id,
    renderItem: (item: ListItem) => (
      <div
        style={{
          padding: '10px 16px',
          borderBottom: '1px solid #e5e7eb',
        }}
      >
        {item.label} - {item.description}
      </div>
    ),
  },
}

export const CardStyle: Story = {
  args: {
    items: generateItems(200),
    estimateSize: 80,
    height: 400,
    getItemKey: (item: ListItem) => item.id,
    renderItem: (item: ListItem) => (
      <div
        style={{
          margin: '4px 0',
          padding: 16,
          borderRadius: 8,
          border: '1px solid #e5e7eb',
          backgroundColor: 'white',
        }}
      >
        <div style={{ fontWeight: 600, marginBottom: 4 }}>{item.label}</div>
        <div style={{ fontSize: 13, color: '#6b7280' }}>{item.description}</div>
      </div>
    ),
  },
}

const generateDynamicItems = (count: number): ListItem[] =>
  Array.from({ length: count }, (_, i) => ({
    id: i + 1,
    label: `Log Entry ${i + 1}`,
    description: i % 3 === 0
      ? `Short message for entry ${i + 1}.`
      : `Detailed log message for entry ${i + 1} with additional context: ${
          'Lorem ipsum dolor sit amet, consectetur adipiscing elit. '.repeat(i % 5 + 1)
        }`,
    hasDetails: i % 4 === 0,
  }))

const ExpandableItem = ({ item }: { item: ListItem }) => {
  const [expanded, setExpanded] = useState(false)
  return (
    <div
      style={{
        padding: '8px 16px',
        borderBottom: '1px solid #e5e7eb',
        cursor: item.hasDetails ? 'pointer' : 'default',
      }}
      onClick={() => item.hasDetails && setExpanded(!expanded)}
    >
      <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
        {item.hasDetails && (
          <span style={{ fontSize: 12, color: '#6b7280' }}>{expanded ? '\u25BC' : '\u25B6'}</span>
        )}
        <span style={{ fontWeight: 500, fontSize: 13 }}>{item.label}</span>
        <span style={{ fontSize: 11, color: '#9ca3af', marginLeft: 'auto' }}>#{item.id}</span>
      </div>
      {expanded && item.hasDetails && (
        <div
          style={{
            marginTop: 8,
            padding: 12,
            backgroundColor: '#fef2f2',
            borderRadius: 6,
            fontSize: 12,
            color: '#991b1b',
            whiteSpace: 'pre-wrap',
          }}
        >
          {item.description}
        </div>
      )}
    </div>
  )
}

export const DynamicHeights: Story = {
  args: {
    items: generateDynamicItems(500),
    estimateSize: 40,
    height: 400,
    getItemKey: (item: ListItem) => item.id,
    renderItem: (item: ListItem) => <ExpandableItem item={item} />,
  },
}
