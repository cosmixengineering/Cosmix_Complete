import { createContext, useContext } from 'react';

export type TaxonomyLifetime = { instance: string; signal: () => AbortSignal; revoke: () => void };
export const TaxonomyLifetimeContext = createContext<TaxonomyLifetime | null>(null);
export function useTaxonomyLifetime() {
  const lifetime = useContext(TaxonomyLifetimeContext);
  if (!lifetime) throw new Error('Taxonomy operations require their feature-owned session.');
  return lifetime;
}
