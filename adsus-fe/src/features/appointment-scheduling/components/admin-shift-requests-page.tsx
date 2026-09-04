"use client";

import { useState } from "react";
import { format } from "date-fns";
import { CheckCircle, XCircle, Search, Loader2 } from "lucide-react";

import { useAdminShiftRequests, useReviewShiftRequest } from "../hooks/use-shift-request";
import { ShiftRequestResponse, ShiftRequestStatus } from "../types/shift-request.types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
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
  DialogHeader,
  DialogTitle,
  DialogDescription,
} from "@/components/ui/dialog";
import { Textarea } from "@/components/ui/textarea";

export function AdminShiftRequestsPage() {
  const [statusFilter, setStatusFilter] = useState<ShiftRequestStatus | "">("");
  const [page, setPage] = useState(1);
  const [selectedRequest, setSelectedRequest] = useState<ShiftRequestResponse | null>(null);
  const [action, setAction] = useState<"APPROVE" | "REJECT" | null>(null);
  const [rejectReason, setRejectReason] = useState("");

  const { data, isLoading } = useAdminShiftRequests(
    statusFilter === "" ? undefined : statusFilter,
    undefined,
    page
  );

  const { mutateAsync: reviewRequest, isPending: isReviewing } = useReviewShiftRequest();

  const confirmApprove = async () => {
    if (!selectedRequest) return;
    try {
      await reviewRequest({ requestId: selectedRequest.requestId, data: { decision: "APPROVED" } });
      setSelectedRequest(null);
      setAction(null);
    } catch (e) {
      // Handled by hook
    }
  };

  const handleReject = async () => {
    if (!selectedRequest) return;
    if (!rejectReason.trim()) {
      alert("Vui lòng nhập lý do từ chối");
      return;
    }
    
    try {
      await reviewRequest({
        requestId: selectedRequest.requestId,
        data: { decision: "REJECTED", rejectReason },
      });
      setSelectedRequest(null);
      setAction(null);
      setRejectReason("");
    } catch (e) {
      // Handled by hook
    }
  };

  const getStatusBadge = (status: ShiftRequestStatus) => {
    switch (status?.toUpperCase()) {
      case "PENDING":
        return <Badge variant="outline" className="text-amber-600 bg-amber-50">Chờ duyệt</Badge>;
      case "APPROVED":
        return <Badge variant="outline" className="text-emerald-600 bg-emerald-50">Đã duyệt</Badge>;
      case "REJECTED":
        return <Badge variant="outline" className="text-red-600 bg-red-50">Từ chối</Badge>;
    }
  };

  return (
    <div className="space-y-6">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="font-heading text-2xl font-semibold">Duyệt nghỉ / Tăng ca</h1>
          <p className="text-sm text-slate-500">Quản lý các yêu cầu thay đổi lịch làm việc của bác sĩ.</p>
        </div>
      </header>

      <div className="flex items-center gap-4 bg-white p-4 rounded-lg border shadow-sm">
        <select
          className="flex h-10 w-[200px] items-center justify-between rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          value={statusFilter}
          onChange={(e) => {
            setStatusFilter(e.target.value as ShiftRequestStatus | "");
            setPage(1);
          }}
        >
          <option value="">Tất cả trạng thái</option>
          <option value="PENDING">Chờ duyệt</option>
          <option value="APPROVED">Đã duyệt</option>
          <option value="REJECTED">Từ chối</option>
        </select>
      </div>

      <div className="bg-white rounded-lg border shadow-sm overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Ngày gửi</TableHead>
              <TableHead>Bác sĩ</TableHead>
              <TableHead>Loại</TableHead>
              <TableHead>Ca áp dụng</TableHead>
              <TableHead>Lý do</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="text-right">Thao tác</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={7} className="h-24 text-center">
                  <Loader2 className="h-6 w-6 animate-spin mx-auto text-slate-400" />
                </TableCell>
              </TableRow>
            ) : data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="h-24 text-center text-slate-500">
                  Không có dữ liệu
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((req) => (
                <TableRow key={req.requestId}>
                  <TableCell>{format(new Date(req.createdAt), 'dd/MM/yyyy HH:mm')}</TableCell>
                  <TableCell className="font-medium">{req.doctorName}</TableCell>
                  <TableCell>
                    {req.requestType?.toUpperCase() === 'LEAVE' ? (
                      <span className="text-rose-600 font-medium">Xin nghỉ</span>
                    ) : (
                      <span className="text-emerald-600 font-medium">Tăng ca</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col">
                      <span>{format(new Date(req.requestDate), 'dd/MM/yyyy')}</span>
                      <span className="text-xs text-slate-500">{req.shiftLabel}</span>
                    </div>
                  </TableCell>
                  <TableCell className="max-w-[200px] truncate" title={req.reason}>
                    {req.reason}
                  </TableCell>
                  <TableCell>{getStatusBadge(req.status)}</TableCell>
                  <TableCell className="text-right">
                    {req.status?.toUpperCase() === 'PENDING' && (
                      <div className="flex justify-end gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          className="text-emerald-600 border-emerald-200 hover:bg-emerald-50"
                          onClick={() => { setSelectedRequest(req); setAction("APPROVE"); }}
                          disabled={isReviewing}
                        >
                          <CheckCircle className="h-4 w-4 mr-1" /> Duyệt
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          className="text-red-600 border-red-200 hover:bg-red-50"
                          onClick={() => { setSelectedRequest(req); setAction("REJECT"); }}
                          disabled={isReviewing}
                        >
                          <XCircle className="h-4 w-4 mr-1" /> Từ chối
                        </Button>
                      </div>
                    )}
                    {req.status?.toUpperCase() === 'REJECTED' && req.rejectReason && (
                      <span className="text-xs text-slate-500" title={req.rejectReason}>
                        Lý do: {req.rejectReason.substring(0, 20)}...
                      </span>
                    )}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      <Dialog open={selectedRequest !== null} onOpenChange={(open) => { if (!open) { setSelectedRequest(null); setAction(null); setRejectReason(""); }}}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{action === "APPROVE" ? "Duyệt yêu cầu" : "Từ chối yêu cầu"}</DialogTitle>
            <DialogDescription>
              {action === "APPROVE" 
                ? `Bạn có chắc chắn muốn duyệt yêu cầu ${selectedRequest?.requestType?.toUpperCase() === 'LEAVE' ? 'Xin nghỉ' : 'Tăng ca'} của bác sĩ ${selectedRequest?.doctorName}? Hệ thống sẽ tự động điều chỉnh lịch khám và thông báo nếu có thay đổi.`
                : `Vui lòng nhập lý do từ chối yêu cầu của bác sĩ ${selectedRequest?.doctorName}.`
              }
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 pt-4">
            {action === "REJECT" && (
              <Textarea
                placeholder="Nhập lý do chi tiết..."
                value={rejectReason}
                onChange={(e) => setRejectReason(e.target.value)}
                rows={4}
              />
            )}
            <div className="flex justify-end gap-3">
              <Button variant="outline" onClick={() => { setSelectedRequest(null); setAction(null); setRejectReason(""); }}>
                Hủy
              </Button>
              {action === "APPROVE" ? (
                <Button className="bg-emerald-600 hover:bg-emerald-700" onClick={confirmApprove} disabled={isReviewing}>
                  {isReviewing ? "Đang xử lý..." : "Xác nhận duyệt"}
                </Button>
              ) : (
                <Button variant="destructive" onClick={handleReject} disabled={isReviewing}>
                  {isReviewing ? "Đang xử lý..." : "Xác nhận từ chối"}
                </Button>
              )}
            </div>
          </div>
        </DialogContent>
      </Dialog>
    </div>
  );
}
