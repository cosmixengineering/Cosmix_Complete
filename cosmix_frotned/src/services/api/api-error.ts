export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly correlationId?: string,
    public readonly code?: string,
    public readonly retryAfterSeconds?: number,
  ) {
    super(
      status === 401
        ? 'Your session has expired.'
        : status === 403
          ? 'You do not have permission to view these records.'
          : status === 404
            ? 'The requested record was not found.'
            : status === 409
              ? 'This record changed. Refresh before trying again.'
              : status === 422 || status === 400
                ? 'The request could not be accepted.'
                : status === 429
                  ? 'Too many requests. Please wait before trying again.'
                  : status === 503
                    ? 'Sign-in is temporarily unavailable. Please contact your administrator.'
                    : 'The request could not be completed. Please try again.',
    );
    this.name = 'ApiError';
  }
}
