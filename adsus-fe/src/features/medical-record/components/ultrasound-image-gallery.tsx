"use client";

import { useState } from "react";
import { formatIsoDateTime } from "../lib/medical-record-labels";
import type { UltrasoundImage } from "../types/medical-record.types";

/**
 * Lưới ảnh siêu âm của một ca khám.
 *
 * `imageUrl` có thể null khi Storage ký URL thất bại (flag F5) — phải có ô hỏng tử tế, đừng
 * để `<img src={null}>` render ra một khung vỡ không giải thích được gì.
 */
export function UltrasoundImageGallery({ images }: { images: UltrasoundImage[] }) {
  const [selectedImage, setSelectedImage] = useState<string | null>(null);

  if (images.length === 0) {
    return (
      <div className="rounded-lg border border-dashed border-border p-8 text-center">
        <p className="text-sm text-muted-foreground">Ca khám này chưa có ảnh siêu âm nào.</p>
      </div>
    );
  }

  return (
    <>
      <ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {images.map((image) => (
        <li key={image.imageId} className="overflow-hidden rounded-lg border border-border">
          {image.imageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={image.imageUrl}
              alt={`Ảnh siêu âm tải lên lúc ${formatIsoDateTime(image.uploadedAt)}`}
              className="aspect-[4/3] w-full bg-black object-contain cursor-pointer transition-opacity hover:opacity-85"
              onClick={() => setSelectedImage(image.imageUrl!)}
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

      {selectedImage && (
        <div 
          className="fixed inset-0 z-[100] flex items-center justify-center bg-black/85 p-4 backdrop-blur-sm"
          onClick={() => setSelectedImage(null)}
        >
          <div className="relative max-w-5xl w-full h-full max-h-[90vh] flex flex-col items-center justify-center">
            <button 
              className="absolute -top-2 right-0 md:-top-4 md:-right-4 m-4 h-10 w-10 flex items-center justify-center rounded-full bg-white/20 text-white hover:bg-white/40 transition-colors z-10 text-xl font-bold"
              onClick={() => setSelectedImage(null)}
            >
              ×
            </button>
            <img 
              src={selectedImage} 
              alt="Ảnh phóng to" 
              className="max-w-full max-h-full object-contain rounded-md shadow-2xl" 
              onClick={(e) => e.stopPropagation()}
            />
          </div>
        </div>
      )}
    </>
  );
}
