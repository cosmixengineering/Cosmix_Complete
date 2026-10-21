import { useRef, useCallback } from 'react'
import { Link, type LinkProps, useNavigate } from 'react-router-dom'
import { startViewTransition, supportsViewTransitions } from '@/hooks/useViewTransition'
import { prefetchRoute } from '@/lib/routePrefetch'

interface ViewTransitionLinkProps extends Omit<LinkProps, 'prefetch'> {
  /** Direction hint for CSS animations. Defaults to 'forward'. */
  vtDirection?: 'forward' | 'back'
  /** Enable route chunk prefetching on hover. Defaults to true. */
  prefetch?: boolean
}

const PREFETCH_DELAY_MS = 100

/**
 * ViewTransitionLink - Drop-in replacement for React Router's <Link>
 *
 * Wraps navigation in document.startViewTransition() for smooth
 * native browser page transitions. Falls back to regular <Link>
 * behavior in unsupported browsers.
 *
 * Prefetches the target route's chunk on hover/focus (100ms delay
 * to avoid prefetching on quick mouse pass). Disable with prefetch={false}.
 *
 * Supports all standard <Link> props including `onClick`.
 *
 * @example
 * <ViewTransitionLink to="/portal/products">Products</ViewTransitionLink>
 * <ViewTransitionLink to="/portal" vtDirection="back">Back</ViewTransitionLink>
 */
export const ViewTransitionLink = ({
  to,
  onClick,
  vtDirection = 'forward',
  prefetch = true,
  children,
  ...props
}: ViewTransitionLinkProps) => {
  const navigate = useNavigate()
  const prefetchTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null)

  const handlePrefetch = useCallback(() => {
    if (!prefetch) return
    prefetchTimerRef.current = setTimeout(() => {
      const path = typeof to === 'string' ? to : to.pathname ?? ''
      prefetchRoute(path)
    }, PREFETCH_DELAY_MS)
  }, [to, prefetch])

  const cancelPrefetch = useCallback(() => {
    if (prefetchTimerRef.current) {
      clearTimeout(prefetchTimerRef.current)
      prefetchTimerRef.current = null
    }
  }, [])

  const handleClick = (e: React.MouseEvent<HTMLAnchorElement>) => {
    // Allow default browser behavior for modifier keys (new tab, etc.)
    if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey || e.button !== 0) {
      onClick?.(e)
      return
    }

    // Allow parent onClick to run (e.g. closing mobile sidebar)
    onClick?.(e)

    // If parent called preventDefault, respect it
    if (e.defaultPrevented) return

    e.preventDefault()

    if (!supportsViewTransitions) {
      navigate(to)
      return
    }

    startViewTransition(() => {
      navigate(to)
    }, vtDirection)
  }

  return (
    <Link
      to={to}
      onClick={handleClick}
      onMouseEnter={handlePrefetch}
      onMouseLeave={cancelPrefetch}
      onFocus={handlePrefetch}
      onBlur={cancelPrefetch}
      {...props}
    >
      {children}
    </Link>
  )
}
