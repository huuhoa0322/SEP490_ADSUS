"use client";

import { Loader2, PlusCircle, Pencil, PlayCircle, Ban, Search, Package } from "lucide-react";
import { useState } from "react";
import { useRouter } from "next/navigation";
import toast from "react-hot-toast";

import { 
  useMedicines, 
  useActivateMedicine,
  useDeleteMedicine 
} from "../hooks/use-medicines";
import { formatDateTime } from "@/features/user-role-management/lib/user-labels";
import type { MedicineResponse } from "../api/medicines-api";
import { getApiErrorMessage } from "@/lib/api-client";
import { Badge } from "@/components/ui/badge";
import { ConfirmDialog } from "@/features/user-role-management/components/confirm-dialog";
import { MedicineFormModal } from "./medicine-form-modal";
import { MedicineDetailModal } from "./medicine-detail-modal";

// A small sub-component for pagination buttons
function PagerButton({ disabled, onClick, children }: { disabled?: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      className="flex h-10 items-center justify-center rounded-full border border-border px-4 text-sm font-medium text-foreground transition-colors hover:bg-secondary disabled:opacity-50"
    >
      {children}
    </button>
  );
}

export function MedicineList() {
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
      <div className="mb-6 flex items-center justify-between">
        <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">Quản lý danh mục thuốc</h1>
        <button
          onClick={handleOpenCreate}
          className="flex h-12 items-center justify-center gap-2 rounded-full bg-accent px-6 font-heading text-sm font-semibold tracking-wider text-white transition-colors hover:bg-accent/90"
        >
          <PlusCircle className="size-4" />
          Thêm thuốc mới
        </button>
      </div>

      <div className="mb-6 rounded-2xl bg-white p-4 shadow-sm border border-border">
        <div className="flex gap-4 mb-4">
          <div className="relative flex-1">
            <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
            <input
              type="text"
              placeholder="Tìm kiếm theo tên thuốc..."
              value={searchInput}
              onChange={(e) => {
                setSearchInput(e.target.value);
                setSearch(e.target.value);
                setPage(1);
              }}
              className="w-full h-12 rounded-full border border-border bg-background py-2 pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-accent"
            />
          </div>
          <div className="w-[180px]">
            <select
              value={inStockFilter}
              onChange={(e) => {
                setInStockFilter(e.target.value as "all" | "in_stock" | "out_of_stock");
                setPage(1);
              }}
              className="w-full h-12 rounded-full border border-border bg-background px-4 text-[15px] outline-none transition-colors focus:border-accent cursor-pointer"
            >
              <option value="all">Tất cả trạng thái</option>
              <option value="in_stock">Còn hàng</option>
              <option value="out_of_stock">Hết hàng</option>
            </select>
          </div>
        </div>

        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border">
              <th className="px-5 py-4 text-left font-semibold text-muted-foreground">Tên thuốc</th>
              <th className="px-5 py-4 text-left font-semibold text-muted-foreground">Tồn kho</th>
              <th className="px-5 py-4 text-left font-semibold text-muted-foreground">Trạng thái</th>
              <th className="px-5 py-4 text-left font-semibold text-muted-foreground">Ngày tạo</th>
              <th className="px-5 py-4 text-right font-semibold text-muted-foreground">Hành động</th>
            </tr>
          </thead>
          <tbody>
            {isLoading ? (
              <tr>
                <td colSpan={4} className="px-5 py-14 text-center text-muted-foreground">
                  <Loader2 className="mx-auto size-5 animate-spin" />
                </td>
              </tr>
            ) : data?.items.length === 0 ? (
              <tr>
                <td colSpan={4} className="px-5 py-14 text-center text-muted-foreground">
                  Không tìm thấy loại thuốc nào.
                </td>
              </tr>
            ) : (
              data?.items.map((medicine) => (
                <tr key={medicine.medicineId} className="border-b border-border last:border-0 hover:bg-secondary/20">
                  <td className="px-5 py-4 font-semibold text-foreground">
                    {medicine.name}
                  </td>
                  <td className="px-5 py-4">
                    {medicine.totalInventoryBase > 0 ? (
                      <span className="font-semibold text-emerald-600">
                        {medicine.totalInventoryBase.toLocaleString()} {medicine.baseUnitName || '?'}
                      </span>
                    ) : (
                      <span className="text-muted-foreground italic">Hết hàng</span>
                    )}
                  </td>
                  <td className="px-5 py-4">
                    <Badge variant={medicine.status === "ACTIVE" ? "default" : "secondary"}>
                      {medicine.status === "ACTIVE" ? "Đang sử dụng" : "Ngừng sử dụng"}
                    </Badge>
                  </td>
                  <td className="px-5 py-4 text-muted-foreground">
                    {formatDateTime(medicine.createdAt)}
                  </td>
                  <td className="px-5 py-4 text-right">
                    <div className="flex items-center justify-end gap-2">
                      <button
                        onClick={() => router.push(`/admin/medicines/${medicine.medicineId}/batches`)}
                        title="Xem lô tồn kho"
                        className="flex size-9 items-center justify-center rounded-full text-indigo-600 hover:bg-indigo-50 hover:text-indigo-700"
                      >
                        <Package className="size-4" />
                      </button>
                      <button
                        onClick={() => setDetailMedicine(medicine)}
                        title="Chi tiết / Quản lý"
                        className="flex size-9 items-center justify-center rounded-full text-blue-600 hover:bg-blue-50 hover:text-blue-700"
                      >
                        <Pencil className="size-4" />
                      </button>
                      {medicine.status === "ACTIVE" ? (
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
                          className="flex size-9 items-center justify-center rounded-full text-emerald-600 hover:bg-emerald-500/10"
                        >
                          <PlayCircle className="size-4" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {data && data.totalPages > 1 && (
        <div className="mt-5 flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Đang xem {data.items.length} / {data.totalItems} kết quả
          </span>
          <div className="flex gap-2">
            <PagerButton disabled={data.page <= 1} onClick={() => setPage((p) => p - 1)}>
              Trước
            </PagerButton>
            
            <div className="flex gap-1.5 items-center mx-2">
              {(() => {
                const total = data.totalPages;
                const current = data.page;
                let pages: number[] = [];
                if (total <= 5) {
                  pages = Array.from({ length: total }, (_, i) => i + 1);
                } else if (current <= 3) {
                  pages = [1, 2, 3, 4, 5];
                } else if (current >= total - 2) {
                  pages = [total - 4, total - 3, total - 2, total - 1, total];
                } else {
                  pages = [current - 2, current - 1, current, current + 1, current + 2];
                }

                return pages.map((p) => {
                  const active = p === current;
                  return (
                    <button
                      key={p}
                      onClick={() => setPage(p)}
                      className={`flex h-10 min-w-10 items-center justify-center rounded-full border px-3 text-sm transition-colors ${
                        active
                          ? "border-accent bg-accent font-bold text-white shadow-sm"
                          : "border-border hover:bg-secondary text-foreground"
                      }`}
                    >
                      {p}
                    </button>
                  );
                });
              })()}
            </div>

            <PagerButton
              disabled={data.page >= data.totalPages}
              onClick={() => setPage((p) => p + 1)}
            >
              Sau
            </PagerButton>
          </div>
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
