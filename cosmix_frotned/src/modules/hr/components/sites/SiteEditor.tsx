import { useState } from 'react';
import { Save, RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { TextInput } from '@/components/forms/TextInput';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrQuery } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import {
  createSiteSchema,
  updateSiteSchema,
  siteStatusSchema,
  siteSchema,
  type Site,
} from '../../schemas/sites';
import { ReferenceField } from '../ReferenceField';
import { Denied, QueryState } from '../Feedback';
import { useSiteAction } from './use-site-action';
import { CreateWorksiteModal } from './CreateWorksiteModal';

export interface SiteEditorProps {
  siteId?: string;
  statusOnly?: boolean;
  onClose: () => void;
  onSaved: (site: Site) => void;
}
export function SiteEditor(props: SiteEditorProps) {
  const access = useHrAccess();
  const [revision, setRevision] = useState(0);
  const query = useHrQuery(
    `sites/${props.siteId}`,
    siteSchema,
    ['hr.sites.read'],
    Boolean(props.siteId),
  );
  if (!access.can('hr.sites.read', 'hr.sites.manage')) return <Denied />;
  const form = (record?: Site) => (
    <SiteForm
      key={`${record?.id ?? 'new'}-${revision}`}
      {...props}
      record={record}
      onReload={async () => {
        const result = await query.refetch();
        if (result.isSuccess) setRevision((value) => value + 1);
      }}
    />
  );
  if (!props.siteId) {
    return <CreateWorksiteModal onClose={props.onClose} onSaved={props.onSaved} />;
  }
  if (query.isSuccess) return form(query.data);
  return (
    <Dialog
      open
      title="Load site"
      description={props.siteId}
      onOpenChange={(open) => {
        if (!open) props.onClose();
      }}
    >
      <QueryState query={query}>{null}</QueryState>
      <Button variant="secondary" onClick={props.onClose}>
        Cancel
      </Button>
    </Dialog>
  );
}
function SiteForm({
  record: initialRecord,
  statusOnly = false,
  onClose,
  onSaved,
  onReload,
}: SiteEditorProps & { record?: Site; onReload: () => Promise<void> }) {
  const [record] = useState(initialRecord);
  const action = useSiteAction();
  const [dirty, setDirty] = useState(false);
  const [validation, setValidation] = useState<string | null>(null);
  const [values, setValues] = useState({
    code: record?.code ?? '',
    name: record?.name ?? '',
    address: record?.address ?? '',
    cityId: record?.cityId ?? null,
    clientName: record?.clientName ?? '',
    projectId: record?.projectId ?? '',
    startsOn: record?.startsOn ?? new Date().toISOString().slice(0, 10),
    endsOn: record?.endsOn ?? '',
    supervisorEmployeeId: record?.supervisorEmployeeId ?? null,
    reason: '',
    isActive: !record?.isActive,
  });
  useDirtyForm(dirty || action.pending || action.locked);
  const change = (key: keyof typeof values, value: string | boolean | null) => {
    setValues((previous) => ({ ...previous, [key]: value }));
    setDirty(true);
  };
  const close = () => {
    if (
      !action.pending &&
      (!(dirty || action.locked) ||
        window.confirm(
          'Discard this form? Check the record before starting another action if the result was uncertain.',
        ))
    )
      onClose();
  };
  return (
    <Dialog
      open
      title={
        statusOnly
          ? `${record?.isActive ? 'Deactivate' : 'Activate'} site`
          : record
            ? 'Edit site'
            : 'Add site'
      }
      description={record?.code ?? 'New site'}
      onOpenChange={(open) => {
        if (!open) close();
      }}
    >
      <form
        onSubmit={async (event) => {
          event.preventDefault();
          const details = {
            name: values.name,
            address: values.address,
            cityId: values.cityId,
            clientName: values.clientName.trim() || null,
            projectId: values.projectId.trim() || null,
            startsOn: values.startsOn,
            endsOn: values.endsOn || null,
            supervisorEmployeeId: values.supervisorEmployeeId,
          };
          const payload = statusOnly
            ? { isActive: values.isActive, expectedVersion: record?.version, reason: values.reason }
            : record
              ? { ...details, expectedVersion: record.version, reason: values.reason }
              : { ...details, code: values.code };
          const parsed = (
            statusOnly ? siteStatusSchema : record ? updateSiteSchema : createSiteSchema
          ).safeParse(payload);
          if (!parsed.success) {
            setValidation(
              parsed.error.issues
                .map((issue) => `${issue.path.join('.')}: ${issue.message}`)
                .join(' '),
            );
            return;
          }
          setValidation(null);
          const result = await action.run(
            `sites${record ? '/' + record.id : ''}${statusOnly ? '/status' : ''}`,
            parsed.data,
            record && !statusOnly ? 'PUT' : 'POST',
            siteSchema,
          );
          if (result) {
            setDirty(false);
            onSaved(result);
          }
        }}
      >
        <fieldset disabled={action.pending || action.locked || action.reloadRequired}>
          {!statusOnly && (
            <div className="hr-form-grid">
              <TextInput
                label="Code"
                value={values.code}
                required
                disabled={Boolean(record)}
                maxLength={30}
                pattern="[A-Za-z0-9][A-Za-z0-9_.-]{0,29}"
                onChange={(e) => change('code', e.target.value)}
              />
              <TextInput
                label="Name"
                value={values.name}
                required
                maxLength={150}
                onChange={(e) => change('name', e.target.value)}
              />
              <TextInput
                label="Address"
                value={values.address}
                required
                maxLength={500}
                onChange={(e) => change('address', e.target.value)}
              />
              <ReferenceField
                label="City"
                kind="cities"
                value={values.cityId}
                required
                currentLabel={record?.cityName ?? undefined}
                onChange={(value) => change('cityId', value)}
              />
              <TextInput
                label="Client name"
                value={values.clientName}
                maxLength={200}
                onChange={(e) => change('clientName', e.target.value)}
              />
              <TextInput
                label="Project reference (UUID)"
                value={values.projectId}
                onChange={(e) => change('projectId', e.target.value)}
              />
              <TextInput
                label="Start date"
                type="date"
                value={values.startsOn}
                required
                onChange={(e) => change('startsOn', e.target.value)}
              />
              <TextInput
                label="End date"
                type="date"
                min={values.startsOn || undefined}
                value={values.endsOn}
                onChange={(e) => change('endsOn', e.target.value)}
              />
              <ReferenceField
                label="Site Supervisor (Assigned Project Lead)"
                kind="employees"
                value={values.supervisorEmployeeId}
                onChange={(value) => change('supervisorEmployeeId', value)}
              />
            </div>
          )}
          {statusOnly && (
            <label className="hr-inline">
              <input
                type="checkbox"
                checked={values.isActive}
                onChange={(e) => change('isActive', e.target.checked)}
              />
              Active
            </label>
          )}
          {record && (
            <TextInput
              label="Reason"
              required
              maxLength={500}
              value={values.reason}
              onChange={(e) => change('reason', e.target.value)}
            />
          )}
        </fieldset>
        {validation && (
          <p role="alert" className="hr-message hr-message--error">
            {validation}
          </p>
        )}
        {action.error && (
          <p role="alert" className="hr-message hr-message--error">
            {action.error}
          </p>
        )}
        <div className="hr-actions">
          <Button
            type="submit"
            variant="success"
            disabled={action.pending || action.reloadRequired}
          >
            <Save size={16} />
            {action.pending ? 'Saving...' : action.locked ? 'Retry unchanged action' : 'Save site'}
          </Button>
          {record && (
            <Button
              variant="secondary"
              disabled={action.pending}
              onClick={() => {
                if (
                  !(dirty || action.locked) ||
                  window.confirm('Discard the form and reload current server values?')
                )
                  void onReload();
              }}
            >
              <RefreshCw size={16} />
              Reload record
            </Button>
          )}
          <Button variant="secondary" disabled={action.pending} onClick={close}>
            Cancel
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
