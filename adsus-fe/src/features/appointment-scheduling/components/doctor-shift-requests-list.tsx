"use client";

import { useState } from "react";
import { format } from "date-fns";
import { Loader2 } from "lucide-react";

import { useMyShiftRequests } from "../hooks/use-shift-request";
import { ShiftRequestStatus } from "../types/shift-request.types";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";

export function DoctorShiftRequestsList() {
  const [statusFilter, setStatusFilter] = useState<ShiftRequestStatus | "">("");
  const [page, setPage] = useState(1);

  const { data, isLoading } = useMyShiftRequests(
    statusFilter === "" ? undefined : statusFilter,
    page
  );

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
    <div className="space-y-4">
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
              <TableHead>Loại</TableHead>
              <TableHead>Ca áp dụng</TableHead>
              <TableHead>Lý do</TableHead>
              <TableHead>Trạng thái</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={5} className="h-24 text-center">
                  <Loader2 className="h-6 w-6 animate-spin mx-auto text-slate-400" />
                </TableCell>
              </TableRow>
            ) : data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="h-24 text-center text-slate-500">
                  Chưa có yêu cầu nào.
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((req) => (
                <TableRow key={req.requestId}>
                  <TableCell>{format(new Date(req.createdAt), 'dd/MM/yyyy HH:mm')}</TableCell>
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
                  <TableCell className="max-w-[300px] truncate" title={req.reason}>
                    {req.reason}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col items-start gap-1">
                      {getStatusBadge(req.status)}
                      {req.status === 'REJECTED' && req.rejectReason && (
                        <span className="text-xs text-red-500" title={req.rejectReason}>
                          Lý do: {req.rejectReason.substring(0, 30)}...
                        </span>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
