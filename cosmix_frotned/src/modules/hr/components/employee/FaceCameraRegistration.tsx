import { useEffect, useRef, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Link } from 'react-router-dom';
import { ApiError } from '@/services/api/api-error';
import { Camera, CheckCircle2, X, AlertCircle, RefreshCw } from 'lucide-react';
import { z } from 'zod';
import { Button } from '@/components/ui/Button';
import { TextInput } from '@/components/forms/TextInput';
import { SelectInput } from '@/components/forms/SelectInput';
import { Pagination } from '@/components/data-display/Pagination';
import { hrGet } from '../../api/hr-api';
import { useHrAccess } from '../../hooks/use-hr-access';
import { useHrAction } from '../../hooks/use-hr-queries';
import { useHrLifetime } from '../../hooks/use-hr-lifetime';
import { errorMessage } from '../../utils/messages';
import { id, pageOf, time, version } from '../../schemas/contracts';

const enrollment = z.object({
  id,
  employeeId: id,
  provider: z.string(),
  providerVersion: z.string(),
  enrolledAtUtc: time,
  isActive: z.boolean(),
  revokedAtUtc: time.nullable(),
  replacesEnrollmentId: id.nullable(),
  version,
});
const devicePage = pageOf(
  z.object({ id, displayName: z.string(), platform: z.string(), verifiedAtUtc: time }),
);
const captureResponse = z.object({ providerEvidenceToken: z.string().min(1), expiresAtUtc: time });

