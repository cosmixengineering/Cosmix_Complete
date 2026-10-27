import { useState } from 'react';
import * as DialogPrimitive from '@radix-ui/react-dialog';
import {
  X,
  Building2,
  UserCheck,
  MapPin,
  Building,
  Layers,
  FileText,
  Plus,
  AlertCircle,
} from 'lucide-react';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { masterSchema, type Master } from '../../schemas/organization';
import { useHrAction } from '../../hooks/use-hr-queries';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import { ActionMessage } from '../Feedback';
import { ReferenceField } from '../ReferenceField';

export function getMasterMeta(kind: string) {
  switch (kind) {
    case 'departments':
      return {
        singular: 'department',
        title: 'Department',
        codePlaceholder: 'e.g. DEPT-IT',
        namePlaceholder: 'e.g. Information Technology',
        icon: Building2,
      };
    case 'designations':
      return {
        singular: 'designation',
        title: 'Designation',
        codePlaceholder: 'e.g. DESG-ENG',
        namePlaceholder: 'e.g. Lead Mechanical Engineer',
        icon: UserCheck,
      };
    case 'cities':
      return {
        singular: 'city',
        title: 'City',
        codePlaceholder: 'e.g. KHI',
        namePlaceholder: 'e.g. Karachi',
        icon: MapPin,
      };
    case 'branches':
      return {
        singular: 'office',
        title: 'Office',
        codePlaceholder: 'e.g. BR-HQ',
        namePlaceholder: 'e.g. Head Office Karachi',
        icon: Building,
      };
    case 'staff-categories':
      return {
        singular: 'staff category',
        title: 'Staff Category',
        codePlaceholder: 'e.g. OFFICE',
        namePlaceholder: 'e.g. Head Office Staff',
        icon: Layers,
      };
    case 'employment-types':
      return {
        singular: 'employment type',
        title: 'Employment Type',
        codePlaceholder: 'e.g. FULL-TIME',
        namePlaceholder: 'e.g. Regular Permanent',
        icon: FileText,
      };
    case 'sites':
      return {
        singular: 'site',
        title: 'Site',
        codePlaceholder: 'e.g. SITE-01',
        namePlaceholder: 'e.g. North Industrial Plant',
        icon: MapPin,
      };
    default:
      return {
        singular: 'record',
        title: 'Record',
        codePlaceholder: 'e.g. REC-01',
        namePlaceholder: 'e.g. Title or name',
        icon: Building2,
      };
  }
}

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

  const meta = getMasterMeta(kind);
  const Icon = meta.icon;

  const dialogTitle = statusOnly
    ? `${record?.isActive ? 'Deactivate' : 'Activate'} ${meta.singular}`
    : record
      ? `Edit ${meta.singular}`
      : `Add ${meta.singular}`;

  const dialogSubtitle = statusOnly
    ? `Confirm status change for this ${meta.singular}.`
    : record
      ? `Update the ${meta.singular} details.`
      : `Enter the ${meta.singular} details.`;

  const close = () => {
    if (!action.pending && (!dirty || window.confirm('Discard unsaved changes?'))) {
      onClose();
    }
  };

  return (
    <DialogPrimitive.Root
      open
      onOpenChange={(isOpen) => {
        if (!isOpen) close();
      }}
    >
      <DialogPrimitive.Portal>
        <DialogPrimitive.Overlay className="dialog-overlay" />
        <DialogPrimitive.Content className="org-modal-content">
          <div className="org-modal-header">
            <div className="org-modal-header-left">
              <div className="org-modal-icon-badge">
                <Icon size={20} strokeWidth={2} />
              </div>
              <div className="org-modal-titles">
                <DialogPrimitive.Title className="org-modal-title">
                  {dialogTitle}
                </DialogPrimitive.Title>
                <DialogPrimitive.Description className="org-modal-desc">
                  {dialogSubtitle}
                </DialogPrimitive.Description>
              </div>
            </div>
            <button
              type="button"
              className="org-modal-close-btn"
              aria-label="Close dialog"
              onClick={close}
            >
              <X size={18} />
            </button>
          </div>

          <form
            className="org-modal-form"
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
            <fieldset disabled={action.pending} className="org-modal-fieldset">
              <div className="org-modal-fields">
                {!statusOnly && (
                  <>
                    {kind === 'staff-categories' && !record ? (
                      <SelectInput
                        label={`${meta.title} code`}
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
                        label={`${meta.title} code`}
                        required
                        placeholder={meta.codePlaceholder}
                        maxLength={30}
                        pattern="[A-Za-z0-9][A-Za-z0-9_.-]{0,29}"
                        value={code}
                        disabled={Boolean(record)}
                        onChange={(e) => setCode(e.target.value)}
                      />
                    )}

                    <TextInput
                      label={`${meta.title} name`}
                      required
                      placeholder={meta.namePlaceholder}
                      maxLength={kind === 'cities' ? 100 : 150}
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                    />

                    {kind === 'cities' && (
                      <TextInput
                        label="Province"
                        placeholder="e.g. Sindh"
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

                {statusOnly && (
                  <div className="org-modal-status-alert">
                    <AlertCircle size={18} className="org-status-alert-icon" />
                    <div>
                      <span>Status change: </span>
                      Record <strong>{record?.name}</strong> will be set to{' '}
                      <span
                        className={
                          record?.isActive ? 'org-status-pill-inactive' : 'org-status-pill-active'
                        }
                      >
                        {record?.isActive ? 'Inactive' : 'Active'}
                      </span>
                    </div>
                  </div>
                )}

                {(record || statusOnly) && (
                  <TextInput
                    label={statusOnly ? 'Reason for status change' : 'Reason for change'}
                    placeholder={
                      statusOnly
                        ? 'e.g. Restructuring or temporary deactivation'
                        : 'e.g. Updating department organization or renaming'
                    }
                    required
                    maxLength={500}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  />
                )}
              </div>

              <ActionMessage action={action} />

              <div className="org-modal-footer">
                <button
                  type="button"
                  className="org-btn-cancel"
                  onClick={close}
                  disabled={action.pending}
                >
                  Cancel
                </button>
                <button type="submit" className="org-btn-submit" disabled={action.pending}>
                  {!record && !statusOnly && <Plus size={15} strokeWidth={2.5} />}
                  {action.pending
                    ? 'Saving...'
                    : statusOnly
                      ? 'Confirm status change'
                      : record
                        ? 'Save changes'
                        : `Add ${meta.singular}`}
                </button>
              </div>
            </fieldset>
          </form>
        </DialogPrimitive.Content>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
