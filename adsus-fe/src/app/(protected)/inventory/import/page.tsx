import { Metadata } from 'next';
import { InventoryImportForm } from '@/features/inventory/components/inventory-import-form';

export const metadata: Metadata = {
  title: 'Nhập kho thuốc',
};

export default function InventoryImportPage() {
  return (
    <div className="flex-1 space-y-4 p-4 md:p-8 pt-6">
      <div className="flex items-center justify-between space-y-2">
        <h2 className="text-3xl font-bold tracking-tight">Nhập kho thuốc</h2>
      </div>
      <div className="grid gap-4 grid-cols-1">
        <InventoryImportForm />
      </div>
    </div>
  );
}
