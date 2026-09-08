import { InventoryAlertsList } from '@/features/medicines/components/inventory-alerts-list';

export const metadata = {
  title: 'Cảnh báo kho thuốc - Quản trị',
};

export default function InventoryAlertsPage() {
  return (
    <div className="mx-auto w-full max-w-screen-2xl px-6 py-8">
      <div>
        <h1 className="font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
          Cảnh báo tồn kho &amp; hạn sử dụng
        </h1>
        <p className="mt-1.5 text-[15px] text-muted-foreground">
          Thuốc hết hàng, dưới ngưỡng an toàn, sắp hết hạn và đã hết hạn — tổng hợp toàn kho.
        </p>
      </div>
      <div className="mt-8">
        <InventoryAlertsList />
      </div>
    </div>
  );
}
