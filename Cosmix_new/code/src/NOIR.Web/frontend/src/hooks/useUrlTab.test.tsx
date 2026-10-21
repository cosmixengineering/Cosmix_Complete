import { renderHook, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useUrlTab } from './useUrlTab'

const createWrapper = (initialEntries?: string[]) => {
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={initialEntries}>{children}</MemoryRouter>
  )
  return Wrapper
}

describe('useUrlTab', () => {
  it('returns default tab when no URL param is set', () => {
    const { result } = renderHook(
      () => useUrlTab({ defaultTab: 'overview' }),
      { wrapper: createWrapper() },
    )
    expect(result.current.activeTab).toBe('overview')
  })

  it('reads tab from URL param', () => {
    const { result } = renderHook(
      () => useUrlTab({ defaultTab: 'overview' }),
      { wrapper: createWrapper(['/?tab=settings']) },
    )
    expect(result.current.activeTab).toBe('settings')
  })

  it('updates activeTab on handleTabChange', () => {
    const { result } = renderHook(
      () => useUrlTab({ defaultTab: 'overview' }),
      { wrapper: createWrapper() },
    )
    act(() => {
      result.current.handleTabChange('settings')
    })
    expect(result.current.activeTab).toBe('settings')
  })

  it('removes URL param when switching to default tab', () => {
    const { result } = renderHook(
      () => useUrlTab({ defaultTab: 'overview' }),
      { wrapper: createWrapper(['/?tab=settings']) },
    )
    act(() => {
      result.current.handleTabChange('overview')
    })
    expect(result.current.activeTab).toBe('overview')
  })

  it('supports custom paramName', () => {
    const { result } = renderHook(
      () => useUrlTab({ defaultTab: 'list', paramName: 'view' }),
      { wrapper: createWrapper(['/?view=grid']) },
    )
    expect(result.current.activeTab).toBe('grid')
  })

  it('provides isPending boolean', () => {
    const { result } = renderHook(
      () => useUrlTab({ defaultTab: 'overview' }),
      { wrapper: createWrapper() },
    )
    expect(typeof result.current.isPending).toBe('boolean')
  })
})
