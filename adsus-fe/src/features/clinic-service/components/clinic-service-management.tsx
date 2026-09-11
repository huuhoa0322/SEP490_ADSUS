"use client";

import { useMemo, useState } from "react";
import {
  AlertTriangle,
  Edit2,
  Loader2,
  Plus,
  PowerOff,
  RefreshCw,
  Search,
  Stethoscope,
} from "lucide-react";
import toast from "react-hot-toast";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { formatCurrency } from "@/lib/utils";
import {
  useClinicServicesList,
  useDeactivateClinicService,
  useUpdateClinicService,
} from "../queries";
import type { ClinicService } from "../types";
import { ClinicServiceModal } from "./clinic-service-modal";

type FilterTab = "ALL" | "ACTIVE" | "INACTIVE";

export function ClinicServiceManagement() {
  const [activeTab, setActiveTab] = useState<FilterTab>("ALL");
  const [searchQuery, setSearchQuery] = useState("");

  // Query isActive param: undefined for ALL, true for ACTIVE, false for INACTIVE
  const isActiveFilter =
    activeTab === "ACTIVE" ? true : activeTab === "INACTIVE" ? false : undefined;

  const { data: services, isLoading, isError } = useClinicServicesList(isActiveFilter);

  // Modal states
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingService, setEditingService] = useState<ClinicService | null>(null);

  // Deactivate confirm dialog states
  const [deactivateService, setDeactivateService] = useState<ClinicService | null>(null);
  const deactivateMutation = useDeactivateClinicService();
  const updateMutation = useUpdateClinicService();

  // Reactivate handler
  const handleReactivate = (service: ClinicService) => {
    updateMutation.mutate(
      {
        id: service.id,
        dto: { isActive: true },
      },
      {
        onSuccess: () => {
          toast.success(`Đã kích hoạt lại dịch vụ "${service.name}".`);
        },
      },
    );
  };

  const handleConfirmDeactivate = () => {
    if (!deactivateService) return;
    deactivateMutation.mutate(deactivateService.id, {
      onSuccess: () => {
        setDeactivateService(null);
      },
    });
  };

  const filteredServices = useMemo(() => {
    if (!services) return [];
    if (!searchQuery.trim()) return services;
    const q = searchQuery.toLowerCase().trim();
    return services.filter(
      (s) =>
        s.code.toLowerCase().includes(q) ||
        s.name.toLowerCase().includes(q) ||
        (s.description && s.description.toLowerCase().includes(q)),
    );
  }, [services, searchQuery]);

  return (
    <div className="container mx-auto space-y-6 p-4 sm:p-6 lg:p-8 max-w-[95%]">
      {/* Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <span className="flex size-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Stethoscope className="size-5" />
            </span>
            <h1 className="font-heading text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
              Quản lý dịch vụ phòng khám
            </h1>
          </div>
          <p className="mt-1 text-sm text-muted-foreground">
            Cấu hình danh mục dịch vụ khám và đơn giá áp dụng tự động hoặc thủ công cho các ca khám.
          </p>
        </div>

        <Button
          data-testid="btn-add-service"
          onClick={() => {
            setEditingService(null);
            setIsModalOpen(true);
          }}
          className="gap-2 font-semibold self-start sm:self-auto"
        >
          <Plus className="size-4" />
          Thêm dịch vụ
        </Button>
      </div>

      {/* Filter and Search Bar */}
      <Card className="border border-border/80 shadow-xs">
        <CardContent className="p-4 sm:p-5">
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            {/* Filter Tabs */}
            <div className="inline-flex rounded-lg bg-muted p-1 text-muted-foreground">
              <button
                type="button"
                onClick={() => setActiveTab("ALL")}
                className={`rounded-md px-3.5 py-1.5 text-xs font-semibold transition-all ${
                  activeTab === "ALL"
                    ? "bg-background text-foreground shadow-xs"
                    : "hover:text-foreground"
                }`}
              >
                Tất cả
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("ACTIVE")}
                className={`rounded-md px-3.5 py-1.5 text-xs font-semibold transition-all ${
                  activeTab === "ACTIVE"
                    ? "bg-background text-foreground shadow-xs"
                    : "hover:text-foreground"
                }`}
              >
                Hoạt động
              </button>
              <button
                type="button"
                onClick={() => setActiveTab("INACTIVE")}
                className={`rounded-md px-3.5 py-1.5 text-xs font-semibold transition-all ${
                  activeTab === "INACTIVE"
                    ? "bg-background text-foreground shadow-xs"
                    : "hover:text-foreground"
                }`}
              >
                Ngưng hoạt động
              </button>
            </div>

            {/* Search Input */}
            <div className="relative w-full sm:w-72">
              <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                placeholder="Tìm theo mã hoặc tên dịch vụ..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-9 text-sm"
              />
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Services Table */}
      <Card className="border border-border/80 shadow-xs overflow-hidden">
        <div className="overflow-x-auto">
          <Table>
            <TableHeader className="bg-muted/40">
              <TableRow>
                <TableHead className="w-[140px] font-bold">Mã dịch vụ</TableHead>
                <TableHead className="font-bold">Tên dịch vụ</TableHead>
                <TableHead className="font-bold">Mô tả</TableHead>
                <TableHead className="text-right font-bold w-[160px]">Đơn giá</TableHead>
                <TableHead className="text-center font-bold w-[150px]">Trạng thái</TableHead>
                <TableHead className="text-right font-bold w-[170px]">Thao tác</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, index) => (
                  <TableRow key={index}>
                    <TableCell>
                      <Skeleton className="h-5 w-20" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-5 w-40" />
                    </TableCell>
                    <TableCell>
                      <Skeleton className="h-5 w-56" />
                    </TableCell>
                    <TableCell className="text-right">
                      <Skeleton className="h-5 w-24 ml-auto" />
                    </TableCell>
                    <TableCell className="text-center">
                      <Skeleton className="h-6 w-24 mx-auto rounded-full" />
                    </TableCell>
                    <TableCell className="text-right">
                      <Skeleton className="h-8 w-20 ml-auto" />
                    </TableCell>
                  </TableRow>
                ))
              ) : isError ? (
                <TableRow>
                  <TableCell colSpan={6} className="h-32 text-center text-destructive">
                    <p className="font-medium">Có lỗi xảy ra khi tải danh sách dịch vụ.</p>
                  </TableCell>
                </TableRow>
              ) : filteredServices.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={6} className="h-36 text-center text-muted-foreground">
                    <p className="font-medium">Không tìm thấy dịch vụ phòng khám nào.</p>
                  </TableCell>
                </TableRow>
              ) : (
                filteredServices.map((service) => (
                  <TableRow key={service.id} className="hover:bg-muted/30">
                    <TableCell className="font-mono font-bold text-xs text-foreground">
                      {service.code}
                    </TableCell>
                    <TableCell className="font-semibold text-foreground">
                      {service.name}
                    </TableCell>
                    <TableCell className="text-sm text-muted-foreground max-w-md truncate">
                      {service.description || "—"}
                    </TableCell>
                    <TableCell className="text-right font-bold text-primary">
                      {formatCurrency(service.price)}
                    </TableCell>
                    <TableCell className="text-center">
                      {service.isActive ? (
                        <Badge variant="soft-success" className="font-semibold">
                          Hoạt động
                        </Badge>
                      ) : (
                        <Badge variant="soft-danger" className="font-semibold">
                          Ngưng hoạt động
                        </Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => {
                            setEditingService(service);
                            setIsModalOpen(true);
                          }}
                          className="h-8 gap-1 text-xs font-semibold"
                        >
                          <Edit2 className="size-3.5" />
                          Sửa
                        </Button>

                        {service.isActive ? (
                          <Button
                            data-testid="btn-deactivate-service"
                            variant="ghost"
                            size="sm"
                            onClick={() => setDeactivateService(service)}
                            className="h-8 gap-1 text-xs font-semibold text-destructive hover:bg-destructive/10 hover:text-destructive"
                          >
                            <PowerOff className="size-3.5" />
                            Ngưng
                          </Button>
                        ) : (
                          <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => handleReactivate(service)}
                            disabled={updateMutation.isPending}
                            className="h-8 gap-1 text-xs font-semibold text-emerald-600 hover:bg-emerald-50 hover:text-emerald-700"
                          >
                            <RefreshCw className="size-3.5" />
                            Kích hoạt
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      </Card>

      {/* Create / Edit Modal */}
      <ClinicServiceModal
        open={isModalOpen}
        onOpenChange={(open) => {
          setIsModalOpen(open);
          if (!open) setEditingService(null);
        }}
        service={editingService}
      />

      {/* Deactivate Confirmation Dialog */}
      <Dialog
        open={Boolean(deactivateService)}
        onOpenChange={(open) => {
          if (!open) setDeactivateService(null);
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
                  Xác nhận ngưng hoạt động
                </DialogTitle>
                <DialogDescription className="text-sm text-muted-foreground mt-1">
                  Hành động này sẽ đánh dấu dịch vụ là ngưng hoạt động.
                </DialogDescription>
              </div>
            </div>
          </DialogHeader>

          <div className="py-2 text-sm text-foreground">
            Bạn có chắc chắn muốn ngưng hoạt động dịch vụ{" "}
            <span className="font-bold text-foreground">
              {deactivateService?.name} ({deactivateService?.code})
            </span>
            ? Dịch vụ này sẽ không còn hiển thị để thêm vào các ca khám mới, nhưng lịch sử hóa đơn
            đã lập vẫn được bảo lưu toàn vẹn.
          </div>

          <DialogFooter className="gap-2 sm:justify-end">
            <Button
              type="button"
              variant="outline"
              onClick={() => setDeactivateService(null)}
              disabled={deactivateMutation.isPending}
            >
              Hủy bỏ
            </Button>
            <Button
              type="button"
              variant="destructive"
              onClick={handleConfirmDeactivate}
              disabled={deactivateMutation.isPending}
            >
              {deactivateMutation.isPending && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Ngưng hoạt động
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
