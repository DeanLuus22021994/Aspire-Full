import { useCallback, useEffect, useRef, useState } from 'react';

interface UsePollingOptions {
  intervalMs?: number;
}

type Fetcher<T> = (signal?: AbortSignal) => Promise<T>;

export function usePollingData<T>(fetcher: Fetcher<T>, options?: UsePollingOptions) {
  const intervalMs = options?.intervalMs ?? 10000;
  const [data, setData] = useState<T | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<Error | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);
  const timerRef = useRef<number>();
  const abortRef = useRef<AbortController | null>(null);

  const executeFetch = useCallback(async () => {
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      setIsLoading(true);
      const result = await fetcher(controller.signal);
      setData(result);
      setError(null);
      setLastUpdated(new Date());
    } catch (err) {
      if ((err as Error).name === 'CanceledError') {
        return;
      }
      setError(err as Error);
    } finally {
      setIsLoading(false);
    }
  }, [fetcher]);

  useEffect(() => {
    executeFetch();

    timerRef.current = window.setInterval(executeFetch, intervalMs);

    return () => {
      if (timerRef.current) {
        window.clearInterval(timerRef.current);
      }
      abortRef.current?.abort();
    };
  }, [executeFetch, intervalMs]);

  return {
    data,
    isLoading,
    error,
    lastUpdated,
    refresh: executeFetch,
  } as const;
}
