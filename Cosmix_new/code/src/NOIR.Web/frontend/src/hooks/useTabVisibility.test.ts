import { renderHook, act } from '@testing-library/react'
import { useTabVisibility } from './useTabVisibility'

describe('useTabVisibility', () => {
  const originalVisibilityState = document.visibilityState

  afterEach(() => {
    Object.defineProperty(document, 'visibilityState', {
      value: originalVisibilityState,
      writable: true,
      configurable: true,
    })
  })

  it('starts with isVisible true when document is visible', () => {
    Object.defineProperty(document, 'visibilityState', {
      value: 'visible',
      writable: true,
      configurable: true,
    })
    const { result } = renderHook(() => useTabVisibility())
    expect(result.current.isVisible).toBe(true)
    expect(result.current.wasHiddenFor).toBeNull()
  })

  it('sets isVisible to false when tab becomes hidden', () => {
    const { result } = renderHook(() => useTabVisibility())

    act(() => {
      Object.defineProperty(document, 'visibilityState', {
        value: 'hidden',
        writable: true,
        configurable: true,
      })
      document.dispatchEvent(new Event('visibilitychange'))
    })

    expect(result.current.isVisible).toBe(false)
  })

  it('sets isVisible back to true when tab becomes visible again', () => {
    const { result } = renderHook(() => useTabVisibility())

    act(() => {
      Object.defineProperty(document, 'visibilityState', {
        value: 'hidden',
        writable: true,
        configurable: true,
      })
      document.dispatchEvent(new Event('visibilitychange'))
    })

    expect(result.current.isVisible).toBe(false)

    act(() => {
      Object.defineProperty(document, 'visibilityState', {
        value: 'visible',
        writable: true,
        configurable: true,
      })
      document.dispatchEvent(new Event('visibilitychange'))
    })

    expect(result.current.isVisible).toBe(true)
  })

  it('wasHiddenFor is null while tab is hidden', () => {
    const { result } = renderHook(() => useTabVisibility())

    act(() => {
      Object.defineProperty(document, 'visibilityState', {
        value: 'hidden',
        writable: true,
        configurable: true,
      })
      document.dispatchEvent(new Event('visibilitychange'))
    })

    expect(result.current.wasHiddenFor).toBeNull()
  })

  it('wasHiddenFor is a number after returning from hidden state', () => {
    const { result } = renderHook(() => useTabVisibility())

    act(() => {
      Object.defineProperty(document, 'visibilityState', {
        value: 'hidden',
        writable: true,
        configurable: true,
      })
      document.dispatchEvent(new Event('visibilitychange'))
    })

    act(() => {
      Object.defineProperty(document, 'visibilityState', {
        value: 'visible',
        writable: true,
        configurable: true,
      })
      document.dispatchEvent(new Event('visibilitychange'))
    })

    expect(result.current.wasHiddenFor).toEqual(expect.any(Number))
    expect(result.current.wasHiddenFor).toBeGreaterThanOrEqual(0)
  })

  it('cleans up event listener on unmount', () => {
    const removeSpy = vi.spyOn(document, 'removeEventListener')
    const { unmount } = renderHook(() => useTabVisibility())

    unmount()

    expect(removeSpy).toHaveBeenCalledWith('visibilitychange', expect.any(Function))
    removeSpy.mockRestore()
  })
})
