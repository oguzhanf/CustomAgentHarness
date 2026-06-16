import { ActivityConsole } from "../_components/ActivityConsole";

export default function ActivityPage() {
  return (
    <div className="p-8">
      <div className="mb-4">
        <div className="chip chip-cyan mb-2">otel · sse</div>
        <h1 className="text-2xl text-glow-cyan">Activity console</h1>
        <p className="text-sm text-ink-dim mt-1">
          Live stream of every event the harness emits — agent boot,
          Purview Graph round-trips, KB MCP tool calls, LLM completions.
        </p>
      </div>
      <ActivityConsole height={820} />
    </div>
  );
}
