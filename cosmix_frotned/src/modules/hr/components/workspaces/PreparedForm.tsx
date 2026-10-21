import { useState } from 'react';
import { Eye, Plus, RotateCcw, Save, Trash2 } from 'lucide-react';
import { Button } from '@/components/ui/Button';
import { Dialog } from '@/components/ui/Dialog';
import { useDirtyForm } from '../../hooks/use-dirty-form';
import type { PreparedDraft, PreparedTab } from '../../types/prepared-workspace';
import {
  isPreparedFieldVisible,
  isOwnEmployeeField,
  preparedReviewValues,
  updatePreparedRow,
  validatePreparedDraft,
} from '../../utils/prepared-draft';
import { RecordView } from '../RecordView';
import { PreparedField } from './PreparedField';
import { CirclePreview } from './BoundaryEditor';

function emptyDraft(tab: PreparedTab, fixedEmployee?: { id: string; name: string }): PreparedDraft {
  return Object.fromEntries(
    tab.sections.map((section) => [
      section.id,
      [
        {
          key: crypto.randomUUID(),
          values: Object.fromEntries(
            section.fields
              .filter((f) => isOwnEmployeeField(f) && fixedEmployee)
              .map((f) => [f.id, fixedEmployee!.id]),
          ),
        },
      ],
    ]),
  );
}

export function PreparedForm({
  tab,
  fixedEmployee,
}: {
  tab: PreparedTab;
  fixedEmployee?: { id: string; name: string };
}) {
  const [draft, setDraft] = useState(() => emptyDraft(tab, fixedEmployee));
  const [dirty, setDirty] = useState(false);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [review, setReview] = useState(false);
  const [epoch, setEpoch] = useState(0);
  useDirtyForm(dirty);
  function reset() {
    if (dirty && !window.confirm('Discard this unsaved draft?')) return;
    setDraft(emptyDraft(tab, fixedEmployee));
    setDirty(false);
    setErrors({});
    setEpoch((value) => value + 1);
  }
  return (
    <>
      <form
        key={epoch}
        onSubmit={(event) => {
          event.preventDefault();
          const result = validatePreparedDraft(tab, draft);
          setErrors(result);
          if (Object.keys(result).length) return;
          setReview(true);
        }}
      >
        {tab.sections.map((section) => (
          <section key={section.id} className="hr-form-section">
            <div className="hr-repeat-heading">
              <h2>{section.title}</h2>
              {section.repeatable && (
                <Button
                  variant="secondary"
                  type="button"
                  onClick={() => {
                    setDraft({
                      ...draft,
                      [section.id]: [
                        ...(draft[section.id] ?? []),
                        { key: crypto.randomUUID(), values: {} },
                      ],
                    });
                    setDirty(true);
                  }}
                >
                  <Plus size={16} />
                  Add entry
                </Button>
              )}
            </div>
            {(draft[section.id] ?? []).map((row, index) => (
              <div key={row.key} className={section.repeatable ? 'hr-repeat' : undefined}>
                {section.repeatable && (
                  <div className="hr-repeat-heading">
                    <h3>Entry {index + 1}</h3>
                    <button
                      type="button"
                      className="icon-button"
                      title="Remove entry"
                      aria-label={`Remove ${section.title} entry ${index + 1}`}
                      onClick={() => {
                        setDraft({
                          ...draft,
                          [section.id]: draft[section.id]!.filter((r) => r.key !== row.key),
                        });
                        setDirty(true);
                      }}
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                )}
                <div className="hr-form-grid">
                  {section.fields
                    .filter((field) => isPreparedFieldVisible(field, row.values))
                    .map((field) => (
                      <PreparedField
                        key={field.id}
                        field={field}
                        fixedEmployee={fixedEmployee}
                        value={row.values[field.id] ?? null}
                        error={errors[`${section.id}.${row.key}.${field.id}`]}
                        onChange={(value) => {
                          setDraft({
                            ...draft,
                            [section.id]: draft[section.id]!.map((r) =>
                              r.key === row.key
                                ? {
                                    ...r,
                                    values: updatePreparedRow(
                                      section.fields,
                                      r.values,
                                      field.id,
                                      value,
                                    ),
                                  }
                                : r,
                            ),
                          });
                          setDirty(true);
                          setErrors({});
                        }}
                      />
                    ))}
                </div>
                {row.values.shape === 'circle' && (
                  <CirclePreview
                    latitude={String(row.values.latitude ?? '')}
                    longitude={String(row.values.longitude ?? '')}
                    radius={String(row.values.radiusMeters ?? '')}
                  />
                )}
              </div>
            ))}
          </section>
        ))}
        {Object.keys(errors).length > 0 && (
          <p role="alert" className="hr-message hr-message--error">
            Correct the highlighted draft fields.
          </p>
        )}
        <div className="hr-actions">
          <Button type="submit">
            <Eye size={16} />
            Review draft
          </Button>
          <Button variant="secondary" type="button" onClick={reset}>
            <RotateCcw size={16} />
            Clear draft
          </Button>
          <Button variant="secondary" type="button" disabled>
            <Save size={16} />
            {tab.actionLabel && tab.actionLabel !== 'Review draft' ? tab.actionLabel : 'Submit'}
          </Button>
        </div>
      </form>
      <Dialog
        open={review}
        onOpenChange={setReview}
        title="Unsaved draft"
        description="Not submitted"
      >
        <RecordView value={preparedReviewValues(tab, draft)} />
        <div className="hr-actions">
          <Button variant="secondary" onClick={() => setReview(false)}>
            Back to draft
          </Button>
        </div>
      </Dialog>
    </>
  );
}
