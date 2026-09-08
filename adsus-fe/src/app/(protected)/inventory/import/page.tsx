import { Metadata } from 'next';
import { InventoryImportForm } from '@/features/inventory/components/inventory-import-form';

export const metadata: Metadata = {
  title: 'Nhập kho thuốc',
};

export default function InventoryImportPage() {
  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      <InventoryImportForm />
    </div>
  );
}
