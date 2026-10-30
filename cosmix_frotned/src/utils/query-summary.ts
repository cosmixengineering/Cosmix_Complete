type SourceQuery = { isError: boolean; isPending: boolean; isFetching: boolean; data: unknown };

export function sourceCount(query: SourceQuery, count: number): string {
  if (query.isError) return 'Unavailable';
  if (query.isPending || query.isFetching) return 'Loading…';
  return query.data === undefined ? 'Unavailable' : String(count);
}

export function sourceReady(query: SourceQuery): boolean {
  return !query.isError && !query.isPending && !query.isFetching && query.data !== undefined;
}
