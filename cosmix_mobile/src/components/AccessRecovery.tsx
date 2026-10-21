import React, { useEffect, useRef, useState } from 'react';
import { useServices } from '../app/AppProvider';
import { Button, Notice } from './Controls';
import { describeError } from '../utils/errors';

export function AccessRecovery({
  service,
}: {
  service: string;
}): React.JSX.Element {
  const { session } = useServices();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const mounted = useRef(true);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  return (
    <>
      <Notice
        tone="info"
        message={`${service} access is currently unavailable. Refresh to check your access. Contact HR if the service remains unavailable.`}
      />
      {error ? <Notice message={error} /> : null}
      <Button
        title="Refresh access"
        secondary
        busy={busy}
        onPress={async () => {
          if (busy) return;
          setBusy(true);
          setError('');
          try {
            await session.revalidate();
          } catch (failure) {
            if (mounted.current) setError(describeError(failure));
          } finally {
            if (mounted.current) setBusy(false);
          }
        }}
      />
    </>
  );
}
