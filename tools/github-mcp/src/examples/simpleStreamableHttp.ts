// @ts-nocheck -- This sample mirrors the upstream JS example; strict typing would add noise here.
import cors from "cors";
import express, { type Request, type Response } from "express";
import { randomUUID } from "node:crypto";
import { z } from "zod";

import { requireBearerAuth } from "@modelcontextprotocol/sdk/server/auth/middleware/bearerAuth.js";
import { getOAuthProtectedResourceMetadataUrl, mcpAuthMetadataRouter } from "@modelcontextprotocol/sdk/server/auth/router.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { isInitializeRequest } from "@modelcontextprotocol/sdk/types.js";

import { checkResourceAllowed, setupAuthServer, type AuthMetadata } from "./shared/demoInMemoryOAuthProvider.js";
import { InMemoryEventStore } from "./shared/inMemoryEventStore.js";

const useOAuth = process.argv.includes("--oauth");
const strictOAuth = process.argv.includes("--oauth-strict");

const MCP_PORT = Number.parseInt(process.env.MCP_PORT ?? "3000", 10);
const AUTH_PORT = Number.parseInt(process.env.MCP_AUTH_PORT ?? "3001", 10);

const transports = new Map<string, StreamableHTTPServerTransport>();

const checkRequestedResource = (requested: string | undefined, expected: URL): void => {
  if (!requested) {
    throw new Error("Resource Indicator (RFC8707) missing");
  }
  if (!checkResourceAllowed(requested, expected)) {
    throw new Error(`Expected resource indicator ${expected}, got: ${requested}`);
  }
};

