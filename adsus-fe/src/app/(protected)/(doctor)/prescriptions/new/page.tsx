"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { useMemo } from "react";

import {
  createPrescription,
  getMedicationCatalog,
} from "@/features/prescriptions/api/prescriptions.api";
import { PrescriptionForm } from "@/features/prescriptions/components/prescription-form";
import type { PrescriptionFormData } from "@/features/prescriptions/components/prescription-form";
import { useCaseDetail } from "@/features/medical-record/hooks/use-cases";

export default function NewPrescriptionPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const caseId = searchParams.get("caseId") ?? undefined;

  const { data: medicalCase, isLoading: isLoadingCase } = useCaseDetail(caseId);

  const medicationsQuery = useQuery({
    queryKey: ["medication-catalog"],
    queryFn: getMedicationCatalog,
    staleTime: 30 * 60 * 1000,
  });

  const prefilledPatient = useMemo(() => {
    if (!medicalCase?.patientProfile) return undefined;
    return {
      caseId: medicalCase.caseId,
      patientName: medicalCase.patientProfile.fullName,
    };
  }, [medicalCase]);

  async function handleSubmit(data: PrescriptionFormData) {
    const targetCaseId = caseId ?? data.caseId;
    if (!targetCaseId) {
      throw new Error("Không xác định được ca khám");
    }

    const request = {
      caseId: targetCaseId,
      items: data.items.map((item) => ({
        medicineName: item.medicineName,
        dosage: item.dosage,
        scheduleSlots: item.scheduleSlots,
        durationDays: item.durationDays,
        startDate: item.startDate,
        instructions: item.instructions ?? "",
      })),
      generalNote: data.generalNote ?? "",
    };

    await createPrescription(request);
    // Sau khi lưu thành công → redirect về trang chi tiết ca.
    router.push(`/cases/${targetCaseId}`);
  }

  const isLoading = isLoadingCase || medicationsQuery.isLoading;

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
          {caseId ? "Kê đơn thuốc" : "Kê đơn thuốc mới"}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Chỉ bác sĩ mới được kê đơn cho ca khám đang Confirmed của mình (GB-04)
        </p>
      </div>

      {isLoading ? (
        <div className="flex h-64 items-center justify-center">
          <div className="h-8 w-8 animate-spin rounded-full border-4 border-teal border-t-transparent" />
        </div>
      ) : caseId && (isLoadingCase || !medicalCase) ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-red-600">
          Không tìm thấy ca khám. Vui lòng kiểm tra lại.
        </div>
      ) : medicationsQuery.isError ? (
        <div className="rounded-2xl border border-red-200 bg-red-50 p-6 text-center text-red-600">
          Không tải được danh mục thuốc. Vui lòng tải lại trang.
        </div>
      ) : (
        <PrescriptionForm
          prefilledPatient={prefilledPatient}
          medications={(medicationsQuery.data ?? []).map((m) => ({
            medicineId: m.medicineId,
            name: m.name,
          }))}
          onSubmit={handleSubmit}
        />
      )}
    </div>
  );
}
