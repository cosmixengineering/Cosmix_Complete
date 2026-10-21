import { renderHook, act } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import type { ReactNode } from 'react'
import { useUrlDialog } from './useUrlDialog'

const createWrapper = (initialEntries?: string[]) => {
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <MemoryRouter initialEntries={initialEntries}>{children}</MemoryRouter>
  )
  return Wrapper
}

describe('useUrlDialog', () => {
  it('isOpen is false by default', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product' }),
      { wrapper: createWrapper() },
    )
    expect(result.current.isOpen).toBe(false)
  })

  it('isOpen is true when URL param matches', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product' }),
      { wrapper: createWrapper(['/?dialog=create-product']) },
    )
    expect(result.current.isOpen).toBe(true)
  })

  it('isOpen is false when URL param does not match', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product' }),
      { wrapper: createWrapper(['/?dialog=create-category']) },
    )
    expect(result.current.isOpen).toBe(false)
  })

  it('open() sets URL param and isOpen becomes true', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product' }),
      { wrapper: createWrapper() },
    )
    act(() => {
      result.current.open()
    })
    expect(result.current.isOpen).toBe(true)
  })

  it('close() removes URL param and isOpen becomes false', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product' }),
      { wrapper: createWrapper(['/?dialog=create-product']) },
    )
    act(() => {
      result.current.close()
    })
    expect(result.current.isOpen).toBe(false)
  })

  it('onOpenChange(true) opens the dialog', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product' }),
      { wrapper: createWrapper() },
    )
    act(() => {
      result.current.onOpenChange(true)
    })
    expect(result.current.isOpen).toBe(true)
  })

  it('onOpenChange(false) closes the dialog', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product' }),
      { wrapper: createWrapper(['/?dialog=create-product']) },
    )
    act(() => {
      result.current.onOpenChange(false)
    })
    expect(result.current.isOpen).toBe(false)
  })

  it('supports custom paramName', () => {
    const { result } = renderHook(
      () => useUrlDialog({ paramValue: 'create-product', paramName: 'modal' }),
      { wrapper: createWrapper(['/?modal=create-product']) },
    )
    expect(result.current.isOpen).toBe(true)
  })
})
