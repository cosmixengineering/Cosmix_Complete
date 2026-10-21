import type { Meta, StoryObj } from 'storybook'
import { CodeBlock } from './CodeBlock'

const meta = {
  title: 'UIKit/CodeBlock',
  component: CodeBlock,
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div style={{ maxWidth: 800, padding: 16 }}>
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof CodeBlock>

export default meta
type Story = StoryObj<typeof meta>

export const TypeScript: Story = {
  args: {
    language: 'typescript',
    code: `interface User {
  id: string
  name: string
  email: string
  roles: Role[]
}

const fetchUser = async (id: string): Promise<User> => {
  const response = await fetch(\`/api/users/\${id}\`)
  if (!response.ok) {
    throw new Error(\`Failed to fetch user: \${response.statusText}\`)
  }
  return response.json()
}

export const useUser = (id: string) => {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    fetchUser(id)
      .then(setUser)
      .finally(() => setLoading(false))
  }, [id])

  return { user, loading }
}`,
  },
}

export const CSS: Story = {
  args: {
    language: 'css',
    code: `.code-block-wrapper {
  position: relative;
  border-radius: 0.5rem;
  overflow: hidden;
}

.code-block-wrapper pre {
  margin: 0;
  padding: 1rem;
  overflow-x: auto;
}

.code-block-line-numbers .line::before {
  content: counter(line);
  counter-increment: line;
  display: inline-block;
  width: 2rem;
  margin-right: 1rem;
  text-align: right;
  color: rgba(115, 138, 148, 0.4);
  user-select: none;
}`,
  },
}

export const JSON: Story = {
  args: {
    language: 'json',
    code: `{
  "name": "noir-frontend",
  "version": "0.0.0",
  "private": true,
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "react": "^19.0.0",
    "shiki": "^3.23.0"
  }
}`,
  },
}

export const WithLineNumbers: Story = {
  args: {
    language: 'typescript',
    showLineNumbers: true,
    code: `import { useState, useEffect } from 'react'

export const Counter = () => {
  const [count, setCount] = useState(0)

  useEffect(() => {
    document.title = \`Count: \${count}\`
  }, [count])

  return (
    <button onClick={() => setCount(c => c + 1)}>
      Clicked {count} times
    </button>
  )
}`,
  },
}

export const NoCopyButton: Story = {
  args: {
    language: 'bash',
    showCopyButton: false,
    code: `# Install dependencies
pnpm install

# Start development server
pnpm run dev

# Build for production
pnpm run build`,
  },
}

export const PlainText: Story = {
  args: {
    code: `This is plain text without any syntax highlighting.
It can span multiple lines and will be rendered
in a monospace font inside a code block.

No language was specified, so it defaults to "text".`,
  },
}

export const CSharp: Story = {
  args: {
    language: 'csharp',
    code: `public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, OrderDto>
{
    private readonly IRepository<Order> _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateOrderCommandHandler(
        IRepository<Order> repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<OrderDto> Handle(
        CreateOrderCommand command,
        CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.Items);
        await _repository.AddAsync(order, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return order.ToDto();
    }
}`,
  },
}

export const HTML: Story = {
  args: {
    language: 'html',
    code: `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>NOIR Platform</title>
</head>
<body>
  <div id="root"></div>
  <script type="module" src="/src/main.tsx"></script>
</body>
</html>`,
  },
}

export const LongLines: Story = {
  args: {
    language: 'typescript',
    code: `// This example demonstrates horizontal scrolling for very long lines of code
const veryLongVariableName = "This is a very long string that will cause the code block to scroll horizontally because it exceeds the typical width of a code container in most viewport sizes"

export const calculateSomethingComplicated = (firstParameter: string, secondParameter: number, thirdParameter: boolean, fourthParameter: Record<string, unknown>): Promise<{ result: string; metadata: Record<string, unknown> }> => {
  return Promise.resolve({ result: firstParameter, metadata: fourthParameter })
}`,
  },
}
