import api from "./api";

export type NextTaskRecommendationQuery = {
  currentLocationId?: string;
  currentZoneId?: string;
  sourceTaskType?: string;
  sourceTaskId?: string;
  operationType?: string;
  maxCandidates?: number;
};

export type TaskScoreExplanationDto = {
  distanceScore: number;
  ageScore: number;
  priorityScore: number;
  continuityScore: number;
  penaltyScore: number;
};

export type TaskRecommendationCandidateDto = {
  taskType: string;
  taskId: string;
  operationType: string;
  locationId?: string;
  zoneId?: string;
  score: number;
  explanation: TaskScoreExplanationDto;
};

export type NextTaskRecommendationResponse = {
  recommendationId: string;
  status: string;
  expiresAt?: string;
  selected?: TaskRecommendationCandidateDto;
  candidates: TaskRecommendationCandidateDto[];
  traceId?: string;
};

export type AcceptTaskRecommendationRequest = {
  idempotencyKey: string;
  acceptedTaskVersion?: string;
};

export type AcceptTaskRecommendationResponse = {
  recommendationId: string;
  taskType: string;
  taskId: string;
  status: string;
  assignedToUserId?: string;
  acceptedAt: string;
  traceId?: string;
};

export type RejectTaskRecommendationRequest = {
  reasonCode: string;
  note?: string;
};

export type RejectTaskRecommendationResponse = {
  recommendationId: string;
  status: string;
  reasonCode: string;
  rejectedAt: string;
  traceId?: string;
};

export type TaskRecommendationListQuery = {
  status?: string;
  userId?: string;
  operationType?: string;
  fromDate?: string;
  toDate?: string;
  page?: number;
  pageSize?: number;
};

export type TaskRecommendationListItemDto = {
  id: string;
  userId: string;
  sourceTaskType?: string;
  sourceTaskId?: string;
  status: string;
  selectedTaskType?: string;
  selectedTaskId?: string;
  selectedScore?: number;
  reasonCode?: string;
  createdAt: string;
};

export type PagedResult<T> = {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
};

export type TaskRecommendationDetailResponse = {
  id: string;
  userId: string;
  shiftId?: string;
  laborSessionId?: string;
  sourceTaskType?: string;
  sourceTaskId?: string;
  currentLocationId?: string;
  currentZoneId?: string;
  status: string;
  selectedTaskType?: string;
  selectedTaskId?: string;
  selectedScore?: number;
  reasonCode?: string;
  decisionNote?: string;
  acceptedAt?: string;
  rejectedAt?: string;
  expiresAt: string;
  traceId?: string;
  createdAt: string;
  createdBy: string;
  candidates: TaskRecommendationCandidateDto[];
};

export type TaskInterleavingKpiQuery = {
  userId?: string;
  shiftId?: string;
  zoneId?: string;
  operationType?: string;
  fromDate?: string;
  toDate?: string;
};

export type TaskInterleavingKpiResponse = {
  acceptRate: number;
  rejectRate: number;
  noCandidateRate: number;
  averageSelectedScore: number;
  averageDecisionSeconds: number;
  conflictRate: number;
  sameZoneSuggestionRate: number;
};

export const taskInterleavingApi = {
  getNext: async (params?: NextTaskRecommendationQuery): Promise<NextTaskRecommendationResponse> => {
    const res = await api.get<NextTaskRecommendationResponse>("/task-interleaving/next", { params });
    return res.data;
  },

  listRecommendations: async (params?: TaskRecommendationListQuery): Promise<PagedResult<TaskRecommendationListItemDto>> => {
    const res = await api.get<PagedResult<TaskRecommendationListItemDto>>("/task-interleaving/recommendations", { params });
    return res.data;
  },

  getRecommendation: async (id: string): Promise<TaskRecommendationDetailResponse> => {
    const res = await api.get<TaskRecommendationDetailResponse>(`/task-interleaving/recommendations/${id}`);
    return res.data;
  },

  acceptRecommendation: async (id: string, data: AcceptTaskRecommendationRequest): Promise<AcceptTaskRecommendationResponse> => {
    const res = await api.post<AcceptTaskRecommendationResponse>(`/task-interleaving/recommendations/${id}/accept`, data);
    return res.data;
  },

  rejectRecommendation: async (id: string, data: RejectTaskRecommendationRequest): Promise<RejectTaskRecommendationResponse> => {
    const res = await api.post<RejectTaskRecommendationResponse>(`/task-interleaving/recommendations/${id}/reject`, data);
    return res.data;
  },

  getKpi: async (params?: TaskInterleavingKpiQuery): Promise<TaskInterleavingKpiResponse> => {
    const res = await api.get<TaskInterleavingKpiResponse>("/task-interleaving/kpi", { params });
    return res.data;
  },
};
