import type { PropsWithChildren } from 'react';
export function Badge({
  children,
  tone = 'neutral',
}: PropsWithChildren<{ tone?: 'neutral' | 'warning' | 'positive' }>) {
  return <span className={'badge badge--' + tone}>{children}</span>;
}
