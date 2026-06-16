"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  ArrowUpRight,
  ShieldCheck,
  Brain,
  KeyRound,
  Server,
  Compass,
} from "lucide-react";
import {
  getAgents,
  getPortals,
  type AgentSummary,
  type PortalLink,
} from "@/lib/api";
import { ActivityConsole } from "./_components/ActivityConsole";

export default function AdminCatalogPage() {
  const [agents, setAgents] = useState<AgentSummary[] | null>(null);
  const [portals, setPortals] = useState<PortalLink[] | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    Promise.all([getAgents(), getPortals()])
      .then(([a, p]) => {
        if (!alive) return;
        setAgents(a);
        setPortals(p);
      })
      .catch((e: Error) => alive && setErr(e.message));
    return () => {
      alive = false;
    };
  }, []);

  return (
    <div className="p-8 grid grid-cols-1 xl:grid-cols-[1fr_360px] gap-8">
      <div className="space-y-10 min-w-0">
        <section>
          <div className="flex items-end justify-between mb-4">
            <div>
              <div className="chip chip-orange mb-2">agent catalog</div>
              <h1 className="text-2xl text-glow-orange">Registered agents</h1>
              <p className="text-sm text-ink-dim mt-1">
                Two reference templates running on this machine, both minted from
                Entra Agent ID blueprints and governed by Microsoft Purview
                (Graph processContent, with a local SIT fallback).
              </p>
            </div>
            <Link
              href="/admin/new"
              className="text-xs uppercase tracking-wider px-4 py-2 border border-electric-purple/60 text-electric-purple-soft hover:bg-electric-purple/10 transition rounded-sm"
            >
              + New definition
            </Link>
          </div>

          {err && (
            <div className="panel p-4 text-sm text-cyber-pink">
              harness.api unreachable: <span className="text-ink-dim">{err}</span>
              <div className="text-xs text-ink-faint mt-1">
                Start it with <code className="text-ink">dotnet run --project apps/harness.api</code>.
              </div>
            </div>
          )}

          {!agents && !err && (
            <div className="panel p-6 text-sm text-ink-dim">
              loading agents from harness.api …
            </div>
          )}

          {agents && (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {agents.length === 0 && (
                <div className="panel p-6 text-sm text-ink-dim col-span-2">
                  No agents discovered.
                </div>
              )}
              {agents.map((a) => (
                <AgentCard key={a.id} agent={a} />
              ))}
            </div>
          )}
        </section>

        <section>
          <div className="flex items-end justify-between mb-4">
            <div>
              <div className="chip chip-purple mb-2">microsoft portals</div>
              <h2 className="text-xl text-glow-purple">Where to verify</h2>
              <p className="text-sm text-ink-dim mt-1">
                Click through to see each agent reflected in the Microsoft
                administrator surfaces.
              </p>
            </div>
          </div>
          {!portals ? (
            <div className="panel p-6 text-sm text-ink-dim">loading…</div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {portals.map((p) => (
                <PortalCard key={p.id} link={p} />
              ))}
            </div>
          )}
        </section>
      </div>

      <aside className="min-w-0">
        <div className="sticky top-4">
          <div className="flex items-center justify-between mb-3">
            <div>
              <div className="chip chip-cyan mb-1">live</div>
              <h2 className="text-lg">Activity console</h2>
            </div>
          </div>
          <ActivityConsole height={720} />
        </div>
      </aside>
    </div>
  );
}

function AgentCard({ agent }: { agent: AgentSummary }) {
  const isObo =
    agent.authMode.toLowerCase().includes("delegated") ||
    agent.authMode.toLowerCase().includes("obo");
  return (
    <div
      className={`panel relative p-5 rounded-sm transition ${
        isObo
          ? "hover:border-neon-orange/60 hover:shadow-[0_0_20px_rgba(255,140,0,0.15)]"
          : "hover:border-electric-purple/60 hover:shadow-[0_0_20px_rgba(176,38,255,0.15)]"
      }`}
    >
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          {isObo ? (
            <KeyRound className="w-5 h-5 text-neon-orange" />
          ) : (
            <Server className="w-5 h-5 text-electric-purple-soft" />
          )}
          <div>
            <div className={isObo ? "text-lg text-neon-orange" : "text-lg text-electric-purple-soft"}>
              {agent.displayName}
            </div>
            <div className="text-xs text-ink-faint">{agent.id}</div>
          </div>
        </div>
        <StatusDot status={agent.status} />
      </div>

      {agent.tagline && (
        <div className="mt-3 text-sm text-ink-dim italic">{agent.tagline}</div>
      )}
      {agent.description && (
        <p className="mt-2 text-sm text-ink-dim line-clamp-3">
          {agent.description}
        </p>
      )}

      <div className="mt-4 flex flex-wrap gap-2">
        <span className={`chip ${isObo ? "chip-orange" : "chip-purple"}`}>
          {agent.authMode}
        </span>
        <span className="chip chip-cyan">
          <Brain className="w-3 h-3 inline" />
          {agent.modelDeployment}
        </span>
        <span className="chip">
          <ShieldCheck className="w-3 h-3 inline" />
          purview-protected
        </span>
      </div>

      <div className="mt-4 flex items-center justify-between text-xs text-ink-faint">
        <span className="font-mono truncate">{agent.endpoint}</span>
        <Link
          href={`/hub?agent=${encodeURIComponent(agent.id)}`}
          className="text-ink-dim hover:text-ink inline-flex items-center gap-1"
        >
          test in hub <ArrowUpRight className="w-3 h-3" />
        </Link>
      </div>
    </div>
  );
}

function PortalCard({ link }: { link: PortalLink }) {
  const colorMap: Record<PortalLink["category"], string> = {
    admin: "chip-purple",
    identity: "chip-orange",
    purview: "chip-cyan",
    foundry: "chip-orange",
    defender: "chip-block",
  };
  return (
    <a
      href={link.url}
      target="_blank"
      rel="noreferrer"
      className="panel p-4 rounded-sm flex items-start gap-3 group hover:shadow-[0_0_18px_rgba(176,38,255,0.15)] transition"
    >
      <Compass className="w-4 h-4 text-ink-faint mt-1" />
      <div className="flex-1 min-w-0">
        <div className="flex items-center justify-between">
          <div className="text-sm text-ink group-hover:text-glow-purple">
            {link.title}
          </div>
          <ArrowUpRight className="w-3.5 h-3.5 text-ink-faint group-hover:text-electric-purple-soft" />
        </div>
        <div className="text-xs text-ink-dim mt-1 line-clamp-2">
          {link.description}
        </div>
        <div className="mt-2">
          <span className={`chip ${colorMap[link.category] ?? ""}`}>
            {link.category}
          </span>
        </div>
      </div>
    </a>
  );
}

function StatusDot({ status }: { status?: string }) {
  const map: Record<string, { label: string; cls: string }> = {
    online: { label: "online", cls: "bg-ok shadow-[0_0_8px_rgba(45,212,191,0.8)]" },
    offline: { label: "offline", cls: "bg-cyber-pink shadow-[0_0_8px_rgba(255,42,109,0.8)]" },
    unknown: { label: "—", cls: "bg-ink-faint" },
  };
  const s = map[status ?? "unknown"] ?? map.unknown;
  return (
    <div className="flex items-center gap-2">
      <span className={`inline-block w-2 h-2 rounded-full pulse ${s.cls}`} />
      <span className="text-[10px] uppercase tracking-wider text-ink-faint">
        {s.label}
      </span>
    </div>
  );
}
