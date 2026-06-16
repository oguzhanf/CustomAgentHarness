"use client";

import {
  PublicClientApplication,
  type Configuration,
  type AccountInfo,
  InteractionRequiredAuthError,
  BrowserCacheLocation,
  LogLevel,
} from "@azure/msal-browser";

const tenantId =
  process.env.NEXT_PUBLIC_AZURE_TENANT_ID ??
  "00000000-0000-0000-0000-000000000000";

const clientId =
  process.env.NEXT_PUBLIC_AZURE_HUB_CLIENT_ID ??
  "00000000-0000-0000-0000-000000000000";

const redirectUri =
  process.env.NEXT_PUBLIC_HUB_REDIRECT_URI ??
  (typeof window !== "undefined"
    ? `${window.location.origin}/hub`
    : "http://localhost:4001/hub");

export const msalConfig: Configuration = {
  auth: {
    clientId,
    authority: `https://login.microsoftonline.com/${tenantId}`,
    redirectUri,
    postLogoutRedirectUri: redirectUri,
  },
  cache: {
    // LocalStorage survives full-page redirects + accidental tab reloads
    // during the auth dance. SessionStorage was breaking the popup flow
    // (per-window isolation) and caused a sign-in window loop.
    cacheLocation: BrowserCacheLocation.LocalStorage,
  },
  system: {
    loggerOptions: {
      loggerCallback: (level, message) => {
        if (level === LogLevel.Error) {
          // eslint-disable-next-line no-console
          console.error("[msal]", message);
        }
      },
      logLevel: LogLevel.Warning,
      piiLoggingEnabled: false,
    },
  },
};

/**
 * Scopes the hub requests for the signed-in user.
 *  - User.Read so we can resolve UPN/OID
 *  - Mail.Read, Calendars.Read, Files.Read.All, Sites.Read.All so the agent
 *    can call Microsoft Graph "on behalf of" the user via the token we forward.
 *
 * For the workshop we forward the user's delegated Graph token straight to the
 * agent (pattern documented as "pass-through OBO" in the run-of-show). In a
 * production deployment each agent would have its own audience and would call
 * OnBehalfOfCredential to exchange the user token for a downstream Graph token.
 */
export const GRAPH_SCOPES = [
  "User.Read",
  "Mail.Read",
  "Calendars.Read",
  "Files.Read.All",
  "Sites.Read.All",
];

let _msalInstance: PublicClientApplication | null = null;
let _msalReady: Promise<PublicClientApplication> | null = null;

/**
 * Returns a singleton MSAL instance. MSAL v5 requires `initialize()` to be
 * awaited before any other API is called, so we cache the promise.
 */
export function getMsalInstance(): Promise<PublicClientApplication> {
  if (typeof window === "undefined") {
    return Promise.reject(new Error("MSAL is browser-only"));
  }
  if (_msalReady) return _msalReady;
  _msalInstance = new PublicClientApplication(msalConfig);
  _msalReady = (async () => {
    await _msalInstance!.initialize();
    // Process any pending redirect promise (we use popup by default, but be safe)
    try {
      const r = await _msalInstance!.handleRedirectPromise();
      if (r?.account) {
        _msalInstance!.setActiveAccount(r.account);
      }
    } catch (e) {
      // eslint-disable-next-line no-console
      console.warn("[msal] handleRedirectPromise:", e);
    }
    return _msalInstance!;
  })();
  return _msalReady;
}

export async function signIn(): Promise<void> {
  const msal = await getMsalInstance();
  // If we already have an account in the cache, just adopt it — no need to
  // bounce the user out to Microsoft for a redirect that they've already done.
  const existing = msal.getActiveAccount() ?? msal.getAllAccounts()[0];
  if (existing) {
    msal.setActiveAccount(existing);
    return;
  }
  // Pre-select the configured default user if the user hasn't picked an account yet.
  const loginHint =
    process.env.NEXT_PUBLIC_HUB_DEFAULT_USER ?? "admin@example.org";
  // Redirect (not popup) — popup flow was opening duplicate hub windows in
  // some Edge enterprise configs. Redirect is also a better demo: the audience
  // sees the actual Entra login UI in the main tab. After auth completes,
  // MSAL navigates back to the URL we were on (navigateToLoginRequestUrl).
  await msal.loginRedirect({
    scopes: GRAPH_SCOPES,
    prompt: "select_account",
    loginHint,
  });
  // This line is unreachable — the page navigates away — but TypeScript
  // wants the return path.
}

export async function signOut(): Promise<void> {
  const msal = await getMsalInstance();
  const account = msal.getActiveAccount();
  if (account) {
    await msal.logoutRedirect({ account });
  }
}

export type GraphToken = {
  accessToken: string;
  expiresOn: Date | null;
  account: AccountInfo;
};

/**
 * Returns a Graph access token for the active user, refreshing silently when
 * possible and falling back to an interactive redirect on consent needs.
 */
export async function getGraphToken(): Promise<GraphToken> {
  const msal = await getMsalInstance();
  const account = msal.getActiveAccount() ?? msal.getAllAccounts()[0];
  if (!account) {
    throw new Error("Not signed in. Click 'Sign in with Microsoft' first.");
  }
  try {
    const r = await msal.acquireTokenSilent({
      account,
      scopes: GRAPH_SCOPES,
    });
    return { accessToken: r.accessToken, expiresOn: r.expiresOn, account };
  } catch (e) {
    if (e instanceof InteractionRequiredAuthError) {
      // Use redirect, not popup — same reason as signIn().
      await msal.acquireTokenRedirect({
        account,
        scopes: GRAPH_SCOPES,
      });
      // Page navigates away; this line is unreachable but keeps TS happy.
      throw new Error("Redirecting to Microsoft for consent…");
    }
    throw e;
  }
}

export function getActiveAccount(): AccountInfo | null {
  if (!_msalInstance) return null;
  return _msalInstance.getActiveAccount() ?? _msalInstance.getAllAccounts()[0] ?? null;
}
