import { render, type RenderOptions } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { MemoryRouter, type MemoryRouterProps } from 'react-router-dom'
import { type ReactNode } from 'react'

interface CustomRenderOptions extends Omit<RenderOptions, 'wrapper'> {
  routerProps?: MemoryRouterProps
  withRouter?: boolean
}

const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        gcTime: 0,
      },
    },
  })

export const renderWithProviders = (
  ui: ReactNode,
  { routerProps, withRouter = true, ...options }: CustomRenderOptions = {}
) => {
  const queryClient = createTestQueryClient()

  const Wrapper = ({ children }: { children: ReactNode }) => {
    const content = (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    )
    if (withRouter) {
      return <MemoryRouter {...routerProps}>{content}</MemoryRouter>
    }
    return content
  }

  return {
    ...render(ui, { wrapper: Wrapper, ...options }),
    queryClient,
  }
}

// Re-export everything from testing-library
export * from '@testing-library/react'
export { renderWithProviders as render }
