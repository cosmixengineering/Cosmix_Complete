import { useCallback, useEffect } from 'react';
import { useBeforeUnload, useBlocker } from 'react-router-dom';
import { useConfirm } from '@/components/feedback/ConfirmDialog';

export function useDirtyForm(dirty: boolean) {
  const confirm = useConfirm();
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      dirty &&
      nextLocation.pathname !== '/sign-in' &&
      (currentLocation.pathname !== nextLocation.pathname ||
        currentLocation.search !== nextLocation.search),
  );

  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    void confirm({
      title: 'Discard unsaved changes?',
      message:
        'You have unsaved changes on this form. Leaving or switching sections now will discard your edits.',
      confirmText: 'Discard Changes',
      cancelText: 'Keep Editing',
      variant: 'danger',
    }).then((proceed) => {
      if (proceed) blocker.proceed();
      else blocker.reset();
    });
  }, [blocker, confirm]);

  useBeforeUnload(
    useCallback(
      (event) => {
        if (dirty) {
          event.preventDefault();
          event.returnValue = '';
        }
      },
      [dirty],
    ),
  );
}
