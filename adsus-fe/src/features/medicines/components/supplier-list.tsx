"use client";

import { Loader2, PlusCircle, Pencil, PlayCircle, Ban, Search } from "lucide-react";
import { useState } from "react";
import toast from "react-hot-toast";

import { 
  useSuppliers, 
  useUpdateSupplierStatus
} from "../hooks/use-suppliers";
import type { SupplierResponse } from "../api/suppliers.api";
import { getApiErrorMessage } from "@/lib/api-client";
import { ConfirmDialog } from "@/features/user-role-management/components/confirm-dialog";
import { SupplierFormModal } from "./supplier-form-modal";
import { PaginationNumbered } from "@/components/ui/pagination-numbered";


export function SupplierList() {
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  
  const { data, isLoading } = useSuppliers(page, pageSize, search);
  
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [detailSupplier, setDetailSupplier] = useState<SupplierResponse | null>(null);
  const [pendingDisableId, setPendingDisableId] = useState<string | null>(null);
  const [pendingActivateId, setPendingActivateId] = useState<string | null>(null);

  const statusMutation = useUpdateSupplierStatus();

  function handleOpenCreate() {
    setDetailSupplier(null);
    setIsModalOpen(true);
  }

  async function handleConfirmDisable() {
    if (!pendingDisableId) return;

    try {
      await statusMutation.mutateAsync({ id: pendingDisableId, isActive: false });
      setPendingDisableId(null);
    } catch (e) {
      // toast is already handled in the hook
    }
  }

  async function handleConfirmActivate() {
    if (!pendingActivateId) return;

    try {
      await statusMutation.mutateAsync({ id: pendingActivateId, isActive: true });
      setPendingActivateId(null);
    } catch (e) {
      // toast is already handled in the hook
    }
  }

  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">Nhà cung cấp</h1>
          <p className="mt-1.5 text-[15px] text-muted-foreground">
            Đối tác cung ứng dược phẩm dùng khi nhập kho lô thuốc mới.
          </p>
        </div>
        <button
          onClick={handleOpenCreate}
          className="flex h-12 items-center gap-2 rounded-full bg-[var(--success)] px-6 font-heading text-sm font-600 uppercase tracking-wider text-white shadow-lg shadow-[var(--success)]/25 transition-all hover:bg-[var(--success)]/90"
        >
          <PlusCircle className="size-4" />
          Thêm nhà cung cấp
        </button>
      </div>

      <div className="mt-8 flex flex-wrap gap-3">
        <div className="relative min-w-64 flex-1">
          <Search className="pointer-events-none absolute left-4 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder="Tìm kiếm theo tên nhà cung cấp..."
            value={searchInput}
            onChange={(e) => {
              setSearchInput(e.target.value);
              setSearch(e.target.value);
              setPage(1);
            }}
            className="h-12 w-full rounded-full border border-border bg-background pl-11 pr-4 text-[15px] outline-none transition-colors focus:border-[var(--success)]"
          />
        </div>
      </div>

      <div className="mt-6 overflow-x-auto rounded-3xl border border-border bg-background">
        <table className="w-full text-left text-sm">
          <thead>
            <tr className="border-b border-border bg-secondary/40">
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Tên nhà cung cấp</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Liên hệ</th>
              <th className="px-5 py-3.5 font-semibold text-muted-foreground">Trạng thái</th>
              <th className="px-5 py-3.5 text-right font-semibold text-muted-foreground">Hành động</th>
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
                  Không tìm thấy nhà cung cấp nào.
                </td>
              </tr>
            ) : (
              data?.items.map((supplier) => (
                <tr key={supplier.supplierId} className="border-b border-border last:border-0 hover:bg-secondary/20">
                  <td className="px-5 py-4 font-semibold text-foreground">
                    <div className="flex flex-col">
                      <span>{supplier.name}</span>
                      {supplier.taxCode && <span className="text-xs font-normal text-muted-foreground">MST: {supplier.taxCode}</span>}
                    </div>
                  </td>
                  <td className="px-5 py-4">
                    <div className="flex flex-col text-muted-foreground">
                      {supplier.phoneNumber && <span>SĐT: {supplier.phoneNumber}</span>}
                      {supplier.email && <span>Email: {supplier.email}</span>}
                    </div>
                  </td>
                  <td className="px-5 py-4">
                    <span className={`inline-flex rounded-full px-3 py-1 text-xs font-600 ${supplier.isActive ? "bg-[var(--success)]/12 text-[var(--success)]" : "bg-destructive/12 text-destructive"}`}>
                      {supplier.isActive ? "Đang giao dịch" : "Ngừng giao dịch"}
                    </span>
                  </td>
                  <td className="px-5 py-4 text-right">
                    <div className="flex items-center justify-end gap-1">
                      <button
                        onClick={() => {
                          setDetailSupplier(supplier);
                          setIsModalOpen(true);
                        }}
                        title="Sửa thông tin"
                        className="flex size-9 items-center justify-center rounded-full text-muted-foreground hover:bg-secondary hover:text-foreground"
                      >
                        <Pencil className="size-4" />
                      </button>
                      {supplier.isActive ? (
                        <button
                          onClick={() => setPendingDisableId(supplier.supplierId)}
                          title="Ngừng giao dịch"
                          className="flex size-9 items-center justify-center rounded-full text-destructive hover:bg-destructive/10"
                        >
                          <Ban className="size-4" />
                        </button>
                      ) : (
                        <button
                          onClick={() => setPendingActivateId(supplier.supplierId)}
                          title="Kích hoạt lại"
                          className="flex size-9 items-center justify-center rounded-full text-[var(--status-good)] hover:bg-[var(--status-good)]/10"
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

      {/* Phân trang */}
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

      {/* Modal Thêm Mới / Sửa */}
      <SupplierFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        supplierToEdit={detailSupplier}
      />

      <ConfirmDialog
        open={!!pendingDisableId}
        title="Ngừng giao dịch nhà cung cấp"
        message="Bạn có chắc chắn muốn ngừng giao dịch với nhà cung cấp này? Nhà cung cấp sẽ không xuất hiện khi nhập lô thuốc mới nhưng dữ liệu lịch sử vẫn được giữ lại."
        confirmLabel="Ngừng giao dịch"
        onConfirm={handleConfirmDisable}
        onCancel={() => setPendingDisableId(null)}
        isPending={statusMutation.isPending}
        destructive={true}
      />
      
      <ConfirmDialog
        open={!!pendingActivateId}
        title="Kích hoạt lại nhà cung cấp"
        message="Bạn có chắc chắn muốn kích hoạt lại nhà cung cấp này? Nhà cung cấp sẽ xuất hiện trở lại trong danh sách khi nhập lô thuốc mới."
        confirmLabel="Kích hoạt"
        onConfirm={handleConfirmActivate}
        onCancel={() => setPendingActivateId(null)}
        isPending={statusMutation.isPending}
        destructive={false}
      />
    </div>
  );
}
