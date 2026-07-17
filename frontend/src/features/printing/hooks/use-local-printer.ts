"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { LocalAgentClient } from "@/lib/local-agent-client";
import type { LocalPrinterConnectionState, LocalPrinterStatus } from "../types";

type AgentResponse = {
  messageId: string;
  type: string;
  payload?: unknown;
};

type AgentErrorPayload = {
  message?: string;
};

function createMessageId() {
  return typeof crypto !== "undefined" && crypto.randomUUID
    ? crypto.randomUUID()
    : Math.random().toString(36).slice(2);
}

export function useLocalPrinter(printerCode = "PRINTER-01", enabled = true) {
  const [state, setState] = useState<LocalPrinterConnectionState>("idle");
  const [status, setStatus] = useState<LocalPrinterStatus | null>(null);
  const [error, setError] = useState<string | null>(null);
  const socketRef = useRef<WebSocket | null>(null);

  const disconnect = useCallback(() => {
    socketRef.current?.close();
    socketRef.current = null;
  }, []);

  const connect = useCallback(async () => {
    if (!enabled) return;

    disconnect();
    setState("connecting");
    setError(null);

    try {
      const agent = await new LocalAgentClient().scanAgentPort();
      if (!agent.port || (agent.status !== "paired" && agent.status !== "unpaired")) {
        setState("unavailable");
        setStatus(null);
        setError(agent.error ?? "Local Agent is unavailable.");
        return;
      }

      const protocol = process.env.NODE_ENV === "development" ? "ws" : "wss";
      const ws = new WebSocket(`${protocol}://127.0.0.1:${agent.port}/ws`);
      socketRef.current = ws;

      ws.onopen = () => {
        ws.send(JSON.stringify({
          messageId: createMessageId(),
          type: "printer.status.request",
          timestamp: new Date().toISOString(),
          payload: { printerCode },
        }));
      };

      ws.onmessage = (event) => {
        const response = JSON.parse(event.data) as AgentResponse;
        if (response.type === "printer.status.response") {
          setState("connected");
          setStatus({
            printerCode,
            status: String(response.payload ?? "online"),
            port: agent.port,
          });
          return;
        }

        if (response.type === "printer.print.response") {
          setState("printed");
          return;
        }

        if (response.type === "agent.error") {
          const payload = response.payload as AgentErrorPayload | undefined;
          setState("error");
          setStatus(null);
          setError(String(payload?.message ?? "Local Agent returned an error."));
        }
      };

      ws.onerror = () => {
        setState("error");
        setError("Cannot connect to Local Agent printer service.");
      };

      ws.onclose = () => {
        socketRef.current = null;
      };
    } catch (err) {
      setState("error");
      setError(err instanceof Error ? err.message : "Cannot connect to Local Agent.");
    }
  }, [disconnect, enabled, printerCode]);

  useEffect(() => {
    if (enabled) queueMicrotask(() => void connect());
    return disconnect;
  }, [connect, disconnect, enabled]);

  const printRawCommand = useCallback((rawCommand: string, signature: string) => {
    if (!socketRef.current || socketRef.current.readyState !== WebSocket.OPEN) {
      throw new Error("Local Agent printer is not connected.");
    }

    socketRef.current.send(JSON.stringify({
      messageId: createMessageId(),
      type: "printer.print.request",
      timestamp: new Date().toISOString(),
      payload: { printerCode, rawCommand },
      signature,
    }));

    setState("printing");
  }, [printerCode]);

  return {
    state,
    status,
    error,
    connect,
    disconnect,
    refreshStatus: connect,
    printRawCommand,
  };
}
