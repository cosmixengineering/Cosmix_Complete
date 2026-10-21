import React, { createContext, useContext, useEffect, useState } from 'react';

const InteractionContext = createContext<{
  locked: boolean;
  setLocked(value: boolean): void;
} | null>(null);

export function WorkspaceInteraction({
  children,
}: React.PropsWithChildren): React.JSX.Element {
  const [locked, setLocked] = useState(false);
  return (
    <InteractionContext.Provider value={{ locked, setLocked }}>
      {children}
    </InteractionContext.Provider>
  );
}

export function useWorkspaceInteraction() {
  const context = useContext(InteractionContext);
  if (!context) throw new Error('WorkspaceInteraction is required');
  return context;
}

export function useWorkspaceLock(locked: boolean): void {
  const { setLocked } = useWorkspaceInteraction();
  useEffect(() => {
    setLocked(locked);
    return () => setLocked(false);
  }, [locked, setLocked]);
}
