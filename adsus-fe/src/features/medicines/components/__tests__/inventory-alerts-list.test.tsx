import { render, screen } from '@testing-library/react';
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
    } as any);

    render(<InventoryAlertsList />);
    expect(screen.getByText('Đang tải dữ liệu cảnh báo...')).toBeInTheDocument();
  });

  it('renders error state', () => {
    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    } as any);

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
    } as any);

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
    } as any);

    render(<InventoryAlertsList />);
    
    // Check summary numbers
    expect(screen.getAllByText('Đã hết hạn')[0].parentElement?.nextElementSibling?.textContent).toBe('1'); // expiredCount
    expect(screen.getByText('Sắp hết hạn').parentElement?.nextElementSibling?.textContent).toBe('1'); // expiringSoonCount
    expect(screen.getByText('Sắp hết hàng').parentElement?.nextElementSibling?.textContent).toBe('1'); // lowStockCount

    // Check low stock alert
    expect(screen.getByText('Paracetamol')).toBeInTheDocument();
    expect(screen.getByText('Tồn: 10 Viên')).toBeInTheDocument();
    expect(screen.getByText('Ngưỡng: 100')).toBeInTheDocument();

    // Check expiry alert
    expect(screen.getByText('Aspirin')).toBeInTheDocument();
    expect(screen.getByText('Lô: LOT-123')).toBeInTheDocument();
    expect(screen.getByText('Tồn: 50 Viên')).toBeInTheDocument();
  });
});
