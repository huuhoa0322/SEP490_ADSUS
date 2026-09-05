import { InventoryAlertsList } from '@/features/medicines/components/inventory-alerts-list';

export const metadata = {
  title: 'Cảnh báo kho thuốc - Quản trị',
};

export default function InventoryAlertsPage() {
  return (
    <div className="flex-1 space-y-4 p-4 md:p-8 pt-6">
      <div className="flex items-center justify-between space-y-2">
        <h2 className="text-3xl font-bold tracking-tight">Cảnh báo tồn kho & hạn sử dụng</h2>
      </div>
      <InventoryAlertsList />
    </div>
  );
}
