import type { Meta, StoryObj } from 'storybook'
import { MermaidBlock } from './MermaidBlock'

const meta = {
  title: 'UIKit/MermaidBlock',
  component: MermaidBlock,
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div style={{ maxWidth: 800, padding: 16 }}>
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof MermaidBlock>

export default meta
type Story = StoryObj<typeof meta>

export const Flowchart: Story = {
  args: {
    code: `graph TD
    A[Start] --> B{Is it working?}
    B -->|Yes| C[Great!]
    B -->|No| D[Debug]
    D --> B`,
  },
}

export const SequenceDiagram: Story = {
  args: {
    code: `sequenceDiagram
    participant Browser
    participant Server
    participant Database
    Browser->>Server: GET /api/users
    Server->>Database: SELECT * FROM users
    Database-->>Server: Results
    Server-->>Browser: JSON Response`,
  },
}

export const ClassDiagram: Story = {
  args: {
    code: `classDiagram
    class Animal {
      +String name
      +int age
      +makeSound() void
    }
    class Dog {
      +String breed
      +fetch() void
    }
    class Cat {
      +String color
      +purr() void
    }
    Animal <|-- Dog
    Animal <|-- Cat`,
  },
}

export const StateDiagram: Story = {
  args: {
    code: `stateDiagram-v2
    [*] --> Draft
    Draft --> Active: Publish
    Active --> Archived: Archive
    Active --> Draft: Unpublish
    Archived --> Active: Restore
    Archived --> [*]: Delete`,
  },
}

export const ErrorState: Story = {
  args: {
    code: `this is not valid mermaid syntax }{][`,
  },
}

export const EmptyCode: Story = {
  args: {
    code: '',
  },
}
