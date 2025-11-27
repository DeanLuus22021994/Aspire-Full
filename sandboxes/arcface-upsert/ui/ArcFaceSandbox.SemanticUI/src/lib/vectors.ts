export function generatePlaceholderVector(size: number, seed = 42): number[] {
  const values: number[] = [];
  let current = seed;
  for (let i = 0; i < size; i += 1) {
    current = (current * 16807) % 2147483647;
    values.push((current % 2000) / 1000 - 1); // range -1..1
  }
  return values;
}

export function downsample(values: number[], bucketCount = 64): number[] {
  if (values.length <= bucketCount) {
    return values;
  }

  const bucketSize = Math.ceil(values.length / bucketCount);
  const buckets: number[] = [];
  for (let i = 0; i < values.length; i += bucketSize) {
    const chunk = values.slice(i, i + bucketSize);
    const average = chunk.reduce((sum, value) => sum + value, 0) / chunk.length;
    buckets.push(average);
  }
  return buckets;
}