const getServer = (): McpServer => {
  const server = new McpServer(
    {
      name: "simple-streamable-http-server",
      version: "1.0.0",
      icons: [{ src: "./mcp.svg", sizes: ["512x512"], mimeType: "image/svg+xml" }],
      websiteUrl: "https://github.com/modelcontextprotocol/typescript-sdk"
    },
    { capabilities: { logging: {} } }
  );

  server.registerTool(
    "greet",
    {
      title: "Greeting Tool",
      description: "A simple greeting tool",
      inputSchema: { name: z.string().describe("Name to greet") }
    },
    async ({ name }: { name: string }) => ({
      content: [{ type: "text" as const, text: `Hello, ${name}!` }]
    })
  );

  server.tool(
    "multi-greet",
    "A tool that sends different greetings with delays between them",
    { name: z.string().describe("Name to greet") },
    {
      title: "Multiple Greeting Tool",
      readOnlyHint: true,
      openWorldHint: false
    },
    async ({ name }: { name: string }, extra) => {
      const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));

      await server.sendLoggingMessage({ level: "debug", data: `Starting multi-greet for ${name}` }, extra.sessionId);
      await sleep(1000);
      await server.sendLoggingMessage({ level: "info", data: `Sending first greeting to ${name}` }, extra.sessionId);
      await sleep(1000);
      await server.sendLoggingMessage({ level: "info", data: `Sending second greeting to ${name}` }, extra.sessionId);

      return {
        content: [{ type: "text" as const, text: `Good morning, ${name}!` }]
      };
    }
  );

  server.tool(
    "collect-user-info",
    "A tool that collects user information through form elicitation",
    { infoType: z.enum(["contact", "preferences", "feedback"]).describe("Type of information to collect") },
    async ({ infoType }: { infoType: "contact" | "preferences" | "feedback" }) => {
      let message: string;
      let requestedSchema: Record<string, unknown>;

      switch (infoType) {
        case "contact":
          message = "Please provide your contact information";
          requestedSchema = {
            type: "object",
            properties: {
              name: { type: "string", title: "Full Name", description: "Your full name" },
              email: {
                type: "string",
                title: "Email Address",
                description: "Your email address",
                format: "email"
              },
              phone: { type: "string", title: "Phone Number", description: "Your phone number (optional)" }
            },
            required: ["name", "email"]
          };
          break;
        case "preferences":
          message = "Please set your preferences";
          requestedSchema = {
            type: "object",
            properties: {
              theme: {
                type: "string",
                title: "Theme",
                description: "Choose your preferred theme",
                enum: ["light", "dark", "auto"],
                enumNames: ["Light", "Dark", "Auto"]
              },
              notifications: {
                type: "boolean",
                title: "Enable Notifications",
                description: "Would you like to receive notifications?",
                default: true
              },
              frequency: {
                type: "string",
                title: "Notification Frequency",
                description: "How often would you like notifications?",
                enum: ["daily", "weekly", "monthly"],
                enumNames: ["Daily", "Weekly", "Monthly"]
              }
            },
            required: ["theme"]
          };
          break;
        case "feedback":
        default:
          message = "Please provide your feedback";
          requestedSchema = {
            type: "object",
            properties: {
              rating: {
                type: "integer",
                title: "Rating",
                description: "Rate your experience (1-5)",
                minimum: 1,
                maximum: 5
              },
              comments: {
                type: "string",
                title: "Comments",
                description: "Additional comments (optional)",
                maxLength: 500
              },
              recommend: {
                type: "boolean",
                title: "Would you recommend this?",
                description: "Would you recommend this to others?"
              }
            },
            required: ["rating", "recommend"]
          };
          break;
      }

      try {
        const internalServer = (server as unknown as {
          server: { elicitInput: (options: { mode: string; message: string; requestedSchema: Record<string, unknown> }) => Promise<{ action: string; content?: unknown }> };
        }).server;

        const result = await internalServer.elicitInput({
          mode: "form",
          message,
          requestedSchema
        });

        if (result.action === "accept") {
          return {
            content: [{ type: "text" as const, text: `Thank you! Collected ${infoType} information: ${JSON.stringify(result.content, null, 2)}` }]
          };
        }

        if (result.action === "decline") {
          return {
            content: [{ type: "text" as const, text: `No information was collected. User declined ${infoType} information request.` }]
          };
        }

        return {
          content: [{ type: "text" as const, text: "Information collection was cancelled by the user." }]
        };
      } catch (error) {
        return {
          content: [{ type: "text" as const, text: `Error collecting ${infoType} information: ${error}` }]
        };
      }
    }
  );

  server.registerPrompt(
    "greeting-template",
    {
      title: "Greeting Template",
      description: "A simple greeting prompt template",
      argsSchema: { name: z.string().describe("Name to include in greeting") }
    },
    async ({ name }: { name: string }) => ({
      messages: [
        {
          role: "user" as const,
          content: { type: "text" as const, text: `Please greet ${name} in a friendly manner.` }
        }
      ]
    })
  );

  server.tool(
    "start-notification-stream",
    "Starts sending periodic notifications for testing resumability",
    {
      interval: z.number().describe("Interval in milliseconds between notifications").default(100),
      count: z.number().describe("Number of notifications to send (0 for 100)").default(50)
    },
    async ({ interval, count }: { interval: number; count: number }, extra) => {
      const sleep = (ms: number) => new Promise(resolve => setTimeout(resolve, ms));
      let counter = 0;
      while (count === 0 || counter < count) {
        counter += 1;
        try {
          await server.sendLoggingMessage({
            level: "info",
            data: `Periodic notification #${counter} at ${new Date().toISOString()}`
          }, extra.sessionId);
        } catch (error) {
          console.error("Error sending notification:", error);
        }
        await sleep(interval);
      }
      return {
        content: [{ type: "text" as const, text: `Started sending periodic notifications every ${interval}ms` }]
      };
    }
  );

  server.registerResource(
    "greeting-resource",
    "https://example.com/greetings/default",
    {
      title: "Default Greeting",
      description: "A simple greeting resource",
      mimeType: "text/plain"
    },
    async () => ({
      contents: [{ uri: "https://example.com/greetings/default", text: "Hello, world!" }]
    })
  );

  server.registerResource(
    "example-file-1",
    "file:///example/file1.txt",
    {
      title: "Example File 1",
      description: "First example file for ResourceLink demonstration",
      mimeType: "text/plain"
    },
    async () => ({ contents: [{ uri: "file:///example/file1.txt", text: "This is the content of file 1" }] })
  );

  server.registerResource(
    "example-file-2",
    "file:///example/file2.txt",
    {
      title: "Example File 2",
      description: "Second example file for ResourceLink demonstration",
      mimeType: "text/plain"
    },
    async () => ({ contents: [{ uri: "file:///example/file2.txt", text: "This is the content of file 2" }] })
  );

  server.registerTool(
    "list-files",
    {
      title: "List Files with ResourceLinks",
      description: "Returns a list of files as ResourceLinks without embedding their content",
      inputSchema: {
        includeDescriptions: z.boolean().optional().describe("Whether to include descriptions in the resource links")
      }
    },
    async ({ includeDescriptions = true }: { includeDescriptions?: boolean }) => {
      const resourceLinks = [
        {
          type: "resource_link" as const,
          uri: "https://example.com/greetings/default",
          name: "Default Greeting",
          mimeType: "text/plain",
          ...(includeDescriptions && { description: "A simple greeting resource" })
        },
        {
          type: "resource_link" as const,
          uri: "file:///example/file1.txt",
          name: "Example File 1",
          mimeType: "text/plain",
          ...(includeDescriptions && { description: "First example file for ResourceLink demonstration" })
        },
        {
          type: "resource_link" as const,
          uri: "file:///example/file2.txt",
          name: "Example File 2",
          mimeType: "text/plain",
          ...(includeDescriptions && { description: "Second example file for ResourceLink demonstration" })
        }
      ];

      return {
        content: [
          { type: "text" as const, text: "Here are the available files as resource links:" },
          ...resourceLinks,
          { type: "text" as const, text: "\nYou can read any of these resources using their URI." }
        ]
      };
    }
  );

  return server;
};

const app = express();
app.use(express.json());
app.use(cors({ origin: "*", exposedHeaders: ["Mcp-Session-Id"] }));

let authMiddleware: ReturnType<typeof requireBearerAuth> | null = null;
let oauthMetadata: AuthMetadata | null = null;