export function FaceCameraRegistration({
  employeeId,
  onReset,
}: {
  employeeId: string;
  onReset: () => void;
}) {
  const access = useHrAccess();
  const lifetime = useHrLifetime();
  const video = useRef<HTMLVideoElement>(null);
  const stream = useRef<MediaStream | null>(null);
  const cameraEpoch = useRef(0);
  const mounted = useRef(true);
  const captureAction = useHrAction();
  const enrollAction = useHrAction();
  const busyRef = useRef(false);
  const [busy, setBusy] = useState(false);
  const [camera, setCamera] = useState(false);
  const [opening, setOpening] = useState(false);
  const [image, setImage] = useState<string | null>(null);
  const [deviceId, setDeviceId] = useState('');
  const [devicePageNumber, setDevicePageNumber] = useState(1);
  const [reason, setReason] = useState('');
  const [noticeAccepted, setNoticeAccepted] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [submitted, setSubmitted] = useState(false);
  const [saved, setSaved] = useState<z.infer<typeof enrollment> | null>(null);
  const token = useRef<z.infer<typeof captureResponse> | null>(null);
  const originalEnrollment = useRef<{ id: string; version: string } | null | undefined>(undefined);
  const path = `employees/${employeeId}/face-enrollments`;
  const devices = useQuery({
    queryKey: ['hr', access.subject, path, 'devices', devicePageNumber],
    queryFn: ({ signal }) =>
      hrGet(`${path}/devices?page=${devicePageNumber}&pageSize=10`, devicePage, signal),
    staleTime: 15000,
    gcTime: 30000,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const history = useQuery({
    queryKey: ['hr', access.subject, path, 'registration-state'],
    queryFn: ({ signal }) =>
      hrGet(
        `${path}/current`,
        z
          .object({ enrollment: enrollment.nullable() })
          .refine(
            (p) =>
              p.enrollment === null ||
              (p.enrollment.employeeId === employeeId &&
                p.enrollment.isActive &&
                p.enrollment.revokedAtUtc === null),
          ),
        signal,
      ),
    staleTime: 15000,
    gcTime: 30000,
    retry: false,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
  });
  const active = history.data?.enrollment ? [history.data.enrollment] : [];
  const employeeSetupRequired =
    devices.isError &&
    devices.error instanceof ApiError &&
    devices.error.code === 'face_employee_ineligible';
  const known = history.isSuccess && !history.isFetching;
  const chosenDevice = devices.data?.items.find((x) => x.id === deviceId);
  const mayCapture =
    known &&
    devices.isSuccess &&
    !devices.isFetching &&
    Boolean(chosenDevice) &&
    noticeAccepted &&
    !submitted;
  function stopCamera() {
    cameraEpoch.current++;
    stream.current?.getTracks().forEach((track) => track.stop());
    stream.current = null;
    if (video.current) video.current.srcObject = null;
  }
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      cameraEpoch.current++;
      stream.current?.getTracks().forEach((track) => track.stop());
      stream.current = null;
    };
  }, []);
  useEffect(() => {
    const signal = lifetime();
    const clearCamera = () => {
      cameraEpoch.current++;
      stream.current?.getTracks().forEach((track) => track.stop());
      stream.current = null;
      if (mounted.current) {
        setCamera(false);
        setImage(null);
      }
    };
    signal.addEventListener('abort', clearCamera, { once: true });
    return () => signal.removeEventListener('abort', clearCamera);
  }, [lifetime]);
  async function openCamera() {
    if (!mayCapture || opening) return;
    const current = ++cameraEpoch.current;
    setOpening(true);
    setError(null);
    try {
      if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia)
        throw new Error('Use HTTPS and a browser with camera support.');
      const media = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'user', width: { ideal: 640 }, height: { ideal: 480 } },
        audio: false,
      });
      if (!mounted.current || current !== cameraEpoch.current || lifetime().aborted) {
        media.getTracks().forEach((t) => t.stop());
        return;
      }
      stream.current = media;
      if (video.current) {
        video.current.srcObject = media;
        await video.current.play();
      }
      if (!mounted.current || current !== cameraEpoch.current) {
        media.getTracks().forEach((t) => t.stop());
        return;
      }
      setCamera(true);
    } catch (failure) {
      if (mounted.current && current === cameraEpoch.current) {
        stopCamera();
        setCamera(false);
        setError(
          failure instanceof DOMException && failure.name === 'NotAllowedError'
            ? 'Camera permission was denied. Allow camera access in your browser, then try again.'
            : failure instanceof Error
              ? failure.message
              : 'Camera could not be opened.',
        );
      }
    } finally {
      if (mounted.current) setOpening(false);
    }
  }
  function capture() {
    if (!video.current || !camera || !mayCapture) return;
    const source = video.current;
    if (!source.videoWidth || !source.videoHeight) {
      setError('The camera is still starting. Try again.');
      return;
    }
    const canvas = document.createElement('canvas');
    const ratio = Math.min(1, 640 / Math.max(source.videoWidth, source.videoHeight));
    canvas.width = Math.round(source.videoWidth * ratio);
    canvas.height = Math.round(source.videoHeight * ratio);
    const context = canvas.getContext('2d');
    if (!context) {
      setError('This browser could not capture the camera image.');
      return;
    }
    context.drawImage(source, 0, 0, canvas.width, canvas.height);
    const captured = canvas.toDataURL('image/jpeg', 0.9);
    if ((captured.split(',')[1]?.length ?? 0) > 699052) {
      setError('Capture is too large. Please try a lower-resolution camera.');
      return;
    }
    setImage(captured);
    stopCamera();
    setCamera(false);
    setError(null);
  }
  async function save() {
    if (busyRef.current || !image || !deviceId || !reason.trim() || !known || saved) return;
    busyRef.current = true;
    setBusy(true);
    setSubmitted(true);
    setError(null);
    if (originalEnrollment.current === undefined)
      originalEnrollment.current = active[0]
        ? { id: active[0].id, version: active[0].version }
        : null;
    const signal = lifetime();
    try {
      if (!token.current) {
        const result = await captureAction.run(
          `${path}/captures`,
          { registeredDeviceId: deviceId, imageBase64: image.split(',')[1], reason: reason.trim() },
          'POST',
          captureResponse,
        );
        if (!result || signal.aborted) return;
        token.current = result;
      }
      const result = await enrollAction.run(
        path,
        {
          registeredDeviceId: deviceId,
          providerEvidenceToken: token.current.providerEvidenceToken,
          expectedActiveEnrollmentId: originalEnrollment.current?.id ?? null,
          expectedActiveVersion: originalEnrollment.current?.version ?? null,
          reason: reason.trim(),
        },
        'POST',
        enrollment.refine(
          (x) => x.employeeId === employeeId && x.isActive && x.revokedAtUtc === null,
          'Enrollment was not confirmed for this employee.',
        ),
      );
      if (!result || signal.aborted) return;
      setSaved(result);
      setImage(null);
      token.current = null;
    } finally {
      busyRef.current = false;
      if (!signal.aborted) setBusy(false);
    }
  }
  const actionError = enrollAction.error ?? captureAction.error;
  const actionCode = enrollAction.errorCode ?? captureAction.errorCode;
  const actionStatus = enrollAction.error ? enrollAction.errorStatus : captureAction.errorStatus;
  // These codes are terminal journal outcomes (or capture preflight rejection),
  // not a transport timeout. A new key is allowed only after reading current state.
  const terminalUnavailable =
    actionStatus === 503 &&
    [
      'face_capture_not_configured',
      'face_policy_not_confirmed',
      'face_provider_not_configured',
      'face_provider_unavailable',
      'face_template_storage_unavailable',
    ].includes(actionCode ?? '');
  const canStartAgain =
    submitted &&
    !busy &&
    actionStatus !== null &&
    ((actionStatus >= 400 && actionStatus < 500) || terminalUnavailable) &&
    actionCode !== 'face_enrollment_reconciliation_pending';
  if (saved)
    return (
      <div className="bioreg-success" role="status">
        <CheckCircle2 size={28} />
        <h3>Face registered</h3>
        <p>The server confirmed an active enrollment for this employee.</p>
        <p>
          Registered {new Date(saved.enrolledAtUtc).toLocaleString()} · {saved.provider}
        </p>
        <p>
          Attendance Check-In still requires live verification from the employee's registered
          device.
        </p>
        <Button variant="secondary" onClick={onReset}>
          Refresh enrollment details
        </Button>
      </div>
    );
  return (
    <div className="bioreg-form-flow">
      {employeeSetupRequired && (
        <div className="bio-inline-alert" role="status">
          <div>
            <strong>Employee setup required before face registration</strong>
            <p>{errorMessage(devices.error)}</p>
            <p>
              An authorized Admin must check the employee account and hiring setup. The employee
              then needs a verified mobile device registered to that same account. Camera
              registration cannot proceed until these requirements pass.
            </p>
            <Link to={`/hr/employees/${employeeId}`}>View employee setup</Link>
            <Button
              variant="secondary"
              disabled={devices.isFetching}
              onClick={() => void devices.refetch()}
            >
              Recheck after setup
            </Button>
          </div>
        </div>
      )}
      {(history.isError || (devices.isError && !employeeSetupRequired)) && (
        <div className="bio-inline-alert" role="alert">
          <div className="bio-inline-alert-msg">
            <AlertCircle size={15} aria-hidden="true" />
            <span>{errorMessage(history.error || devices.error)}</span>
          </div>
          <button
            type="button"
            className="bio-inline-retry-btn"
            onClick={() => {
              if (history.isError) void history.refetch();
              if (devices.isError) void devices.refetch();
            }}
          >
            <RefreshCw size={12} aria-hidden="true" /> Retry
          </button>
        </div>
      )}

      {known && (
        <div className="bio-enrollment-status-card">
          <div className="bio-status-badge-wrap">
            <span className={`bio-status-indicator ${active[0] ? 'is-active' : 'is-none'}`} />
            <span className="bio-status-text">
              {active[0] ? 'Active Face Enrollment On File' : 'No Active Face Enrollment'}
            </span>
          </div>
          <span className="bio-status-desc">
            {active[0]
              ? 'An active face enrollment exists. A successful new registration will replace it.'
              : 'No active face enrollment is recorded for this employee.'}
          </span>
        </div>
      )}

      <div className="bioreg-form-grid">
        <div className="bio-field-wrap">
          <SelectInput
            label="Employee's registered mobile device"
            value={deviceId}
            disabled={submitted || opening || camera || devices.isPending || devices.isError}
            onChange={(e) => {
              setDeviceId(e.target.value);
              setImage(null);
            }}
            options={[
              {
                value: '',
                label: devices.isPending ? 'Loading devices…' : 'Choose a verified device',
              },
              ...(devices.data?.items ?? []).map((d) => ({
                value: d.id,
                label: `${d.displayName} · ${d.platform}`,
              })),
            ]}
          />
          {devices.data && devices.data.items.length === 0 && (
            <p className="bio-field-hint bio-field-hint--warning">
              No eligible mobile device found. The employee must complete approved mobile device
              registration first.
            </p>
          )}
          {devices.data && devices.data.totalCount > 10 && !submitted && !camera && !opening && (
            <Pagination
              page={devicePageNumber}
              pageSize={10}
              total={devices.data.totalCount}
              onChange={(p) => {
                setDeviceId('');
                setImage(null);
                setDevicePageNumber(p);
              }}
            />
          )}
        </div>

        <TextInput
          label="Registration / replacement reason"
          value={reason}
          placeholder="e.g. Initial face registration for attendance"
          disabled={submitted}
          required
          maxLength={1000}
          onChange={(e) => setReason(e.target.value)}
        />
      </div>

      <label className="bio-consent-card">
        <input
          type="checkbox"
          checked={noticeAccepted}
          disabled={submitted || camera || opening}
          onChange={(e) => {
            setNoticeAccepted(e.target.checked);
            setImage(null);
          }}
        />
        <div className="bio-consent-text">
          <strong>Biometric Capture Notice & Consent</strong>
          <p>
            I have explained the company biometric notice and confirmed the employee agrees to this
            capture. Approved company retention and access rules still apply.
          </p>
        </div>
      </label>

      <div className="bio-camera-container">
        <video
          ref={video}
          autoPlay
          muted
          playsInline
          aria-label="Live camera preview"
          className={camera && !image ? '' : 'bioreg-hidden'}
        />
        {image ? (
          <img src={image} alt="Employee face capture for review before registration" />
        ) : camera ? (
          <>
            <div className="bio-camera-live-badge">
              <span className="bio-live-dot" />
              <span>LIVE</span>
            </div>
            <div className="bio-oval-guide-frame" aria-hidden="true" />
            <div className="bio-oval-guide-text">Align face inside the oval</div>
          </>
        ) : (
          <div className="bio-camera-offline">
            <Camera size={38} aria-hidden="true" />
            <h4>Camera is off</h4>
            <p>Face the camera in even lighting. Only one person should be visible.</p>
          </div>
        )}
      </div>

      <div className="bio-actions-bar">
        {!camera ? (
          <Button
            variant="secondary"
            disabled={!mayCapture || opening}
            onClick={() => {
              setImage(null);
              void openCamera();
            }}
          >
            <Camera size={16} />
            {opening ? 'Opening camera…' : image ? 'Retake photo' : 'Open camera'}
          </Button>
        ) : (
          <>
            <Button onClick={capture}>Capture photo</Button>
            <Button
              variant="secondary"
              onClick={() => {
                stopCamera();
                setCamera(false);
              }}
            >
              <X size={16} />
              Stop camera
            </Button>
          </>
        )}
        <Button
          disabled={!image || !known || !reason.trim() || busy || terminalUnavailable}
          onClick={() => void save()}
        >
          {busy
            ? 'Registering…'
            : submitted
              ? 'Retry unchanged registration'
              : active[0]
                ? 'Replace face enrollment'
                : 'Register face'}
        </Button>
      </div>

      {(error || actionError) && (
        <div className="bioreg-error" role="alert">
          <p>{error ?? actionError}</p>
          {actionCode && <p>Reason: {actionCode}</p>}
          {submitted && !canStartAgain && (
            <p>
              Registration is not confirmed. Retry this unchanged request, or check enrollment
              history before leaving and starting another capture.
            </p>
          )}
          {canStartAgain && (
            <Button
              variant="secondary"
              disabled={history.isFetching}
              onClick={() => {
                void history.refetch().then((result) => {
                  if (result.isSuccess && !lifetime().aborted) onReset();
                });
              }}
            >
              Reload enrollment status and start again
            </Button>
          )}
        </div>
      )}

      <p className="bioreg-footnote">
        Photo capture alone is not approval. The server validates liveness, device ownership, access
        and enrollment policy before saving.
      </p>
    </div>
  );
}
