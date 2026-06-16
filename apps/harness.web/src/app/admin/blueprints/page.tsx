"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { FileCode2, ShieldCheck } from "lucide-react";
import { getBlueprints, type BlueprintSummary } from "@/lib/api";

export default function BlueprintsPage() {
  const [blueprints, setBlueprints] = useState<BlueprintSummary[] | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    getBlueprints()
      .then((b) => alive && setBlueprints(b))
      .catch((e: Error) => alive && setErr(e.message));
    return () => {
      alive = false;
    };
  }, []);

  return (
    <div className="p-8">
      <div className="flex items-end justify-between mb-6">
        <div>
          <div className="chip chip-purple mb-2">entra agent id</div>
          <h1 className="text-2xl text-glow-purple">Agent blueprints</h1>
          <p className="text-sm text-ink-dim mt-1">
            YAML manifests in <code className="text-ink">blueprints/*.harness.yaml</code>.
            Each renders to an Entra Agent Blueprint object and a corresponding
            Agent ID. Edit the YAML directly or use the wizard for new ones.
          </p>
        </div>
        <Link
          href="/admin/new"
          className="text-xs uppercase tracking-wider px-4 py-2 border border-electric-purple/60 text-electric-purple-soft hover:bg-electric-purple/10 transition rounded-sm"
        >
          + New blueprint
        </Link>
      </div>

      {err && (
        <div className="panel p-4 text-sm text-cyber-pink">
          harness.api unreachable: <span className="text-ink-dim">{err}</span>
        </div>
      )}

      {!blueprints && !err && (
        <div className="panel p-6 text-sm text-ink-dim">loading…</div>
      )}

      {blueprints && (
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {blueprints.map((b) => (
            <div key={b.id} className="panel p-5 rounded-sm">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-3">
                  <FileCode2 className="w-5 h-5 text-electric-purple-soft" />
                  <div>
                    <div className="text-lg text-electric-purple-soft">
                      {b.displayName}
                    </div>
                    <div className="text-xs text-ink-faint font-mono">
                      {b.fileName}
                    </div>
                  </div>
                </div>
                {b.draft && <span className="chip chip-warn">draft</span>}
              </div>

              <dl className="mt-4 grid grid-cols-2 gap-2 text-xs">
                <DT>id</DT>
                <DD>{b.id}</DD>
                <DT>apiVersion</DT>
                <DD className="font-mono">{b.apiVersion}</DD>
                <DT>auth mode</DT>
                <DD>
                  <span className="chip chip-orange">{b.authMode}</span>
                </DD>
                <DT>model</DT>
                <DD>
                  <span className="chip chip-cyan">{b.modelDeployment}</span>
                </DD>
                <DT>owner</DT>
                <DD>{b.owner}</DD>
                <DT>sponsor</DT>
                <DD>{b.sponsor}</DD>
              </dl>

              <div className="mt-4 flex items-center gap-2 text-xs">
                <ShieldCheck className="w-3 h-3 text-cyber-cyan" />
                <span className="text-ink-dim">
                  purview-protected · bidirectional
                </span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

function DT({ children }: { children: React.ReactNode }) {
  return <dt className="text-ink-faint uppercase tracking-wider">{children}</dt>;
}
function DD({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <dd className={`text-ink truncate ${className ?? ""}`.trim()}>{children}</dd>
  );
}
