"use client";

import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import {
  Send,
  ShieldAlert,
  CheckCircle2,
  KeyRound,
  Server,
  UserCircle2,
  Bot,
  LogIn,
  LogOut,
  Loader2,
} from "lucide-react";
import {
  getAgents,
  postChat,
  type AgentSummary,
  type ChatTurnResponse,
} from "@/lib/api";
import {
  getMsalInstance,
  signIn,
  signOut,
  getGraphToken,
} from "@/lib/msal";
import type { AccountInfo } from "@azure/msal-browser";

type Turn = {
  who: "user" | "agent" | "system";
  text: string;
  blocked?: boolean;
  direction?: string;
  reason?: string;
  citations?: ChatTurnResponse["citations"];
  ts: string;
};

export function HubShell() {
  const router = useRouter();
  const search = useSearchParams();
  const [agents, setAgents] = useState<AgentSummary[] | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(
    search.get("agent")
  );
  const [account, setAccount] = useState<AccountInfo | null>(null);
  const [authBusy, setAuthBusy] = useState(false);
  const [authError, setAuthError] = useState<string | null>(null);
  const [turns, setTurns] = useState<Turn[]>([]);
  const [input, setInput] = useState("");
  const [busy, setBusy] = useState(false);
  const [convoId] = useState(() =>
    typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : String(Date.now())
  );
  const scrollRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    let alive = true;
    getAgents()
      .then((a) => {
        if (!alive) return;
        setAgents(a);
        if (!selectedId && a.length > 0) setSelectedId(a[0].id);
      })
      .catch(() => {});
    return () => {
      alive = false;
    };
  }, [selectedId]);

  // Initialise MSAL on mount + restore any cached account
  useEffect(() => {
    let alive = true;
    getMsalInstance()
      .then((msal) => {
        if (!alive) return;
        const a = msal.getActiveAccount() ?? msal.getAllAccounts()[0] ?? null;
        if (a) {
          msal.setActiveAccount(a);
          setAccount(a);
        }
      })
      .catch((e) => {
        // eslint-disable-next-line no-console
        console.warn("[hub] msal init:", e);
      });
    return () => {
      alive = false;
    };
  }, []);

  const handleSignIn = useCallback(async () => {
    setAuthBusy(true);
    setAuthError(null);
    try {
      await signIn();
      // If we get here without a navigation, the user already had a cached
      // account — read it back and update local state.
      const msal = await getMsalInstance();
      const a = msal.getActiveAccount() ?? msal.getAllAccounts()[0] ?? null;
      if (a) setAccount(a);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setAuthError(msg);
    } finally {
      setAuthBusy(false);
    }
  }, []);

  const handleSignOut = useCallback(async () => {
    setAuthBusy(true);
    try {
      await signOut();
      setAccount(null);
      setTurns([]);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setAuthError(msg);
    } finally {
      setAuthBusy(false);
    }
  }, []);

  useEffect(() => {
    if (selectedId) {
      const next = new URLSearchParams(search.toString());
      if (next.get("agent") !== selectedId) {
        next.set("agent", selectedId);
        router.replace(`/hub?${next.toString()}`);
      }
    }
    setTurns([]);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedId]);

  useEffect(() => {
    scrollRef.current?.scrollTo({
      top: scrollRef.current.scrollHeight,
      behavior: "smooth",
    });
  }, [turns]);

  const selectedAgent = useMemo(
    () => agents?.find((a) => a.id === selectedId) ?? null,
    [agents, selectedId]
  );

  async function send() {
    const text = input.trim();
    if (!text || !selectedAgent || busy) return;
    setBusy(true);
    setInput("");
    const ts = new Date().toISOString();
    setTurns((t) => [...t, { who: "user", text, ts }]);

    let userUpn: string | undefined;
    let userObjectId: string | undefined;
    let userAccessToken: string | undefined;

    // If the user is signed in, acquire a Graph token and forward identity.
    // ForgedAgentOne's GraphPlugin uses the token to make real OBO Graph calls.
    if (account) {
      userUpn = account.username;
      userObjectId =
        (account.idTokenClaims as Record<string, unknown> | undefined)?.oid as
          | string
          | undefined;
      try {
        const tok = await getGraphToken();
        userAccessToken = tok.accessToken;
      } catch (e: unknown) {
        // Token acquisition can fail (consent revoked, network) — surface it but still send.
        const msg = e instanceof Error ? e.message : String(e);
        setTurns((t) => [
          ...t,
          {
            who: "system",
            text:
              "Could not acquire a Microsoft Graph token (" +
              msg +
              "). The agent will fall back to canned data for any Graph-backed tools.",
            ts: new Date().toISOString(),
          },
        ]);
      }
    }

    try {
      const r = await postChat(selectedAgent.id, {
        message: text,
        userUpn,
        userObjectId,
        userAccessToken,
        conversationId: convoId,
      });
      setTurns((t) => [
        ...t,
        {
          who: r.blocked ? "system" : "agent",
          text: r.reply,
          blocked: r.blocked,
          direction: r.direction,
          reason: r.reason,
          citations: r.citations,
          ts: new Date().toISOString(),
        },
      ]);
    } catch (e: unknown) {
      const msg = e instanceof Error ? e.message : String(e);
      setTurns((t) => [
        ...t,
        {
          who: "system",
          text: `error talking to ${selectedAgent.displayName}: ${msg}`,
          ts: new Date().toISOString(),
        },
      ]);
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="grid grid-cols-[300px_1fr] h-full">
      <aside className="border-r border-navy-border bg-navy-deep/80 overflow-auto">
        <div className="px-4 pt-6 pb-3 text-xs uppercase tracking-[0.3em] text-ink-faint">
          choose an agent
        </div>
        {!agents && <div className="px-4 text-ink-dim text-sm">loading…</div>}
        {agents &&
          agents.map((a) => {
            const isObo =
              a.authMode.toLowerCase().includes("delegated") ||
              a.authMode.toLowerCase().includes("obo");
            const active = a.id === selectedId;
            return (
              <button
                key={a.id}
                onClick={() => setSelectedId(a.id)}
                className={[
                  "block w-full text-left px-4 py-3 border-l-2 transition",
                  active
                    ? isObo
                      ? "border-neon-orange bg-neon-orange/10"
                      : "border-electric-purple bg-electric-purple/10"
                    : "border-transparent hover:bg-navy-card/50",
                ].join(" ")}
              >
                <div className="flex items-start gap-2">
                  {isObo ? (
                    <KeyRound className="w-4 h-4 text-neon-orange mt-0.5" />
                  ) : (
                    <Server className="w-4 h-4 text-electric-purple-soft mt-0.5" />
                  )}
                  <div className="flex-1">
                    <div
                      className={
                        active
                          ? isObo
                            ? "text-neon-orange"
                            : "text-electric-purple-soft"
                          : "text-ink"
                      }
                    >
                      {a.displayName}
                    </div>
                    <div className="text-[10px] text-ink-faint">{a.id}</div>
                  </div>
                </div>
                <div className="mt-2 flex flex-wrap gap-1">
                  <span
                    className={`chip ${isObo ? "chip-orange" : "chip-purple"}`}
                  >
                    {a.authMode}
                  </span>
                  <span className="chip chip-cyan">{a.modelDeployment}</span>
                </div>
              </button>
            );
          })}

        <div className="px-4 pt-8 pb-3 text-xs uppercase tracking-[0.3em] text-ink-faint">
          signed in as
        </div>
        <div className="px-4 pb-4">
          {account ? (
            <>
              <div className="panel rounded-sm px-3 py-2 text-sm">
                <div className="flex items-center gap-2 text-neon-orange">
                  <UserCircle2 className="w-4 h-4" />
                  <span className="font-mono truncate">{account.username}</span>
                </div>
                {account.name && (
                  <div className="text-[10px] text-ink-faint mt-1 truncate">
                    {account.name}
                  </div>
                )}
              </div>
              <p className="text-[10px] text-ink-faint mt-2 leading-relaxed">
                Graph token is acquired silently and forwarded to the agent on
                each turn. The agent calls Mail / Calendar / SharePoint on your
                behalf using that token.
              </p>
              <button
                onClick={handleSignOut}
                disabled={authBusy}
                className="mt-3 w-full px-3 py-2 text-xs uppercase tracking-wider border border-navy-border text-ink-dim hover:text-ink hover:border-electric-purple/40 rounded-sm inline-flex items-center justify-center gap-2 transition disabled:opacity-50"
              >
                {authBusy ? (
                  <Loader2 className="w-3 h-3 animate-spin" />
                ) : (
                  <LogOut className="w-3 h-3" />
                )}
                sign out
              </button>
            </>
          ) : (
            <>
              <p className="text-[10px] text-ink-faint leading-relaxed">
                Sign in with your Microsoft 365 account to enable on-behalf-of
                Graph calls (Mail, Calendar, SharePoint). Without sign-in the
                agent answers with canned demo data.
              </p>
              <button
                onClick={handleSignIn}
                disabled={authBusy}
                className="mt-3 w-full px-3 py-2 text-xs uppercase tracking-wider border border-neon-orange/60 bg-neon-orange/10 text-neon-orange hover:bg-neon-orange/20 rounded-sm inline-flex items-center justify-center gap-2 transition disabled:opacity-50"
              >
                {authBusy ? (
                  <Loader2 className="w-3 h-3 animate-spin" />
                ) : (
                  <LogIn className="w-3 h-3" />
                )}
                sign in with Microsoft
              </button>
            </>
          )}
          {authError && (
            <div className="mt-2 text-[10px] text-cyber-pink leading-relaxed break-words">
              {authError}
            </div>
          )}
        </div>
      </aside>

      <section className="flex flex-col min-w-0">
        {selectedAgent && (
          <div className="px-6 py-4 border-b border-navy-border flex items-center gap-4">
            <div className="flex-1 min-w-0">
              <div className="text-lg text-ink">{selectedAgent.displayName}</div>
              {selectedAgent.tagline && (
                <div className="text-xs text-ink-dim italic">
                  {selectedAgent.tagline}
                </div>
              )}
            </div>
            <span className="chip chip-cyan">{selectedAgent.modelDeployment}</span>
            <span className="chip">
              <ShieldAlert className="w-3 h-3 inline" /> purview-protected
            </span>
          </div>
        )}

        <div ref={scrollRef} className="flex-1 overflow-auto px-6 py-6 space-y-4">
          {turns.length === 0 && selectedAgent && (
            <SuggestionGrid agent={selectedAgent} onPick={(p) => setInput(p)} />
          )}
          {turns.map((t, i) => (
            <TurnBubble key={i} turn={t} />
          ))}
        </div>

        <div className="border-t border-navy-border px-6 py-4 bg-navy-deep/60">
          <div className="flex items-end gap-3">
            <textarea
              value={input}
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && !e.shiftKey) {
                  e.preventDefault();
                  send();
                }
              }}
              placeholder={
                selectedAgent
                  ? `ask ${selectedAgent.displayName}… (shift+enter for newline)`
                  : "select an agent first"
              }
              rows={2}
              className="flex-1 bg-navy-deep/80 border border-navy-border focus:border-neon-orange/60 focus:outline-none focus:shadow-[0_0_12px_rgba(255,140,0,0.15)] rounded-sm px-3 py-2 text-sm text-ink placeholder:text-ink-faint resize-none font-mono"
              disabled={!selectedAgent || busy}
            />
            <button
              onClick={send}
              disabled={!input.trim() || !selectedAgent || busy}
              className="px-4 py-3 bg-neon-orange/15 border border-neon-orange/60 text-neon-orange hover:bg-neon-orange/25 transition rounded-sm text-sm uppercase tracking-wider disabled:opacity-40 inline-flex items-center gap-2"
            >
              <Send className="w-4 h-4" />
              {busy ? "thinking…" : "send"}
            </button>
          </div>
        </div>
      </section>
    </div>
  );
}

