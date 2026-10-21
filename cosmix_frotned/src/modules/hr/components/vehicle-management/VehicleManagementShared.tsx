import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useId,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from 'react';
import { z } from 'zod';
import { useBlocker } from 'react-router-dom';
import { RefreshCw, RotateCcw, Send } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { ActionMessage } from '../Feedback';
import { TextAreaField } from '../finance/FinanceChrome';
import { useFinanceAccess } from '../../hooks/use-finance-access';
import { useFinanceAction } from '../../hooks/use-finance-queries';
import { inputError, requiredText, vehicleManagementRoot } from '../../api/vehicle-management';

type WriteIntent = { state: 'pending' | 'uncertain'; path: string };
const VehicleIntentContext = createContext<{
  pending: boolean;
  retained: boolean;
  update: (id: string, intent: WriteIntent | null) => void;
  canStart: (id: string) => boolean;
  navigate: (change: () => void) => boolean;
} | null>(null);

export function VehicleManagementIntents({ children }: { children: ReactNode }) {
  const access = useFinanceAccess();
  const intents = useRef(new Map<string, WriteIntent>());
  const [snapshot, setSnapshot] = useState<WriteIntent[]>([]);
  const update = useCallback((id: string, intent: WriteIntent | null) => {
    if (intent) intents.current.set(id, intent);
    else intents.current.delete(id);
    setSnapshot([...intents.current.values()]);
  }, []);
  const canStart = useCallback(
    (id: string) => [...intents.current.keys()].every((key) => key === id),
    [],
  );
  const navigate = useCallback((change: () => void) => {
    const retained = [...intents.current.values()];
    if (retained.some((intent) => intent.state === 'pending')) return false;
    if (
      retained.length &&
      !window.confirm(
        'The vehicle request outcome is unknown. Leaving may discard its retained payload and retry key. ' +
          'Cancel to retry the unchanged request. Before submitting again, reload and reconcile the retained record/history. ' +
          'Leave this view and discard its retry?\n\n' +
          retained.map((intent) => intent.path).join('\n'),
      )
    )
      return false;
    change();
    return true;
  }, []);
  const blocker = useBlocker(
    ({ currentLocation, nextLocation }) =>
      access.ready &&
      nextLocation.pathname !== '/sign-in' &&
      intents.current.size > 0 &&
      (currentLocation.pathname !== nextLocation.pathname ||
        currentLocation.search !== nextLocation.search),
  );
  useEffect(() => {
    if (blocker.state !== 'blocked') return;
    if (!access.ready || blocker.location.pathname === '/sign-in') {
      blocker.proceed();
      return;
    }
    if (!navigate(blocker.proceed)) blocker.reset();
  }, [access.ready, blocker, navigate]);
  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => {
      if (!intents.current.size) return;
      event.preventDefault();
      event.returnValue = '';
    };
    window.addEventListener('beforeunload', warn);
    return () => window.removeEventListener('beforeunload', warn);
  }, []);
  const pending = snapshot.some((intent) => intent.state === 'pending');
  return (
    <VehicleIntentContext.Provider
      value={{ pending, retained: snapshot.length > 0, update, canStart, navigate }}
    >
      {snapshot.length > 0 && (
        <p className="hr-notice" role="status">
          {pending
            ? 'Vehicle request pending. View and record changes are temporarily blocked.'
            : 'Vehicle request outcome uncertain. Its unchanged payload and retry key are retained in this view.'}
        </p>
      )}
      {children}
    </VehicleIntentContext.Provider>
  );
}

export function useVehicleNavigation() {
  const context = useContext(VehicleIntentContext);
  if (!context) throw new Error('Vehicle management intent scope is required.');
  return context;
}

