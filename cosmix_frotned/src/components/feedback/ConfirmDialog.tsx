import {
  createContext,
  useContext,
  useState,
  useCallback,
  useRef,
  type PropsWithChildren,
} from 'react';
import * as Primitive from '@radix-ui/react-dialog';
import { AlertTriangle, AlertCircle, HelpCircle, X } from 'lucide-react';
import { Button } from '@/components/ui/Button';

export interface ConfirmOptions {
  title?: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  variant?: 'danger' | 'warning' | 'info';
}

export type ConfirmFn = (options: ConfirmOptions | string) => Promise<boolean>;

const ConfirmContext = createContext<ConfirmFn | null>(null);

let globalConfirmHandler: ConfirmFn | null = null;

export function appConfirm(options: ConfirmOptions | string): Promise<boolean> {
  if (globalConfirmHandler) {
    return globalConfirmHandler(options);
  }
  return Promise.resolve(window.confirm(typeof options === 'string' ? options : options.message));
}

export function useConfirm(): ConfirmFn {
  const context = useContext(ConfirmContext);
  if (!context) {
    return (options) => appConfirm(options);
  }
  return context;
}

export function ConfirmDialogProvider({ children }: PropsWithChildren) {
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState<ConfirmOptions>({ message: '' });
  const resolverRef = useRef<((value: boolean) => void) | null>(null);

  const confirm = useCallback<ConfirmFn>((opts) => {
    return new Promise<boolean>((resolve) => {
      resolverRef.current = resolve;
      const parsed: ConfirmOptions =
        typeof opts === 'string'
          ? {
              title: 'Confirm Action',
              message: opts,
              confirmText: 'Confirm',
              cancelText: 'Cancel',
              variant: 'warning',
            }
          : {
              title: opts.title ?? 'Confirm Action',
              message: opts.message,
              confirmText: opts.confirmText ?? 'Confirm',
              cancelText: opts.cancelText ?? 'Cancel',
              variant: opts.variant ?? 'danger',
            };
      setOptions(parsed);
      setOpen(true);
    });
  }, []);

  globalConfirmHandler = confirm;

  const handleClose = (result: boolean) => {
    setOpen(false);
    if (resolverRef.current) {
      resolverRef.current(result);
      resolverRef.current = null;
    }
  };

  const variant = options.variant ?? 'danger';

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      <Primitive.Root
        open={open}
        onOpenChange={(isOpen) => {
          if (!isOpen) handleClose(false);
        }}
      >
        <Primitive.Portal>
          <Primitive.Overlay className="dialog-overlay" />
          <Primitive.Content className="dialog-content confirm-dialog-content">
            <div className="confirm-dialog-body">
              <div className={`confirm-icon-badge confirm-icon-badge--${variant}`}>
                {variant === 'danger' ? (
                  <AlertTriangle size={24} />
                ) : variant === 'warning' ? (
                  <AlertCircle size={24} />
                ) : (
                  <HelpCircle size={24} />
                )}
              </div>
              <Primitive.Title className="confirm-dialog-title">
                {options.title ?? 'Confirm Action'}
              </Primitive.Title>
              <Primitive.Description className="confirm-dialog-desc">
                {options.message}
              </Primitive.Description>
              <div className="confirm-dialog-actions">
                <Button
                  variant="secondary"
                  className="confirm-btn confirm-btn--cancel"
                  onClick={() => handleClose(false)}
                >
                  {options.cancelText ?? 'Cancel'}
                </Button>
                <Button
                  variant={variant === 'danger' ? 'danger' : 'primary'}
                  className="confirm-btn confirm-btn--confirm"
                  onClick={() => handleClose(true)}
                  autoFocus
                >
                  {options.confirmText ?? 'Confirm'}
                </Button>
              </div>
            </div>
            <Primitive.Close
              className="icon-button confirm-dialog-close"
              aria-label="Close"
              onClick={() => handleClose(false)}
            >
              <X size={16} />
            </Primitive.Close>
          </Primitive.Content>
        </Primitive.Portal>
      </Primitive.Root>
    </ConfirmContext.Provider>
  );
}