function TurnBubble({ turn }: { turn: Turn }) {
  const ts = (() => {
    try {
      return new Date(turn.ts).toISOString().slice(11, 19);
    } catch {
      return "";
    }
  })();

  if (turn.who === "user") {
    return (
      <div className="flex justify-end">
        <div className="max-w-[70%] panel border-glow-orange rounded-sm px-4 py-3 text-sm text-ink">
          <div className="flex items-center gap-2 mb-1 text-[10px] uppercase tracking-wider text-ink-faint">
            <UserCircle2 className="w-3 h-3" />
            you · {ts}
          </div>
          <div className="whitespace-pre-wrap">{turn.text}</div>
        </div>
      </div>
    );
  }
  if (turn.who === "system") {
    const cls = turn.blocked
      ? "border-glow-orange text-cyber-pink"
      : "text-warn";
    return (
      <div className="flex justify-center">
        <div className={`max-w-[80%] panel ${cls} rounded-sm px-4 py-3 text-sm`}>
          <div className="flex items-center gap-2 mb-1 text-[10px] uppercase tracking-wider">
            <ShieldAlert className="w-3 h-3" />
            {turn.blocked
              ? `purview blocked · ${turn.direction ?? "?"}`
              : "system"}{" "}
            · {ts}
          </div>
          <div className="whitespace-pre-wrap text-ink">{turn.text}</div>
          {turn.reason && (
            <div className="mt-2 text-xs text-ink-dim italic">{turn.reason}</div>
          )}
        </div>
      </div>
    );
  }
  return (
    <div className="flex">
      <div className="max-w-[80%] panel border-glow-purple rounded-sm px-4 py-3 text-sm text-ink">
        <div className="flex items-center gap-2 mb-1 text-[10px] uppercase tracking-wider text-ink-faint">
          <Bot className="w-3 h-3 text-electric-purple-soft" />
          agent · {ts}
          <span className="ml-2 chip chip-ok">
            <CheckCircle2 className="w-3 h-3 inline" /> purview clear
          </span>
        </div>
        <div className="whitespace-pre-wrap">{turn.text}</div>
        {turn.citations && turn.citations.length > 0 && (
          <div className="mt-3 border-t border-navy-border pt-2">
            <div className="text-[10px] text-ink-faint uppercase tracking-wider mb-1">
              citations
            </div>
            <ul className="space-y-1">
              {turn.citations.map((c, i) => (
                <li key={i} className="text-xs text-ink-dim">
                  <span className="text-electric-purple-soft">{c.title}</span>
                  <span className="text-ink-faint"> · {c.source}</span>
                  <span className="text-ink-faint ml-2">
                    score {c.score.toFixed(2)}
                  </span>
                </li>
              ))}
            </ul>
          </div>
        )}
      </div>
    </div>
  );
}

function SuggestionGrid({
  agent,
  onPick,
}: {
  agent: AgentSummary;
  onPick: (prompt: string) => void;
}) {
  const isObo =
    agent.authMode.toLowerCase().includes("delegated") ||
    agent.authMode.toLowerCase().includes("obo");
  const prompts = isObo
    ? [
        "Summarize my unread emails from this morning.",
        "What's on my calendar today?",
        "Find the latest credit-policy doc in our SharePoint.",
        "Block test: my card is 4532 6677 8521 3500, can you store it?",
      ]
    : [
        "What's our KYC threshold for high-risk customers?",
        "Summarize our AML transaction-monitoring policy.",
        "When does the AI-acceptable-use policy require human review?",
        "Block test: customer SSN 120-98-1437 needs verification.",
      ];
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
      {prompts.map((p) => (
        <button
          key={p}
          onClick={() => onPick(p)}
          className="panel rounded-sm p-4 text-left text-sm text-ink-dim hover:text-ink hover:border-neon-orange/40 transition"
        >
          {p}
        </button>
      ))}
    </div>
  );
}
