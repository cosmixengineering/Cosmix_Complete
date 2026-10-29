import type { CSSProperties } from 'react';

export interface LumaSpinProps {
  size?: number;
  className?: string;
  style?: CSSProperties;
  color?: string;
  strokeWidth?: number;
}

/**
 * Luma Spin Loader by @theritikk (21st.dev)
 * Multi-layer morphing pill animation along a 65px coordinate path.
 */
export const Component = ({
  size = 65,
  className = '',
  style,
  color,
  strokeWidth = 3.2,
}: LumaSpinProps) => {
  const scale = size / 65;

  return (
    <div
      className={`luma-spin-wrapper relative aspect-square ${className}`.trim()}
      style={{
        width: size,
        height: size,
        display: 'inline-flex',
        alignItems: 'center',
        justifyContent: 'center',
        position: 'relative',
        ...style,
      }}
      role="presentation"
      aria-hidden="true"
    >
      <div
        className="luma-spin-frame"
        style={{
          width: 65,
          height: 65,
          position: 'relative',
          transform: `scale(${scale})`,
          transformOrigin: 'center center',
          flexShrink: 0,
        }}
      >
        <span
          className="luma-spin-pill luma-pill-1 absolute rounded-[50px] animate-loaderAnim shadow-gray-800 dark:shadow-gray-100"
          style={color ? { boxShadow: `inset 0 0 0 ${strokeWidth}px ${color}` } : undefined}
        />
        <span
          className="luma-spin-pill luma-pill-2 absolute rounded-[50px] animate-loaderAnim animation-delay shadow-gray-800 dark:shadow-gray-100"
          style={color ? { boxShadow: `inset 0 0 0 ${strokeWidth}px ${color}` } : undefined}
        />
      </div>

      <style>{`
        @keyframes loaderAnim {
          0% {
            inset: 0 35px 35px 0;
            top: 0; right: 35px; bottom: 35px; left: 0;
          }
          12.5% {
            inset: 0 35px 0 0;
            top: 0; right: 35px; bottom: 0; left: 0;
          }
          25% {
            inset: 35px 35px 0 0;
            top: 35px; right: 35px; bottom: 0; left: 0;
          }
          37.5% {
            inset: 35px 0 0 0;
            top: 35px; right: 0; bottom: 0; left: 0;
          }
          50% {
            inset: 35px 0 0 35px;
            top: 35px; right: 0; bottom: 0; left: 35px;
          }
          62.5% {
            inset: 0 0 0 35px;
            top: 0; right: 0; bottom: 0; left: 35px;
          }
          75% {
            inset: 0 0 35px 35px;
            top: 0; right: 0; bottom: 35px; left: 35px;
          }
          87.5% {
            inset: 0 0 35px 0;
            top: 0; right: 0; bottom: 35px; left: 0;
          }
          100% {
            inset: 0 35px 35px 0;
            top: 0; right: 35px; bottom: 35px; left: 0;
          }
        }
        .animate-loaderAnim {
          animation: loaderAnim 2.5s cubic-bezier(0.65, 0, 0.35, 1) infinite;
        }
        .animation-delay {
          animation-delay: -1.25s;
        }
        .luma-spin-pill {
          position: absolute;
          border-radius: 50px;
          box-shadow: inset 0 0 0 3.2px #0f172a;
          pointer-events: none;
        }
        :root[data-theme='dark'] .luma-spin-pill,
        .dark .luma-spin-pill {
          box-shadow: inset 0 0 0 3.2px #f8fafc;
        }
      `}</style>
    </div>
  );
};

export const LumaSpin = Component;

export default function DemoOne() {
  return <Component />;
}
