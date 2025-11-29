/**
 * Auth helpers re-exported from the MCP SDK so consumers avoid direct imports.
 */
export { requireBearerAuth } from "@modelcontextprotocol/sdk/server/auth/middleware/bearerAuth.js";
export { getOAuthProtectedResourceMetadataUrl, mcpAuthMetadataRouter } from "@modelcontextprotocol/sdk/server/auth/router.js";
export { checkResourceAllowed, DemoInMemoryAuthProvider, setupAuthServer } from "./demoInMemoryOAuthProvider.js";
export type { AuthMetadata, SetupAuthServerOptions } from "./demoInMemoryOAuthProvider.js";

