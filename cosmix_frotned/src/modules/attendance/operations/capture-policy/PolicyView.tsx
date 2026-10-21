import type { ReactNode } from 'react';
import type { CapturePolicy, CaptureSettings } from './contracts';

function Setting({
  title,
  enabled,
  children,
}: {
  title: string;
  enabled: boolean;
  children?: ReactNode;
}) {
  return (
    <div className="capture-policy-setting">
      <div className="attendance-heading">
        <h3>{title}</h3>
        <span>{enabled ? 'Enabled' : 'Disabled'}</span>
      </div>
      {enabled && children}
    </div>
  );
}
export function SettingsView({ settings }: { settings: CaptureSettings }) {
  return (
    <div className="capture-policy-settings">
      <Setting title="Duplicate capture" enabled={settings.duplicate.enabled}>
        <dl className="attendance-record">
          <dt>Tolerance</dt>
          <dd>{settings.duplicate.toleranceSeconds} seconds</dd>
        </dl>
      </Setting>
      <Setting title="Active duration" enabled={settings.activeDuration.enabled}>
        <dl className="attendance-record">
          <dt>Maximum duration</dt>
          <dd>{settings.activeDuration.maximumMinutes} minutes</dd>
        </dl>
      </Setting>
      <Setting title="Travel validation" enabled={settings.travel.enabled}>
        <dl className="attendance-record">
          <dt>Lookback</dt>
          <dd>{settings.travel.lookbackSeconds} seconds</dd>
          <dt>Maximum speed</dt>
          <dd>{settings.travel.maximumMetersPerSecond} meters/second</dd>
          <dt>Maximum accuracy radius</dt>
          <dd>{settings.travel.maximumAccuracyMeters} meters</dd>
        </dl>
      </Setting>
      <Setting title="Device risk" enabled={settings.device.enabled}>
        <dl className="attendance-record">
          <dt>Approved code set</dt>
          <dd>{settings.device.approvedCodeSetReference}</dd>
        </dl>
        <ul className="capture-policy-mappings">
          {settings.device.riskMappings?.map((row) => (
            <li key={row.observationCode}>
              <span>{row.observationCode}</span>
              <span>{row.riskKind === 'mock_location' ? 'Mock location' : 'Device integrity'}</span>
            </li>
          ))}
        </ul>
      </Setting>
    </div>
  );
}
export function PolicyView({ policy }: { policy: CapturePolicy }) {
  return (
    <>
      <dl className="attendance-record">
        <dt>Configuration revision</dt>
        <dd>{policy.revision}</dd>
        <dt>Effective from (UTC)</dt>
        <dd>
          <time dateTime={policy.effectiveFromUtc}>{policy.effectiveFromUtc}</time>
        </dd>
        <dt>Published at (UTC)</dt>
        <dd>
          <time dateTime={policy.publishedAtUtc}>{policy.publishedAtUtc}</time>
        </dd>
        <dt>Published by</dt>
        <dd>{policy.publishedBy}</dd>
        <dt>Approval reference</dt>
        <dd>{policy.approvalReference}</dd>
        <dt>Publication reason</dt>
        <dd>{policy.reason}</dd>
      </dl>
      <SettingsView settings={policy.settings} />
      <details>
        <summary>Version evidence</summary>
        <dl className="attendance-record">
          <dt>Configuration version ID</dt>
          <dd>{policy.id}</dd>
          <dt>Supersedes version ID</dt>
          <dd>{policy.supersedesVersionId ?? 'First publication'}</dd>
          <dt>Source fingerprint</dt>
          <dd>{policy.sourceFingerprint}</dd>
        </dl>
      </details>
    </>
  );
}
