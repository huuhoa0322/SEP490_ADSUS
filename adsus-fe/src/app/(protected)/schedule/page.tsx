import { ScheduleSlotManagementView } from "@/features/appointment-scheduling/components/schedule-slot-management-view";

/**
 * SCR-20 — Manage Schedule Slots (UC-15).
 * Allowed roles: Doctor, Nurse (enforced bởi backend [Authorize(Roles = "Doctor,Nurse")]).
 */
export default function SchedulePage() {
  return <ScheduleSlotManagementView />;
}