import type { ReactNode } from 'react';
import { Component, LumaSpin } from '@/components/ui/luma-spin';

export { Component, LumaSpin };
export const CosmixLoader = Component;

export interface LoadingStateProps {
  message?: ReactNode;
  size?: number;
  fullscreen?: boolean;
  className?: string;
}

/**
 * Enterprise Loading & Preloader State
 * Uses 21st-dev Luma Spin (@theritikk) with scalable morphing pills.
 */
export function LoadingState({
  message = 'Loading...',
  size,
  fullscreen = false,
  className = '',
}: LoadingStateProps) {
  if (fullscreen) {
    return (
      <div
        className={`cosmix-fullscreen-preloader ${className}`.trim()}
        role="status"
        aria-live="polite"
      >
        <div className="preloader-brand-chip">
          <span className="preloader-dot" />
          <span>COSMIX ERP</span>
        </div>

        <Component size={size || 65} />

        <p className="loading-state-text">{message}</p>

        <div className="preloader-progress-track">
          <div className="preloader-progress-bar" />
        </div>

        <span className="preloader-caption">Enterprise Management Workspace</span>
      </div>
    );
  }

  return (
    <div className={`loading-state ${className}`.trim()} role="status">
      <Component size={size || 46} />
      {message && <p className="loading-state-text">{message}</p>}
    </div>
  );
}

export default LoadingState;
