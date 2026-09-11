import { render, screen, fireEvent, within } from '@testing-library/react';
import { MedicineList } from '@/features/medicines/components/medicine-list';
import { useMedicines } from '@/features/medicines/hooks/use-medicines';
import { useInventoryAlerts } from '@/features/medicines/api/inventory.api';
import { useAuthStore } from '@/store/auth-store';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/features/medicines/hooks/use-medicines', () => ({
  useMedicines: vi.fn(),
  useActivateMedicine: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
  useDeleteMedicine: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
  useMedicineUnits: vi.fn(() => ({ data: [] })),
  useCreateMedicine: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
  useUpdateMedicine: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
}));

vi.mock('@/features/medicines/api/inventory.api', () => ({
  useInventoryAlerts: vi.fn(),
}));

vi.mock('@/store/auth-store', () => ({
  useAuthStore: vi.fn(),
}));

describe('MedicineList', () => {
  const mockMedicines = {
    items: [
      {
        medicineId: '1',
        name: 'Paracetamol',
        totalInventoryBase: 100,
        lowStockThreshold: 10,
        baseUnitName: 'Viên',
        status: 'ACTIVE',
        createdAt: '2023-01-01T00:00:00Z',
      },
      {
        medicineId: '2',
        name: 'Ibuprofen',
        totalInventoryBase: 100,
        lowStockThreshold: 10,
        baseUnitName: 'Viên',
        status: 'INACTIVE',
        createdAt: '2023-01-01T00:00:00Z',
      },
    ],
    totalItems: 2,
    totalPages: 1,
    page: 1,
  };

  it('hides Add and Delete/Activate buttons for DOCTOR role', () => {
    vi.mocked(useMedicines).mockReturnValue({
      data: mockMedicines,
      isLoading: false,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: { totalMedicinesCount: 2, inStockCount: 2, lowStockCount: 0, outOfStockCount: 0 },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    // Mock as DOCTOR
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.mocked(useAuthStore).mockImplementation((selector: any) => selector({ user: { role: 'DOCTOR' } }));

    render(<MedicineList />);

    // Add button should be hidden
    expect(screen.queryByText('Thêm thuốc mới')).not.toBeInTheDocument();
    
    // Pencil button is there but title is "Xem chi tiết" (no straightforward text match for the title but we can check the icon if needed)
    // Actually the prompt says DOCTOR does not see Add/Edit/Delete buttons.
    // In our UI logic, the pencil button is shown for DOCTOR but acts as "Xem chi tiết".
    // Wait, the "Ngừng sử dụng" (Ban icon) and "Kích hoạt lại" (Play icon) buttons have title attributes.
    expect(screen.queryByTitle('Ngừng sử dụng')).not.toBeInTheDocument();
    expect(screen.queryByTitle('Kích hoạt lại')).not.toBeInTheDocument();
  });

  it('shows Add and Delete/Activate buttons for ADMIN role', () => {
    vi.mocked(useMedicines).mockReturnValue({
      data: mockMedicines,
      isLoading: false,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: { totalMedicinesCount: 2, inStockCount: 2, lowStockCount: 0, outOfStockCount: 0 },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.mocked(useAuthStore).mockImplementation((selector: any) => selector({ user: { role: 'ADMIN' } }));

    render(<MedicineList />);

    // Add button should be visible
    expect(screen.getByText('Thêm thuốc mới')).toBeInTheDocument();
    
    // Delete/Activate buttons should be visible
    expect(screen.getByTitle('Ngừng sử dụng')).toBeInTheDocument();
    expect(screen.getByTitle('Kích hoạt lại')).toBeInTheDocument();
  });

  it('renders status and stock filter dropdowns and queries with selected filters', () => {
    vi.mocked(useMedicines).mockReturnValue({
      data: mockMedicines,
      isLoading: false,
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    vi.mocked(useInventoryAlerts).mockReturnValue({
      data: { totalMedicinesCount: 2, inStockCount: 2, lowStockCount: 0, outOfStockCount: 0 },
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    vi.mocked(useAuthStore).mockImplementation((selector: any) => selector({ user: { role: 'ADMIN' } }));

    render(<MedicineList />);

    // Check dropdown options exist
    const statusSelect = screen.getByDisplayValue('Tất cả trạng thái');
    expect(statusSelect).toBeInTheDocument();
    expect(within(statusSelect).getByText('Đang sử dụng (Active)')).toBeInTheDocument();
    expect(within(statusSelect).getByText('Ngừng sử dụng (Inactive)')).toBeInTheDocument();

    const stockSelect = screen.getByDisplayValue('Tất cả tồn kho');
    expect(stockSelect).toBeInTheDocument();
    expect(within(stockSelect).getByText('Còn hàng')).toBeInTheDocument();
    expect(within(stockSelect).getByText('Hết hàng')).toBeInTheDocument();

    // Change status filter to ACTIVE
    fireEvent.change(statusSelect, { target: { value: 'ACTIVE' } });
    expect(useMedicines).toHaveBeenCalledWith(1, 10, '', undefined, 'ACTIVE');

    // Change stock filter to in_stock
    fireEvent.change(stockSelect, { target: { value: 'in_stock' } });
    expect(useMedicines).toHaveBeenCalledWith(1, 10, '', true, 'ACTIVE');
  });
});

