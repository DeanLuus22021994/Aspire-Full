type CacheEntry<T> = {
  expiresAt: number;
  value: T;
};

export class TimedCache<T> {
  private entry?: CacheEntry<T>;

  constructor(private readonly ttlMs: number) {}

  get(): T | undefined {
    if (!this.entry) {
      return undefined;
    }

    if (Date.now() > this.entry.expiresAt) {
      this.entry = undefined;
      return undefined;
    }

    return this.entry.value;
  }

  set(value: T): void {
    this.entry = {
      value,
      expiresAt: Date.now() + this.ttlMs
    };
  }

  clear(): void {
    this.entry = undefined;
  }
}
