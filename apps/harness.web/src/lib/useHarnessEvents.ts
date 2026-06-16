"use client";

import { useEffect, useRef, useState } from "react";
import type { HarnessEvent } from "./api";
import { HARNESS_API_BASE } from "./api";

/**
 * Subscribes to the Server-Sent-Event stream on harness.api/api/events.
 * Returns the last `maxEvents` events in reverse-chronological order.
 */
export function useHarnessEvents(maxEvents = 200) {
  const [events, setEvents] = useState<HarnessEvent[]>([]);
  const [connected, setConnected] = useState(false);
  const esRef = useRef<EventSource | null>(null);

  useEffect(() => {
    const es = new EventSource(`${HARNESS_API_BASE}/api/events`);
    esRef.current = es;

    es.onopen = () => setConnected(true);
    es.onerror = () => setConnected(false);
    es.onmessage = (msg) => {
      try {
        const ev = JSON.parse(msg.data) as HarnessEvent;
        setEvents((prev) => {
          const next = [ev, ...prev];
          if (next.length > maxEvents) next.length = maxEvents;
          return next;
        });
      } catch {
        // ignore malformed
      }
    };

    return () => {
      es.close();
      esRef.current = null;
    };
  }, [maxEvents]);

  return { events, connected };
}
