"use client";

import { Loader2, PlusCircle, Pencil, PlayCircle, Ban, Search, Package, AlertTriangle, Pill, PackageCheck, PackageX } from "lucide-react";
import { useState } from "react";
import { useRouter } from "next/navigation";
import toast from "react-hot-toast";

import { 
  useMedicines, 
  useActivateMedicine,
  useDeleteMedicine 
} from "../hooks/use-medicines";
import { useInventoryAlerts } from "@/features/medicines/api/inventory.api";
import { formatDateTime } from "@/features/user-role-management/lib/user-labels";
import { useAuthStore } from "@/store/auth-store";
import type { MedicineResponse } from "../api/medicines-api";
import { getApiErrorMessage } from "@/lib/api-client";
import { ConfirmDialog } from "@/features/user-role-management/components/confirm-dialog";
import { MedicineFormModal } from "./medicine-form-modal";
import { MedicineDetailModal } from "./medicine-detail-modal";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";


export function MedicineList() {
  const user = useAuthStore((s) => s.user);
  const isDoctor = user?.role === "DOCTOR";
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [inStockFilter, setInStockFilter] = useState<"all" | "in_stock" | "out_of_stock">("all");
  
  const { data, isLoading } = useMedicines(
    page, 
    pageSize, 
    search, 
    inStockFilter === "all" ? undefined : inStockFilter === "in_stock"
  );
  
  const { data: alertSummary } = useInventoryAlerts();

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [detailMedicine, setDetailMedicine] = useState<MedicineResponse | null>(null);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);
  const [pendingActivateId, setPendingActivateId] = useState<string | null>(null);

  const deleteMutation = useDeleteMedicine();
  const activateMutation = useActivateMedicine();
  const router = useRouter();

  function handleOpenCreate() {
    setIsModalOpen(true);
  }

  async function handleConfirmDelete() {
    if (!pendingDeleteId) return;

    try {
      await deleteMutation.mutateAsync(pendingDeleteId);
      toast.success("Ngừng sử dụng thuốc thành công");
      setPendingDeleteId(null);
    } catch (e) {
      toast.error(getApiErrorMessage(e, "Có lỗi xảy ra"));
    }
  }

  async function handleConfirmActivate() {
    if (!pendingActivateId) return;

    try {
      await activateMutation.mutateAsync(pendingActivateId);
      toast.success("Kích hoạt thuốc thành công");
      setPendingActivateId(null);
    } catch (e) {
      toast.error(getApiErrorMessage(e, "Có lỗi xảy ra"));
    }
  }

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">Danh mục thuốc</h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Quản lý biệt dược, quy cách đóng gói và ngưỡng cảnh báo tồn kho.
          </p>
        </div>
        <div className="flex gap-3">
          {!isDoctor && (
            <button
              onClick={() => router.push('/medicines/inventory-alerts')}
              className="flex h-12 items-center justify-center gap-2 rounded-full border border-[var(--status-warning)]/30 bg-[var(--status-warning)]/10 px-6 font-heading text-sm font-600 tracking-wider text-[var(--status-warning)] transition-colors hover:bg-[var(--status-warning)]/15"
            >
              <AlertTriangle className="size-4" />
              Cảnh báo kho
            </button>
          )}
          {!isDoctor && (
            <button
              onClick={handleOpenCreate}
              className="flex h-12 items-center gap-2 rounded-full bg-accent px-6 font-heading text-sm font-600 uppercase tracking-wider text-accent-foreground shadow-lg shadow-accent/25 transition-all hover:bg-accent/90"
            >
              <PlusCircle className="size-4" />
              Thêm thuốc mới
            </button>
          )}
        </div>
      </div>

      {/* Tổng quan tồn kho — 4 số thật lấy từ InventoryAlertSummary, không suy diễn thêm. */}
      {!isDoctor && (
        <div className="mt-8 grid gap-4 md:grid-cols-4">
          <div className="preclinic-card flex items-center gap-3.5 p-4">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary">
              <Pill className="size-5" />
            </span>
            <div>
              <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Tổng thuốc</p>
              <p className="font-heading text-2xl font-bold text-foreground">{alertSummary?.totalMedicinesCount ?? 0}</p>
            </div>
          </div>

          <div className="preclinic-card flex items-center gap-3.5 p-4">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-[var(--status-good)]/12 text-[var(--status-good)]">
              <PackageCheck className="size-5" />
            </span>
            <div>
              <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Còn hàng</p>
              <p className="font-heading text-2xl font-bold text-[var(--status-good)]">{alertSummary?.inStockCount ?? 0}</p>
            </div>
          </div>

          <div className="preclinic-card flex items-center gap-3.5 p-4">
            <span className="relative flex size-10 shrink-0 items-center justify-center rounded-full bg-[var(--status-warning)]/12 text-[var(--status-warning)]">
              {(alertSummary?.lowStockCount ?? 0) > 0 && (
                <span className="absolute -right-0.5 -top-0.5 flex size-2.5 animate-pulse rounded-full bg-[var(--status-warning)]" />
              )}
              <AlertTriangle className="size-5" />
            </span>
            <div>
              <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Sắp hết</p>
              <p className="font-heading text-2xl font-bold text-[var(--status-warning)]">{alertSummary?.lowStockCount ?? 0}</p>
            </div>
          </div>

          <div className="preclinic-card flex items-center gap-3.5 p-4">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-destructive/12 text-destructive">
              <PackageX className="size-5" />
            </span>
            <div>
              <p className="text-xs font-600 uppercase tracking-wide text-muted-foreground">Hết hàng</p>
              <p className="font-heading text-2xl font-bold text-destructive">{alertSummary?.outOfStockCount ?? 0}</p>
            </div>
          </div>
        </div>
      )}

      <div className="mt-6 flex flex-wrap gap-3">
        <div className="relative min-w-64 flex-1">
          <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder="Tìm kiếm theo tên thuốc..."
            value={searchInput}
            onChange={(e) => {
              setSearchInput(e.target.value);
              setSearch(e.target.value);
              setPage(1);
            }}
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
          />
        </div>
        <select
          value={inStockFilter}
          onChange={(e) => {
            setInStockFilter(e.target.value as "all" | "in_stock" | "out_of_stock");
            setPage(1);
          }}
          className="h-12 rounded-full border border-border bg-background px-5 text-[15px] outline-none focus:border-accent"
        >
          <option value="all">Tất cả trạng thái</option>
          <option value="in_stock">Còn hàng</option>
          <option value="out_of_stock">Hết hàng</option>
        </select>
      </div>

      <div className="mt-6 overflow-x-auto rounded-3xl border border-border bg-background">
        <table className="w-full min-w-3xl text-left text-sm">
          <thead>
            <tr className="border-b border-border bg-secondary/40">
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Tên thuốc</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Tồn kho</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Trạng thái</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Ngày tạo</th>
              <th className="px-5 py-3.5 text-right font-semibold text-muted-foreground">Hành động</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={5} className="px-5 py-14 text-center text-muted-foreground">
                  <Loader2 className="mx-auto size-5 animate-spin" />
                </td>
              </tr>
            ) : data?.items.length === 0 ? (
              <tr>
                <td colSpan={5} className="px-5 py-14 text-center text-muted-foreground">
                  Không tìm thấy loại thuốc nào.
                </td>
              </tr>
            ) : (
              data?.items.map((medicine) => {
                // Mức độ tồn kho suy ra thuần từ 2 trường DTO thật: totalInventoryBase &
                // lowStockThreshold. threshold=0 nghĩa là bỏ theo dõi (đúng ý nghĩa API).
                const isOut = medicine.totalInventoryBase <= 0;
                const isLow = !isOut && medicine.lowStockThreshold > 0 && medicine.totalInventoryBase <= medicine.lowStockThreshold;
                return (
                <tr key={medicine.medicineId} className="border-b border-border last:border-0 hover:bg-secondary/20">
                  <td className="px-5 py-4 font-semibold text-foreground">
                    {medicine.name}
                  </td>
                  <td className="px-5 py-4">
                    {isOut ? (
                      <span className="text-muted-foreground italic">Hết hàng</span>
                    ) : (
                      <span className={`font-mono font-semibold ${isLow ? "text-[var(--status-warning)]" : "text-[var(--status-good)]"}`}>
                        {medicine.totalInventoryBase.toLocaleString()} {medicine.baseUnitName || '?'}
                        {isLow && <span className="ml-1.5 text-xs font-sans font-500">(dưới ngưỡng {medicine.lowStockThreshold})</span>}
                      </span>
                    )}
                  </td>
                  <td className="px-5 py-4">
                    <span className={`inline-flex rounded-full px-3 py-1 text-xs font-600 ${medicine.status === "ACTIVE" ? "bg-accent/12 text-accent" : "bg-destructive/12 text-destructive"}`}>
                      {medicine.status === "ACTIVE" ? "Đang sử dụng" : "Ngừng sử dụng"}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-muted-foreground">
                    {formatDateTime(medicine.createdAt)}
                  </td>
                  <td className="px-5 py-4 text-right">
                    <div className="flex items-center justify-end gap-1">
                      {!isDoctor && (
                        <button
                          onClick={() => router.push(`/medicines/${medicine.medicineId}/batches`)}
                          title="Xem lô tồn kho"
                          className="flex size-9 items-center justify-center rounded-full text-primary hover:bg-primary/10"
                        >
                          <Package className="size-4" />
                        </button>
                      )}
                      <button
                        onClick={() => setDetailMedicine(medicine)}
                        title={isDoctor ? "Xem chi tiết" : "Chi tiết / Quản lý"}
                        className="flex size-9 items-center justify-center rounded-full text-muted-foreground hover:bg-secondary hover:text-foreground"
                      >
                        <Pencil className="size-4" />
                      </button>
                      {!isDoctor && (
                        medicine.status === "ACTIVE" ? (
                          <button
                            onClick={() => setPendingDeleteId(medicine.medicineId)}
                            title="Ngừng sử dụng"
                            className="flex size-9 items-center justify-center rounded-full text-destructive hover:bg-destructive/10"
                          >
                            <Ban className="size-4" />
                          </button>
                        ) : (
                          <button
                            onClick={() => setPendingActivateId(medicine.medicineId)}
                            title="Kích hoạt lại"
                            className="flex size-9 items-center justify-center rounded-full text-[var(--status-good)] hover:bg-[var(--status-good)]/10"
                          >
                            <PlayCircle className="size-4" />
                          </button>
                        )
                      )}
                    </div>
                  </td>
                </tr>
              );})
            )}
          </tbody>
        </table>
      </div>

      {data && data.totalPages > 1 && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Đang xem {data.items.length} / {data.totalItems} kết quả
          </span>
          <PaginationNumbered
            currentPage={data.page}
            totalPages={data.totalPages}
            setPage={setPage}
          />
        </div>
      )}

      {/* Modal Thêm Mới */}
      <MedicineFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        medicineToEdit={null}
        onSuccessCreate={(medicine) => {
          setDetailMedicine(medicine);
        }}
      />

      {/* Modal Chi tiết & Quy cách */}
      {detailMedicine && (
        <MedicineDetailModal
          medicine={detailMedicine}
          isOpen={!!detailMedicine}
          onClose={() => setDetailMedicine(null)}
        />
      )}


      <ConfirmDialog
        open={!!pendingDeleteId}
        title="Ngừng sử dụng thuốc"
        message="Bạn có chắc chắn muốn ngừng sử dụng loại thuốc này? Thuốc sẽ không thể kê đơn được nữa nhưng dữ liệu lịch sử vẫn được giữ lại."
        confirmLabel="Ngừng sử dụng"
        onConfirm={handleConfirmDelete}
        onCancel={() => setPendingDeleteId(null)}
        isPending={deleteMutation.isPending}
        destructive={true}
      />
      
      <ConfirmDialog
        open={!!pendingActivateId}
        title="Kích hoạt lại thuốc"
        message="Bạn có chắc chắn muốn kích hoạt lại thuốc này? Thuốc sẽ xuất hiện trở lại trong danh sách để chọn khi kê đơn."
        confirmLabel="Kích hoạt"
        onConfirm={handleConfirmActivate}
        onCancel={() => setPendingActivateId(null)}
        isPending={activateMutation.isPending}
        destructive={false}
      />
    </div>
  );
}
