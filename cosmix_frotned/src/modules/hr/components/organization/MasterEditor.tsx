import { useState } from 'react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { masterSchema, type Master } from '../../schemas/organization';
import { useHrAction } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { ActionMessage } from '../Feedback';
import { ReferenceField } from '../ReferenceField';
export function MasterEditor({
  kind,
  record,
  statusOnly = false,
  onClose,
  onSaved,
}: {
  kind: string;
  record?: Master;
  statusOnly?: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const action = useHrAction();
  const [dirty, setDirty] = useState(false);
  useDirtyForm(dirty || action.pending);
  const [code, setCode] = useState(record?.code ?? '');
  const [name, setName] = useState(record?.name ?? '');
  const [province, setProvince] = useState(record?.province ?? '');
  const [city, setCity] = useState(record?.cityId ?? null);
  const [reason, setReason] = useState('');
  const close = () => {
    if (!action.pending && (!dirty || window.confirm('Discard unsaved changes?'))) onClose();
  };
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open) close();
      }}
      title={
        statusOnly
          ? `${record?.isActive ? 'Deactivate' : 'Activate'} record`
          : record
            ? 'Edit record'
            : 'Add record'
      }
      description={record?.code ?? 'Organization record'}
    >
      <form
        onChange={() => setDirty(true)}
        onSubmit={async (e) => {
          e.preventDefault();
          const payload = statusOnly
            ? { isActive: !record!.isActive, expectedVersion: record!.version, reason }
            : {
                name,
                ...(kind === 'branches' ? { cityId: city } : {}),
                ...(kind === 'cities' ? { province: province || null } : {}),
                ...(record ? { expectedVersion: record.version, reason } : { code }),
              };
          const result = await action.run(
            kind + (record ? '/' + record.id : '') + (statusOnly ? '/status' : ''),
            payload,
            record && !statusOnly ? 'PUT' : 'POST',
            masterSchema,
          );
          if (result) {
            setDirty(false);
            onSaved();
          }
        }}
      >
        <fieldset disabled={action.pending}>
          {!statusOnly && (
            <>
              <div className="hr-form-section">
                {kind === 'staff-categories' && !record ? (
                  <SelectInput
                    label="Code"
                    value={code}
                    onChange={(e) => setCode(e.target.value)}
                    required
                    options={[
                      { value: '', label: 'Select category' },
                      { value: 'OFFICE', label: 'OFFICE' },
                      { value: 'SITE', label: 'SITE' },
                    ]}
                  />
                ) : (
                  <TextInput
                    label="Code"
                    required
                    maxLength={30}
                    pattern="[A-Za-z0-9][A-Za-z0-9_.-]{0,29}"
                    value={code}
                    disabled={Boolean(record)}
                    onChange={(e) => setCode(e.target.value)}
                  />
                )}
              </div>
              <TextInput
                label="Name"
                required
                maxLength={kind === 'cities' ? 100 : 150}
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
              {kind === 'cities' && (
                <TextInput
                  label="Province"
                  maxLength={100}
                  value={province}
                  onChange={(e) => setProvince(e.target.value)}
                />
              )}
              {kind === 'branches' && (
                <ReferenceField
                  label="City"
                  kind="cities"
                  required
                  value={city}
                  currentLabel={record?.cityName ?? undefined}
                  onChange={(v) => {
                    setCity(v);
                    setDirty(true);
                  }}
                />
              )}
            </>
          )}
          {record && (
            <TextInput
              label="Reason"
              required
              maxLength={500}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          )}
          <ActionMessage action={action} />
          <div className="hr-actions">
            <Button type="submit">
              {action.pending ? 'Saving...' : statusOnly ? 'Confirm status change' : 'Save record'}
            </Button>
            <Button variant="secondary" onClick={close}>
              Cancel
            </Button>
          </div>
        </fieldset>
      </form>
    </Dialog>
  );
}
