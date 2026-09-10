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
        return <Badge variant="outline" className="text-[var(--status-warning)] bg-[var(--status-warning)]/10 border-[var(--status-warning)]/25">Chờ duyệt</Badge>;
      case "APPROVED":
        return <Badge variant="outline" className="text-[var(--success)] bg-[var(--success)]/10 border-[var(--success)]/25">Đã duyệt</Badge>;
      case "REJECTED":
        return <Badge variant="outline" className="text-destructive bg-destructive/10 border-destructive/25">Từ chối</Badge>;
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-4 bg-background p-4 rounded-lg border border-border shadow-sm">
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

      <div className="bg-background rounded-lg border border-border shadow-sm overflow-hidden">
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
                  <Loader2 className="h-6 w-6 animate-spin mx-auto text-muted-foreground" />
                </TableCell>
              </TableRow>
            ) : data?.items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} className="h-24 text-center text-muted-foreground">
                  Chưa có yêu cầu nào.
                </TableCell>
              </TableRow>
            ) : (
              data?.items.map((req) => (
                <TableRow key={req.requestId}>
                  <TableCell>{format(new Date(req.createdAt), 'dd/MM/yyyy HH:mm')}</TableCell>
                  <TableCell>
                    {req.requestType?.toUpperCase() === 'LEAVE' ? (
                      <span className="text-destructive font-medium">Xin nghỉ</span>
                    ) : (
                      <span className="text-[var(--success)] font-medium">Tăng ca</span>
                    )}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col">
                      <span>{format(new Date(req.requestDate), 'dd/MM/yyyy')}</span>
                      <span className="text-xs text-muted-foreground">{req.shiftLabel}</span>
                    </div>
                  </TableCell>
                  <TableCell className="max-w-[300px] truncate" title={req.reason}>
                    {req.reason}
                  </TableCell>
                  <TableCell>
                    <div className="flex flex-col items-start gap-1">
                      {getStatusBadge(req.status)}
                      {req.status === 'REJECTED' && req.rejectReason && (
                        <div className="text-sm text-destructive text-left mt-1" title={req.rejectReason}>
                          {req.rejectReason}
                        </div>
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
