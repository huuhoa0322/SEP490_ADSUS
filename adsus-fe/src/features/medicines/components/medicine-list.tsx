"use client";

import { Ban, Loader2, Pencil, Search, PlusCircle, PlayCircle } from "lucide-react";
import { useState } from "react";
import toast from "react-hot-toast";

import { Badge } from "@/components/ui/badge";
import { getApiErrorMessage } from "@/lib/api-client";
import { formatDateTime } from "@/features/user-role-management/lib/user-labels";

import {
  useCreateMedicine,
  useDeleteMedicine,
  useMedicines,
  useUpdateMedicine,
  useActivateMedicine,
} from "../hooks/use-medicines";
import type { MedicineResponse } from "../api/medicines-api";

import { ConfirmDialog } from "@/features/user-role-management/components/confirm-dialog";

function PagerButton({
  children,
  disabled,
  onClick,
}: {
  children: React.ReactNode;
  disabled: boolean;
  onClick: () => void;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      className="rounded-full border border-border px-4 py-2 transition-colors hover:bg-secondary disabled:cursor-not-allowed disabled:opacity-40"
    >
      {children}
    </button>
  );
}

export function MedicineList() {
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");

  const { data, isLoading } = useMedicines(page, pageSize, search);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingMedicine, setEditingMedicine] = useState<MedicineResponse | null>(null);
  const [medicineName, setMedicineName] = useState("");

  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const createMutation = useCreateMedicine();
  const updateMutation = useUpdateMedicine();
  const deleteMutation = useDeleteMedicine();
  const activateMutation = useActivateMedicine();

  function handleOpenCreate() {
    setEditingMedicine(null);
    setMedicineName("");
    setIsModalOpen(true);
  }

  function handleOpenEdit(medicine: MedicineResponse) {
    setEditingMedicine(medicine);
    setMedicineName(medicine.name);
    setIsModalOpen(true);
  }

  async function handleSaveMedicine() {
    if (!medicineName.trim()) {
      toast.error("Vui lòng nhập tên thuốc");
      return;
    }

    try {
      if (editingMedicine) {
        await updateMutation.mutateAsync({
          id: editingMedicine.medicineId,
          request: { name: medicineName },
        });
        toast.success("Cập nhật thuốc thành công");
      } else {
        await createMutation.mutateAsync({ name: medicineName });
        toast.success("Thêm thuốc thành công");
      }
      setIsModalOpen(false);
    } catch (e) {
      toast.error(getApiErrorMessage(e, "Có lỗi xảy ra"));
    }
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

  async function handleActivateMedicine(id: string) {
    try {
      await activateMutation.mutateAsync(id);
      toast.success("Kích hoạt thuốc thành công");
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
        <div className="flex gap-4">
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
        </div>
      </div>

      <div className="flex-1 overflow-x-auto rounded-3xl border border-border bg-background">
        <table className="w-full min-w-4xl border-collapse text-left text-sm">
          <thead>
            <tr className="border-b border-border bg-secondary/40">
              <th className="px-5 py-4 font-semibold text-muted-foreground">Tên thuốc</th>
              <th className="px-5 py-4 font-semibold text-muted-foreground">Trạng thái</th>
              <th className="px-5 py-4 font-semibold text-muted-foreground">Ngày tạo</th>
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
                        onClick={() => {
                          setEditingMedicine(medicine);
                          setMedicineName(medicine.name);
                          setIsModalOpen(true);
                        }}
                        title="Sửa thuốc"
                        className="flex size-9 items-center justify-center rounded-full text-muted-foreground hover:bg-secondary hover:text-foreground"
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
                          onClick={() => handleActivateMedicine(medicine.medicineId)}
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

      {/* Modal Thêm/Sửa */}
      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
          <div className="w-full max-w-md rounded-3xl bg-white p-6 shadow-xl">
            <h2 className="mb-6 text-xl font-bold text-primary">
              {editingMedicine ? "Sửa thông tin thuốc" : "Thêm thuốc mới"}
            </h2>
            <div className="space-y-4">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-primary">
                  Tên thuốc <span className="text-red-500">*</span>
                </label>
                <input
                  type="text"
                  value={medicineName}
                  onChange={(e) => setMedicineName(e.target.value)}
                  placeholder="Nhập tên thuốc..."
                  className="w-full rounded-full border border-border bg-surface px-4 py-2.5 text-sm focus:border-teal focus:outline-none"
                  autoFocus
                />
              </div>
            </div>
            <div className="mt-8 flex justify-end gap-3">
              <button
                onClick={() => setIsModalOpen(false)}
                className="rounded-full px-6 py-2 text-sm font-medium text-muted-foreground transition hover:bg-surface"
              >
                Hủy
              </button>
              <button
                onClick={handleSaveMedicine}
                disabled={createMutation.isPending || updateMutation.isPending}
                className="rounded-full bg-accent px-6 py-2 text-sm font-medium text-white transition hover:bg-accent/90 disabled:opacity-50"
              >
                {createMutation.isPending || updateMutation.isPending ? (
                  <Loader2 className="size-4 animate-spin" />
                ) : (
                  "Lưu lại"
                )}
              </button>
            </div>
          </div>
        </div>
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
    </div>
  );
}

