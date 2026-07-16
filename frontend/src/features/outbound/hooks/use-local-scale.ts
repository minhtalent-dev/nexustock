import { useCallback, useEffect, useRef, useState } from "react";
import { LocalAgentClient } from "@/lib/local-agent-client";

export interface LocalScaleReading {
  deviceId?: string;
  weightKg: number;
  stable: boolean;
  rawFrame?: string;
  profile?: string;
  connectionState?: string;
  errorCode?: string;
  timestamp?: string;
}

export type LocalScaleStatus = "idle" | "connecting" | "connected" | "unavailable" | "error";

function messageId(): string {
  return typeof crypto !== "undefined" && crypto.randomUUID
    ? crypto.randomUUID()
    : Math.random().toString(36).slice(2, 15);
}

export function useLocalScale(active: boolean) {
  const [status, setStatus] = useState<LocalScaleStatus>("idle");
  const [reading, setReading] = useState<LocalScaleReading | null>(null);
  const [error, setError] = useState<string | null>(null);
  const socketRef = useRef<WebSocket | null>(null);

  const disconnect = useCallback(() => {
    socketRef.current?.close();
    socketRef.current = null;
    setStatus("idle");
  }, []);

  const connect = useCallback(async () => {
    disconnect();
    setStatus("connecting");
    setError(null);

    const agent = await new LocalAgentClient().scanAgentPort();
    if (!agent.port || (agent.status !== "paired" && agent.status !== "unpaired")) {
      setStatus("unavailable");
      setError(agent.error ?? "Local Agent is unavailable.");
      return;
    }

    const protocol = process.env.NODE_ENV === "development" ? "ws" : "wss";
    const ws = new WebSocket(`${protocol}://127.0.0.1:${agent.port}/ws`);
    socketRef.current = ws;

    ws.onopen = () => {
      setStatus("connected");
      ws.send(JSON.stringify({
        messageId: messageId(),
        type: "scale.weight.subscribe",
        timestamp: new Date().toISOString(),
        payload: {}
      }));
    };

    ws.onmessage = (event) => {
      const response = JSON.parse(event.data);
      if (response.type === "scale.weightChanged" && response.payload) {
        setReading({
          deviceId: response.payload.deviceId,
          weightKg: Number(response.payload.weightKg ?? 0),
          stable: Boolean(response.payload.stable),
          rawFrame: response.payload.rawFrame,
          profile: response.payload.profile,
          connectionState: response.payload.connectionState,
          errorCode: response.payload.errorCode,
          timestamp: response.payload.timestamp
        });
      }
      if (response.type === "agent.error") {
        setStatus("error");
        setError(response.payload?.message ?? "Local Agent returned an error.");
      }
    };

    ws.onerror = () => {
      setStatus("error");
      setError("Cannot connect to Local Agent scale channel.");
    };
  }, [disconnect]);

  useEffect(() => {
    if (active) {
      queueMicrotask(() => {
        connect().catch((err) => {
          setStatus("error");
          setError(err instanceof Error ? err.message : "Cannot connect to Local Agent.");
        });
      });
      return disconnect;
    }

    queueMicrotask(disconnect);
    return undefined;
  }, [active, connect, disconnect]);

  return { status, reading, error, reconnect: connect, disconnect };
}
