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
