import fs from 'fs';
import * as XLSX from 'xlsx';
import { describe, it, expect } from 'vitest';
import { parseExcelExpiryDate } from '@/features/inventory/utils/excel-date-parser';

describe('parseExcelExpiryDate', () => {
  it('xử lý chính xác định dạng YYYY-MM-DD (như trong file DataThat_NhapKho.xlsx: 2027-09-10)', () => {
    const result = parseExcelExpiryDate('2027-09-10');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chính xác định dạng YYYY/MM/DD', () => {
    const result = parseExcelExpiryDate('2027/09/10');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chính xác định dạng YYYY.MM.DD', () => {
    const result = parseExcelExpiryDate('2027.09.10');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chính xác định dạng YYYY MM DD (cách nhau bởi dấu cách)', () => {
    const result = parseExcelExpiryDate('2027 09 10');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chính xác định dạng chuẩn Việt Nam DD/MM/YYYY', () => {
    const result = parseExcelExpiryDate('10/09/2027');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chính xác định dạng DD-MM-YYYY', () => {
    const result = parseExcelExpiryDate('10-09-2027');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chính xác định dạng DD.MM.YYYY', () => {
    const result = parseExcelExpiryDate('10.09.2027');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chính xác định dạng DD MM YYYY', () => {
    const result = parseExcelExpiryDate('10 09 2027');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý số serial ngày của Excel (ví dụ 46640)', () => {
    const result = parseExcelExpiryDate(46640);
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chuỗi số serial (ví dụ "46640")', () => {
    const result = parseExcelExpiryDate('46640');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý đối tượng Date hợp lệ', () => {
    const dateObj = new Date(2027, 8, 10);
    const result = parseExcelExpiryDate(dateObj);
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('xử lý chuỗi ISO chuẩn', () => {
    const result = parseExcelExpiryDate('2027-09-10T00:00:00.000Z');
    expect(result).toBe('2027-09-10T00:00:00.000Z');
  });

  it('trả về null khi giá trị rỗng hoặc không hợp lệ', () => {
    expect(parseExcelExpiryDate(null)).toBeNull();
    expect(parseExcelExpiryDate(undefined)).toBeNull();
    expect(parseExcelExpiryDate('')).toBeNull();
    expect(parseExcelExpiryDate('invalid-date')).toBeNull();
  });

  it('đọc trực tiếp file DataThat_NhapKho.xlsx và parse đúng hạn sử dụng 2027-09-10 cho toàn bộ các lô', () => {
    const filePath = 'C:/Users/quyka/OneDrive/Máy tính/DataThat_NhapKho.xlsx';
    if (fs.existsSync(filePath)) {
      const wb = XLSX.readFile(filePath, { cellDates: true });
      const ws = wb.Sheets[wb.SheetNames[0]];
      const data = XLSX.utils.sheet_to_json(ws) as Record<string, unknown>[];
      expect(data.length).toBeGreaterThan(0);
      for (const row of data) {
        const rawExpiry = row['Hạn Sử Dụng'];
        const parsed = parseExcelExpiryDate(rawExpiry);
        expect(parsed).toBe('2027-09-10T00:00:00.000Z');
      }
    }
  });
});

