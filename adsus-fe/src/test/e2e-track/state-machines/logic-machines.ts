/**
 * Authoritative Domain State Machines & Verification Logic for E2E Testing Track
 */

export interface DateRangeState {
  startDate: string; // "yyyy-MM-dd"
  endDate: string;   // "yyyy-MM-dd"
}

export class DateRangeMachine {
  private state: DateRangeState;

  constructor(initialToday?: string) {
    const today = initialToday ?? new Date().toISOString().split("T")[0];
    this.state = {
      startDate: today,
      endDate: today,
    };
  }

  public getState(): DateRangeState {
    return { ...this.state };
  }

  public setRange(start: string, end: string): { valid: boolean; error?: string } {
    if (new Date(start) > new Date(end)) {
      // Auto-correct end date to match start date, or report validation error
      return {
        valid: false,
        error: "Từ ngày không thể lớn hơn đến ngày (Start date cannot be after end date)",
      };
    }
    this.state = { startDate: start, endDate: end };
    return { valid: true };
  }

  public setRangeWithAutoCorrection(start: string, end: string): DateRangeState {
    if (new Date(start) > new Date(end)) {
      this.state = { startDate: start, endDate: start };
    } else {
      this.state = { startDate: start, endDate: end };
    }
    return { ...this.state };
  }

  public applyPreset(preset: "today" | "last7days" | "last30days", baseDate?: Date): DateRangeState {
    const now = baseDate ?? new Date();
    const format = (d: Date) => d.toISOString().split("T")[0];

    const todayStr = format(now);
    if (preset === "today") {
      this.state = { startDate: todayStr, endDate: todayStr };
    } else if (preset === "last7days") {
      const past = new Date(now);
      past.setDate(past.getDate() - 6);
      this.state = { startDate: format(past), endDate: todayStr };
    } else if (preset === "last30days") {
      const past = new Date(now);
      past.setDate(past.getDate() - 29);
      this.state = { startDate: format(past), endDate: todayStr };
    }
    return { ...this.state };
  }

  public getDaySpan(): number {
    const start = new Date(this.state.startDate);
    const end = new Date(this.state.endDate);
    const diffTime = Math.abs(end.getTime() - start.getTime());
    return Math.ceil(diffTime / (1000 * 60 * 60 * 24)) + 1;
  }
}

export class PaginationMachine<T> {
  private items: T[];
  private currentPage: number;
  private readonly pageSize: number;

  constructor(items: T[], pageSize = 15, initialPage = 1) {
    this.items = items;
    this.pageSize = pageSize <= 0 ? 15 : pageSize;
    this.currentPage = Math.max(1, initialPage);
  }

  public setItems(items: T[]): void {
    this.items = items;
    this.currentPage = 1; // Automatic reset on data/filter change
  }

  public getTotalItems(): number {
    return this.items.length;
  }

  public getTotalPages(): number {
    if (this.items.length === 0) return 0;
    return Math.ceil(this.items.length / this.pageSize);
  }

  public getCurrentPage(): number {
    return this.currentPage;
  }

  public setPage(page: number): boolean {
    const totalPages = this.getTotalPages();
    if (totalPages === 0) {
      this.currentPage = 1;
      return false;
    }
    if (page < 1 || page > totalPages) {
      return false;
    }
    this.currentPage = page;
    return true;
  }

  public nextPage(): boolean {
    return this.setPage(this.currentPage + 1);
  }

  public prevPage(): boolean {
    return this.setPage(this.currentPage - 1);
  }

  public isPrevDisabled(): boolean {
    return this.currentPage <= 1;
  }

  public isNextDisabled(): boolean {
    const total = this.getTotalPages();
    return total === 0 || this.currentPage >= total;
  }

  public getPagedSlice(): T[] {
    if (this.items.length === 0) return [];
    const start = (this.currentPage - 1) * this.pageSize;
    const end = start + this.pageSize;
    return this.items.slice(start, end);
  }
}

