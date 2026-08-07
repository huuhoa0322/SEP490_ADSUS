"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect } from "react";

import {
  createPrescription,
  listMyCases,
} from "@/features/prescriptions/api/prescriptions.api";
import { PrescriptionForm } from "@/features/prescriptions/components/prescription-form";
import type { PrescriptionFormData } from "@/features/prescriptions/components/prescription-form";

export default function NewPrescriptionPage() {
  const router = useRouter();

  // Load cases (Confirmed) + medication catalog in parallel
  const casesQuery = useQuery({
    queryKey: ["prescriptions", "my-cases"],
    queryFn: listMyCases,
    staleTime: 5 * 60 * 1000, // 5 phút — danh sách ca khám ít thay đổi
  });

  const medicationsQuery = useQuery({
    queryKey: ["medication-catalog"],
    queryFn: async () => {
      const { getMedicationCatalog } = await import(
        "@/features/prescriptions/api/prescriptions.api"
      );
      return getMedicationCatalog();
    },
    staleTime: 30 * 60 * 1000, // 30 phút — danh mục thuốc hiếm thay đổi
  });

  async function handleSubmit(data: PrescriptionFormData) {
    // Map form data → CreatePrescriptionRequest (backend expects PascalCase)
    const request = {
      caseId: data.caseId,
      items: data.items.map((item) => ({
        medicineId: item.medicineId,
        dosage: item.dosage,
        scheduleSlots: item.scheduleSlots,
        durationDays: item.durationDays,
        startDate: item.startDate,
        instructions: item.instructions ?? "",
      })),
      generalNote: data.generalNote ?? "",
    };

    const result = await createPrescription(request);
    router.push(`/doctor/prescriptions/${result.prescriptionId}`);
  }

  const isLoading = casesQuery.isLoading || medicationsQuery.isLoading;

  return (
    <div className="container mx-auto max-w-4xl py-8">
      <div className="mb-6">
        <button
          type="button"
          onClick={() => router.back()}
          className="mb-2 text-sm text-teal hover:underline"
        >
          ← Quay lại
        </button>
        <h1 className="font-exo text-2xl font-semibold text-navy">
          Kê đơn thuốc mới
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Chỉ bác sĩ mới được kê đơn cho ca khám đang Confirmed của mình (GB-04)
        </p>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-teal border-t-transparent" />
        </div>
      ) : casesQuery.isError || medicationsQuery.isError ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-red-600">
          Không tải được dữ liệu. Vui lòng tải lại trang.
        </div>
      ) : (
        <PrescriptionForm
          cases={casesQuery.data ?? []}
          medications={
            (medicationsQuery.data ?? []).map((m) => ({
              medicineId: m.medicineId,
              name: m.name,
            }))
          }
          onSubmit={handleSubmit}
        />
      )}
    </div>
  );
}
