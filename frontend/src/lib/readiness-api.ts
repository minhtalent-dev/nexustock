import api from "./api";

export type ProbeComponentDto = {
  name: string;
  status: string;
  detail?: string | null;
};

export type ReadinessProbeResponse = {
  overallStatus: string;
  components: ProbeComponentDto[];
  traceId?: string | null;
};

export type UatRunDto = {
  id: string;
  scenarioCode: string;
  status: string;
  resultNote?: string | null;
  signedOffBy?: string | null;
  signedOffAt?: string | null;
  evidenceUrl?: string | null;
  traceId?: string | null;
  createdAt: string;
};

export type UatRunListResponse = {
  items: UatRunDto[];
  total: number;
  page: number;
  pageSize: number;
};

export type CreateUatRunRequest = {
  scenarioCode: string;
  status: string;
  resultNote?: string;
  evidenceUrl?: string;
};

export type CutoverLogDto = {
  id: string;
  stepCode: string;
  status: string;
  startedAt?: string | null;
  endedAt?: string | null;
  actor: string;
  note?: string | null;
  traceId?: string | null;
};

export type CutoverLogListResponse = {
  items: CutoverLogDto[];
  total: number;
  page: number;
  pageSize: number;
};

export type FreezeStatusResponse = {
  isFrozen: boolean;
  frozenAt?: string | null;
  frozenBy?: string | null;
  reason?: string | null;
};

export type CreateIncidentDrillRequest = {
  scenarioCode: string;
  rtoMinutes: number;
  passed: boolean;
  evidenceNote?: string;
};

export type IncidentDrillDto = {
  id: string;
  scenarioCode: string;
  rtoMinutes: number;
  passed: boolean;
  conductedBy: string;
  conductedAt: string;
  evidenceNote?: string | null;
  traceId?: string | null;
};

export const readinessApi = {
  getProbe: async () => {
    const { data } = await api.get<ReadinessProbeResponse>("/admin/readiness");
    return data;
  },
  listUatRuns: async (page = 1, pageSize = 20) => {
    const { data } = await api.get<UatRunListResponse>("/admin/readiness/uat-runs", {
      params: { page, pageSize },
    });
    return data;
  },
  createUatRun: async (body: CreateUatRunRequest) => {
    const { data } = await api.post<UatRunDto>("/admin/readiness/uat-runs", body);
    return data;
  },
  signoffUatRun: async (id: string, body?: { resultNote?: string; evidenceUrl?: string }) => {
    const { data } = await api.post<UatRunDto>(`/admin/readiness/uat-runs/${id}/signoff`, body ?? {});
    return data;
  },
  createIncidentDrill: async (body: CreateIncidentDrillRequest) => {
    const { data } = await api.post<IncidentDrillDto>("/admin/readiness/incident-drills", body);
    return data;
  },
  listCutoverLogs: async (page = 1, pageSize = 20) => {
    const { data } = await api.get<CutoverLogListResponse>("/admin/cutover/logs", {
      params: { page, pageSize },
    });
    return data;
  },
  getFreezeStatus: async () => {
    const { data } = await api.get<FreezeStatusResponse>("/admin/cutover/freeze-status");
    return data;
  },
  freeze: async (reason?: string) => {
    const { data } = await api.post<FreezeStatusResponse>("/admin/cutover/freeze", { reason });
    return data;
  },
  unfreeze: async (reason?: string) => {
    const { data } = await api.post<FreezeStatusResponse>("/admin/cutover/unfreeze", { reason });
    return data;
  },
};
