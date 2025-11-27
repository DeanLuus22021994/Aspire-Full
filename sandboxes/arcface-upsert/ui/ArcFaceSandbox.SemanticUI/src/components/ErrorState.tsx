import { Message } from 'semantic-ui-react';

interface ErrorStateProps {
  error: Error;
  onRetry?: () => void;
}

const ErrorState = ({ error, onRetry }: ErrorStateProps) => (
  <Message negative icon="warning sign" onClick={onRetry} style={{ cursor: onRetry ? 'pointer' : 'default' }}>
    <Message.Content>
      <Message.Header>Unable to reach the sandbox API</Message.Header>
      {error.message}
      {onRetry && <p>Tap to retry.</p>}
    </Message.Content>
  </Message>
);

export default ErrorState;
