import api from "./api";

export type StartLaborSessionRequest = {
  sourceTaskType: string;
  sourceTaskId?: string;
  operationType: string;
  locationId?: string;
};

export type LaborSessionActionResponse = {
  sessionId: string;
  status: string;
  startedAt: string;
  shiftId: string;
};

export type LaborSessionDto = {
  id: string;
  sourceTaskType: string;
  sourceTaskId?: string;
  referenceType: string;
  referenceId?: string;
  userId: string;
  shiftId: string;
  locationId?: string;
  zoneId?: string;
  operationType: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  durationSeconds: number;
  pausedSeconds: number;
  lastPausedAt?: string;
  timeoutAt?: string;
};

export type LaborSessionsQuery = {
  status?: string;
  userId?: string;
  operationType?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
};

export type LaborSessionsResponse = {
  items: LaborSessionDto[];
  total: number;
  page: number;
  pageSize: number;
};

export type LaborKpiQuery = {
  userId?: string;
  shiftId?: string;
  zoneId?: string;
  operationType?: string;
  fromDate?: string;
  toDate?: string;
};

export type LaborKpiSummaryDto = {
  completedTaskCount: number;
  activeSeconds: number;
  pausedSeconds: number;
  averageSecondsPerTask: number;
  tasksPerHour: number;
  idleSeconds: number;
};

export type LaborKpiGroupDto = {
  key: string;
  completedTaskCount: number;
  activeSeconds: number;
  averageSecondsPerTask: number;
  tasksPerHour: number;
};

export type LaborKpiResponse = {
  summary: LaborKpiSummaryDto;
  groupByUser: LaborKpiGroupDto[];
  groupByShift: LaborKpiGroupDto[];
  groupByZone: LaborKpiGroupDto[];
  groupByOperation: LaborKpiGroupDto[];
};

export type LaborKpiPointDto = {
  label: string;
  value: number;
};

export type LaborKpiChartResponse = {
  throughputTrend: LaborKpiPointDto[];
  tasksPerHourTrend: LaborKpiPointDto[];
  operationMix: LaborKpiPointDto[];
  userProductivityRanking: LaborKpiPointDto[];
  zoneProductivity: LaborKpiPointDto[];
};

export type CurrentShiftResponse = {
  shiftId: string;
  shiftCode: string;
  startedAt: string;
  status: string;
};

export const laborApi = {
  startSession: async (data: StartLaborSessionRequest): Promise<LaborSessionActionResponse> => {
    const res = await api.post<LaborSessionActionResponse>("/labor/sessions/start", data);
    return res.data;
  },

  pauseSession: async (id: string): Promise<LaborSessionDto> => {
    const res = await api.post<LaborSessionDto>(`/labor/sessions/${id}/pause`);
    return res.data;
  },

  resumeSession: async (id: string): Promise<LaborSessionDto> => {
    const res = await api.post<LaborSessionDto>(`/labor/sessions/${id}/resume`);
    return res.data;
  },

  completeSession: async (id: string): Promise<LaborSessionDto> => {
    const res = await api.post<LaborSessionDto>(`/labor/sessions/${id}/complete`);
    return res.data;
  },

  cancelSession: async (id: string, reason: string): Promise<LaborSessionDto> => {
    const res = await api.post<LaborSessionDto>(`/labor/sessions/${id}/cancel`, { reason });
    return res.data;
  },

  listSessions: async (params?: LaborSessionsQuery): Promise<LaborSessionsResponse> => {
    const res = await api.get<LaborSessionsResponse>("/labor/sessions", { params });
    return res.data;
  },

  getKpi: async (params?: LaborKpiQuery): Promise<LaborKpiResponse> => {
    const res = await api.get<LaborKpiResponse>("/labor/kpi", { params });
    return res.data;
  },

  getKpiCharts: async (params?: LaborKpiQuery): Promise<LaborKpiChartResponse> => {
    const res = await api.get<LaborKpiChartResponse>("/labor/kpi/charts", { params });
    return res.data;
  },

  getCurrentShift: async (): Promise<CurrentShiftResponse> => {
    const res = await api.get<CurrentShiftResponse>("/labor/shifts/current");
    return res.data;
  },
};
