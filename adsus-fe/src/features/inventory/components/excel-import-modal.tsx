import React, { useState, useRef } from 'react';
import * as XLSX from 'xlsx';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription } from '@/components/ui/dialog';
import { getPagedMedicines, getPackagingsByMedicineId } from '@/features/medicines/api/medicines-api';
import { getSuppliers } from '@/features/medicines/api/suppliers.api';
import toast from 'react-hot-toast';
import { Loader2, UploadCloud, FileSpreadsheet } from 'lucide-react';
import type { ImportInventoryRequest } from '@/features/medicines/api/inventory.api';

interface ExcelImportModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: (data: ImportInventoryRequest[]) => void;
  isPending: boolean;
}

export function ExcelImportModal({ isOpen, onClose, onConfirm, isPending }: ExcelImportModalProps) {
  const [parsedData, setParsedData] = useState<any[]>([]);
  const [mappedRequests, setMappedRequests] = useState<ImportInventoryRequest[]>([]);
  const [importErrors, setImportErrors] = useState<string[]>([]);
  const [isProcessing, setIsProcessing] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = async (evt) => {
      try {
        setIsProcessing(true);
        const bstr = evt.target?.result;
        const wb = XLSX.read(bstr, { type: 'binary', cellDates: true });
        const wsname = wb.SheetNames[0];
        const ws = wb.Sheets[wsname];
        const data = XLSX.utils.sheet_to_json(ws);
        
        if (data.length === 0) {
          toast.error("File trống hoặc không đúng định dạng.");
          setIsProcessing(false);
          return;
        }

        setParsedData(data);
        setImportErrors([]);
        await processData(data);
      } catch (error) {
        console.error(error);
        toast.error("Lỗi khi đọc file. Vui lòng kiểm tra định dạng.");
        setIsProcessing(false);
      }
    };
    reader.readAsBinaryString(file);
    
    if (fileInputRef.current) {
      fileInputRef.current.value = '';
    }
  };

  const processData = async (data: any[]) => {
    try {
      // In a real app, you would map names to IDs here by calling APIs or passing mapping dicts
      // For simplicity in this demo, we assume the excel file contains IDs directly, OR
      // we just do a quick fetch to get all active medicines and match by name.
      
      const medicines = await getPagedMedicines(1, 10000);
      const suppliers = await getSuppliers(1, 10000);

      const requests: ImportInventoryRequest[] = [];
      const errors: string[] = [];
      
      const getVal = (row: any, key: string) => {
        const foundKey = Object.keys(row).find(k => k.toLowerCase().trim() === key.toLowerCase());
        return foundKey ? row[foundKey] : undefined;
      };

      for (let i = 0; i < data.length; i++) {
        const row = data[i];
        // Expected columns: Tên Thuốc, Nhà Cung Cấp, Số Lô, Hạn Sử Dụng, Đơn vị nhập, Số lượng, Giá Nhập
        const medName = getVal(row, 'Tên Thuốc')?.toString().trim();
        const supName = getVal(row, 'Nhà Cung Cấp')?.toString().trim();
        const lotNumber = getVal(row, 'Số Lô')?.toString().trim();
        
        const rawExpiry = getVal(row, 'Hạn Sử Dụng');
        let expiryStr = '';
        if (rawExpiry instanceof Date) {
          expiryStr = rawExpiry.toISOString();
        } else if (rawExpiry) {
          const str = rawExpiry.toString().trim();
          const parts = str.split(/[\/-]/);
          if (parts.length === 3) {
            const day = parseInt(parts[0], 10);
            const month = parseInt(parts[1], 10) - 1;
            const year = parseInt(parts[2], 10);
            const d = new Date(year, month, day);
            if (!isNaN(d.getTime())) {
              expiryStr = d.toISOString();
            }
          } else {
            const d = new Date(str);
            if (!isNaN(d.getTime())) {
              expiryStr = d.toISOString();
            }
          }
        }

        const unitName = getVal(row, 'Đơn vị nhập')?.toString().trim();
        const quantity = parseFloat(getVal(row, 'Số lượng'));
        const price = parseFloat(getVal(row, 'Giá Nhập'));

        const missingFields = [];
        if (!medName) missingFields.push("Tên Thuốc");
        if (!supName) missingFields.push("Nhà Cung Cấp");
        if (!lotNumber) missingFields.push("Số Lô");
        if (!expiryStr) missingFields.push("Hạn Sử Dụng (hoặc sai định dạng)");
        if (!unitName) missingFields.push("Đơn vị nhập");
        if (isNaN(quantity)) missingFields.push("Số lượng (không hợp lệ)");
        if (isNaN(price)) missingFields.push("Giá Nhập (không hợp lệ)");

        if (missingFields.length > 0) {
          errors.push(`Dòng ${i + 2}: Thiếu/Sai thông tin - ${missingFields.join(', ')}.`);
          continue;
        }

        const med = medicines.items.find(m => m.name.toLowerCase() === medName.toLowerCase());
        if (!med) {
          errors.push(`Dòng ${i + 2}: Không tìm thấy thuốc "${medName}".`);
          continue;
        }

        const sup = suppliers.items.find(s => s.name.toLowerCase() === supName.toLowerCase());
        if (!sup) {
          errors.push(`Dòng ${i + 2}: Không tìm thấy nhà cung cấp "${supName}".`);
          continue;
        }

        const packagings = await getPackagingsByMedicineId(med.medicineId);
        const pack = packagings.find(p => p.unitName.toLowerCase() === unitName.toLowerCase());
        
        if (!pack) {
          errors.push(`Dòng ${i + 2}: Không tìm thấy đơn vị "${unitName}" cho thuốc "${medName}".`);
          continue;
        }

        requests.push({
          medicineId: med.medicineId,
          supplierId: sup.supplierId,
          medicinePackagingId: pack.id,
          lotNumber: lotNumber,
          expiryDate: expiryStr,
          quantity: quantity,
          importPricePerUnit: price,
          _medicineName: med.name,
          _supplierName: sup.name,
          _unitName: pack.unitName
        } as any);
      }

      if (errors.length > 0) {
        toast.error(`Có ${errors.length} dòng bị lỗi. Hãy kiểm tra danh sách bên dưới.`);
        setImportErrors(errors);
        setMappedRequests([]);
      } else {
        setImportErrors([]);
        setMappedRequests(requests);
        toast.success(`Phân tích thành công ${requests.length} danh mục.`);
        onConfirm(requests); // Auto submit if no errors
      }
    } catch (e: any) {
      console.error(e);
      toast.error("Lỗi xử lý dữ liệu từ file: " + e.message);
    } finally {
      setIsProcessing(false);
    }
  };

  const handleConfirm = () => {
    if (mappedRequests.length > 0) {
      onConfirm(mappedRequests);
    }
  };

  const handleOpenChange = (open: boolean) => {
    if (!open) {
      setParsedData([]);
      setMappedRequests([]);
      setImportErrors([]);
      onClose();
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleOpenChange}>
      <DialogContent className="sm:max-w-xl">
        <DialogHeader>
          <DialogTitle>Nhập Kho Từ File Excel/CSV</DialogTitle>
          <DialogDescription>
            Vui lòng chuẩn bị file với các cột: Tên Thuốc, Nhà Cung Cấp, Số Lô, Hạn Sử Dụng (YYYY-MM-DD), Đơn vị nhập, Số lượng, Giá Nhập
          </DialogDescription>
        </DialogHeader>
        
        <div className="py-6">
          {!parsedData.length ? (
            <div className="flex flex-col items-center justify-center border-2 border-dashed rounded-lg p-12 hover:bg-muted/50 transition-colors">
              <FileSpreadsheet className="h-10 w-10 text-muted-foreground mb-4" />
              <Button onClick={() => fileInputRef.current?.click()} disabled={isProcessing}>
                {isProcessing ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : <UploadCloud className="mr-2 h-4 w-4" />}
                {isProcessing ? "Đang xử lý..." : "Chọn file tải lên"}
              </Button>
              <input
                type="file"
                ref={fileInputRef}
                className="hidden"
                accept=".xlsx, .xls, .csv"
                onChange={handleFileUpload}
              />
            </div>
          ) : (
            <div className="space-y-4">
              <div className="p-4 bg-muted rounded-md text-sm">
                <p><strong>Tổng số dòng:</strong> {parsedData.length}</p>
                <p><strong>Hợp lệ:</strong> <span className="text-green-600 font-bold">{mappedRequests.length}</span></p>
                <p><strong>Lỗi:</strong> <span className="text-red-600 font-bold">{parsedData.length - mappedRequests.length}</span></p>
              </div>
              
              {importErrors.length > 0 && (
                <div className="mt-4 p-4 border border-destructive bg-destructive/10 rounded-md">
                  <h4 className="font-semibold text-destructive mb-2">Chi tiết lỗi ({importErrors.length}):</h4>
                  <ul className="list-disc list-inside text-sm text-destructive space-y-1 max-h-40 overflow-auto">
                    {importErrors.map((err, idx) => (
                      <li key={idx}>{err}</li>
                    ))}
                  </ul>
                  <p className="text-xs text-muted-foreground mt-3 italic">Vui lòng sửa lại dữ liệu trong file Excel và tải lên lại.</p>
                </div>
              )}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => { setParsedData([]); setMappedRequests([]); setImportErrors([]); onClose(); }} disabled={isPending || isProcessing}>
            Hủy
          </Button>
          <Button onClick={handleConfirm} disabled={mappedRequests.length === 0 || mappedRequests.length < parsedData.length || isPending || isProcessing}>
            {isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
            Xác nhận Nhập kho
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
