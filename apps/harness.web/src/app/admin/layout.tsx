import Link from "next/link";
import { TenantBar } from "./_components/TenantBar";
import { SideRail } from "./_components/SideRail";
import { HarnessMark } from "@/components/HarnessMark";

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <div className="min-h-screen grid grid-cols-[260px_1fr] grid-rows-[64px_1fr]">
      <div className="col-span-2 row-start-1 row-end-2 border-b border-navy-border bg-navy/80 backdrop-blur-md flex items-center px-6 z-10">
        <Link
          href="/"
          className="flex items-center gap-3 text-sm uppercase tracking-[0.3em] text-ink-dim hover:text-ink transition"
        >
          <HarnessMark size="sm" variant="orange" pulse />
          <span>CustomAgentHarness</span>
          <span className="text-ink-faint">/</span>
          <span className="text-glow-orange">admin</span>
        </Link>
        <div className="flex-1" />
        <TenantBar />
      </div>

      <aside className="row-start-2 row-end-3 col-start-1 col-end-2 border-r border-navy-border bg-navy-deep/80">
        <SideRail />
      </aside>

      <main className="row-start-2 row-end-3 col-start-2 col-end-3 overflow-auto">
        {children}
      </main>
    </div>
  );
}
