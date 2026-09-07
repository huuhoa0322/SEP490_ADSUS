import type { Metadata } from "next";
import { NurseCheckinView } from "@/features/nurse-checkin/components/nurse-checkin-view";

export const metadata: Metadata = {
  title: "Nurse Check-In | ADSUS",
};

export default function NurseCheckinPage() {
  return <NurseCheckinView />;
}
