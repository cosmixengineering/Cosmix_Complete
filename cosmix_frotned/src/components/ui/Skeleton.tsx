import type { CSSProperties } from 'react';

export interface SkeletonProps {
  width?: string | number;
  height?: string | number;
  borderRadius?: string | number;
  circle?: boolean;
  className?: string;
  style?: CSSProperties;
}

/**
 * Primitive Skeleton component with shimmering animation.
 */
export function Skeleton({
  width = '100%',
  height = 14,
  borderRadius,
  circle = false,
  className = '',
  style,
}: SkeletonProps) {
  return (
    <div
      className={`skeleton-shimmer ${circle ? 'skeleton-circle' : ''} ${className}`.trim()}
      style={{
        width,
        height,
        borderRadius: circle ? '50%' : borderRadius,
        ...style,
      }}
      aria-hidden="true"
    />
  );
}

export interface TableSkeletonProps {
  rows?: number;
  columns?: number;
  className?: string;
}

/**
 * Enterprise Table Skeleton Loader
 * Renders shimmering placeholder table header, rows, badges, and action buttons.
 */
export function TableSkeleton({ rows = 6, className = '' }: TableSkeletonProps) {
  return (
    <div className={`table-skeleton-container ${className}`.trim()} role="status" aria-busy="true">
      <table className="table-skeleton">
        <thead>
          <tr>
            <th style={{ width: '32%' }}>
              <div className="skeleton-bar" style={{ width: '90px', height: '11px' }} />
            </th>
            <th style={{ width: '22%' }}>
              <div className="skeleton-bar" style={{ width: '60px', height: '11px' }} />
            </th>
            <th style={{ width: '20%' }}>
              <div className="skeleton-bar" style={{ width: '70px', height: '11px' }} />
            </th>
            <th style={{ width: '14%' }}>
              <div className="skeleton-bar" style={{ width: '50px', height: '11px' }} />
            </th>
            <th style={{ width: '12%', textAlign: 'right' }}>
              <div
                className="skeleton-bar"
                style={{ width: '45px', height: '11px', marginLeft: 'auto' }}
              />
            </th>
          </tr>
        </thead>
        <tbody>
          {Array.from({ length: rows }).map((_, i) => (
            <tr key={i}>
              <td>
                <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                  <div className="skeleton-circle" style={{ width: '20px', height: '20px' }} />
                  <div
                    className="skeleton-bar"
                    style={{
                      width: `${55 + ((i * 19) % 35)}%`,
                      height: '13px',
                      borderRadius: '3px',
                    }}
                  />
                </div>
              </td>
              <td>
                <div
                  className="skeleton-bar"
                  style={{ width: `${50 + ((i * 13) % 25)}px`, height: '12px' }}
                />
              </td>
              <td>
                <div
                  className="skeleton-bar"
                  style={{ width: `${70 + ((i * 17) % 30)}px`, height: '12px' }}
                />
              </td>
              <td>
                <div className="skeleton-pill" style={{ width: '54px', height: '18px' }} />
              </td>
              <td style={{ textAlign: 'right' }}>
                <div
                  style={{
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'flex-end',
                    gap: '5px',
                  }}
                >
                  <div className="skeleton-btn" style={{ width: '24px', height: '22px' }} />
                  <div className="skeleton-btn" style={{ width: '24px', height: '22px' }} />
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

export function CardSkeleton({ className = '' }: { className?: string }) {
  return (
    <div
      className={`table-skeleton-container ${className}`.trim()}
      style={{ padding: '16px', display: 'flex', flexDirection: 'column', gap: '12px' }}
      role="status"
      aria-busy="true"
    >
      <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
        <div className="skeleton-bar" style={{ width: '140px', height: '15px' }} />
        <div className="skeleton-pill" style={{ width: '60px' }} />
      </div>
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))',
          gap: '10px',
        }}
      >
        {Array.from({ length: 6 }).map((_, i) => (
          <div
            key={i}
            style={{
              background: '#f8fafc',
              padding: '10px',
              borderRadius: '6px',
              display: 'flex',
              flexDirection: 'column',
              gap: '6px',
            }}
          >
            <div className="skeleton-bar" style={{ width: '50px', height: '10px' }} />
            <div className="skeleton-bar" style={{ width: '80%', height: '12px' }} />
          </div>
        ))}
      </div>
    </div>
  );
}

export function ListSkeleton({ rows = 4, className = '' }: { rows?: number; className?: string }) {
  return (
    <div
      className={`table-skeleton-container ${className}`.trim()}
      style={{ padding: '10px 14px', display: 'flex', flexDirection: 'column', gap: '10px' }}
      role="status"
      aria-busy="true"
    >
      {Array.from({ length: rows }).map((_, i) => (
        <div
          key={i}
          style={{
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            padding: '8px 0',
            borderBottom: i < rows - 1 ? '1px solid #f1f5f9' : 'none',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <div className="skeleton-circle" style={{ width: '28px', height: '28px' }} />
            <div style={{ display: 'flex', flexDirection: 'column', gap: '5px' }}>
              <div className="skeleton-bar" style={{ width: '130px', height: '12px' }} />
              <div className="skeleton-bar" style={{ width: '85px', height: '10px' }} />
            </div>
          </div>
          <div className="skeleton-pill" style={{ width: '60px' }} />
        </div>
      ))}
    </div>
  );
}

export default Skeleton;
