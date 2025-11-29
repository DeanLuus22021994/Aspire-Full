// @ts-nocheck -- Demo code intentionally mirrors the upstream MCP SDK example without full typings.
import express, { type Response } from "express";
import { randomUUID } from "node:crypto";

import { InvalidRequestError } from "@modelcontextprotocol/sdk/server/auth/errors.js";
import { createOAuthMetadata, mcpAuthRouter } from "@modelcontextprotocol/sdk/server/auth/router.js";

interface OAuthClientMetadata {
  client_id: string;
  redirect_uris: string[];
}

interface AuthorizationParams {
  state?: string;
  redirectUri: string;
  codeChallenge?: string;
  scopes?: string[];
  resource?: string;
}

interface TokenInfo {
  token: string;
  clientId: string;
  scopes: string[];
  expiresAt: number;
  resource?: string;
  type: "access";
}

export type AuthMetadata = ReturnType<typeof createOAuthMetadata> & { introspection_endpoint?: string };

export interface SetupAuthServerOptions {
  authServerUrl: URL;
  mcpServerUrl: URL;
  strictResource: boolean;
}

const resourceUrlFromServerUrl = (url: URL): URL => {
  const resourceURL = new URL(url.href);
  resourceURL.hash = "";
  return resourceURL;
};

export const checkResourceAllowed = (
  requestedResource: URL | string | undefined,
  configuredResource: URL | string
): boolean => {
  if (!requestedResource) {
    return false;
  }

  const requested = typeof requestedResource === "string" ? new URL(requestedResource) : new URL(requestedResource.href);
  const configured = typeof configuredResource === "string" ? new URL(configuredResource) : new URL(configuredResource.href);

  if (requested.origin !== configured.origin) {
    return false;
  }

  const requestedPath = requested.pathname.endsWith("/") ? requested.pathname : `${requested.pathname}/`;
  const configuredPath = configured.pathname.endsWith("/") ? configured.pathname : `${configured.pathname}/`;
  return requestedPath.startsWith(configuredPath);
};

class DemoInMemoryClientsStore {
  private readonly clients = new Map<string, OAuthClientMetadata>();

  async getClient(clientId: string): Promise<OAuthClientMetadata | undefined> {
    return this.clients.get(clientId);
  }

  async registerClient(clientMetadata: OAuthClientMetadata): Promise<OAuthClientMetadata> {
    this.clients.set(clientMetadata.client_id, clientMetadata);
    return clientMetadata;
  }
}

interface AuthorizationCodeRecord {
  client: OAuthClientMetadata;
  params: AuthorizationParams;
}

export class DemoInMemoryAuthProvider {
  readonly clientsStore = new DemoInMemoryClientsStore();
  private readonly codes = new Map<string, AuthorizationCodeRecord>();
  private readonly tokens = new Map<string, TokenInfo>();

  constructor(private readonly validateResource?: (resource?: string) => boolean) {}

  async authorize(client: OAuthClientMetadata, params: AuthorizationParams, res: Response): Promise<void> {
    const code = randomUUID();
    const searchParams = new URLSearchParams({ code });
    if (params.state !== undefined) {
      searchParams.set("state", params.state);
    }

    this.codes.set(code, { client, params });

    if (res.cookie) {
      res.cookie("demo_session", JSON.stringify({ userId: "demo_user", name: "Demo User", timestamp: Date.now() }), {
        httpOnly: true,
        secure: false,
        sameSite: "lax",
        maxAge: 24 * 60 * 60 * 1000,
        path: "/"
      });
    }

    if (!client.redirect_uris.includes(params.redirectUri)) {
      throw new InvalidRequestError("Unregistered redirect_uri");
    }

    const targetUrl = new URL(params.redirectUri);
    targetUrl.search = searchParams.toString();
    res.redirect(targetUrl.toString());
  }

