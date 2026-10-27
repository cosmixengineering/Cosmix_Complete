import { Platform } from 'react-native';
import type { BackendApi } from '../api/ApiClient';
import { AppError } from '../../utils/errors';
import { isGuid, isRecord, isTimestamp } from '../../utils/validation';

export interface LocationConsentStatus {
  hasActivePolicy: boolean;
  activePolicyVersion?: number;
  noticeText?: string;
  status: 'NotRequired' | 'PendingConsent' | 'Consented';
  consentedAt?: string;
}

export interface ConsentAcceptedResponse {
  consentId: string;
  acceptedAtUtc: string;
}

export class LocationConsentService {
  constructor(private readonly session: BackendApi) {}

  async getConsentStatus(): Promise<LocationConsentStatus> {
    const value = await this.session.request<unknown>(
      '/api/v1/me/location-consent',
      {
        method: 'GET',
      },
    );
    if (!isRecord(value) || typeof value.hasActivePolicy !== 'boolean') {
      throw new AppError('invalid_server_response');
    }
    const status =
      value.status === 0 || value.status === 'NotRequired'
        ? 'NotRequired'
        : value.status === 1 || value.status === 'PendingConsent'
        ? 'PendingConsent'
        : value.status === 2 || value.status === 'Consented'
        ? 'Consented'
        : null;
    if (
      !status ||
      value.hasActivePolicy !== (status !== 'NotRequired') ||
      (value.hasActivePolicy &&
        (!Number.isSafeInteger(value.activePolicyVersion) ||
          (value.activePolicyVersion as number) <= 0 ||
          typeof value.noticeText !== 'string' ||
          !value.noticeText.trim())) ||
      (value.consentedAt != null && !isTimestamp(value.consentedAt))
    ) {
      throw new AppError('invalid_server_response');
    }
    return {
      hasActivePolicy: value.hasActivePolicy,
      status,
      activePolicyVersion: value.hasActivePolicy
        ? (value.activePolicyVersion as number)
        : undefined,
      noticeText: value.hasActivePolicy
        ? (value.noticeText as string)
        : undefined,
      consentedAt: value.consentedAt as string | undefined,
    };
  }

  async acceptConsent(
    expectedPolicyVersion: number,
  ): Promise<ConsentAcceptedResponse> {
    if (
      !Number.isSafeInteger(expectedPolicyVersion) ||
      expectedPolicyVersion <= 0
    ) {
      throw new AppError('invalid_server_response');
    }
    const value = await this.session.request<unknown>(
      '/api/v1/me/location-consent/accept',
      {
        method: 'POST',
        body: {
          expectedPolicyVersion,
          deviceMetadata: `Platform=${Platform.OS}, OSVersion=${Platform.Version}`,
        },
      },
    );
    if (
      !isRecord(value) ||
      !isGuid(value.consentId) ||
      !isTimestamp(value.acceptedAtUtc)
    ) {
      throw new AppError('invalid_server_response');
    }
    return { consentId: value.consentId, acceptedAtUtc: value.acceptedAtUtc };
  }
}
