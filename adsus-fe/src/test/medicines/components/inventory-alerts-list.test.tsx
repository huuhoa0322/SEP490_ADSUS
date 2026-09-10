import { render, screen } from '@testing-library/react';
import { InventoryAlertsList } from '@/features/medicines/components/inventory-alerts-list';
import { useInventoryAlerts } from '@/features/medicines/api/inventory.api';
import { describe, it, expect, vi } from 'vitest';

vi.mock('@/features/medicines/api/inventory.api', () => ({
  useInventoryAlerts: vi.fn(),
}));

vi.mock('next/link', () => {
  const MockLink = ({ children, href }: { children: React.ReactNode; href: string }) => {
    return <a href={href}>{children}</a>;
  };
  MockLink.displayName = 'Link';
  return { default: MockLink };
});

describe('InventoryAlertsList', () => {
  it('renders loading state', () => {
    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    } as unknown as ReturnType<typeof useInventoryAlerts>);

    render(<InventoryAlertsList />);
    expect(screen.getByText('Đang tải dữ liệu cảnh báo...')).toBeInTheDocument();
  });

  it('should render alerts and paginate correctly', async () => {
    const lowStockAlerts = Array.from({ length: 25 }).map((_, i) => ({
      medicineId: `med-${i}`,
      medicineName: `Medicine ${i}`,
      currentStock: 10,
      threshold: 100,
      baseUnitName: 'Viên',
      severity: 'CRITICAL',
    }));

    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: {
        lowStockCount: 25,
        expiringSoonCount: 0,
        expiredCount: 0,
        lowStockAlerts,
        expiryAlerts: [],
      },
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useInventoryAlerts>);

    render(<InventoryAlertsList />);

    expect(screen.getByText('Medicine 0')).toBeInTheDocument();
    expect(screen.getByText('Medicine 9')).toBeInTheDocument();
    expect(screen.queryByText('Medicine 10')).not.toBeInTheDocument();
  });

  it('renders empty state when no alerts', () => {
    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: {
        lowStockCount: 0,
        expiringSoonCount: 0,
        expiredCount: 0,
        lowStockAlerts: [],
        expiryAlerts: [],
      },
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useInventoryAlerts>);

    render(<InventoryAlertsList />);

    expect(screen.getByText('Kho hoạt động ổn định')).toBeInTheDocument();
  });
});
