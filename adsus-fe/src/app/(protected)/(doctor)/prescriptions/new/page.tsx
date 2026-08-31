"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { useMemo } from "react";

import { useCreatePrescription } from "@/features/prescriptions/hooks/use-prescriptions";
import { PrescriptionForm } from "@/features/prescriptions/components/prescription-form";
import type { PrescriptionFormData } from "@/features/prescriptions/components/prescription-form";
import { useCaseDetail } from "@/features/medical-record/hooks/use-cases";

export default function NewPrescriptionPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const caseId = searchParams.get("caseId") ?? undefined;
  
  const createMutation = useCreatePrescription();

  const { data: medicalCase, isLoading: isLoadingCase } = useCaseDetail(caseId);



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
        quantityPerDose: item.quantityPerDose,
        scheduleSlots: item.scheduleSlots,
        durationDays: item.durationDays,
        startDate: item.startDate,
        instructions: item.instructions ?? "",
      })),
      generalNote: data.generalNote ?? "",
    };

    await createMutation.mutateAsync(request);
    // Sau khi lưu thành công → redirect về trang chi tiết ca.
    router.push(`/cases/${targetCaseId}`);
  }

  const isLoading = isLoadingCase;

  return (
    <div className="mx-auto w-4/5 py-8">
      <div className="mb-6">
        <button
          type="button"
          onClick={() => router.back()}
          className="mb-2 text-sm text-teal hover:underline"
        >
          ← Quay lại
        </button>
        <h1 className="font-heading text-2xl font-semibold text-primary">
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
      ) : (
        <PrescriptionForm
          prefilledPatient={prefilledPatient}
          onSubmit={handleSubmit}
        />
      )}
    </div>
  );
}

