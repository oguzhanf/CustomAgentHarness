import { Suspense } from "react";
import Link from "next/link";
import { HubShell } from "./_components/HubShell";
import { HarnessMark } from "@/components/HarnessMark";

export default function HubPage() {
  return (
    <div className="min-h-screen grid grid-rows-[64px_1fr]">
      <header className="border-b border-navy-border bg-navy/80 backdrop-blur-md flex items-center px-6">
        <Link
          href="/"
          className="flex items-center gap-3 text-sm uppercase tracking-[0.3em] text-ink-dim hover:text-ink transition"
        >
          <HarnessMark size="sm" variant="purple" pulse />
          <span>CustomAgentHarness</span>
          <span className="text-ink-faint">/</span>
          <span className="text-glow-purple">hub</span>
        </Link>
        <div className="flex-1" />
        <Link href="/admin" className="chip chip-orange">
          ← admin
        </Link>
      </header>
      <main className="overflow-hidden">
        <Suspense fallback={<div className="p-8 text-ink-dim">loading hub…</div>}>
          <HubShell />
        </Suspense>
      </main>
    </div>
  );
}
