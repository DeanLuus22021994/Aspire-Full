import cors from "cors";
import express, { type Request, type Response } from "express";
import { randomUUID } from "node:crypto";
import pino from "pino";
import { z } from "zod";
import {
    McpServer,
    StreamableHTTPServerTransport,
    isInitializeRequest,
    type AnySchema
} from "./mcp-support/index.js";

import { TimedCache } from "./cache.js";
import { loadConfig } from "./config.js";
import { collectDashboardInsights, type DashboardInsightResult } from "./dashboardInsights.js";
import { GitHubClient, type IssueInsight, type RepoSummary, type WorkflowStatus } from "./githubClient.js";

const config = loadConfig();
const logger = pino({ level: process.env.LOG_LEVEL ?? "info" });

const githubClient = new GitHubClient(config);
const repoCache = new TimedCache<RepoSummary>(awaitableMs(config.cacheTtlSeconds));
const workflowCache = new TimedCache<{ limit: number; data: WorkflowStatus[] }>(awaitableMs(config.cacheTtlSeconds));
const issueCache = new TimedCache<{ limit: number; data: IssueInsight[] }>(awaitableMs(config.cacheTtlSeconds));
const dashboardCache = new TimedCache<DashboardInsightResult>(awaitableMs(config.cacheTtlSeconds));

const repoHealthInputSchema = z.object({
  includeIssues: z.boolean().optional().describe("Include top open issues"),
  includeWorkflows: z.boolean().optional().describe("Include recent workflow runs"),
  issueLimit: z.number().int().min(1).max(20).optional()
});

const workflowInputSchema = z.object({
  limit: z.number().int().min(1).max(10).optional()
});

const dashboardInputSchema = z.object({
  refresh: z.boolean().optional().describe("Force bypassing the cache")
});

type RepoHealthInput = z.infer<typeof repoHealthInputSchema>;
type WorkflowInput = z.infer<typeof workflowInputSchema>;
type DashboardInput = z.infer<typeof dashboardInputSchema>;

const repoHealthSchema = repoHealthInputSchema as unknown as AnySchema;
const workflowSchema = workflowInputSchema as unknown as AnySchema;
const dashboardSchema = dashboardInputSchema as unknown as AnySchema;

const repoHealthHandler = async (rawArgs?: unknown) => {
  const args: RepoHealthInput = (rawArgs ?? {}) as RepoHealthInput;
  const includeIssues = args.includeIssues ?? true;
  const includeWorkflows = args.includeWorkflows ?? true;
  const issueLimit = args.issueLimit ?? 5;
  const [summary, workflows, issues] = await Promise.all([
    getRepoSummaryCached(),
    includeWorkflows ? getWorkflowStatusesCached(5) : Promise.resolve([]),
    includeIssues ? getIssueInsightsCached(issueLimit) : Promise.resolve([])
  ]);

  const payload = {
    repository: config.repository,
    summary,
    workflows,
    issues
  };

  return {
    content: [
      {
        type: "text" as const,
        text: JSON.stringify(payload, null, 2)
      }
    ]
  };
};

const workflowHandler = async (rawArgs?: unknown) => {
  const args: WorkflowInput = (rawArgs ?? {}) as WorkflowInput;
  const limit = args.limit ?? 3;
  const workflows = await getWorkflowStatusesCached(limit);
  return {
    content: [
      {
        type: "text" as const,
        text: JSON.stringify(
          {
            repository: config.repository,
            workflows
          },
          null,
          2
        )
      }
    ]
  };
};

const dashboardHandler = async (rawArgs?: unknown) => {
  const args: DashboardInput = (rawArgs ?? {}) as DashboardInput;
  if (args.refresh) {
    dashboardCache.clear();
  }
  const snapshot = await getDashboardSnapshot();
  return {
    content: [
      {
        type: "text" as const,
        text: JSON.stringify({
          aspireDashboardUrl: config.aspireDashboardUrl,
          snapshot
        }, null, 2)
      }
    ]
  };
};

function awaitableMs(seconds: number): number {
  return Math.max(5, seconds) * 1000;
}

async function getRepoSummaryCached() {
  const cached = repoCache.get();
  if (cached) {
    return cached;
  }
  const summary = await githubClient.getRepoSummary();
  repoCache.set(summary);
  return summary;
}

async function getWorkflowStatusesCached(limit: number) {
  const cached = workflowCache.get();
  if (cached && cached.limit === limit) {
    return cached.data;
  }
  const data = await githubClient.getWorkflowStatuses(limit);
  workflowCache.set({ limit, data });
  return data;
}

async function getIssueInsightsCached(limit: number) {
  const cached = issueCache.get();
  if (cached && cached.limit === limit) {
    return cached.data;
  }
  const data = await githubClient.getIssueInsights(limit);
  issueCache.set({ limit, data });
  return data;
}

