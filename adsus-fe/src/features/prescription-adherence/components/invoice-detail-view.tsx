"use client";

import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import { invoiceService, InvoiceDetailResponse } from "@/api/invoiceService";
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
import { ArrowLeft, CheckCircle2, Ban } from "lucide-react";
import toast from "react-hot-toast";

export function InvoiceDetailView({ invoiceId }: { invoiceId: string }) {
  const router = useRouter();
  // useToast removed
  const [data, setData] = useState<InvoiceDetailResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [paying, setPaying] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [canceling, setCanceling] = useState(false);

  const fetchDetail = useCallback(async () => {
    try {
      setLoading(true);
      const res = await invoiceService.getInvoiceDetail(invoiceId);
      setData(res);
    } catch (error) {
      console.error(error);
    } finally {
      setLoading(false);
    }
  }, [invoiceId]);

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    fetchDetail();
  }, [fetchDetail]);

  const handlePay = async (method: string) => {
    try {
      setPaying(true);
      await invoiceService.payAndDispense(invoiceId, method);
      toast.success("Hóa đơn đã được thanh toán và cập nhật tồn kho (FEFO).");
      fetchDetail(); // reload to get new status
    } catch (error: unknown) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      toast.error((error as any).response?.data?.message || "Có lỗi xảy ra khi thanh toán.");
    } finally {
      setPaying(false);
    }
  };

  const handleCancel = async () => {
    if (!cancelReason.trim()) {
      toast.error("Vui lòng nhập lý do hủy hóa đơn.");
      return;
    }
    try {
      setCanceling(true);
      await invoiceService.cancelInvoice(invoiceId, cancelReason);
      toast.success("Hủy hóa đơn thành công.");
      setCancelOpen(false);
      fetchDetail(); // reload to get new status
    } catch (error: unknown) {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      toast.error((error as any).response?.data?.message || "Có lỗi xảy ra khi hủy.");
    } finally {
      setCanceling(false);
    }
  };

  const formatCurrency = (amount: number) => {
    return new Intl.NumberFormat("vi-VN", {
      style: "currency",
      currency: "VND",
    }).format(amount);
  };

  if (loading || !data) {
    return <div className="p-6">Đang tải dữ liệu hóa đơn...</div>;
  }

  const isPending = data.status === "PENDING";
  // Mock vietqr link with the total amount
  const qrUrl = `https://api.vietqr.io/image/970436-123456789-9z73xT0.jpg?amount=${data.totalAmount}&addInfo=TT%20HOA%20DON%20${data.id.substring(0,8)}&accountName=PHONG%20KHAM%20ADSUS`;

  return (
    <div className="p-6 max-w-6xl mx-auto space-y-6">
      <Button variant="ghost" onClick={() => router.push("/invoices")} className="mb-4">
        <ArrowLeft className="mr-2 h-4 w-4" /> Quay lại danh sách
      </Button>

      <div className="flex justify-between items-start">
        <div>
          <h1 className="text-3xl font-bold text-primary">Chi tiết Hóa Đơn</h1>
          <p className="text-sm text-muted-foreground mt-1">ID: {data.id}</p>
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

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        <div className="md:col-span-2 space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Thông tin bệnh nhân</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <span className="text-muted-foreground">Tên bệnh nhân:</span>
                  <p className="font-medium text-base">{data.caseName}</p>
                </div>
                <div>
                  <span className="text-muted-foreground">Ngày tạo:</span>
                  <p className="font-medium">{new Date(data.createdAt).toLocaleString("vi-VN")}</p>
                </div>
                {data.paidAt && (
                  <div>
                    <span className="text-muted-foreground">Ngày thanh toán:</span>
                    <p className="font-medium text-green-700">{new Date(data.paidAt).toLocaleString("vi-VN")}</p>
                  </div>
                )}
                {data.paymentMethod && (
                  <div>
                    <span className="text-muted-foreground">Phương thức:</span>
                    <p className="font-medium">{data.paymentMethod}</p>
                  </div>
                )}
                {data.cancelledReason && (
                  <div className="col-span-2">
                    <span className="text-muted-foreground">Lý do hủy:</span>
                    <p className="font-medium text-destructive">{data.cancelledReason}</p>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Chi tiết bóc tách thuốc</CardTitle>
            </CardHeader>
            <CardContent>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Tên thuốc & Quy cách</TableHead>
                    <TableHead className="text-right">Số lượng</TableHead>
                    <TableHead className="text-right">Đơn giá</TableHead>
                    <TableHead className="text-right">Thành tiền</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map((item) => (
                    <TableRow key={item.id}>
                      <TableCell className="font-medium">{item.description}</TableCell>
                      <TableCell className="text-right">{item.quantity}</TableCell>
                      <TableCell className="text-right">{formatCurrency(item.unitPrice)}</TableCell>
                      <TableCell className="text-right font-semibold">{formatCurrency(item.totalPrice)}</TableCell>
                    </TableRow>
                  ))}
                  <TableRow>
                    <TableCell colSpan={3} className="text-right font-bold text-lg">Tổng cộng:</TableCell>
                    <TableCell className="text-right font-bold text-lg text-primary">{formatCurrency(data.totalAmount)}</TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </CardContent>
          </Card>
        </div>

        <div>
          {isPending && (
            <Card className="border-primary bg-primary/5">
              <CardHeader className="text-center pb-2">
                <CardTitle>Thanh Toán QR Code</CardTitle>
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
                    disabled={paying}
                  >
                    Đã chuyển khoản (Bank)
                  </Button>
                  <Button 
                    className="w-full" 
                    variant="outline"
                    onClick={() => handlePay("CASH")}
                    disabled={paying}
                  >
                    Đã thu tiền mặt
                  </Button>
                </div>
              </CardContent>
            </Card>
          )}

          {!isPending && data.status === "PAID" && (
            <Card className="bg-green-50 border-green-200">
              <CardContent className="flex flex-col items-center justify-center p-8 space-y-4 text-green-700">
                <CheckCircle2 className="w-16 h-16" />
                <h3 className="text-xl font-bold">Thanh Toán Hoàn Tất</h3>
                <p className="text-center text-sm">Kho đã tự động xuất thuốc (FEFO) và ghi nhận giá vốn.</p>
              </CardContent>
            </Card>
          )}

          {data.status === "CANCELLED" && (
            <Card className="bg-red-50 border-red-200">
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
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Xác Nhận Hủy Hóa Đơn</DialogTitle>
          </DialogHeader>
          <div className="space-y-4 py-4">
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
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)} disabled={canceling}>Đóng</Button>
            <Button variant="destructive" onClick={handleCancel} disabled={canceling}>
              {canceling ? "Đang xử lý..." : "Xác nhận Hủy"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
