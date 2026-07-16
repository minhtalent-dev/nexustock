export type AgentStatus = "paired" | "unpaired" | "backend_offline" | "certificate_error" | "port_unavailable" | "connecting";

export interface AgentStatusInfo {
  status: AgentStatus;
  stationId?: string;
  stationCode?: string;
  port?: number;
  error?: string;
}

function getMessageId(): string {
  return typeof crypto !== "undefined" && crypto.randomUUID 
    ? crypto.randomUUID() 
    : Math.random().toString(36).substring(2, 15);
}

export class LocalAgentClient {
  private static PORT_RANGE = [9000, 9001, 9002, 9003, 9004, 9005];

  /**
   * Dò tìm cổng hoạt động của Local Agent trong dải 9000-9005
   */
  public async scanAgentPort(): Promise<AgentStatusInfo> {
    const isDev = process.env.NODE_ENV === "development";
    let lastCertErrorPort: number | null = null;
    
    for (const port of LocalAgentClient.PORT_RANGE) {
      try {
        const info = await this.tryConnectAndGetStatus(port, isDev);
        if (info.status === "paired" || info.status === "unpaired") {
          return info;
        }
      } catch (err: unknown) {
        if (err instanceof Error && err.message === "certificate_error") {
          lastCertErrorPort = port;
        }
      }
    }

    if (lastCertErrorPort !== null) {
      return { 
        status: "certificate_error", 
        port: lastCertErrorPort, 
        error: "Lỗi chứng chỉ SSL (WSS) của localhost. Vui lòng xác thực chứng chỉ tự ký." 
      };
    }

    return { 
      status: "port_unavailable", 
      error: "Không tìm thấy Local Agent chạy trên các cổng từ 9000 đến 9005." 
    };
  }

  private tryConnectAndGetStatus(port: number, isDev: boolean): Promise<AgentStatusInfo> {
    return new Promise((resolve, reject) => {
      const protocol = isDev ? "ws" : "wss";
      const wsUrl = `${protocol}://127.0.0.1:${port}/ws`;
      
      let ws: WebSocket;

      try {
        ws = new WebSocket(wsUrl);
      } catch (e) {
        reject(e);
        return;
      }

      const timeoutId: ReturnType<typeof setTimeout> = setTimeout(() => {
        ws.close();
        reject(new Error("timeout"));
      }, 800); // Timeout nhanh để quét dải cổng mượt mà

      ws.onopen = () => {
        const msgId = getMessageId();
        const request = {
          messageId: msgId,
          type: "agent.status.request",
          timestamp: new Date().toISOString(),
          payload: {}
        };
        ws.send(JSON.stringify(request));
      };

      ws.onmessage = (event) => {
        clearTimeout(timeoutId);
        ws.close();
        try {
          const response = JSON.parse(event.data);
          if (response.type === "agent.status.response" && response.payload) {
            resolve({
              status: response.payload.status as AgentStatus,
              stationId: response.payload.stationId,
              stationCode: response.payload.stationCode,
              port: port
            });
          } else if (response.type === "agent.error") {
            resolve({
              status: "unpaired",
              port: port,
              error: response.payload?.message
            });
          } else {
            reject(new Error("unknown_response"));
          }
        } catch (e) {
          reject(e);
        }
      };

      ws.onerror = (err) => {
        clearTimeout(timeoutId);
        if (!isDev && protocol === "wss") {
          reject(new Error("certificate_error"));
        } else {
          reject(err);
        }
      };
    });
  }

  /**
   * Gửi lệnh ghép cặp đến Local Agent qua WebSocket
   */
  public async pairAgent(port: number, stationCode: string, pairingCode: string): Promise<AgentStatusInfo> {
    return new Promise((resolve, reject) => {
      const isDev = process.env.NODE_ENV === "development";
      const protocol = isDev ? "ws" : "wss";
      const wsUrl = `${protocol}://127.0.0.1:${port}/ws`;

      let ws: WebSocket;

      try {
        ws = new WebSocket(wsUrl);
      } catch (e) {
        reject(e);
        return;
      }

      const timeoutId: ReturnType<typeof setTimeout> = setTimeout(() => {
        ws.close();
        reject(new Error("Kết nối quá thời gian (timeout) khi gửi yêu cầu ghép cặp."));
      }, 5000);

      ws.onopen = () => {
        const msgId = getMessageId();
        const request = {
          messageId: msgId,
          type: "agent.pair.request",
          timestamp: new Date().toISOString(),
          payload: {
            stationCode,
            pairingCode
          }
        };
        ws.send(JSON.stringify(request));
      };

      ws.onmessage = (event) => {
        clearTimeout(timeoutId);
        ws.close();
        try {
          const response = JSON.parse(event.data);
          if (response.type === "agent.pair.response" && response.payload) {
            resolve({
              status: response.payload.status as AgentStatus,
              stationId: response.payload.stationId,
              stationCode: stationCode,
              port: port
            });
          } else if (response.type === "agent.error") {
            reject(new Error(response.payload?.message || "Lỗi ghép cặp chưa rõ nguyên nhân."));
          } else {
            reject(new Error("Phản hồi không đúng định dạng."));
          }
        } catch (e) {
          reject(e);
        }
      };

      ws.onerror = () => {
        clearTimeout(timeoutId);
        reject(new Error("Lỗi kết nối WebSocket khi đang ghép cặp."));
      };
    });
  }
}
