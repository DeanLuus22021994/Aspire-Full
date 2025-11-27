import { z } from "zod";

const configSchema = z.object({
  githubToken: z.string().min(1, "GITHUB_MCP_TOKEN is required"),
  repository: z
    .string()
    .min(1, "GITHUB_MCP_REPOSITORY is required")
    .regex(/^[^/]+\/[\w.-]+$/, "Repository must be in owner/name format"),
  port: z.coerce.number().default(17071),
  bindAddress: z.string().default("0.0.0.0"),
  cacheTtlSeconds: z.coerce.number().default(30),
  aspireDashboardUrl: z.string().default("http://aspire-dashboard:18888"),
  aspireMcpUrl: z.string().default("http://aspire-dashboard:16036"),
  apiKey: z.string().optional()
});

export type Config = z.infer<typeof configSchema>;

export function loadConfig(): Config {
  const parsed = configSchema.safeParse({
    githubToken: process.env.GITHUB_MCP_TOKEN,
    repository: process.env.GITHUB_MCP_REPOSITORY,
    port: process.env.GITHUB_MCP_PORT,
    bindAddress: process.env.GITHUB_MCP_BIND ?? "0.0.0.0",
    cacheTtlSeconds: process.env.GITHUB_MCP_CACHE_SECONDS,
    aspireDashboardUrl: process.env.ASPIRE_DASHBOARD_URL,
    aspireMcpUrl: process.env.ASPIRE_DASHBOARD_MCP_ENDPOINT_URL,
    apiKey: process.env.GITHUB_MCP_API_KEY
  });

  if (!parsed.success) {
    const errors = parsed.error.issues.map((issue) => `${issue.path.join(".")}: ${issue.message}`).join("; ");
    throw new Error(`Invalid MCP configuration: ${errors}`);
  }

  return parsed.data;
}
