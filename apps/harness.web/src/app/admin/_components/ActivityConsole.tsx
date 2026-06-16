"use client";

import { useMemo } from "react";
import { useHarnessEvents } from "@/lib/useHarnessEvents";
import type { HarnessEvent } from "@/lib/api";

export function ActivityConsole({ height = 560 }: { height?: number }) {
  const { events, connected } = useHarnessEvents(300);

  return (
    <div className="panel-glow scanline relative rounded-sm overflow-hidden">
      <div className="flex items-center justify-between px-4 py-2 border-b border-navy-border text-xs">
        <span className="text-ink-dim">SSE · /api/events</span>
        <span className={`chip ${connected ? "chip-ok pulse" : "chip-block"}`}>
          {connected ? "CONNECTED" : "DISCONNECTED"}
        </span>
      </div>
      <div className="overflow-auto bg-navy-deep/70" style={{ maxHeight: height }}>
        {events.length === 0 ? (
          <div className="p-6 text-sm text-ink-faint">
            waiting for events… start an agent or send a prompt from the hub.
          </div>
        ) : (
          <ul className="divide-y divide-navy-border">
            {events.map((ev, i) => (
              <EventRow key={`${ev.ts}-${i}`} ev={ev} />
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}

function EventRow({ ev }: { ev: HarnessEvent }) {
  const kindStyle = useMemo(() => {
    const k = ev.kind.toLowerCase();
    if (ev.status === "block") return "text-cyber-pink";
    if (ev.status === "error") return "text-warn";
    if (k.includes("purview")) return "text-cyber-cyan";
    if (k.includes("block")) return "text-cyber-pink";
    if (k.includes("llm") || k.includes("model")) return "text-neon-orange";
    if (k.includes("tool")) return "text-electric-purple-soft";
    return "text-ink-dim";
  }, [ev]);

  const ts = (() => {
    try {
      return new Date(ev.ts).toISOString().slice(11, 23);
    } catch {
      return "??:??:??";
    }
  })();

  return (
    <li className="px-4 py-2 grid grid-cols-[88px_140px_1fr_auto] gap-3 text-xs items-start hover:bg-navy-card/40">
      <span className="text-ink-faint font-mono">{ts}</span>
      <span className={`uppercase tracking-wider ${kindStyle}`}>
        {ev.kind}/{ev.status}
      </span>
      <span className="text-ink">
        <span className="text-ink-faint">{ev.source}</span>
        <span className="text-ink-faint"> · </span>
        <span>{ev.name}</span>
        {ev.attributes && Object.keys(ev.attributes).length > 0 && (
          <div className="mt-1 text-ink-dim">
            {Object.entries(ev.attributes)
              .filter(([, v]) => v !== null && v !== undefined && v !== "")
              .slice(0, 6)
              .map(([k, v]) => (
                <span key={k} className="mr-3">
                  <span className="text-ink-faint">{k}=</span>
                  <span className="text-ink">{stringify(v)}</span>
                </span>
              ))}
          </div>
        )}
      </span>
      <span className="text-ink-faint font-mono">
        {ev.durationMs ? `${ev.durationMs.toFixed(0)}ms` : ""}
      </span>
    </li>
  );
}

function stringify(v: unknown): string {
  if (v === null || v === undefined) return "—";
  if (typeof v === "string") return v.length > 60 ? v.slice(0, 60) + "…" : v;
  if (typeof v === "number" || typeof v === "boolean") return String(v);
  try {
    const s = JSON.stringify(v);
    return s.length > 60 ? s.slice(0, 60) + "…" : s;
  } catch {
    return "[obj]";
  }
}
