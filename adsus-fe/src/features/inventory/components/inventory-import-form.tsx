'use client';

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import * as z from 'zod';
import toast from 'react-hot-toast';
import { useRouter } from 'next/navigation';
import { format } from 'date-fns';

import { Controller } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { DatePicker } from '@/components/ui/date-picker';
import { SearchableSelect } from '@/components/shared/searchable-select';
import { UploadCloud } from 'lucide-react';
import { ExcelImportModal } from './excel-import-modal';

// ConfirmDialog removed

import { useImportInventory, useBulkImportInventory, useValidateImport, type ImportInventoryRequest } from '@/features/medicines/api/inventory.api';
import { getPagedMedicines, getPackagingsByMedicineId } from '@/features/medicines/api/medicines-api';
import { getSuppliers } from '@/features/medicines/api/suppliers.api';

// Validation schema matching backend Business Rules
const importSchema = z.object({
  medicineId: z.string().min(1, 'Vui lòng chọn thuốc'),
  supplierId: z.string().min(1, 'Vui lòng chọn nhà cung cấp'),
  medicinePackagingId: z.string().min(1, 'Vui lòng chọn đơn vị đóng gói'),
  lotNumber: z
    .string()
    .min(1, 'Vui lòng nhập số lô')
    .regex(
      /^[a-zA-Z0-9]+([-_][a-zA-Z0-9]+)*$/,
      'Số lô chỉ gồm chữ, số, dấu "-" và "_". Không ở đầu/cuối, không liền nhau.'
    ),
  expiryDate: z.string().min(1, 'Vui lòng chọn hạn sử dụng').refine((date) => {
    return new Date(date) > new Date();
  }, 'Hạn sử dụng phải lớn hơn ngày hiện tại'),
  quantity: z
    .number({ invalid_type_error: 'Vui lòng nhập số lượng' })
    .int('Số lượng phải là số nguyên')
    .positive('Số lượng phải lớn hơn 0'),
  importPricePerUnit: z
    .number({ invalid_type_error: 'Vui lòng nhập giá nhập' })
    .nonnegative('Giá nhập không được âm'),
});

type ImportFormValues = z.infer<typeof importSchema>;

