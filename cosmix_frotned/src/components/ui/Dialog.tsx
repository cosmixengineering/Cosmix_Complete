import * as Primitive from '@radix-ui/react-dialog';
import { X } from 'lucide-react';
import type { PropsWithChildren } from 'react';
export function Dialog({
  open,
  onOpenChange,
  title,
  description,
  className,
  children,
}: PropsWithChildren<{
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  className?: string;
}>) {
  return (
    <Primitive.Root open={open} onOpenChange={onOpenChange}>
      <Primitive.Portal>
        <Primitive.Overlay className="dialog-overlay" />
        <Primitive.Content className={['dialog-content', className].filter(Boolean).join(' ')}>
          <div className="dialog-heading">
            <Primitive.Title>{title}</Primitive.Title>
            <Primitive.Close className="icon-button" aria-label="Close dialog" title="Close dialog">
              <X size={18} />
            </Primitive.Close>
          </div>
          <Primitive.Description>{description}</Primitive.Description>
          {children}
        </Primitive.Content>
      </Primitive.Portal>
    </Primitive.Root>
  );
}
