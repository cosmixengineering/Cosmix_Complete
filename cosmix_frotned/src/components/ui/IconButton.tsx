import type { ButtonHTMLAttributes } from 'react';
import { cn } from '@/utils/cn';
export function IconButton({
  label,
  className,
  ...props
}: Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'aria-label'> & { label: string }) {
  return (
    <button
      type="button"
      {...props}
      aria-label={label}
      title={label}
      className={cn('icon-button', className)}
    />
  );
}