if (useOAuth) {
  const mcpServerUrl = new URL(`http://localhost:${MCP_PORT}/mcp`);
  const authServerUrl = new URL(`http://localhost:${AUTH_PORT}`);
  oauthMetadata = setupAuthServer({ authServerUrl, mcpServerUrl, strictResource: strictOAuth });

  const tokenVerifier = {
    verifyAccessToken: async (token: string) => {
      const endpoint = oauthMetadata?.introspection_endpoint;
      if (!endpoint) {
        throw new Error("No token verification endpoint available in metadata");
      }

      const response = await fetch(endpoint, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: new URLSearchParams({ token }).toString()
      });

      if (!response.ok) {
        throw new Error(`Invalid or expired token: ${await response.text()}`);
      }

      const data = (await response.json()) as { client_id: string; scope?: string; exp?: number; aud?: string };
      if (strictOAuth) {
        checkRequestedResource(data.aud, mcpServerUrl);
      }

      return {
        token,
        clientId: data.client_id,
        scopes: data.scope ? data.scope.split(" ") : [],
        expiresAt: data.exp
      };
    }
  };

  app.use(
    mcpAuthMetadataRouter({
      oauthMetadata,
      resourceServerUrl: mcpServerUrl,
      scopesSupported: ["mcp:tools"],
      resourceName: "MCP Demo Server"
    })
  );

  authMiddleware = requireBearerAuth({
    verifier: tokenVerifier,
    requiredScopes: [],
    resourceMetadataUrl: getOAuthProtectedResourceMetadataUrl(mcpServerUrl)
  });
}

const handleMcpPost = async (req: Request, res: Response): Promise<void> => {
  const sessionId = req.header("mcp-session-id");
  if (sessionId) {
    console.log(`Received MCP request for session: ${sessionId}`);
  } else {
    console.log("Request body:", req.body);
  }

  try {
    if (sessionId && transports.has(sessionId)) {
      await transports.get(sessionId)!.handleRequest(req, res, req.body);
      return;
    }

    if (!sessionId && isInitializeRequest(req.body)) {
      const eventStore = new InMemoryEventStore();
      const transport = new StreamableHTTPServerTransport({
        sessionIdGenerator: () => randomUUID(),
        eventStore,
        onsessioninitialized: (newSessionId: string) => {
          console.log(`Session initialized with ID: ${newSessionId}`);
          transports.set(newSessionId, transport);
        }
      });

      transport.onclose = () => {
        const sid = transport.sessionId;
        if (sid && transports.delete(sid)) {
          console.log(`Transport closed for session ${sid}`);
        }
      };

      const server = getServer();
      await server.connect(transport);
      await transport.handleRequest(req, res, req.body);
      return;
    }

    res.status(400).json({
      jsonrpc: "2.0",
      error: { code: -32000, message: "Bad Request: No valid session ID provided" },
      id: null
    });
  } catch (error) {
    console.error("Error handling MCP request:", error);
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: "2.0",
        error: { code: -32603, message: "Internal server error" },
        id: null
      });
    }
  }
};

const handleMcpGet = async (req: Request, res: Response): Promise<void> => {
  const sessionId = req.header("mcp-session-id");
  if (!sessionId || !transports.has(sessionId)) {
    res.status(400).send("Invalid or missing session ID");
    return;
  }

  const lastEventId = req.header("last-event-id");
  if (lastEventId) {
    console.log(`Client reconnecting with Last-Event-ID: ${lastEventId}`);
  } else {
    console.log(`Establishing new SSE stream for session ${sessionId}`);
  }

  const transport = transports.get(sessionId)!;
  await transport.handleRequest(req, res);
};

const handleMcpDelete = async (req: Request, res: Response): Promise<void> => {
  const sessionId = req.header("mcp-session-id");
  if (!sessionId || !transports.has(sessionId)) {
    res.status(400).send("Invalid or missing session ID");
    return;
  }

  console.log(`Received session termination request for session ${sessionId}`);
  const transport = transports.get(sessionId)!;
  await transport.handleRequest(req, res);
};

if (useOAuth && authMiddleware) {
  app.post("/mcp", authMiddleware, handleMcpPost);
  app.get("/mcp", authMiddleware, handleMcpGet);
  app.delete("/mcp", authMiddleware, handleMcpDelete);
} else {
  app.post("/mcp", handleMcpPost);
  app.get("/mcp", handleMcpGet);
  app.delete("/mcp", handleMcpDelete);
}

app.listen(MCP_PORT, error => {
  if (error) {
    console.error("Failed to start server:", error);
    process.exit(1);
  }
  console.log(`MCP Streamable HTTP Server listening on port ${MCP_PORT}`);
});

process.on("SIGINT", async () => {
  console.log("Shutting down server...");
  for (const [sessionId, transport] of transports.entries()) {
    try {
      console.log(`Closing transport for session ${sessionId}`);
      await transport.close();
      transports.delete(sessionId);
    } catch (error) {
      console.error(`Error closing transport for session ${sessionId}:`, error);
    }
  }
  console.log("Server shutdown complete");
  process.exit(0);
});