export class KpiCalculator {
  public static calculateProgress(checkedIn: number, total: number): number {
    if (total <= 0) return 0; // Guard against NaN / division by zero
    const percentage = (checkedIn / total) * 100;
    return Math.min(100, Math.max(0, Math.round(percentage)));
  }

  public static summarizeQueue(
    appointments: Array<{ status: string }>
  ): {
    total: number;
    checkedIn: number;
    pending: number;
    completed: number;
    cancelled: number;
    progressPercentage: number;
  } {
    let checkedIn = 0;
    let pending = 0;
    let completed = 0;
    let cancelled = 0;

    for (const app of appointments) {
      switch (app.status) {
        case "APPROVED":
          checkedIn++;
          break;
        case "BOOKED":
          pending++;
          break;
        case "COMPLETED":
          completed++;
          break;
        case "CANCELLED":
          cancelled++;
          break;
      }
    }

    const total = appointments.length;
    const progressPercentage = this.calculateProgress(checkedIn, total);

    return {
      total,
      checkedIn,
      pending,
      completed,
      cancelled,
      progressPercentage,
    };
  }
}

export class StarRatingCalculator {
  public static normalizeRating(rawRating: number): number {
    if (Number.isNaN(rawRating)) return 1;
    const rounded = Math.round(rawRating);
    return Math.min(5, Math.max(1, rounded));
  }

  public static getVisualStars(rating: number): {
    filledStars: number;
    mutedStars: number;
    badgeText: string;
  } {
    const normalized = this.normalizeRating(rating);
    return {
      filledStars: normalized,
      mutedStars: 5 - normalized,
      badgeText: `(${normalized}/5)`,
    };
  }
}

export type Role = "ADMIN" | "DOCTOR" | "STAFF" | "PATIENT" | "PHARMACIST";

export class RbacPolicyEvaluator {
  private routeRoles: Array<{ prefix: string; roles: Role[] }>;

  constructor(customRouteRoles?: Array<{ prefix: string; roles: Role[] }>) {
    // Baseline + Target configuration matching auth-store.ts & M4 grant
    this.routeRoles = customRouteRoles ?? [
      { prefix: "/dashboard", roles: ["ADMIN"] },
      { prefix: "/patients", roles: ["DOCTOR", "STAFF", "ADMIN"] }, // Target grant for Admin
      { prefix: "/cases", roles: ["DOCTOR", "STAFF", "ADMIN"] },    // Target grant for Admin
      { prefix: "/admin", roles: ["ADMIN"] },
      { prefix: "/medicines", roles: ["ADMIN", "PHARMACIST", "DOCTOR"] },
      { prefix: "/suppliers", roles: ["ADMIN", "PHARMACIST"] },
      { prefix: "/inventory", roles: ["ADMIN", "PHARMACIST"] },
      { prefix: "/prescriptions", roles: ["DOCTOR"] },
      { prefix: "/schedule", roles: ["DOCTOR"] },
    ];
  }

  public isAllowed(role: Role, pathname: string): boolean {
    const rule = this.routeRoles.find(
      (r) => pathname === r.prefix || pathname.startsWith(`${r.prefix}/`)
    );
    if (!rule) return true; // Generic authenticated route
    return rule.roles.includes(role);
  }
}

export class DebounceSimulator {
  private lastCallTime = 0;
  private pendingTimer: NodeJS.Timeout | null = null;
  private executedCalls = 0;
  private latestQuery = "";

  public queueInput(
    query: string,
    delayMs = 300,
    callback?: (q: string) => void
  ): Promise<string> {
    return new Promise((resolve) => {
      if (this.pendingTimer) {
        clearTimeout(this.pendingTimer);
      }
      this.latestQuery = query;
      this.pendingTimer = setTimeout(() => {
        this.executedCalls++;
        this.lastCallTime = Date.now();
        if (callback) callback(this.latestQuery);
        resolve(this.latestQuery);
      }, delayMs);
    });
  }

  public getExecutedCalls(): number {
    return this.executedCalls;
  }

  public cancel(): void {
    if (this.pendingTimer) {
      clearTimeout(this.pendingTimer);
      this.pendingTimer = null;
    }
  }
}
