"use client";

import { useEffect, useState } from "react";
import { ArrowUpRight, Compass } from "lucide-react";
import { getPortals, type PortalLink } from "@/lib/api";

export default function PortalsPage() {
  const [portals, setPortals] = useState<PortalLink[] | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    getPortals()
      .then((p) => alive && setPortals(p))
      .catch((e: Error) => alive && setErr(e.message));
    return () => {
      alive = false;
    };
  }, []);

  return (
    <div className="p-8">
      <div className="mb-6">
        <div className="chip chip-orange mb-2">microsoft surfaces</div>
        <h1 className="text-2xl text-glow-orange">Portal deep links</h1>
        <p className="text-sm text-ink-dim mt-1">
          Each agent registered with Agent 365 is reflected in these Microsoft
          administrator surfaces. Use these during the workshop to demonstrate
          the round-trip from custom harness → Microsoft governance.
        </p>
      </div>

      {err && (
        <div className="panel p-4 text-sm text-cyber-pink">
          harness.api unreachable: <span className="text-ink-dim">{err}</span>
        </div>
      )}

      {!portals && !err && (
        <div className="panel p-6 text-sm text-ink-dim">loading…</div>
      )}

      {portals && (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
          {portals.map((p) => (
            <a
              key={p.id}
              href={p.url}
              target="_blank"
              rel="noreferrer"
              className="panel p-4 rounded-sm flex items-start gap-3 group hover:shadow-[0_0_18px_rgba(176,38,255,0.15)] transition"
            >
              <Compass className="w-4 h-4 text-ink-faint mt-1" />
              <div className="flex-1 min-w-0">
                <div className="flex items-center justify-between">
                  <div className="text-sm text-ink group-hover:text-glow-purple">
                    {p.title}
                  </div>
                  <ArrowUpRight className="w-3.5 h-3.5 text-ink-faint group-hover:text-electric-purple-soft" />
                </div>
                <div className="text-xs text-ink-dim mt-1">{p.description}</div>
                <div className="mt-2 flex items-center gap-2">
                  <span className="chip">{p.category}</span>
                  <span className="text-[10px] text-ink-faint truncate font-mono">
                    {p.url.replace(/^https?:\/\//, "")}
                  </span>
                </div>
              </div>
            </a>
          ))}
        </div>
      )}
    </div>
  );
}
