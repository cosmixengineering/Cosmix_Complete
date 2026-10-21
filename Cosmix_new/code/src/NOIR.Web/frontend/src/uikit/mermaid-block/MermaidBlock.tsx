/**
 * MermaidBlock Component
 *
 * Lazily loads mermaid and renders diagrams with theme awareness.
 * Shows loading state during initialization, error fallback with raw code.
 */

import { useState, useRef, useCallback, useEffect, useId } from 'react'
import { cn } from '@/lib/utils'
import { Skeleton } from '../skeleton/Skeleton'

export interface MermaidBlockProps {
  /** Mermaid diagram code to render */
  code: string
  /** Additional CSS class names */
  className?: string
}

// Module-level singleton state for mermaid
let mermaidInstance: typeof import('mermaid').default | null = null
let mermaidLoadPromise: Promise<typeof import('mermaid').default> | null = null
let currentMermaidTheme: string | null = null

const loadMermaid = async (theme: 'dark' | 'default'): Promise<typeof import('mermaid').default> => {
  if (mermaidInstance && currentMermaidTheme === theme) {
    return mermaidInstance
  }

  if (mermaidInstance && currentMermaidTheme !== theme) {
    // Re-initialize with new theme
    mermaidInstance.initialize({ startOnLoad: false, theme })
    currentMermaidTheme = theme
    return mermaidInstance
  }

  if (mermaidLoadPromise && currentMermaidTheme === theme) {
    return mermaidLoadPromise
  }

  mermaidLoadPromise = import('mermaid').then((mod) => {
    const instance = mod.default
    instance.initialize({ startOnLoad: false, theme })
    mermaidInstance = instance
    currentMermaidTheme = theme
    return instance
  })

  return mermaidLoadPromise
}

const detectTheme = (): 'dark' | 'default' => {
  return document.documentElement.classList.contains('dark') ? 'dark' : 'default'
}

export const MermaidBlock = ({ code, className }: MermaidBlockProps) => {
  const [svg, setSvg] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const containerRef = useRef<HTMLDivElement>(null)
  const renderIdRef = useRef(0)
  const uniqueId = useId()
  // Create a stable counter for render IDs to use as mermaid element IDs
  const renderCountRef = useRef(0)

  const renderDiagram = useCallback(async (diagramCode: string) => {
    renderIdRef.current += 1
    const currentRenderId = renderIdRef.current

    setLoading(true)
    setError(null)

    try {
      const theme = detectTheme()
      const mermaid = await loadMermaid(theme)

      // Check if this render is still current
      if (renderIdRef.current !== currentRenderId) return

      renderCountRef.current += 1
      // Mermaid requires a valid DOM id (no colons from useId)
      const elementId = `mermaid-${uniqueId.replace(/:/g, '')}-${renderCountRef.current}`

      const { svg: renderedSvg } = await mermaid.render(elementId, diagramCode)

      // Check again after async render
      if (renderIdRef.current !== currentRenderId) return

      setSvg(renderedSvg)
      setLoading(false)
    } catch (err) {
      // Only apply error if this render is still current
      if (renderIdRef.current !== currentRenderId) return

      const message = err instanceof Error ? err.message : 'Failed to render diagram'
      setError(message)
      setSvg(null)
      setLoading(false)
    }
  }, [uniqueId])

  // Render on code change
  useEffect(() => {
    if (!code.trim()) {
      setLoading(false)
      setError('No diagram code provided')
      return
    }

    renderDiagram(code)
  }, [code, renderDiagram])

  // Watch for theme changes via MutationObserver on <html> class
  useEffect(() => {
    const observer = new MutationObserver((mutations) => {
      for (const mutation of mutations) {
        if (mutation.type === 'attributes' && mutation.attributeName === 'class') {
          // Theme class changed, re-render
          if (code.trim()) {
            // Reset mermaid theme tracking to force re-init
            currentMermaidTheme = null
            renderDiagram(code)
          }
          break
        }
      }
    })

    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ['class'],
    })

    return () => observer.disconnect()
  }, [code, renderDiagram])

  if (loading) {
    return (
      <div className={cn('rounded-lg border bg-muted/30 p-6', className)}>
        <div className="space-y-3">
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-4 w-1/2" />
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-4 w-2/3" />
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className={cn('rounded-lg border border-destructive/30 bg-destructive/5 p-4', className)}>
        <p className="text-sm font-medium text-destructive mb-2">
          Diagram render error
        </p>
        <pre className="text-xs text-muted-foreground bg-muted/50 rounded p-3 overflow-auto whitespace-pre-wrap font-mono">
          {code}
        </pre>
      </div>
    )
  }

  return (
    <div
      ref={containerRef}
      className={cn(
        'rounded-lg border bg-background p-4 overflow-auto [&_svg]:max-w-full [&_svg]:h-auto',
        className
      )}
      dangerouslySetInnerHTML={svg ? { __html: svg } : undefined}
    />
  )
}