export const InventoryImportForm = () => {
  const router = useRouter();
  const [isExcelModalOpen, setIsExcelModalOpen] = useState(false);
  const [bulkData, setBulkData] = useState<(ImportInventoryRequest & { _medicineName?: string, _supplierName?: string, _unitName?: string })[]>([]);

  // importInventory removed as it's bulk only now
  const { mutateAsync: importBulkInventory, isPending: isBulkPending } = useBulkImportInventory();
  const { mutateAsync: validateImport, isPending: isValidatePending } = useValidateImport();
  
  const { data: medicinesData, isLoading: isLoadingMedicines } = useQuery({
    queryKey: ['medicines-all'],
    queryFn: () => getPagedMedicines(1, 1000)
  });
  
  const { data: suppliersData, isLoading: isLoadingSuppliers } = useQuery({
    queryKey: ['suppliers-all'],
    queryFn: () => getSuppliers(1, 1000)
  });

  const form = useForm<ImportFormValues>({
    resolver: zodResolver(importSchema),
    defaultValues: {
      medicineId: '',
      supplierId: '',
      medicinePackagingId: '',
      lotNumber: '',
      expiryDate: '',
      quantity: 0,
      importPricePerUnit: 0,
    },
  });

  const watchMedicineId = useWatch({ control: form.control, name: 'medicineId' });
  
  const { data: packagingsData } = useQuery({
    queryKey: ['medicine-packagings', watchMedicineId],
    queryFn: () => getPackagingsByMedicineId(watchMedicineId),
    enabled: !!watchMedicineId,
  });
  
  const packagings = packagingsData || [];

  const onSubmit = async (data: ImportFormValues) => {
    // Tìm tên để hiển thị
    const medName = medicinesData?.items.find(m => m.medicineId === data.medicineId)?.name || '';
    const supName = suppliersData?.items.find(s => s.supplierId === data.supplierId)?.name || '';
    const packName = packagings.find(p => p.id === data.medicinePackagingId)?.unitName || '';

    const formattedExpiryDate = new Date(data.expiryDate).toISOString();

    // 1. Kiểm tra chéo với danh sách chờ (bulkData)
    const existingInQueue = bulkData.find(item => item.lotNumber === data.lotNumber);
    if (existingInQueue) {
      if (existingInQueue.medicineId !== data.medicineId) {
        toast.error(`Mã lô ${data.lotNumber} đã có trong bảng chờ nhưng thuộc về một loại thuốc khác!`);
        return;
      }
      if (existingInQueue.expiryDate.split('T')[0] !== formattedExpiryDate.split('T')[0]) {
        toast.error(`Mã lô ${data.lotNumber} đã có trong bảng chờ nhưng khác Hạn sử dụng! Vui lòng kiểm tra lại.`);
        return;
      }
    }

    const newRequest: ImportInventoryRequest & { _medicineName?: string, _supplierName?: string, _unitName?: string } = {
      ...data,
      expiryDate: formattedExpiryDate,
      _medicineName: medName,
      _supplierName: supName,
      _unitName: packName,
    };

    // 2. Validate với DB qua API
    try {
      const result = await validateImport(newRequest);
      if (!result.isValid) {
        toast.error(result.errorMessage || 'Dữ liệu lô không hợp lệ');
        return;
      }
    } catch (error) {
      console.error(error);
      toast.error('Lỗi khi kiểm tra dữ liệu từ máy chủ. Vui lòng thử lại.');
      return;
    }

    setBulkData(prev => [...prev, newRequest]);
    toast.success('Đã thêm vào bảng xem trước');
    
    // Giữ nguyên nhà cung cấp và thuốc nếu họ muốn nhập liên tiếp, chỉ xoá số lô/số lượng
    form.reset({
      ...data,
      lotNumber: '',
      quantity: 0,
    });
  };

  const handleBulkConfirm = (data: ImportInventoryRequest[]) => {
    setBulkData(data);
    setIsExcelModalOpen(false);
  };

  const submitBulkData = async () => {
    try {
      await importBulkInventory(bulkData);
      toast.success(`Đã nhập thành công ${bulkData.length} danh mục vào kho`);
      setBulkData([]);
      router.push('/inventory'); // Navigate to inventory list if exists
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } catch (error: any) {
      // Bỏ console.error để tránh Next.js dev server bật bảng lỗi đỏ (Error Overlay)
      // console.error("Bulk Import Error:", error.response?.data || error);
      
      let errMsg = 'Có lỗi xảy ra khi nhập kho hàng loạt';
      if (error.response?.data) {
        if (error.response.data.message) {
          errMsg = error.response.data.message;
        } else if (error.response.data.errors) {
          errMsg = "Lỗi dữ liệu: " + JSON.stringify(error.response.data.errors);
        } else if (typeof error.response.data === 'string') {
          errMsg = error.response.data;
        }
      }
      toast.error(errMsg);
    }
  };

  return (
    <>
      <div className="w-full space-y-6">
        <div className="rounded-lg border bg-card text-card-foreground shadow-sm">
          <div className="p-6 pt-6">
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Chọn Thuốc */}
                  <Controller
                    control={form.control}
                    name="medicineId"
                    render={({ field, fieldState }) => (
                      <div className="space-y-2 md:col-span-2">
                        <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">Thuốc</label>
                      <SearchableSelect
                        disabled={isLoadingMedicines}
                        placeholder="Chọn thuốc..."
                        value={field.value}
                        onChange={(val) => {
                          field.onChange(val);
                          form.setValue('medicinePackagingId', '');
                        }}
                        options={medicinesData?.items.map(m => ({ label: m.name, value: m.medicineId })) || []}
                      />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

                {/* Chọn Đơn vị đóng gói */}
                <Controller
                  control={form.control}
                  name="medicinePackagingId"
                  render={({ field, fieldState }) => (
                    <div className="space-y-2">
                      <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">Đơn vị nhập</label>
                      <SearchableSelect
                        disabled={!watchMedicineId || packagings.length === 0}
                        placeholder="Chọn đơn vị đóng gói..."
                        value={field.value}
                        onChange={field.onChange}
                        options={packagings.map(pkg => ({ label: `${pkg.unitName} (Quy đổi: ${pkg.conversionFactor})`, value: pkg.id }))}
                      />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

                {/* Chọn Nhà Cung Cấp */}
                <Controller
                  control={form.control}
                  name="supplierId"
                  render={({ field, fieldState }) => (
                    <div className="space-y-2 md:col-span-2">
                      <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">Nhà cung cấp</label>
                      <SearchableSelect
                        disabled={isLoadingSuppliers}
                        placeholder="Chọn nhà cung cấp..."
                        value={field.value}
                        onChange={field.onChange}
                        options={suppliersData?.items.map(s => ({ label: s.name, value: s.supplierId })) || []}
                      />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

                {/* Số Lô */}
                <Controller
                  control={form.control}
                  name="lotNumber"
                  render={({ field, fieldState }) => (
                    <div className="space-y-2">
                      <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">Số Lô</label>
                      <Input placeholder="VD: LOT-123" {...field} />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

                {/* Hạn Sử Dụng */}
                <Controller
                  control={form.control}
                  name="expiryDate"
                  render={({ field, fieldState }) => (
                    <div className="space-y-2">
                      <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">Hạn sử dụng</label>
                      <DatePicker
                        value={field.value}
                        onChange={field.onChange}
                      />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

                {/* Số Lượng */}
                <Controller
                  control={form.control}
                  name="quantity"
                  render={({ field, fieldState }) => (
                    <div className="space-y-2">
                      <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">Số lượng</label>
                      <Input
                        type="number"
                        {...field}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                      />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

                {/* Giá Nhập (VND) */}
                <Controller
                  control={form.control}
                  name="importPricePerUnit"
                  render={({ field, fieldState }) => (
                    <div className="space-y-2">
                      <label className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">Giá nhập trên 1 đơn vị nhập (VND)</label>
                      <Input
                        type="number"
                        {...field}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                      />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

              </div>
              
              <div className="flex justify-between items-center w-full">
                <Button type="button" variant="secondary" onClick={() => setIsExcelModalOpen(true)}>
                  <UploadCloud className="mr-2 h-4 w-4" />
                  Nhập từ file Excel
                </Button>
                <div className="flex space-x-2">
                  <Button type="button" variant="outline" onClick={() => form.reset()}>
                    Hủy
                  </Button>
                  <Button type="submit" disabled={isBulkPending || isValidatePending}>
                    {isValidatePending ? 'Đang kiểm tra...' : 'Thêm vào bảng chờ'}
                  </Button>
                </div>
              </div>
            </form>
          </div>
        </div>

        {bulkData.length > 0 && (
          <div className="p-6 rounded-lg border bg-card text-card-foreground shadow-sm mt-8">
            <h3 className="text-xl font-semibold mb-4">Xem trước {bulkData.length} lô thuốc chờ nhập</h3>
            <div className="overflow-x-auto">
              <table className="w-full text-sm text-left mb-6">
                <thead className="bg-muted text-muted-foreground border-b uppercase">
                  <tr>
                    <th className="px-4 py-3">#</th>
                    <th className="px-4 py-3">Tên Thuốc</th>
                    <th className="px-4 py-3">Nhà Cung Cấp</th>
                    <th className="px-4 py-3">Số Lô</th>
                    <th className="px-4 py-3">Hạn SD</th>
                    <th className="px-4 py-3">Đơn vị</th>
                    <th className="px-4 py-3 text-right">Giá nhập</th>
                    <th className="px-4 py-3 text-right">Số lượng</th>
                    <th className="px-4 py-3 text-center">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y">
                  {bulkData.map((row, idx) => (
                    <tr key={idx} className="hover:bg-muted/50">
                      <td className="px-4 py-2 text-muted-foreground">{idx + 1}</td>
                      <td className="px-4 py-2 font-medium">{row._medicineName || 'N/A'}</td>
                      <td className="px-4 py-2">{row._supplierName || 'N/A'}</td>
                      <td className="px-4 py-2">{row.lotNumber}</td>
                      <td className="px-4 py-2">{row.expiryDate ? format(new Date(row.expiryDate), 'dd/MM/yyyy') : 'N/A'}</td>
                      <td className="px-4 py-2">{row._unitName || 'N/A'}</td>
                      <td className="px-4 py-2 text-right">{row.importPricePerUnit?.toLocaleString() || '0'} đ</td>
                      <td className="px-4 py-2 text-right">{row.quantity}</td>
                      <td className="px-4 py-2 text-center">
                        <Button variant="ghost" size="sm" onClick={() => setBulkData(prev => prev.filter((_, i) => i !== idx))} className="text-destructive">Xóa</Button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            
            <div className="flex justify-between items-center w-full mt-4">
              <Button type="button" variant="outline" onClick={() => setBulkData([])}>
                Xóa toàn bộ bảng chờ
              </Button>
              <Button type="button" onClick={submitBulkData} disabled={isBulkPending}>
                {isBulkPending ? "Đang xử lý..." : "Lưu tất cả vào kho"}
              </Button>
            </div>
          </div>
        )}
      </div>


      
      <ExcelImportModal
        isOpen={isExcelModalOpen}
        onClose={() => setIsExcelModalOpen(false)}
        onConfirm={handleBulkConfirm}
        isPending={isBulkPending}
      />
    </>
  );
};
