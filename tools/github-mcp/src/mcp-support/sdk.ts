/**
 * Centralizes imports from the MCP SDK so the rest of the repo never references node_modules directly.
 */
export { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
export { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
export type { AnySchema } from "@modelcontextprotocol/sdk/server/zod-compat.js";
export { isInitializeRequest } from "@modelcontextprotocol/sdk/types.js";