async function getDashboardSnapshot() {
  const cached = dashboardCache.get();
  if (cached) {
    return cached;
  }
  const data = await collectDashboardInsights(config.aspireDashboardUrl);
  dashboardCache.set(data);
  return data;
}

function createServer(): McpServer {
  const server = new McpServer({
    name: "aspire-github-mcp",
    version: "0.1.0",
    icons: [
      {
        src: "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png",
        sizes: ["64x64"],
        mimeType: "image/png"
      }
    ],
    websiteUrl: "https://github.com/DeanLuus22021994/Aspire-Full"
  }, {
    capabilities: {
      logging: {},
      tools: {}
    },
    instructions:
      "Use github.repo_health for an overview, github.workflow_status for latest runs, and aspire.dashboard_snapshot when Aspire telemetry looks suspicious."
  });

  server.registerTool(
    "github.repo_health",
    {
      title: "GitHub repository health",
      description: "Summarize open work, releases, and telemetry for the configured repo.",
      inputSchema: repoHealthSchema
    },
    repoHealthHandler
  );

  server.registerTool(
    "github.workflow_status",
    {
      title: "GitHub workflow status",
      description: "Show the most recent workflow run outcome.",
      inputSchema: workflowSchema
    },
    workflowHandler
  );

  server.registerTool(
    "aspire.dashboard_snapshot",
    {
      title: "Aspire dashboard snapshot",
      description: "Inspect dashboard health and derived metrics to explain runtime failures.",
      inputSchema: dashboardSchema
    },
    dashboardHandler
  );

  return server;
}

function authorize(req: Request, res: Response): boolean {
  if (!config.apiKey) {
    return true;
  }

  const provided = req.header("x-mcp-api-key") ?? req.header("authorization")?.replace(/^Bearer\s+/i, "");
  if (provided && provided === config.apiKey) {
    return true;
  }

  res.status(401).json({
    jsonrpc: "2.0",
    error: { code: -32001, message: "Unauthorized" },
    id: null
  });
  return false;
}

const sessions = new Map<string, { transport: StreamableHTTPServerTransport; server: McpServer }>();

const app = express();
app.use(express.json({ limit: "2mb" }));
app.use(cors({ origin: "*", exposedHeaders: ["Mcp-Session-Id"] }));

app.get("/healthz", async (_req, res) => {
  try {
    const [summary, dashboard] = await Promise.all([getRepoSummaryCached(), getDashboardSnapshot()]);
    res.json({
      status: "ok",
      repository: config.repository,
      openIssues: summary.openIssues,
      dashboardStatus: dashboard.health.status
    });
  } catch (error) {
    res.status(500).json({ status: "error", message: error instanceof Error ? error.message : String(error) });
  }
});

app.post("/mcp", async (req, res) => {
  if (!authorize(req, res)) {
    return;
  }

  const sessionId = req.header("mcp-session-id");
  try {
    if (sessionId && sessions.has(sessionId)) {
      const existing = sessions.get(sessionId)!;
      await existing.transport.handleRequest(req, res, req.body);
      return;
    }

    if (!isInitializeRequest(req.body)) {
      res.status(400).json({
        jsonrpc: "2.0",
        error: { code: -32000, message: "First request must initialize an MCP session" },
        id: req.body?.id ?? null
      });
      return;
    }

    const server = createServer();
    const transport = new StreamableHTTPServerTransport({
      sessionIdGenerator: () => randomUUID(),
      onsessioninitialized: (newSessionId: string) => {
        logger.info({ newSessionId }, "MCP session ready");
        sessions.set(newSessionId, { transport, server });
      }
    });
    transport.onclose = () => {
      const id = transport.sessionId;
      if (id && sessions.delete(id)) {
        logger.info({ id }, "Session closed");
      }
      server.close().catch((error: unknown) => logger.error({ err: error }, "Error closing server"));
    };

    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
  } catch (error) {
    logger.error({ err: error }, "Error handling MCP POST");
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: "2.0",
        error: { code: -32603, message: "Internal server error" },
        id: req.body?.id ?? null
      });
    }
  }
});

app.get("/mcp", async (req, res) => {
  if (!authorize(req, res)) {
    return;
  }

  const sessionId = req.header("mcp-session-id");
  if (!sessionId || !sessions.has(sessionId)) {
    res.status(404).send("Unknown MCP session");
    return;
  }

  const entry = sessions.get(sessionId)!;
  await entry.transport.handleRequest(req, res);
});

app.delete("/mcp", async (req, res) => {
  if (!authorize(req, res)) {
    return;
  }

  const sessionId = req.header("mcp-session-id");
  if (!sessionId || !sessions.has(sessionId)) {
    res.status(404).send("Unknown MCP session");
    return;
  }

  const entry = sessions.get(sessionId)!;
  await entry.transport.handleRequest(req, res);
});

app.listen(config.port, config.bindAddress, () => {
  logger.info({ port: config.port, bind: config.bindAddress }, "GitHub MCP server started");
});