export function useVehicleCommand(permission: string) {
  const access = useFinanceAccess();
  const intents = useVehicleNavigation();
  const id = useId();
  const mounted = useRef(true);
  const updateIntent = intents.update;
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      updateIntent(id, null);
    };
  }, [id, updateIntent]);
  const action = useFinanceAction();
  const active = useRef(false);
  const [retry, setRetry] = useState<(() => Promise<void>) | null>(null);
  async function run<T>(
    path: string,
    body: unknown,
    schema: z.ZodType<T>,
    onSuccess: (result: T) => void,
    onRejected?: () => void,
  ) {
    if (
      active.current ||
      retry ||
      !intents.canStart(id) ||
      !access.can(permission) ||
      !path.startsWith(vehicleManagementRoot + '/')
    )
      return;
    const execute = async () => {
      if (active.current || !mounted.current || !intents.canStart(id) || !access.can(permission))
        return;
      active.current = true;
      updateIntent(id, { state: 'pending', path });
      let rejected = false;
      try {
        const result = await action.run(path, body, 'POST', schema, () => {
          rejected = true;
          updateIntent(id, null);
          setRetry(null);
          onRejected?.();
        });
        if (result !== undefined && mounted.current) {
          updateIntent(id, null);
          setRetry(null);
          onSuccess(result);
        } else if (!rejected && mounted.current) {
          updateIntent(id, { state: 'uncertain', path });
        }
      } finally {
        active.current = false;
      }
    };
    setRetry(() => execute);
    await execute();
  }
  return { ...action, run, retry, locked: retry !== null || action.pending || intents.retained };
}

export function VehicleCommandStatus({
  command,
}: {
  command: ReturnType<typeof useVehicleCommand>;
}) {
  return (
    <>
      <ActionMessage action={command} />
      {command.retry && !command.pending && command.error && (
        <div className="hr-actions">
          <p role="status">Result uncertain. The unchanged request is retained for retry.</p>
          <Button variant="secondary" onClick={() => void command.retry?.()}>
            <RotateCcw size={15} /> Retry unchanged request
          </Button>
        </div>
      )}
    </>
  );
}
export function VehicleRefresh({ busy, onClick }: { busy: boolean; onClick: () => void }) {
  return (
    <Button
      variant="secondary"
      disabled={busy}
      onClick={onClick}
      title="Refresh retained records"
      aria-label="Refresh retained records"
    >
      <RefreshCw size={16} />
    </Button>
  );
}

export function VehicleDecisionForm<T>({
  permission,
  path,
  schema,
  version,
  requestedByAccountId,
  employeeId,
  onSuccess,
  onRejected,
}: {
  permission: string;
  path: string;
  schema: z.ZodType<T>;
  version: string;
  requestedByAccountId: string;
  employeeId?: string;
  onSuccess: (result: T) => void;
  onRejected?: () => void;
}) {
  const access = useFinanceAccess();
  const command = useVehicleCommand(permission);
  const [decision, setDecision] = useState('');
  const [evidence, setEvidence] = useState('');
  const [reason, setReason] = useState('');
  const [error, setError] = useState<string | null>(null);
  const isSelf =
    access.user?.account?.accountId === requestedByAccountId ||
    (employeeId !== undefined && access.employeeId === employeeId);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (isSelf || command.locked || !access.can(permission)) return;
    setError(null);
    try {
      if (!['1', '2', '3'].includes(decision))
        throw new Error('Choose an explicit review decision.');
      await command.run(
        path,
        {
          expectedVersion: version,
          decision: Number(decision),
          evidenceReference: requiredText(evidence, 'Review evidence', 500),
          reason: requiredText(reason, 'Review reason'),
        },
        schema,
        onSuccess,
        onRejected,
      );
    } catch (failure) {
      setError(inputError(failure));
    }
  }
  if (!access.can(permission)) return null;
  if (isSelf)
    return (
      <p role="status">
        Independent reviewer required. Requester and employee cannot review this request.
      </p>
    );
  return (
    <form onSubmit={(event) => void submit(event)}>
      <fieldset disabled={command.locked} className="hr-form-grid">
        <legend>Independent review</legend>
        <SelectInput
          label="Decision"
          required
          value={decision}
          onChange={(event) => setDecision(event.target.value)}
          options={[
            { value: '', label: 'Choose a decision' },
            { value: '1', label: 'Approve' },
            { value: '2', label: 'Return' },
            { value: '3', label: 'Reject' },
          ]}
        />
        <TextInput
          label="Review evidence reference"
          required
          maxLength={500}
          value={evidence}
          onChange={(event) => setEvidence(event.target.value)}
        />
        <TextAreaField label="Review reason" required value={reason} onChange={setReason} />
        <div className="hr-actions wide">
          <Button type="submit">
            <Send size={15} /> Record decision
          </Button>
        </div>
      </fieldset>
      {error && (
        <p role="alert" className="hr-message hr-message--error">
          {error}
        </p>
      )}
      <VehicleCommandStatus command={command} />
    </form>
  );
}
