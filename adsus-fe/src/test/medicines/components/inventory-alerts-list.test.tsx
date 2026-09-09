import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InventoryAlertsList } from '@/features/medicines/components/inventory-alerts-list';
import { useInventoryAlerts } from '@/features/medicines/api/inventory.api';
import { describe, it, expect, vi } from 'vitest';
import { useAuthStore } from '@/store/auth-store';

vi.mock('@/features/medicines/api/inventory.api', () => ({
  useInventoryAlerts: vi.fn(),
  useTriggerInventoryAlerts: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
}));

vi.mock('next/link', () => {
  const MockLink = ({ children, href }: { children: React.ReactNode; href: string }) => {
    return <a href={href}>{children}</a>;
  };
  MockLink.displayName = 'Link';
  return { default: MockLink };
});

vi.mock('@/store/auth-store', () => ({
  useAuthStore: vi.fn(),
}));

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

    // Mock as ADMIN for this test to show the button
    vi.mocked(useAuthStore).mockImplementation((selector: any) => selector({ user: { role: 'ADMIN' } }));

    render(<InventoryAlertsList />);
    
    expect(screen.getByText('Medicine 0')).toBeInTheDocument();
    expect(screen.getByText('Medicine 9')).toBeInTheDocument();
    expect(screen.queryByText('Medicine 10')).not.toBeInTheDocument();
    
    // Test Trigger button presence for ADMIN
    expect(screen.getByText('Test Gửi Thông Báo')).toBeInTheDocument();
  });

  it('hides Trigger Alerts button for DOCTOR role', () => {
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

    // Mock as DOCTOR
    vi.mocked(useAuthStore).mockImplementation((selector: any) => selector({ user: { role: 'DOCTOR' } }));

    render(<InventoryAlertsList />);
    
    expect(screen.queryByText('Test Gửi Thông Báo')).not.toBeInTheDocument();
  });
});
