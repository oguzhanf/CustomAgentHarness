"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  LayoutGrid,
  FileCode2,
  PlusCircle,
  Activity,
  Compass,
  Users,
  ExternalLink,
} from "lucide-react";
import { HarnessMark } from "@/components/HarnessMark";

const nav = [
  { href: "/admin", label: "Catalog", icon: LayoutGrid, exact: true },
  { href: "/admin/blueprints", label: "Blueprints", icon: FileCode2 },
  { href: "/admin/new", label: "New agent", icon: PlusCircle },
  { href: "/admin/activity", label: "Activity", icon: Activity },
  { href: "/admin/portals", label: "Portals", icon: Compass },
];

export function SideRail() {
  const path = usePathname();

  return (
    <nav className="flex flex-col h-full py-6">
      <div className="px-4 mb-6 flex items-center gap-2 text-xs uppercase tracking-[0.3em] text-ink-faint">
        <HarnessMark size="xs" variant="duo" />
        <span>harness</span>
      </div>
      <ul className="space-y-1 px-2 flex-1">
        {nav.map((item) => {
          const Icon = item.icon;
          const active = item.exact
            ? path === item.href
            : path === item.href || path.startsWith(item.href + "/");
          return (
            <li key={item.href}>
              <Link
                href={item.href}
                className={[
                  "group flex items-center gap-3 px-3 py-2 text-sm rounded-sm transition",
                  active
                    ? "bg-neon-orange/10 text-neon-orange border-l-2 border-neon-orange"
                    : "text-ink-dim border-l-2 border-transparent hover:bg-navy-card/60 hover:text-ink",
                ].join(" ")}
              >
                <Icon className="w-4 h-4" />
                <span>{item.label}</span>
              </Link>
            </li>
          );
        })}
      </ul>

      <div className="px-4 pt-4 mt-2 border-t border-navy-border text-xs uppercase tracking-[0.3em] text-ink-faint mb-2">
        end users
      </div>
      <ul className="px-2 space-y-1">
        <li>
          <Link
            href="/hub"
            className="flex items-center gap-3 px-3 py-2 text-sm text-electric-purple-soft hover:bg-electric-purple/10 transition rounded-sm border-l-2 border-transparent hover:border-electric-purple"
          >
            <Users className="w-4 h-4" />
            <span>Open hub</span>
            <ExternalLink className="w-3 h-3 ml-auto opacity-60" />
          </Link>
        </li>
      </ul>

      <div className="px-4 pb-6 pt-6 text-[10px] text-ink-faint border-t border-navy-border mt-2">
        agenticbank workshop · v0.1
      </div>
    </nav>
  );
}
