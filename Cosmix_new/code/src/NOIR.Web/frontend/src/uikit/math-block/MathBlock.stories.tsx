import type { Meta, StoryObj } from 'storybook'
import { MathBlock } from './MathBlock'

const meta = {
  title: 'UIKit/MathBlock',
  component: MathBlock,
  tags: ['autodocs'],
  decorators: [
    (Story) => (
      <div style={{ maxWidth: 600, padding: 16 }}>
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof MathBlock>

export default meta
type Story = StoryObj<typeof meta>

export const InlineFormula: Story = {
  args: {
    formula: 'E = mc^2',
    displayMode: false,
  },
}

export const DisplayMode: Story = {
  args: {
    formula: '\\int_{-\\infty}^{\\infty} e^{-x^2} dx = \\sqrt{\\pi}',
    displayMode: true,
  },
}

export const Matrix: Story = {
  args: {
    formula: '\\begin{pmatrix} a & b \\\\ c & d \\end{pmatrix}',
    displayMode: true,
  },
}

export const SumSeries: Story = {
  args: {
    formula: '\\sum_{n=1}^{\\infty} \\frac{1}{n^2} = \\frac{\\pi^2}{6}',
    displayMode: true,
  },
}

export const QuadraticFormula: Story = {
  args: {
    formula: 'x = \\frac{-b \\pm \\sqrt{b^2 - 4ac}}{2a}',
    displayMode: true,
  },
}

export const ErrorState: Story = {
  args: {
    formula: '\\invalid{command}}}}{{{',
    displayMode: false,
  },
}

export const InlineInText: Story = {
  render: () => (
    <p className="text-base leading-relaxed">
      The famous equation <MathBlock formula="E = mc^2" /> shows the equivalence of mass and
      energy, where <MathBlock formula="c" /> is the speed of light in vacuum.
    </p>
  ),
}

export const MultipleDisplayFormulas: Story = {
  render: () => (
    <div className="space-y-4">
      <MathBlock formula="\\nabla \\times \\mathbf{E} = -\\frac{\\partial \\mathbf{B}}{\\partial t}" displayMode />
      <MathBlock formula="\\nabla \\times \\mathbf{B} = \\mu_0 \\mathbf{J} + \\mu_0 \\epsilon_0 \\frac{\\partial \\mathbf{E}}{\\partial t}" displayMode />
      <MathBlock formula="\\nabla \\cdot \\mathbf{E} = \\frac{\\rho}{\\epsilon_0}" displayMode />
      <MathBlock formula="\\nabla \\cdot \\mathbf{B} = 0" displayMode />
    </div>
  ),
}
