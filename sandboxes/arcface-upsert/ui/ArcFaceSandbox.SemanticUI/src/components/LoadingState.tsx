import { Loader, Segment } from 'semantic-ui-react';

interface LoadingStateProps {
  message?: string;
}

const LoadingState = ({ message = 'Loading the latest sandbox data…' }: LoadingStateProps) => (
  <Segment inverted tertiary textAlign="center">
    <Loader active inline="centered" size="large" content={message} />
  </Segment>
);

export default LoadingState;
