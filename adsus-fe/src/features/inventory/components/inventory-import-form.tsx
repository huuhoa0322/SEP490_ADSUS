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
import { UploadCloud, Plus, X } from 'lucide-react';
import { ExcelImportModal } from './excel-import-modal';

import { useBulkImportInventory, useValidateImport, type ImportInventoryRequest } from '@/features/medicines/api/inventory.api';
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
    const medName = medicinesData?.items.find(m => m.medicineId === data.medicineId)?.name || '';
    const supName = suppliersData?.items.find(s => s.supplierId === data.supplierId)?.name || '';
    const packName = packagings.find(p => p.id === data.medicinePackagingId)?.unitName || '';

    const formattedExpiryDate = new Date(data.expiryDate).toISOString();

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
      router.push('/inventory');
    } catch (error) {
      const err = error as any;
      let errMsg = 'Có lỗi xảy ra khi nhập kho hàng loạt';
      if (err.response?.data) {
        if (err.response.data.message) {
          errMsg = err.response.data.message;
        } else if (err.response.data.errors) {
          errMsg = "Lỗi dữ liệu: " + JSON.stringify(err.response.data.errors);
        } else if (typeof err.response.data === 'string') {
          errMsg = err.response.data;
        }
      }
      toast.error(errMsg);
    }
  };

  return (
    <>
      <div className="w-full space-y-6">
        
        <div className="flex items-center justify-between mb-4">
          <h2 className="text-3xl font-bold tracking-tight">Nhập kho thuốc</h2>
          <Button 
            type="button" 
            onClick={() => setIsExcelModalOpen(true)}
            className="bg-[#FFFFFF] text-[#0A1B39] border border-[#E7E8EB] hover:bg-[#F5F6F8] shadow-sm flex h-11 items-center justify-center gap-2 rounded-full px-6 font-heading text-sm font-semibold tracking-wider transition-colors"
          >
            <UploadCloud className="size-4 text-[#2E37A4]" />
            Nhập từ file Excel
          </Button>
        </div>

        <div className="rounded-[5px] border border-[#E7E8EB] bg-[#FFFFFF] shadow-[0px_0px_35px_0px_rgba(104,134,177,0.15)]">
          <div className="p-6 pt-6">
            <h2 className="text-xl font-bold text-[#0A1B39] mb-6 pb-4 border-b border-[#E7E8EB]">Nhập lô thuốc mới</h2>
            <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-6">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                  {/* Chọn Thuốc */}
                  <Controller
                    control={form.control}
                    name="medicineId"
                    render={({ field, fieldState }) => (
                      <div className="space-y-2 md:col-span-2">
                        <label htmlFor="medicineId" className="text-sm font-semibold text-[#0A1B39]">Thuốc</label>
                        <SearchableSelect
                          id="medicineId"
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
                      <label htmlFor="medicinePackagingId" className="text-sm font-semibold text-[#0A1B39]">Đơn vị nhập</label>
                      <SearchableSelect
                        id="medicinePackagingId"
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
                      <label htmlFor="supplierId" className="text-sm font-semibold text-[#0A1B39]">Nhà cung cấp</label>
                      <SearchableSelect
                        id="supplierId"
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
                      <label htmlFor="lotNumber" className="text-sm font-semibold text-[#0A1B39]">Số Lô</label>
                      <Input id="lotNumber" placeholder="VD: LOT-123" className="border-[#E7E8EB] focus-visible:ring-[#2E37A4] rounded-[5px] h-11" {...field} />
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
                      <label htmlFor="expiryDate" className="text-sm font-semibold text-[#0A1B39]">Hạn sử dụng</label>
                      <DatePicker
                        id="expiryDate"
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
                      <label htmlFor="quantity" className="text-sm font-semibold text-[#0A1B39]">Số lượng nhập</label>
                      <Input
                        id="quantity"
                        type="number"
                        className="border-[#E7E8EB] focus-visible:ring-[#2E37A4] rounded-[5px] h-11"
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
                      <label htmlFor="importPricePerUnit" className="text-sm font-semibold text-[#0A1B39]">Giá nhập trên 1 đơn vị (VND)</label>
                      <Input
                        id="importPricePerUnit"
                        type="number"
                        className="border-[#E7E8EB] focus-visible:ring-[#2E37A4] rounded-[5px] h-11"
                        {...field}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                      />
                      {fieldState.error && <p className="text-sm font-medium text-destructive">{fieldState.error.message}</p>}
                    </div>
                  )}
                />

              </div>
              
              <div className="flex justify-end items-center w-full mt-8 pt-6 border-t border-[#E7E8EB]">
                <div className="flex space-x-3">
                  <Button type="button" variant="outline" onClick={() => form.reset()} className="rounded-full px-6 h-12 text-[#6C7688] hover:text-[#0A1B39]">
                    Hủy bỏ
                  </Button>
                  <Button type="submit" disabled={isBulkPending || isValidatePending} className="bg-[#2E37A4] hover:bg-[#2E37A4]/90 rounded-full px-6 h-12 font-semibold text-white">
                    <Plus className="mr-2 h-4 w-4" />
                    {isValidatePending ? 'Đang kiểm tra...' : 'Thêm vào bảng chờ'}
                  </Button>
                </div>
              </div>
            </form>
          </div>
        </div>

        {bulkData.length > 0 && (
          <div className="rounded-[5px] border border-[#E7E8EB] bg-[#FFFFFF] shadow-[0px_0px_35px_0px_rgba(104,134,177,0.15)] mt-8 overflow-hidden">
            <div className="p-6 bg-[#F5F6F8] border-b border-[#E7E8EB]">
              <h3 className="text-xl font-bold text-[#0A1B39]">Danh sách chờ nhập kho ({bulkData.length} lô)</h3>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full text-sm text-left">
                <thead className="bg-[#F5F6F8] text-[#0A1B39] border-b border-[#E7E8EB]">
                  <tr>
                    <th className="px-5 py-4 font-semibold">#</th>
                    <th className="px-5 py-4 font-semibold">Tên Thuốc</th>
                    <th className="px-5 py-4 font-semibold">Nhà Cung Cấp</th>
                    <th className="px-5 py-4 font-semibold">Số Lô</th>
                    <th className="px-5 py-4 font-semibold">Hạn SD</th>
                    <th className="px-5 py-4 font-semibold">Đơn vị</th>
                    <th className="px-5 py-4 text-right font-semibold">Giá nhập</th>
                    <th className="px-5 py-4 text-right font-semibold">Số lượng</th>
                    <th className="px-5 py-4 text-center font-semibold">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#E7E8EB]">
                  {bulkData.map((row, idx) => (
                    <tr key={idx} className="hover:bg-[#F5F6F8]/50 transition-colors">
                      <td className="px-5 py-4 text-[#6C7688]">{idx + 1}</td>
                      <td className="px-5 py-4 font-medium text-[#0A1B39]">{row._medicineName || 'N/A'}</td>
                      <td className="px-5 py-4 text-[#6C7688]">{row._supplierName || 'N/A'}</td>
                      <td className="px-5 py-4 text-[#0A1B39]">{row.lotNumber}</td>
                      <td className="px-5 py-4 text-[#0A1B39]">{row.expiryDate ? format(new Date(row.expiryDate), 'dd/MM/yyyy') : 'N/A'}</td>
                      <td className="px-5 py-4 text-[#6C7688]">{row._unitName || 'N/A'}</td>
                      <td className="px-5 py-4 text-right text-[#0A1B39] font-medium">{row.importPricePerUnit?.toLocaleString() || '0'} đ</td>
                      <td className="px-5 py-4 text-right font-bold text-[#27AE60]">{row.quantity}</td>
                      <td className="px-5 py-4 text-center">
                        <button 
                          onClick={() => setBulkData(prev => prev.filter((_, i) => i !== idx))}
                          className="p-2 text-[#EF1E1E] hover:bg-[#EF1E1E]/10 rounded-full transition-colors"
                          title="Xóa"
                        >
                          <X className="w-4 h-4" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            
            <div className="flex justify-between items-center w-full p-6 border-t border-[#E7E8EB] bg-[#F5F6F8]/50">
              <Button type="button" variant="outline" onClick={() => setBulkData([])} className="text-[#EF1E1E] border-[#EF1E1E] hover:bg-[#EF1E1E]/10 rounded-full px-6 bg-white">
                Xóa toàn bộ
              </Button>
              <Button type="button" onClick={submitBulkData} disabled={isBulkPending} className="bg-[#27AE60] hover:bg-[#27AE60]/90 rounded-full px-8 h-12 font-semibold text-[15px] text-white">
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
