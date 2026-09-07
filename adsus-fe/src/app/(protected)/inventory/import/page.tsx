import { Metadata } from 'next';
import { InventoryImportForm } from '@/features/inventory/components/inventory-import-form';

export const metadata: Metadata = {
  title: 'Nhập kho thuốc',
};

export default function InventoryImportPage() {
  return (
    <div className="flex-1 p-4 md:p-8 pt-6">
      <div className="grid gap-4 grid-cols-1">
        <InventoryImportForm />
      </div>
    </div>
  );
}