  async challengeForAuthorizationCode(client: OAuthClientMetadata, authorizationCode: string): Promise<string> {
    const codeData = this.codes.get(authorizationCode);
    if (!codeData) {
      throw new Error("Invalid authorization code");
    }

    if (codeData.client.client_id !== client.client_id) {
      throw new Error("Authorization code was not issued to this client");
    }

    return codeData.params.codeChallenge ?? "";
  }

  async exchangeAuthorizationCode(
    client: OAuthClientMetadata,
    authorizationCode: string
  ): Promise<{ access_token: string; token_type: "bearer"; expires_in: number; scope: string }> {
    const codeData = this.codes.get(authorizationCode);
    if (!codeData) {
      throw new Error("Invalid authorization code");
    }

    if (codeData.client.client_id !== client.client_id) {
      throw new Error(`Authorization code was not issued to this client, ${codeData.client.client_id} != ${client.client_id}`);
    }

    if (this.validateResource && !this.validateResource(codeData.params.resource)) {
      throw new Error(`Invalid resource: ${codeData.params.resource}`);
    }

    this.codes.delete(authorizationCode);
    const token = randomUUID();
    const tokenData: TokenInfo = {
      token,
      clientId: client.client_id,
      scopes: codeData.params.scopes ?? [],
      expiresAt: Date.now() + 3600000,
      resource: codeData.params.resource,
      type: "access"
    };
    this.tokens.set(token, tokenData);

    return {
      access_token: token,
      token_type: "bearer",
      expires_in: 3600,
      scope: (codeData.params.scopes ?? []).join(" ")
    };
  }

  async exchangeRefreshToken(): Promise<never> {
    throw new Error("Not implemented for demo");
  }

  async verifyAccessToken(token: string): Promise<{ token: string; clientId: string; scopes: string[]; expiresAt: number; resource?: string }> {
    const tokenData = this.tokens.get(token);
    if (!tokenData || tokenData.expiresAt < Date.now()) {
      throw new Error("Invalid or expired token");
    }

    return {
      token,
      clientId: tokenData.clientId,
      scopes: tokenData.scopes,
      expiresAt: Math.floor(tokenData.expiresAt / 1000),
      resource: tokenData.resource
    };
  }
}

export const setupAuthServer = ({ authServerUrl, mcpServerUrl, strictResource }: SetupAuthServerOptions): AuthMetadata => {
  const validateResource = strictResource
    ? (resource?: string) => {
        if (!resource) {
          return false;
        }
        const expected = resourceUrlFromServerUrl(mcpServerUrl);
        return checkResourceAllowed(resource, expected);
      }
    : undefined;

  const provider = new DemoInMemoryAuthProvider(validateResource);
  const authApp = express();
  authApp.use(express.json());
  authApp.use(express.urlencoded());

  authApp.use(
    mcpAuthRouter({
      provider,
      issuerUrl: authServerUrl,
      scopesSupported: ["mcp:tools"]
    })
  );

  authApp.post("/introspect", async (req, res) => {
    try {
      const { token } = req.body ?? {};
      if (!token) {
        res.status(400).json({ error: "Token is required" });
        return;
      }

      const tokenInfo = await provider.verifyAccessToken(token);
      res.json({
        active: true,
        client_id: tokenInfo.clientId,
        scope: tokenInfo.scopes.join(" "),
        exp: tokenInfo.expiresAt,
        aud: tokenInfo.resource
      });
    } catch (error) {
      res.status(401).json({
        active: false,
        error: "Unauthorized",
        error_description: `Invalid token: ${error}`
      });
    }
  });

  const port = authServerUrl.port;
  authApp.listen(port, (error?: unknown) => {
    if (error) {
      console.error("Failed to start auth server", error);
      process.exit(1);
    }
    console.log(`OAuth Authorization Server listening on port ${port}`);
  });

  const oauthMetadata = createOAuthMetadata({
    provider,
    issuerUrl: authServerUrl,
    scopesSupported: ["mcp:tools"]
  }) as AuthMetadata;
  oauthMetadata.introspection_endpoint = new URL("/introspect", authServerUrl).href;
  return oauthMetadata;
};
