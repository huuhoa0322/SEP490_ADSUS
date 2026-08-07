"use client";

import { formatIsoDateTime } from "../lib/medical-record-labels";
import type { UltrasoundImage } from "../types/medical-record.types";

/**
 * Lưới ảnh siêu âm của một ca khám.
 *
 * `imageUrl` có thể null khi Storage ký URL thất bại (flag F5) — phải có ô hỏng tử tế, đừng
 * để `<img src={null}>` render ra một khung vỡ không giải thích được gì.
 */
export function UltrasoundImageGallery({ images }: { images: UltrasoundImage[] }) {
  if (images.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border p-8 text-center">
        <p className="text-sm text-muted-foreground">Ca khám này chưa có ảnh siêu âm nào.</p>
      </div>
    );
  }

  return (
    <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {images.map((image) => (
        <li key={image.imageId} className="overflow-hidden rounded-lg border border-border">
          {image.imageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={image.imageUrl}
              alt={`Ảnh siêu âm tải lên lúc ${formatIsoDateTime(image.uploadedAt)}`}
              className="aspect-[4/3] w-full bg-black object-contain"
            />
          ) : (
            <div className="flex aspect-[4/3] w-full flex-col items-center justify-center gap-1 bg-destructive/10 p-4 text-center">
              <p className="text-sm font-semibold text-destructive">Không tải được ảnh</p>
              <p className="text-xs text-muted-foreground">
                Liên kết truy cập đã hết hạn, tải lại trang để thử lại.
              </p>
            </div>
          )}

          <div className="p-3">
            <p className="font-mono text-xs text-muted-foreground">
              {formatIsoDateTime(image.uploadedAt)}
            </p>
            <p className="mt-1 text-sm">
              {image.note ?? <span className="italic text-muted-foreground">Không có ghi chú</span>}
            </p>
          </div>
        </li>
      ))}
    </ul>
  );
}
