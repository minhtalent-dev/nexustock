import api from "@/lib/api";

export type MigrateDryRun = {
  eligibleCount: number;
  alreadyOnTarget: number;
  jobTotal: number;
  truncated: boolean;
  sampleKeys: string[];
  targetTestOk: boolean;
  targetProvider: string | null;
};

export type MigrateJob = {
  jobId: string;
  status: string;
  sourceProvider: string | null;
  targetProvider: string;
  totalCount: number;
  successCount: number;
  skipCount: number;
  failCount: number;
  truncated: boolean;
  eligibleFullCount: number;
  deleteSourceAfter: boolean;
  cancelRequested: boolean;
  errorSummary: string | null;
  createdAt: string;
  startedAt: string | null;
  finishedAt: string | null;
  updatedAt: string | null;
};

export type MigrateJobError = {
  attachmentId: string;
  message: string;
  createdAt: string;
};

export async function dryRunMigrate(body: {
  sourceProvider?: string | null;
  targetProvider?: string | null;
}): Promise<MigrateDryRun> {
  const res = await api.post<MigrateDryRun>("/files/storage-migrate/dry-run", body);
  return res.data;
}

export async function startMigrateJob(body: {
  sourceProvider?: string | null;
  targetProvider?: string | null;
  deleteSourceAfter?: boolean;
}): Promise<MigrateJob> {
  const res = await api.post<MigrateJob>("/files/storage-migrate/jobs", body);
  return res.data;
}

export async function getMigrateJob(id: string): Promise<MigrateJob> {
  const res = await api.get<MigrateJob>(`/files/storage-migrate/jobs/${id}`);
  return res.data;
}

export async function getActiveMigrateJob(): Promise<MigrateJob | null> {
  const res = await api.get<MigrateJob>("/files/storage-migrate/jobs/active", {
    validateStatus: (s) => s === 200 || s === 204,
  });
  if (res.status === 204 || !res.data) return null;
  return res.data;
}

export async function cancelMigrateJob(id: string): Promise<MigrateJob> {
  const res = await api.post<MigrateJob>(`/files/storage-migrate/jobs/${id}/cancel`);
  return res.data;
}

export async function resumeMigrateJob(id: string): Promise<MigrateJob> {
  const res = await api.post<MigrateJob>(`/files/storage-migrate/jobs/${id}/resume`);
  return res.data;
}

export async function purgeMigrateSource(id: string): Promise<MigrateJob> {
  const res = await api.post<MigrateJob>(`/files/storage-migrate/jobs/${id}/purge-source`);
  return res.data;
}

export async function getMigrateErrors(id: string, take = 50): Promise<MigrateJobError[]> {
  const res = await api.get<MigrateJobError[]>(`/files/storage-migrate/jobs/${id}/errors`, {
    params: { take },
  });
  return res.data;
}
