import { Component, type ErrorInfo, type PropsWithChildren } from 'react';
import { ErrorState } from './ErrorState';
export class ErrorBoundary extends Component<PropsWithChildren, { failed: boolean }> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  componentDidCatch(_error: Error, _info: ErrorInfo) {
    // Connect approved redacted error reporting here; never send employee payloads.
  }
  render() {
    return this.state.failed ? (
      <ErrorState
        message="This view could not be loaded."
        onRetry={() => window.location.reload()}
      />
    ) : (
      this.props.children
    );
  }
}
