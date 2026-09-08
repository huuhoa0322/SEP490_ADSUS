import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InventoryAlertsList } from '../inventory-alerts-list';
import { useInventoryAlerts } from '@/features/medicines/api/inventory.api';
import { describe, it, expect, vi } from 'vitest';

// Mock the hook
vi.mock('@/features/medicines/api/inventory.api', () => ({
  useInventoryAlerts: vi.fn(),
}));

// Mock Next.js Link
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

  it('renders error state', () => {
    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    } as unknown as ReturnType<typeof useInventoryAlerts>);

    render(<InventoryAlertsList />);
    expect(screen.getByText('Lỗi khi tải dữ liệu cảnh báo.')).toBeInTheDocument();
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
    expect(screen.getByText('Không có cảnh báo nào về số lượng hay hạn sử dụng.')).toBeInTheDocument();
  });

  it('renders alerts data correctly', () => {
    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: {
        lowStockCount: 1,
        expiringSoonCount: 1,
        expiredCount: 1,
        lowStockAlerts: [
          {
            medicineId: 'med-1',
            medicineName: 'Paracetamol',
            currentStock: 10,
            threshold: 100,
            baseUnitName: 'Viên',
            severity: 'CRITICAL',
          },
        ],
        expiryAlerts: [
          {
            batchId: 'batch-1',
            medicineId: 'med-2',
            medicineName: 'Aspirin',
            lotNumber: 'LOT-123',
            expiryDate: '2026-10-10',
            daysUntilExpiry: -5,
            quantityBase: 50,
            baseUnitName: 'Viên',
            severity: 'EXPIRED',
          },
        ],
      },
      isLoading: false,
      isError: false,
    } as unknown as ReturnType<typeof useInventoryAlerts>);

    render(<InventoryAlertsList />);
    
    // Check summary numbers in the cards
    expect(screen.getAllByText('Đã hết hạn')[0].nextElementSibling?.textContent).toBe('1');
    expect(screen.getAllByText('Sắp hết hạn')[0].nextElementSibling?.textContent).toBe('1');
    expect(screen.getAllByText('Sắp hết hàng')[0].nextElementSibling?.textContent).toBe('1');

    // Check low stock alert table row
    expect(screen.getByText('Paracetamol')).toBeInTheDocument();
    expect(screen.getByText('10 Viên')).toBeInTheDocument();
    expect(screen.getByText('Hết hàng nghiêm trọng')).toBeInTheDocument();

    // Check expiry alert table row
    expect(screen.getByText('Aspirin')).toBeInTheDocument();
    expect(screen.getByText('LOT-123')).toBeInTheDocument();
    expect(screen.getByText('50 Viên')).toBeInTheDocument();
    expect(screen.getByText('Hết hạn')).toBeInTheDocument();
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
    
    // Page 1 should show Med 0 to Med 9 (pageSize is 10 now)
    expect(screen.getByText('Medicine 0')).toBeInTheDocument();
    expect(screen.getByText('Medicine 9')).toBeInTheDocument();
    expect(screen.queryByText('Medicine 10')).not.toBeInTheDocument();
    
    // Click page 2
    const user = userEvent.setup();
    const page2Button = screen.getByText('2');
    await act(async () => {
      await user.click(page2Button);
    });
    
    // Page 2 should show Med 10 to Med 19
    expect(screen.getByText('Medicine 10')).toBeInTheDocument();
    expect(screen.getByText('Medicine 19')).toBeInTheDocument();
    expect(screen.queryByText('Medicine 9')).not.toBeInTheDocument();
  });
});
