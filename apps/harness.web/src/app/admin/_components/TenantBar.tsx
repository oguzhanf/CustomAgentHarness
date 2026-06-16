"use client";

import { useEffect, useState } from "react";
import { getTenant, type TenantInfo } from "@/lib/api";

export function TenantBar() {
  const [tenant, setTenant] = useState<TenantInfo | null>(null);
  const [err, setErr] = useState<string | null>(null);

  useEffect(() => {
    let alive = true;
    getTenant()
      .then((t) => alive && setTenant(t))
      .catch((e: Error) => alive && setErr(e.message));
    return () => {
      alive = false;
    };
  }, []);

  if (err) {
    return (
      <span className="chip chip-block" title={err}>
        harness.api offline
      </span>
    );
  }
  if (!tenant) {
    return <span className="chip text-ink-faint">loading…</span>;
  }

  const tid = tenant.tenantId
    ? tenant.tenantId.slice(0, 8) + "…" + tenant.tenantId.slice(-4)
    : "—";

  return (
    <div className="flex items-center gap-3 text-xs">
      <span className="chip chip-purple" title={tenant.tenantId}>
        tenant <span className="text-ink">{tid}</span>
      </span>
      {tenant.tenantDomain && (
        <span className="chip text-ink-dim">{tenant.tenantDomain}</span>
      )}
      {tenant.foundry && (
        <span
          className="chip chip-orange"
          title={`${tenant.foundry.accountName} (${tenant.foundry.region})`}
        >
          foundry <span className="text-ink">{tenant.foundry.accountName}</span>
        </span>
      )}
      {tenant.foundry?.deployments.length ? (
        <span className="chip chip-cyan">
          {tenant.foundry.deployments.length} deployments
        </span>
      ) : null}
    </div>
  );
}
