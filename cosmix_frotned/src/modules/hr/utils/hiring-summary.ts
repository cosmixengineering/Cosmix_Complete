export function hiringSummaryNotice(draft: boolean, canReview: boolean) {
  if (draft) {
    return {
      label: 'Draft not submitted',
      message:
        'Complete and submit the onboarding record before hiring review can begin. Approval decisions are unavailable for a draft.',
    };
  }
  return canReview
    ? {
        label: 'Read-only summary',
        message:
          'Use Admin Review on the submitted request to inspect the retained evidence and available decisions.',
      }
    : {
        label: 'Read-only summary',
        message: 'Final hiring decisions require authorized Admin review of the submitted request.',
      };
}
