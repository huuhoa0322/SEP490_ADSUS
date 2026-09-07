export type ModelVersionStatus = "Inactive" | "Active";

export interface AiModelVersion {
  modelVersionId: string; // GUID
  versionCode: string;
  description?: string;
  metricsPrecision?: number;
  metricsMap50?: number;
  metricsRecall?: number;
  hfRepoId: string;
  hfFilename: string;
  status: ModelVersionStatus;
  registeredAt: string; // ISO DateTime
  registeredBy?: string; // GUID
  
  // Live Metrics
  liveTp?: number;
  liveFp?: number;
  liveFn?: number;
  liveMap50?: number;
  lastEvaluatedAt?: string;
  livePrecision?: number;
  liveRecall?: number;
}

export interface RegisterModelVersionRequest {
  versionCode: string;
  description?: string;
  hfRepoId: string;
  hfFilename: string;
  metricsPrecision?: number;
  metricsMap50?: number;
  metricsRecall?: number;
}

export interface UpdateModelVersionRequest {
  description?: string;
  hfRepoId: string;
  hfFilename: string;
  metricsPrecision?: number;
  metricsMap50?: number;
  metricsRecall?: number;
}

export interface ActivateVersionRequest {
  status: "ACTIVE";
}

// Doctor-facing (UC-20): chỉ code/status của phiên bản đang Active, không có Metrics/Live/RegisteredBy.
export interface ActiveAiModelVersion {
  versionCode: string;
  status: ModelVersionStatus;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalItems: number;
  totalPages: number;
}

export interface AiModelListQuery {
  keyword?: string;
  page?: number;
  pageSize?: number;
}
