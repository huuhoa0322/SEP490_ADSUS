"use client";

import { useMemo, useState } from "react";
import {
  AlertCircle,
  AlertTriangle,
  Loader2,
  Plus,
  Stethoscope,
  Trash2,
  X,
} from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { formatCurrency } from "@/lib/utils";
import { useCaseInvoices } from "@/features/prescription-adherence/hooks/use-invoices";
import {
  useAddCaseClinicService,
  useCaseClinicServices,
  useClinicServicesList,
  useRemoveCaseClinicService,
} from "../queries";
import type { CaseClinicService } from "../types";

export interface CaseClinicServicesPanelProps {
  caseId: string;
  caseStatus: string;
  variant?: "card" | "compact";
}

function CaseClinicServicesPanelContent({
  caseId,
  caseStatus,
  variant = "card",
}: CaseClinicServicesPanelProps) {
  const { data: caseServices, isLoading: isLoadingServices } =
    useCaseClinicServices(caseId);
  const { data: activeCatalog } = useClinicServicesList(true);
  const { data: invoices } = useCaseInvoices(caseId);

  const addMutation = useAddCaseClinicService(caseId);
  const removeMutation = useRemoveCaseClinicService(caseId);

  const [isAddOpen, setIsAddOpen] = useState(false);
  const [selectedServiceId, setSelectedServiceId] = useState("");
  const [serviceToDelete, setServiceToDelete] =
    useState<CaseClinicService | null>(null);

  // Business Rules / Deletion & Addition Guards
  const isCaseClosed = caseStatus === "END" || caseStatus === "CANCELLED";
  const hasPaidInvoice = useMemo(
    () => invoices?.some((inv) => inv.status === "PAID") ?? false,
    [invoices],
  );

  const canAdd = !isCaseClosed;
  const canDelete = !isCaseClosed && !hasPaidInvoice;

  // Filter out services already attached to this case
  const availableServices = useMemo(() => {
    if (!activeCatalog) return [];
    const attachedIds = new Set(
      caseServices?.map((cs) => cs.clinicServiceId) ?? [],
    );
    return activeCatalog.filter((s) => !attachedIds.has(s.id));
  }, [activeCatalog, caseServices]);

  const totalPrice = useMemo(() => {
    return (
      caseServices?.reduce((sum, item) => sum + (item.priceAtTime || 0), 0) ?? 0
    );
  }, [caseServices]);

  const handleAddSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedServiceId) return;

    addMutation.mutate(selectedServiceId, {
      onSuccess: () => {
        setIsAddOpen(false);
        setSelectedServiceId("");
      },
    });
  };

  const handleConfirmDelete = () => {
    if (!serviceToDelete) return;

    removeMutation.mutate(serviceToDelete.id, {
      onSuccess: () => {
        setServiceToDelete(null);
      },
    });
  };

  const renderAddDialog = () => (
    <Dialog open={isAddOpen} onOpenChange={setIsAddOpen}>
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle className="font-heading text-lg font-bold text-foreground">
            Thêm dịch vụ vào ca khám
          </DialogTitle>
          <DialogDescription className="text-sm text-muted-foreground">
            Chọn dịch vụ phòng khám đang hoạt động để áp dụng cho ca khám này.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleAddSubmit} className="space-y-4 py-2">
          {availableServices.length === 0 ? (
            <p className="py-4 text-center text-sm text-muted-foreground">
              Tất cả các dịch vụ đang hoạt động đã được gắn vào ca khám này hoặc không
              có dịch vụ khả dụng.
            </p>
          ) : (
            <div className="space-y-2">
              <label
                htmlFor="select-clinic-service"
                className="text-sm font-semibold text-foreground"
              >
                Chọn dịch vụ <span className="text-destructive">*</span>
              </label>
              <select
                id="select-clinic-service"
                value={selectedServiceId}
                onChange={(e) => setSelectedServiceId(e.target.value)}
                className="flex h-10 w-full rounded-md border border-input bg-background px-3 py-2 text-sm shadow-xs focus:border-ring focus:outline-none focus:ring-1 focus:ring-ring font-medium"
              >
                {availableServices.map((service) => (
                  <option key={service.id} value={service.id}>
                    {variant === "compact"
                      ? service.name
                      : `${service.name} (${service.code}) — ${formatCurrency(service.price)}`}
                  </option>
                ))}
              </select>
            </div>
          )}

          <DialogFooter className="pt-2 gap-2">
            <Button
              type="button"
              variant="outline"
              onClick={() => setIsAddOpen(false)}
              disabled={addMutation.isPending}
            >
              Hủy bỏ
            </Button>
            <Button
              type="submit"
              disabled={
                availableServices.length === 0 ||
                !selectedServiceId ||
                addMutation.isPending
              }
            >
              {addMutation.isPending && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Thêm vào ca khám
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );

  const renderDeleteDialog = () => (
    <Dialog
      open={Boolean(serviceToDelete)}
      onOpenChange={(open) => {
        if (!open) setServiceToDelete(null);
      }}
    >
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <div className="flex items-center gap-3">
            <span className="flex size-10 items-center justify-center rounded-full bg-destructive/10 text-destructive">
              <AlertTriangle className="size-5" />
            </span>
            <div>
              <DialogTitle className="font-heading text-lg font-bold text-foreground">
                Xác nhận xóa dịch vụ
              </DialogTitle>
              <DialogDescription className="text-sm text-muted-foreground mt-1">
                Thao tác này sẽ gỡ dịch vụ khỏi ca khám hiện tại.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        <div className="py-2 text-sm text-foreground">
          Bạn có chắc chắn muốn xóa dịch vụ{" "}
          <span className="font-bold text-foreground">
            {serviceToDelete?.serviceName}
          </span>{" "}
          khỏi ca khám này không? Nếu ca khám có hóa đơn chờ thanh toán, khoản phí này sẽ
          được tự động trừ khỏi hóa đơn.
        </div>

        <DialogFooter className="gap-2 sm:justify-end">
          <Button
            type="button"
            variant="outline"
            onClick={() => setServiceToDelete(null)}
            disabled={removeMutation.isPending}
          >
            Hủy bỏ
          </Button>
          <Button
            type="button"
            variant="destructive"
            onClick={handleConfirmDelete}
            disabled={removeMutation.isPending}
          >
            {removeMutation.isPending && (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            )}
            Xóa dịch vụ
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (variant === "compact") {
    return (
      <div className="flex flex-wrap items-center gap-2 pt-3 border-t border-border/80 text-sm">
        <div className="flex items-center gap-1.5 font-bold text-foreground text-xs">
          <Stethoscope className="size-3.5 text-primary" />
          <span>Dịch vụ khám:</span>
        </div>

        {isLoadingServices ? (
          <div className="flex items-center gap-2">
            <Skeleton className="h-6 w-24 rounded-full" />
            <Skeleton className="h-6 w-28 rounded-full" />
          </div>
        ) : !caseServices || caseServices.length === 0 ? (
          <span className="text-xs text-muted-foreground italic">Chưa có dịch vụ nào</span>
        ) : (
          <div className="flex flex-wrap items-center gap-1.5">
            {caseServices.map((service) => {
              const deleteDisabled = !canDelete;
              return (
                <span
                  key={service.id}
                  className="inline-flex items-center gap-1.5 rounded-full bg-blue-50 dark:bg-blue-950/40 border border-blue-200 dark:border-blue-800 px-3 py-1 text-xs font-semibold text-blue-900 dark:text-blue-200"
                >
                  <span>{service.serviceName}</span>
                  {canDelete && (
                    <button
                      type="button"
                      disabled={deleteDisabled || removeMutation.isPending}
                      onClick={() => setServiceToDelete(service)}
                      title="Xóa dịch vụ này khỏi ca khám"
                      className="ml-0.5 rounded-full p-0.5 text-blue-400 hover:bg-destructive/10 hover:text-destructive transition-colors disabled:opacity-40"
                    >
                      <X className="size-3" />
                      <span className="sr-only">Xóa {service.serviceName}</span>
                    </button>
                  )}
                </span>
              );
            })}
          </div>
        )}

        {hasPaidInvoice && (
          <span className="text-xs text-amber-700 dark:text-amber-400 flex items-center gap-1 font-medium bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 px-2.5 py-0.5 rounded-full">
            <AlertCircle className="size-3 shrink-0" /> Đã thanh toán
          </span>
        )}

        {canAdd && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => {
              setSelectedServiceId(availableServices[0]?.id ?? "");
              setIsAddOpen(true);
            }}
            className="h-6 px-2.5 text-xs gap-1 rounded-full border-dashed hover:border-primary hover:text-primary transition-colors"
          >
            <Plus className="size-3" />
            Thêm dịch vụ
          </Button>
        )}

        {renderAddDialog()}
        {renderDeleteDialog()}
      </div>
    );
  }

  return (
    <Card className="rounded-xl border border-gray-300 dark:border-gray-700 bg-card shadow-sm">
      <CardHeader className="flex flex-row items-center justify-between border-b border-border pb-4">
        <div className="flex items-center gap-2.5">
          <span className="flex size-9 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Stethoscope className="size-5" />
          </span>
          <div>
            <CardTitle className="font-heading text-lg font-bold text-foreground">
              Dịch vụ khám
            </CardTitle>
            <p className="text-xs text-muted-foreground mt-0.5">
              Các dịch vụ y tế và thủ thuật áp dụng cho ca khám này
            </p>
          </div>
        </div>

        {canAdd && (
          <Button
            size="sm"
            onClick={() => {
              setSelectedServiceId(availableServices[0]?.id ?? "");
              setIsAddOpen(true);
            }}
            className="gap-1.5 font-semibold text-xs h-8"
          >
            <Plus className="size-3.5" />
            Thêm dịch vụ
          </Button>
        )}
      </CardHeader>

      <CardContent className="p-4 sm:p-6">
        {hasPaidInvoice && (
          <div className="mb-4 flex items-center gap-2 rounded-lg bg-amber-50 dark:bg-amber-950/40 p-3 text-xs font-semibold text-amber-800 dark:text-amber-300 border border-amber-200 dark:border-amber-800">
            <AlertCircle className="size-4 shrink-0" />
            <span>
              Hóa đơn cho ca khám này đã được thanh toán. Không thể xóa hoặc thay đổi
              dịch vụ đã áp dụng.
            </span>
          </div>
        )}

        <div className="overflow-x-auto">
          <Table>
            <TableHeader className="bg-muted/30">
              <TableRow>
                <TableHead className="w-[120px] font-bold">Mã DV</TableHead>
                <TableHead className="font-bold">Tên dịch vụ</TableHead>
                <TableHead className="text-right font-bold w-[160px]">
                  Đơn giá
                </TableHead>
                <TableHead className="w-[180px] font-bold">Thời gian thêm</TableHead>
                <TableHead className="text-right font-bold w-[90px]">Xóa</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoadingServices ? (
                Array.from({ length: 3 }).map((_, idx) => (
                  <TableRow key={idx}>
                    <TableCell>
                      <Skeleton className="h-4 w-16" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-40" />
                    </TableCell>
                    <TableCell className="text-right">
                      <Skeleton className="h-4 w-20 ml-auto" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-4 w-28" />
                    </TableCell>
                    <TableCell className="text-right">
                      <Skeleton className="h-7 w-7 ml-auto rounded-md" />
                    </TableCell>
                  </TableRow>
                ))
              ) : !caseServices || caseServices.length === 0 ? (
                <TableRow>
                  <TableCell
                    colSpan={5}
                    className="h-28 text-center text-muted-foreground"
                  >
                    <p className="font-medium">
                      Chưa có dịch vụ nào được gắn cho ca khám này.
                    </p>
                  </TableCell>
                </TableRow>
              ) : (
                <>
                  {caseServices.map((service) => {
                    const deleteDisabled = !canDelete;
                    const tooltipText = hasPaidInvoice
                      ? "Không thể xóa dịch vụ vì hóa đơn đã được thanh toán."
                      : isCaseClosed
                        ? "Không thể xóa dịch vụ khi ca khám đã kết thúc."
                        : "Xóa dịch vụ này khỏi ca khám";

                    return (
                      <TableRow key={service.id} className="hover:bg-muted/20">
                        <TableCell className="font-mono text-xs font-bold text-foreground">
                          {service.serviceCode}
                        </TableCell>
                        <TableCell className="font-semibold text-foreground">
                          {service.serviceName}
                        </TableCell>
                        <TableCell className="text-right font-bold text-primary">
                          {formatCurrency(service.priceAtTime)}
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {service.createdAt
                            ? new Date(service.createdAt).toLocaleString("vi-VN")
                            : "—"}
                        </TableCell>
                        <TableCell className="text-right">
                          <Button
                            variant="ghost"
                            size="icon"
                            disabled={deleteDisabled || removeMutation.isPending}
                            title={tooltipText}
                            onClick={() => setServiceToDelete(service)}
                            className="size-8 text-destructive hover:bg-destructive/10 hover:text-destructive disabled:opacity-40"
                          >
                            <Trash2 className="size-4" />
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}

                  <TableRow className="bg-muted/15 font-bold border-t-2">
                    <TableCell colSpan={2} className="text-right text-foreground">
                      Tổng tiền dịch vụ:
                    </TableCell>
                    <TableCell className="text-right text-base text-primary">
                      {formatCurrency(totalPrice)}
                    </TableCell>
                    <TableCell colSpan={2} />
                  </TableRow>
                </>
              )}
            </TableBody>
          </Table>
        </div>
      </CardContent>

      {renderAddDialog()}
      {renderDeleteDialog()}
    </Card>
  );
}

export function CaseClinicServicesPanel(props: CaseClinicServicesPanelProps) {
  let hasClient = true;
  try {
    useQueryClient();
  } catch {
    hasClient = false;
  }

  if (!hasClient) {
    return null;
  }

  return <CaseClinicServicesPanelContent {...props} />;
}
