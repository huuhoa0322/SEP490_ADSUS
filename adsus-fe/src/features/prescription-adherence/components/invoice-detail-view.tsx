"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useAuthStore } from "@/store/auth-store";
import { getApiErrorMessage } from "@/lib/api-client";
import {
  useInvoiceDetail,
  usePayInvoice,
  useCancelInvoice,
  useRemoveMedicineItem,
} from "../hooks/use-invoices";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import Image from "next/image";
import { ArrowLeft, CheckCircle2, Ban, X } from "lucide-react";
import toast from "react-hot-toast";

export function InvoiceDetailView({ invoiceId }: { invoiceId: string }) {
  const router = useRouter();
  const isNurse = useAuthStore((s) => s.user?.role === "STAFF");
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [removeItemId, setRemoveItemId] = useState<string | null>(null);

  const { data, isLoading } = useInvoiceDetail(invoiceId);

  const payInvoice = usePayInvoice();
  const cancelInvoice = useCancelInvoice();
  const removeMedicine = useRemoveMedicineItem();

  const handlePay = (method: string) => {
    payInvoice.mutate(
      { id: invoiceId, paymentMethod: method },
      {
        onSuccess: () => {
          toast.success("Hóa đơn đã được thanh toán và cập nhật tồn kho (FEFO).");
        },
        onError: (error) => {
          toast.error(getApiErrorMessage(error, "Có lỗi xảy ra khi thanh toán."));
        },
      },
    );
  };

  const handleCancel = () => {
    if (!cancelReason.trim()) {
      toast.error("Vui lòng nhập lý do hủy hóa đơn.");
      return;
    }
    cancelInvoice.mutate(
      { id: invoiceId, reason: cancelReason },
      {
        onSuccess: () => {
          toast.success("Hủy hóa đơn thành công.");
          setCancelOpen(false);
          setCancelReason("");
        },
        onError: (error) => {
          toast.error(getApiErrorMessage(error, "Có lỗi xảy ra khi hủy."));
        },
      },
    );
  };

  const handleRemoveItem = () => {
    if (!removeItemId) return;
    removeMedicine.mutate(
      { invoiceId, itemId: removeItemId },
      {
        onSuccess: () => {
          toast.success("Đã xóa dòng thuốc và hoàn kho thành công.");
          setRemoveItemId(null);
        },
        onError: (error) => {
          toast.error(getApiErrorMessage(error, "Có lỗi xảy ra khi xóa dòng thuốc."));
        },
      },
    );
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
    }).format(amount);
  };

  if (isLoading || !data) {
    return <div className="p-6">Đang tải dữ liệu hóa đơn...</div>;
  }

  const isPending = data.status === "PENDING";
  const qrUrl = `https://api.vietqr.io/image/970436-123456789-9z73xT0.jpg?amount=${data.totalAmount}&addInfo=TT%20HOA%20DON%20${data.id.substring(0,8)}&accountName=PHONG%20KHAM%20ADSUS`;

  return (
    <div className="mx-auto w-[90%] max-w-[90%] py-8 space-y-6">
      <Button variant="ghost" onClick={() => router.push("/invoices")} className="mb-4">
        <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại danh sách
      </Button>

      <div className="flex justify-between items-start">
        <div>
          <h1 className="text-3xl font-extrabold text-primary">Chi tiết Hóa Đơn</h1>
          <p className="text-sm text-muted-foreground mt-1">ID: {data.id}</p>
          {isNurse && data.caseId && (
            <p className="text-sm text-muted-foreground mt-0.5">
              Ca khám:{" "}
              <Link
                href={`/cases/${data.caseId}`}
                className="font-mono text-xs text-teal-600 hover:underline"
              >
                {data.caseId}
              </Link>
            </p>
          )}
        </div>
        <div>
          {data.status === "PENDING" ? (
            <Badge variant="secondary" className="text-lg py-1 px-4">Chờ thanh toán</Badge>
          ) : data.status === "PAID" ? (
            <Badge className="bg-green-600 text-lg py-1 px-4"><CheckCircle2 className="mr-2 h-5 w-5" /> Đã thanh toán</Badge>
          ) : (
            <Badge variant="destructive" className="text-lg py-1 px-4">Đã hủy</Badge>
          )}
        </div>
      </div>
      
      {data.status !== "CANCELLED" && (
        <div className="flex justify-end">
          <Button variant="destructive" onClick={() => setCancelOpen(true)}>
            <Ban className="mr-2 h-4 w-4" /> Hủy Hóa Đơn
          </Button>
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 space-y-6">
          <Card className="border-2">
            <CardHeader>
              <CardTitle className="font-bold">Thông tin bệnh nhân</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
                <div>
                  <span className="text-muted-foreground font-bold">Tên bệnh nhân:</span>
                  <p className="font-bold text-base">{data.caseName}</p>
                </div>
                <div>
                  <span className="text-muted-foreground font-bold">Ngày tạo:</span>
                  <p className="font-bold">{new Date(data.createdAt).toLocaleString("vi-VN")}</p>
                </div>
                {data.paidAt && (
                  <div>
                    <span className="text-muted-foreground font-bold">Ngày thanh toán:</span>
                    <p className="font-bold text-green-700">{new Date(data.paidAt).toLocaleString("vi-VN")}</p>
                  </div>
                )}
                {data.paymentMethod && (
                  <div>
                    <span className="text-muted-foreground font-bold">Phương thức:</span>
                    <p className="font-bold">{data.paymentMethod}</p>
                  </div>
                )}
                {data.cancelledReason && (
                  <div className="col-span-2 md:col-span-4">
                    <span className="text-muted-foreground font-bold">Lý do hủy:</span>
                    <p className="font-bold text-destructive">{data.cancelledReason}</p>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          <Card className="border-2">
            <CardHeader>
              <CardTitle className="font-bold">Chi tiết hóa đơn</CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                    <TableRow>
                    <TableHead className="font-bold">Mô tả chi tiết</TableHead>
                    <TableHead className="font-bold w-[120px]">Loại</TableHead>
                    <TableHead className="text-right font-bold">Đơn vị</TableHead>
                    <TableHead className="text-right font-bold">Số lượng</TableHead>
                    <TableHead className="text-right font-bold">Đơn giá</TableHead>
                    <TableHead className="text-right font-bold">Thành tiền</TableHead>
                    {isNurse && isPending && (
                      <TableHead className="w-[50px]"></TableHead>
                    )}
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell className="font-bold">{item.description}</TableCell>
                      <TableCell>
                        {item.itemType === "SERVICE" ? (
                          <Badge variant="soft-primary" className="font-semibold">
                            Dịch vụ
                          </Badge>
                        ) : (
                          <Badge variant="soft-success" className="font-semibold">
                            Thuốc
                          </Badge>
                        )}
                      </TableCell>
                      <TableCell className="text-right">{item.unit}</TableCell>
                      <TableCell className="text-right">{item.quantity}</TableCell>
                      <TableCell className="text-right">{formatCurrency(item.unitPrice)}</TableCell>
                      <TableCell className="text-right font-bold">{formatCurrency(item.totalPrice)}</TableCell>
                      {isNurse && isPending && (
                        <TableCell className="text-center">
                          {item.itemType === "MEDICINE" && (
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-7 w-7 text-destructive hover:bg-destructive/10"
                              onClick={() => setRemoveItemId(item.id)}
                              title="Xóa dòng thuốc (bệnh nhân không lấy)"
                            >
                              <X className="h-4 w-4" />
                            </Button>
                          )}
                        </TableCell>
                      )}
                    </TableRow>
                  ))}
                  <TableRow>
                    <TableCell colSpan={isNurse && isPending ? 6 : 5} className="text-right font-extrabold text-lg">Tổng cộng:</TableCell>
                    <TableCell className="text-right font-extrabold text-lg text-primary">{formatCurrency(data.totalAmount)}</TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </div>

        <div>
          {isPending && (
            <Card className="border-2 border-primary bg-primary/5">
              <CardHeader className="text-center pb-2">
                <CardTitle className="font-bold">Thanh Toán QR Code</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col items-center space-y-4">
                <div className="bg-white p-2 rounded-xl shadow-sm">
                  <Image src={qrUrl} alt="QR Code" width={192} height={192} className="w-48 h-48 object-contain" unoptimized />
                </div>
                <div className="text-center space-y-1 w-full">
                  <p className="text-sm text-muted-foreground">Quét mã để thanh toán</p>
                  <p className="font-bold text-2xl text-primary">{formatCurrency(data.totalAmount)}</p>
                </div>
                
                <div className="w-full pt-4 space-y-2 border-t border-primary/20">
                  <p className="text-sm font-medium text-center">Xác nhận thanh toán thủ công:</p>
                  <Button
                    className="w-full bg-blue-600 hover:bg-blue-700"
                    onClick={() => handlePay("BANK_TRANSFER")}
                    disabled={payInvoice.isPending}
                  >
                    Đã chuyển khoản (Bank)
                  </Button>
                  <Button
                    className="w-full"
                    variant="outline"
                    onClick={() => handlePay("CASH")}
                    disabled={payInvoice.isPending}
                  >
                    Đã thu tiền mặt
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {!isPending && data.status === "PAID" && (
            <Card className="bg-green-50 border-2 border-green-200">
              <CardContent className="flex flex-col items-center justify-center p-8 space-y-4 text-green-700">
                <CheckCircle2 className="w-16 h-16" />
                <h3 className="text-xl font-bold">Thanh Toán Hoàn Tất</h3>
                <p className="text-center text-sm">Kho đã tự động xuất thuốc (FEFO) và ghi nhận giá vốn.</p>
              </CardContent>
            </Card>
          )}

          {data.status === "CANCELLED" && (
            <Card className="bg-red-50 border-2 border-red-200">
              <CardContent className="flex flex-col items-center justify-center p-8 space-y-4 text-red-700">
                <Ban className="w-16 h-16" />
                <h3 className="text-xl font-bold">Hóa Đơn Đã Hủy</h3>
                {data.paidAt && <p className="text-center text-sm">Thuốc đã được hoàn kho tự động.</p>}
              </CardContent>
            </Card>
          )}
        </div>
      </div>

      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Xác Nhận Hủy Hóa Đơn</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 pt-1 pb-2">
            <p className="text-sm text-muted-foreground">
              {data.status === "PAID" 
                ? "Hóa đơn này đã thanh toán. Hủy hóa đơn sẽ tự động hoàn số lượng thuốc về lại các lô trong kho (Reverse Dispense)."
                : "Hóa đơn này chưa thanh toán và sẽ bị hủy bỏ."}
            </p>
            <div className="space-y-2">
              <Label htmlFor="reason" className="text-destructive font-semibold">Lý do hủy (bắt buộc)</Label>
              <Textarea
                id="reason"
                placeholder="Nhập lý do hủy hóa đơn..."
                value={cancelReason}
                onChange={(e) => setCancelReason(e.target.value)}
                className="min-h-[100px] resize-none"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)} disabled={cancelInvoice.isPending}>Đóng</Button>
            <Button variant="destructive" onClick={handleCancel} disabled={cancelInvoice.isPending}>
              {cancelInvoice.isPending ? "Đang xử lý..." : "Xác nhận Hủy"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={removeItemId !== null} onOpenChange={(open) => { if (!open) setRemoveItemId(null); }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Xác Nhận Xóa Dòng Thuốc</DialogTitle>
          </DialogHeader>
          <div className="space-y-3 pt-1 pb-2">
            <p className="text-sm text-muted-foreground">
              Bệnh nhân không lấy thuốc này. Xóa sẽ tự động hoàn toàn bộ số lượng thuốc về lại kho và xóa các quy cách liên quan trên hóa đơn.
            </p>
            {(() => {
              const selectedItem = data.items.find((i) => i.id === removeItemId);
              if (!selectedItem) return null;
              const relatedItems = selectedItem.referenceId
                ? data.items.filter((i) => i.referenceId === selectedItem.referenceId && i.itemType === "MEDICINE")
                : [selectedItem];
              const totalRefund = relatedItems.reduce((acc, curr) => acc + curr.totalPrice, 0);

              return (
                <div className="bg-muted/70 p-3 rounded-md text-sm space-y-2 border">
                  <div className="font-semibold text-foreground">
                    {relatedItems.length > 1
                      ? `Các dòng thuốc sẽ xóa (${relatedItems.length} dòng):`
                      : "Dòng thuốc sẽ xóa:"}
                  </div>
                  <ul className="space-y-1 divide-y divide-border/50 text-xs">
                    {relatedItems.map((rel) => (
                      <li key={rel.id} className="pt-1 first:pt-0 flex justify-between items-center">
                        <div>
                          <span className="font-medium">{rel.description}</span>
                          <span className="text-muted-foreground ml-1">({rel.quantity} {rel.unit})</span>
                        </div>
                        <span className="font-semibold text-primary">{formatCurrency(rel.totalPrice)}</span>
                      </li>
                    ))}
                  </ul>
                  {relatedItems.length > 1 && (
                    <div className="pt-1 border-t border-border flex justify-between font-bold text-xs">
                      <span>Tổng tiền thuốc giảm:</span>
                      <span className="text-destructive">{formatCurrency(totalRefund)}</span>
                    </div>
                  )}
                </div>
              );
            })()}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRemoveItemId(null)} disabled={removeMedicine.isPending}>Đóng</Button>
            <Button variant="destructive" onClick={handleRemoveItem} disabled={removeMedicine.isPending}>
              {removeMedicine.isPending ? "Đang xử lý..." : "Xác nhận Xóa"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
