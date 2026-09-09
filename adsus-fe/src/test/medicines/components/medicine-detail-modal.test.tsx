import { render, screen } from '@testing-library/react';
import { MedicineDetailModal } from '@/features/medicines/components/medicine-detail-modal';
import { useMedicineUnits, useMedicinePackagings, useUpdateMedicine, useAddPackaging, useUpdatePackaging, useDeletePackaging } from '@/features/medicines/hooks/use-medicines';
import { useAuthStore } from '@/store/auth-store';
import { describe, it, expect, vi } from 'vitest';

vi.mock('@/features/medicines/hooks/use-medicines', () => ({
  useMedicineUnits: vi.fn(),
  useMedicinePackagings: vi.fn(),
  useUpdateMedicine: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
  useAddPackaging: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
  useUpdatePackaging: vi.fn(() => ({ mutate: vi.fn(), isPending: false })),
  useDeletePackaging: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
}));

vi.mock('@/store/auth-store', () => ({
  useAuthStore: vi.fn(),
}));

class ResizeObserver {
  observe() {}
  unobserve() {}
  disconnect() {}
}
window.ResizeObserver = ResizeObserver;

describe('MedicineDetailModal', () => {
  const mockMedicine = {
    medicineId: '1',
    name: 'Paracetamol',
    usageUnit: 'Viên',
    volumePerBaseUnit: 500,
    lowStockThreshold: 10,
    baseUnitName: 'Viên',
    status: 'ACTIVE',
    createdAt: '2023-01-01T00:00:00Z',
    totalInventoryBase: 100,
  } as any;

  beforeEach(() => {
    vi.mocked(useMedicineUnits).mockReturnValue({ data: [] } as any);
    vi.mocked(useMedicinePackagings).mockReturnValue({ data: [], isLoading: false } as any);
  });

  it('disables inputs and hides Save button for DOCTOR role', () => {
    // Mock as DOCTOR
    vi.mocked(useAuthStore).mockImplementation((selector: any) => selector({ user: { role: 'DOCTOR' } }));

    render(<MedicineDetailModal medicine={mockMedicine} isOpen={true} onClose={() => {}} />);

    // Usage Unit input should be disabled
    const usageUnitInput = screen.getByPlaceholderText('VD: ml, viên...');
    expect(usageUnitInput).toBeDisabled();

    // Volume input should be disabled
    const volumeInput = screen.getByPlaceholderText('VD: 5');
    expect(volumeInput).toBeDisabled();

    // Low stock threshold input should be disabled
    const thresholdInput = screen.getByPlaceholderText('Nhập 0 để bỏ qua');
    expect(thresholdInput).toBeDisabled();

    // Save general info button should be hidden
    expect(screen.queryByText('Lưu thông tin cơ bản')).not.toBeInTheDocument();

    // Thao tác column in packaging table should be hidden
    expect(screen.queryByText('Thao tác')).not.toBeInTheDocument();

    // Thêm Quy cách Mới form should be hidden
    expect(screen.queryByText('Thêm Quy cách Mới')).not.toBeInTheDocument();
  });

  it('enables inputs and shows Save button for ADMIN role', () => {
    // Mock as ADMIN
    vi.mocked(useAuthStore).mockImplementation((selector: any) => selector({ user: { role: 'ADMIN' } }));

    render(<MedicineDetailModal medicine={mockMedicine} isOpen={true} onClose={() => {}} />);

    // Usage Unit input should be enabled
    const usageUnitInput = screen.getByPlaceholderText('VD: ml, viên...');
    expect(usageUnitInput).not.toBeDisabled();

    // Save general info button should be visible
    expect(screen.getByText('Lưu thông tin cơ bản')).toBeInTheDocument();

    // Thêm Quy cách Mới form should be visible
    expect(screen.getByText('Thêm Quy cách Mới')).toBeInTheDocument();
  });
});
