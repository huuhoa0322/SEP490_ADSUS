import * as XLSX from 'xlsx';

/**
 * Chuyển đổi giá trị Hạn Sử Dụng từ Excel thành chuỗi ISO chuẩn UTC (YYYY-MM-DDTHH:mm:ss.sssZ).
 * Khắc phục triệt để lỗi khi người dùng nhập theo định dạng YYYY-MM-DD, YYYY/MM/DD, YYYY MM DD, v.v.
 * bị hiểu nhầm thành năm 1916 (do parse thứ tự ngày/tháng/năm sai).
 *
 * Hỗ trợ các định dạng:
 * - Date object từ SheetJS
 * - Số serial ngày của Excel (ví dụ: 46640)
 * - Chuỗi YYYY-MM-DD, YYYY/MM/DD, YYYY.MM.DD, YYYY MM DD
 * - Chuỗi DD-MM-YYYY, DD/MM/YYYY, DD.MM.YYYY, DD MM YYYY
 * - Chuỗi MM/DD/YYYY khi ngày > 12
 */
export function parseExcelExpiryDate(rawExpiry: unknown): string | null {
  if (rawExpiry === null || rawExpiry === undefined || rawExpiry === '') {
    return null;
  }

  // 1. Trường hợp SheetJS đã parse thành Date object
  if (rawExpiry instanceof Date) {
    if (Number.isNaN(rawExpiry.getTime())) return null;
    return new Date(
      Date.UTC(rawExpiry.getFullYear(), rawExpiry.getMonth(), rawExpiry.getDate())
    ).toISOString();
  }

  // 2. Trường hợp là số serial của Excel (hoặc chuỗi toàn số)
  const numVal =
    typeof rawExpiry === 'number'
      ? rawExpiry
      : typeof rawExpiry === 'string' && /^\d+(\.\d+)?$/.test(rawExpiry.trim())
        ? Number(rawExpiry.trim())
        : Number.NaN;

  if (!Number.isNaN(numVal) && numVal > 0) {
    try {
      const parsed = XLSX.SSF.parse_date_code(numVal);
      if (parsed && parsed.y && parsed.m && parsed.d) {
        return new Date(Date.UTC(parsed.y, parsed.m - 1, parsed.d)).toISOString();
      }
    } catch {
      // Bỏ qua nếu parse_date_code lỗi
    }
  }

  // 3. Trường hợp là chuỗi văn bản
  const str = String(rawExpiry).trim();
  if (!str) return null;

  // Tách theo các ký tự phân cách phổ biến: /, -, ., hoặc khoảng trắng
  const parts = str.split(/[\/\-\.\s]+/);
  if (parts.length === 3) {
    const p0 = Number.parseInt(parts[0], 10);
    const p1 = Number.parseInt(parts[1], 10);
    const p2 = Number.parseInt(parts[2], 10);

    let year: number | undefined;
    let month: number | undefined; // 0-indexed
    let day: number | undefined;

    // A. Định dạng YYYY-MM-DD (năm đứng đầu: có 4 chữ số hoặc >= 1000)
    if (parts[0].length === 4 || p0 >= 1000) {
      year = p0;
      month = p1 - 1;
      day = p2;
    }
    // B. Định dạng DD-MM-YYYY hoặc MM-DD-YYYY (năm đứng cuối: có 4 chữ số hoặc >= 1000)
    else if (parts[2].length === 4 || p2 >= 1000) {
      year = p2;
      if (p0 > 12 && p1 <= 12) {
        // p0 chắc chắn là ngày (DD/MM/YYYY)
        day = p0;
        month = p1 - 1;
      } else if (p1 > 12 && p0 <= 12) {
        // p1 chắc chắn là ngày (MM/DD/YYYY)
        month = p0 - 1;
        day = p1;
      } else {
        // Mặc định chuẩn Việt Nam: DD/MM/YYYY
        day = p0;
        month = p1 - 1;
      }
    }
    // C. Định dạng năm 2 chữ số (ví dụ: 10/09/27)
    else if (parts[2].length === 2) {
      year = 2000 + p2;
      day = p0;
      month = p1 - 1;
    }

    if (
      year !== undefined &&
      month !== undefined &&
      day !== undefined &&
      month >= 0 &&
      month < 12 &&
      day >= 1 &&
      day <= 31
    ) {
      const utcDate = new Date(Date.UTC(year, month, day));
      if (!Number.isNaN(utcDate.getTime())) {
        return utcDate.toISOString();
      }
    }
  }

  // 4. Dự phòng: Thử parse ISO chuẩn hoặc chuỗi hợp lệ khác bằng Date constructor
  const fallback = new Date(str);
  if (!Number.isNaN(fallback.getTime())) {
    return new Date(
      Date.UTC(fallback.getFullYear(), fallback.getMonth(), fallback.getDate())
    ).toISOString();
  }

  return null;
}
