import { Label } from 'semantic-ui-react';

interface LastUpdatedTagProps {
  timestamp: Date | null;
}

const LastUpdatedTag = ({ timestamp }: LastUpdatedTagProps) => {
  if (!timestamp) {
    return null;
  }

  return (
    <Label basic color="green" size="small" content={`Last updated ${timestamp.toLocaleTimeString()}`} />
  );
};

export default LastUpdatedTag;
