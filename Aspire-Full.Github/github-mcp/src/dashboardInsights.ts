export interface DashboardHealth {
  status: "healthy" | "degraded" | "unreachable";
  httpStatus?: number;
  message?: string;
}

export interface DashboardMetrics {
  activeServices?: number;
  failedServices?: number;
  lastUpdated?: string;
}

export interface DashboardInsightResult {
  health: DashboardHealth;
  metrics: DashboardMetrics;
}

async function safeJson<T>(response: Response): Promise<T | undefined> {
  try {
    return (await response.json()) as T;
  } catch {
    return undefined;
  }
}

export async function collectDashboardInsights(baseUrl: string): Promise<DashboardInsightResult> {
  const health: DashboardHealth = { status: "unreachable" };
  const metrics: DashboardMetrics = {};

  try {
    const response = await fetch(new URL("/health", baseUrl));
    health.httpStatus = response.status;

    if (response.ok) {
      health.status = "healthy";
      const payload = await safeJson<{ status?: string; details?: Record<string, unknown> }>(response);
      health.message = payload?.status ?? "Healthy";
    } else {
      health.status = "degraded";
      health.message = `Dashboard responded with ${response.status}`;
    }
  } catch (error) {
    health.status = "unreachable";
    health.message = error instanceof Error ? error.message : "Unknown error";
  }

  try {
    const metricsResponse = await fetch(new URL("/metrics/summary", baseUrl));
    if (metricsResponse.ok) {
      const payload = await safeJson<{ services?: { healthy?: number; unhealthy?: number }; generatedAt?: string }>(
        metricsResponse
      );
      metrics.activeServices = payload?.services?.healthy;
      metrics.failedServices = payload?.services?.unhealthy;
      metrics.lastUpdated = payload?.generatedAt;
    }
  } catch {
    // Best-effort metrics collection
  }

  return { health, metrics };
}
