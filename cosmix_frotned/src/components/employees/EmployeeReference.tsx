import { useEffect, useId, useState } from 'react';
import { useInfiniteQuery, useQuery } from '@tanstack/react-query';
import { z } from 'zod';
import { useAuth } from '@/auth/use-auth';
import { request } from '@/services/api/http-client';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { employeeSearchTerm, formatEmployeeCode } from '@/utils/employee-code';

const employee = z.object({
  id: z.uuid(),
  employeeNumber: z.number().int().positive(),
  name: z.string().nullable(),
});
const employeePage = z.object({
  items: z.array(employee),
  totalCount: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

function useDirectoryAccess() {
  const { user, status } = useAuth();
  const allowed =
    status === 'authenticated' &&
    Boolean(
      user?.grants.some(
        (g) => g.permission === 'hr.employees.read' && g.scopeKind === 7 && g.targetId === null,
      ),
    );
  const key = [
    user?.account?.accountId ?? 'none',
    ...(user?.grants
      .map((g) => `${g.roleAssignmentId}:${g.permission}:${g.scopeKind}:${g.targetId}`)
      .sort() ?? []),
  ];
  return { allowed, key };
}

async function readPage(
  page: number,
  size: number,
  search: string,
  archived: boolean,
  signal: AbortSignal,
) {
  const params = new URLSearchParams({
    page: String(page),
    pageSize: String(size),
    includeArchived: String(archived),
  });
  if (search) params.set('search', search);
  const result = employeePage.parse(await request(`/hr/employees?${params}`, { signal }));
  if (result.page !== page || result.pageSize !== size)
    throw new Error('Employee directory page did not match the request.');
  return result;
}

/** Shared, paginated, authorized lookup. Later pages load only while a displayed ID is unresolved. */
function useEmployeeReference(
  employeeId: string | null | undefined,
  suppliedNumber?: number | string | null,
) {
  const access = useDirectoryAccess();
  const providedCode = formatEmployeeCode(suppliedNumber);
  const needsLookup = Boolean(
    employeeId && z.uuid().safeParse(employeeId).success && providedCode === 'Unavailable',
  );
  const directory = useInfiniteQuery({
    queryKey: ['employee-display-directory', ...access.key],
    initialPageParam: 1,
    queryFn: ({ pageParam, signal }) => readPage(pageParam, 100, '', true, signal),
    getNextPageParam: (last) =>
      last.items.length && last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
    enabled: access.allowed && needsLookup,
    staleTime: 60_000,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const found =
    access.allowed && needsLookup
      ? directory.data?.pages
          .flatMap((p) => p.items)
          .find((row) => row.id.toLowerCase() === employeeId?.toLowerCase())
      : undefined;
  const { hasNextPage, isFetching, isError, fetchNextPage } = directory;
  useEffect(() => {
    if (access.allowed && needsLookup && !found && hasNextPage && !isFetching && !isError)
      void fetchNextPage({ cancelRefetch: false });
  }, [access.allowed, needsLookup, found, hasNextPage, isFetching, isError, fetchNextPage]);
  return {
    code: providedCode !== 'Unavailable' ? providedCode : formatEmployeeCode(found?.employeeNumber),
    name: found?.name,
    loading:
      access.allowed &&
      needsLookup &&
      !found &&
      !isError &&
      (directory.isPending || isFetching || Boolean(hasNextPage)),
    unavailable: !access.allowed || isError,
  };
}

/** Text labels for native/select option lists; all matching remains by the original UUID. */
export function useEmployeeCodes(employeeIds: readonly string[]): Record<string, string> {
  const access = useDirectoryAccess();
  const ids = employeeIds.filter((id) => z.uuid().safeParse(id).success);
  const directory = useInfiniteQuery({
    queryKey: ['employee-display-directory', ...access.key],
    initialPageParam: 1,
    queryFn: ({ pageParam, signal }) => readPage(pageParam, 100, '', true, signal),
    getNextPageParam: (last) =>
      last.items.length && last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
    enabled: access.allowed && ids.length > 0,
    staleTime: 60_000,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const rows = access.allowed ? (directory.data?.pages.flatMap((page) => page.items) ?? []) : [];
  const unresolved = ids.some(
    (id) => !rows.some((row) => row.id.toLowerCase() === id.toLowerCase()),
  );
  const { hasNextPage, isFetching, isError, fetchNextPage } = directory;
  useEffect(() => {
    if (access.allowed && unresolved && hasNextPage && !isFetching && !isError)
      void fetchNextPage({ cancelRefetch: false });
  }, [access.allowed, unresolved, hasNextPage, isFetching, isError, fetchNextPage]);
  return Object.fromEntries(
    ids.map((id) => [
      id,
      formatEmployeeCode(
        rows.find((row) => row.id.toLowerCase() === id.toLowerCase())?.employeeNumber,
      ),
    ]),
  );
}
export function EmployeeReference({
  employeeId,
  employeeNumber,
  employeeName,
  showName = false,
}: {
  employeeId?: string | null;
  employeeNumber?: number | string | null;
  employeeName?: string | null;
  showName?: boolean;
}) {
  const reference = useEmployeeReference(employeeId, employeeNumber);
  if (!employeeId && employeeNumber == null) return <span>—</span>;
  const code = reference.loading ? 'Loading employee…' : reference.code;
  const name = employeeName || reference.name;
  return (
    <span
      title={
        reference.code === 'Unavailable' && !reference.loading
          ? 'Employee code is unavailable in the authorized directory.'
          : undefined
      }
    >
      {code}
      {showName && name ? ` · ${name}` : ''}
    </span>
  );
}

export function EmployeePicker({
  value,
  onChange,
  label = 'Employee',
  disabled = false,
  required = false,
}: {
  value: string;
  onChange: (id: string) => void;
  label?: string;
  disabled?: boolean;
  required?: boolean;
}) {
  const access = useDirectoryAccess();
  const selectId = useId();
  const [search, setSearch] = useState('');
  const [term, setTerm] = useState('');
  const [page, setPage] = useState(1);
  useEffect(() => {
    const timer = setTimeout(() => {
      setTerm(employeeSearchTerm(search));
      setPage(1);
    }, 250);
    return () => clearTimeout(timer);
  }, [search]);
  const selected = useEmployeeReference(value);
  const results = useQuery({
    queryKey: ['employee-picker', ...access.key, term, page],
    queryFn: ({ signal }) => readPage(page, 25, term, false, signal),
    enabled: access.allowed && !disabled,
    staleTime: 15_000,
    gcTime: 0,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const items = access.allowed ? (results.data?.items ?? []) : [];
  const selectedInPage = items.some((item) => item.id === value);
  return (
    <div className="field">
      <TextInput
        label={`Find ${label.toLowerCase()} by name or EMP code`}
        placeholder="Name or EMP-0001"
        maxLength={100}
        value={search}
        disabled={disabled || !access.allowed}
        onChange={(event) => setSearch(event.target.value)}
      />
      <label htmlFor={selectId}>
        {label}
        {required ? ' *' : ''}
      </label>
      <select
        id={selectId}
        value={value}
        required={required}
        disabled={disabled || !access.allowed || results.isFetching}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">Select employee</option>
        {value && !selectedInPage && (
          <option value={value}>
            {selected.loading ? 'Loading employee…' : selected.code}
            {selected.name ? ` · ${selected.name}` : ''}
          </option>
        )}
        {items.map((item) => (
          <option key={item.id} value={item.id}>
            {formatEmployeeCode(item.employeeNumber)}
            {item.name ? ` · ${item.name}` : ''}
          </option>
        ))}
      </select>
      {!access.allowed && (
        <p role="status">
          Employee selection requires employee-directory access. Existing references are retained.
        </p>
      )}
      {results.isError && (
        <p role="alert">
          Employee search is unavailable.{' '}
          <Button
            type="button"
            variant="secondary"
            disabled={results.isFetching}
            onClick={() => void results.refetch()}
          >
            Retry search
          </Button>
        </p>
      )}
      {access.allowed && results.isSuccess && items.length === 0 && (
        <p role="status">No matching employees.</p>
      )}
      {access.allowed && (page > 1 || (results.data?.totalCount ?? 0) > 25) && (
        <div className="hr-actions">
          <Button
            type="button"
            variant="secondary"
            disabled={disabled || page <= 1 || results.isFetching}
            onClick={() => setPage((current) => current - 1)}
          >
            Previous employees
          </Button>
          <span>Page {page}</span>
          <Button
            type="button"
            variant="secondary"
            disabled={
              disabled ||
              results.isFetching ||
              !results.isSuccess ||
              page * 25 >= results.data.totalCount
            }
            onClick={() => setPage((current) => current + 1)}
          >
            Next employees
          </Button>
        </div>
      )}
    </div>
  );
}
