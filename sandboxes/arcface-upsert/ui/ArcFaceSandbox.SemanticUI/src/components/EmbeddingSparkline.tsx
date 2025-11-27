import { useMemo } from 'react';
import { downsample } from '../lib/vectors';

interface EmbeddingSparklineProps {
  values: number[];
}

const EmbeddingSparkline = ({ values }: EmbeddingSparklineProps) => {
  const bars = useMemo(() => {
    const bucketed = downsample(values, 72);
    const maxMagnitude = Math.max(...bucketed.map((value) => Math.abs(value)), 1);

    return bucketed.map((value, index) => {
      const normalized = Math.abs(value) / maxMagnitude;
      const heightIndex = Math.min(20, Math.max(1, Math.round(normalized * 20)));
      return (
        <div
          key={`bar-${index.toString()}`}
          className={`sparkline-bar h-${heightIndex}`}
        />
      );
    });
  }, [values]);

  return <div className="sparkline">{bars}</div>;
};

export default EmbeddingSparkline;
