import Link from "next/link";
import { ArrowRight, Cpu, Network, ShieldCheck } from "lucide-react";
import { HarnessMark } from "@/components/HarnessMark";

export default function LandingPage() {
  return (
    <main className="relative min-h-screen overflow-hidden flex flex-col">
      <div className="absolute inset-0 -z-10 pointer-events-none">
        <div className="absolute inset-x-0 top-0 h-[60vh] bg-gradient-to-b from-[rgba(176,38,255,0.15)] via-[rgba(255,140,0,0.05)] to-transparent" />
      </div>

      <header className="px-10 pt-8 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <HarnessMark size="xl" variant="duo" pulse />
          <div>
            <div className="text-sm uppercase tracking-[0.4em] text-ink-faint">
              CustomAgentHarness
            </div>
            <div className="text-xs text-ink-dim">
              Workshop reference build · Agent 365 integration
            </div>
          </div>
        </div>
        <nav className="flex items-center gap-8 text-sm">
          <Link href="/admin" className="nav-link text-ink-dim">Admin</Link>
          <Link href="/hub" className="nav-link text-ink-dim">Hub</Link>
          <a
            href="https://learn.microsoft.com/en-us/microsoft-agent-365/developer/"
            target="_blank"
            rel="noreferrer"
            className="nav-link text-ink-dim"
          >
            Agent 365 docs ↗
          </a>
        </nav>
      </header>

      <section className="flex-1 px-10 grid grid-cols-1 lg:grid-cols-2 gap-10 items-center pt-16">
        <div>
          <div className="chip chip-orange mb-6">workshop / agenticbank</div>
          <h1 className="text-5xl lg:text-6xl font-bold leading-tight">
            <span className="text-glow-orange">Your harness.</span>
            <br />
            <span className="text-glow-purple">Microsoft governance.</span>
          </h1>
          <p className="mt-6 text-ink-dim max-w-xl">
            A bring-your-own-runtime reference for enterprise agent platforms.
            Two locally-hosted .NET agents — one with delegated permissions
            (OBO), one with application permissions and an internal knowledge
            base MCP — are minted from Entra Agent ID blueprints, ready to
            register in the Microsoft 365 admin center, evaluated through the
            Microsoft Purview Graph API (with a local sensitive-information-type
            fallback) on every prompt &amp; response, and grounded against a
            model deployment in Microsoft Foundry.
          </p>
          <div className="mt-10 flex flex-wrap gap-3">
            <Link
              href="/admin"
              className="inline-flex items-center gap-2 px-5 py-3 bg-neon-orange/15 border border-neon-orange/60 text-neon-orange hover:bg-neon-orange/25 transition rounded-sm border-glow-orange text-sm uppercase tracking-wider"
            >
              Open admin console <ArrowRight className="w-4 h-4" />
            </Link>
            <Link
              href="/hub"
              className="inline-flex items-center gap-2 px-5 py-3 bg-electric-purple/15 border border-electric-purple/60 text-electric-purple-soft hover:bg-electric-purple/25 transition rounded-sm border-glow-purple text-sm uppercase tracking-wider"
            >
              Open end-user hub <ArrowRight className="w-4 h-4" />
            </Link>
          </div>
        </div>

        <div className="panel-glow scanline relative rounded-sm p-6">
          <div className="flex items-center justify-between mb-4">
            <div className="text-sm uppercase tracking-wider text-ink-dim">
              Harness components
            </div>
            <span className="chip chip-cyan">live</span>
          </div>
          <ComponentRow
            icon={<Cpu className="w-5 h-5" />}
            title="ForgedAgentOne"
            desc="Delegated OBO · Mail/Cal/Files · gpt-4.1 · :3979"
            color="orange"
          />
          <ComponentRow
            icon={<Network className="w-5 h-5" />}
            title="ForgedScholarTwo"
            desc="App-permission · AgenticBank KB MCP · gpt-5.1 · :3980"
            color="purple"
          />
          <ComponentRow
            icon={<ShieldCheck className="w-5 h-5" />}
            title="Purview Graph protection"
            desc="uploadText / downloadText · regex fallback"
            color="cyan"
          />
        </div>
      </section>

      <footer className="px-10 py-6 text-xs text-ink-faint flex items-center justify-between border-t border-navy-border mt-16">
        <div>
          example.org · tenant <span className="text-ink-dim">00000000…0000</span>
        </div>
        <div>
          built for the AgenticBank IT workshop · {new Date().getFullYear()}
        </div>
      </footer>
    </main>
  );
}

function ComponentRow({
  icon,
  title,
  desc,
  color,
}: {
  icon: React.ReactNode;
  title: string;
  desc: string;
  color: "orange" | "purple" | "cyan";
}) {
  const ringClass =
    color === "orange"
      ? "text-neon-orange"
      : color === "purple"
      ? "text-electric-purple-soft"
      : "text-cyber-cyan";
  return (
    <div className="flex items-start gap-3 py-3 border-b border-navy-border last:border-b-0">
      <div className={`${ringClass} mt-0.5`}>{icon}</div>
      <div>
        <div className="text-ink">{title}</div>
        <div className="text-xs text-ink-dim">{desc}</div>
      </div>
    </div>
  );
}

