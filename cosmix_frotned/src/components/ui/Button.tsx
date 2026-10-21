import type { ButtonHTMLAttributes } from 'react';
import { cn } from '@/utils/cn';
export function Button({
  className,
  variant = 'primary',
  type = 'button',
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger' | 'success' | 'slate';
}) {
  return (
    <button type={type} className={cn('button', 'button--' + variant, className)} {...props} />
  );
}
