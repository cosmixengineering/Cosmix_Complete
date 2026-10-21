import { renderHook, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useUrlEditDialog } from './useUrlEditDialog'

interface TestItem {
  id: string
  name: string
}

const mockItems: TestItem[] = [
  { id: 'abc-123', name: 'Item A' },
  { id: 'def-456', name: 'Item B' },
  { id: 'ghi-789', name: 'Item C' },
]

const createWrapper = (initialEntries?: string[]) => {
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={initialEntries}>{children}</MemoryRouter>
  )
  return Wrapper
}

describe('useUrlEditDialog', () => {
  it('editItem is null when no URL param is set', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems),
      { wrapper: createWrapper() },
    )
    expect(result.current.editItem).toBeNull()
  })

  it('resolves editItem from URL param', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems),
      { wrapper: createWrapper(['/?edit=def-456']) },
    )
    expect(result.current.editItem).toEqual({ id: 'def-456', name: 'Item B' })
  })

  it('returns null when URL param ID is not found in items', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems),
      { wrapper: createWrapper(['/?edit=nonexistent']) },
    )
    expect(result.current.editItem).toBeNull()
  })

  it('returns null when items is undefined', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(undefined),
      { wrapper: createWrapper(['/?edit=abc-123']) },
    )
    expect(result.current.editItem).toBeNull()
  })

  it('openEdit sets the URL param and resolves item', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems),
      { wrapper: createWrapper() },
    )
    act(() => {
      result.current.openEdit(mockItems[2])
    })
    expect(result.current.editItem).toEqual({ id: 'ghi-789', name: 'Item C' })
  })

  it('closeEdit removes URL param and editItem becomes null', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems),
      { wrapper: createWrapper(['/?edit=abc-123']) },
    )
    act(() => {
      result.current.closeEdit()
    })
    expect(result.current.editItem).toBeNull()
  })

  it('onEditOpenChange(false) calls closeEdit', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems),
      { wrapper: createWrapper(['/?edit=abc-123']) },
    )
    act(() => {
      result.current.onEditOpenChange(false)
    })
    expect(result.current.editItem).toBeNull()
  })

  it('onEditOpenChange(true) does not change state', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems),
      { wrapper: createWrapper() },
    )
    act(() => {
      result.current.onEditOpenChange(true)
    })
    // onEditOpenChange only handles close (open is done via openEdit)
    expect(result.current.editItem).toBeNull()
  })

  it('supports custom paramName', () => {
    const { result } = renderHook(
      () => useUrlEditDialog<TestItem>(mockItems, 'editing'),
      { wrapper: createWrapper(['/?editing=abc-123']) },
    )
    expect(result.current.editItem).toEqual({ id: 'abc-123', name: 'Item A' })
  })
})
