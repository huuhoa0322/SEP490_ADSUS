/**
 * Shared envelope for every ADSUS_BE response, per the team's api_design_rules.
 * The backend always returns this shape, including on errors.
 */
export interface ApiResponse<T> {
  code: number;
  message: string;
  data: T | null;
}

/**
 * Account role. Matches the user_role enum in the database.
 * NURSE exists in the UCS but has not been added to the database yet.
 */
export type Role = "ADMIN" | "DOCTOR" | "PATIENT";
