/**
 * Shared client for talking to harness.api (default http://localhost:4000).
 * In development the URL is read from NEXT_PUBLIC_HARNESS_API_BASE.
 */

export const HARNESS_API_BASE =
  process.env.NEXT_PUBLIC_HARNESS_API_BASE?.replace(/\/$/, "") ??
  "http://localhost:4000";

export type AgentSummary = {
  id: string;
  displayName: string;
  description?: string;
  tagline?: string;
  authMode: string;
  modelDeployment: string;
  endpoint: string;
  blueprintFile: string;
  blueprintId?: string;
  owner?: string;
  sponsor?: string;
  status?: "online" | "offline" | "unknown";
  cliHint?: string;
};

export type BlueprintSummary = {
  id: string;
  displayName: string;
  authMode: string;
  owner: string;
  sponsor: string;
  modelDeployment: string;
  modelEndpoint: string;
  apiVersion: string;
  fileName: string;
  draft?: boolean;
};

export type PortalLink = {
  id: string;
  title: string;
  description: string;
  url: string;
  category: "admin" | "identity" | "purview" | "foundry" | "defender";
};

export type TenantInfo = {
  tenantId: string;
  tenantDomain: string;
  subscriptionId?: string;
  foundry?: {
    accountName: string;
    resourceGroup: string;
    region: string;
    endpoint: string;
    deployments: Array<{ name: string; model: string; capacity: number }>;
  };
};

async function get<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${HARNESS_API_BASE}${path.startsWith("/") ? path : "/" + path}`;
  const r = await fetch(url, {
    cache: "no-store",
    headers: { Accept: "application/json", ...(init?.headers ?? {}) },
    ...init,
  });
  if (!r.ok) {
    throw new Error(`harness.api ${path} → HTTP ${r.status} ${r.statusText}`);
  }
  return (await r.json()) as T;
}

export async function getTenant(): Promise<TenantInfo> {
  return get<TenantInfo>("/api/tenant");
}

export async function getAgents(): Promise<AgentSummary[]> {
  return get<AgentSummary[]>("/api/agents");
}

export async function getBlueprints(): Promise<BlueprintSummary[]> {
  return get<BlueprintSummary[]>("/api/blueprints");
}

export async function getPortals(): Promise<PortalLink[]> {
  return get<PortalLink[]>("/api/portals");
}

export type ChatTurnRequest = {
  message: string;
  userUpn?: string;
  userObjectId?: string;
  userAccessToken?: string;
  conversationId?: string;
};

export type ChatTurnResponse = {
  reply: string;
  blocked: boolean;
  direction: string;
  reason: string;
  citations?: Array<{ documentId: string; title: string; source: string; score: number }>;
};

export async function postChat(
  agentId: string,
  body: ChatTurnRequest
): Promise<ChatTurnResponse> {
  const url = `${HARNESS_API_BASE}/api/agents/${encodeURIComponent(agentId)}/chat`;
  const r = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json", Accept: "application/json" },
    body: JSON.stringify(body),
  });
  if (!r.ok) {
    throw new Error(`chat ${agentId} → HTTP ${r.status} ${r.statusText}`);
  }
  return (await r.json()) as ChatTurnResponse;
}

export type HarnessEvent = {
  ts: string;
  source: string;
  kind: string;
  name: string;
  status: string;
  attributes?: Record<string, unknown>;
  durationMs?: number;
};
