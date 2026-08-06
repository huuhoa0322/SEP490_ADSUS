"use client";

import { useId, useState } from "react";

/** PRD §6.1 — chỉ JPEG và PNG, tối đa 20MB mỗi ảnh. KHÔNG hỗ trợ DICOM. */
const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024;
const ACCEPTED_TYPES = ["image/jpeg", "image/png"];

interface Props {
  files: File[];
  onChange: (files: File[]) => void;
  disabled?: boolean;
}

/**
 * Ô chọn ảnh siêu âm, dùng chung cho `#20` (tạo ca) và `#21` (bổ sung ảnh).
 *
 * Kiểm định dạng và kích thước ngay tại đây để người dùng biết ngay ảnh nào hỏng, thay vì
 * đợi backend trả lỗi cho cả lô. Backend vẫn kiểm lại bằng magic-byte (UC-07 BR-01) — đổi
 * đuôi file thành .jpg không qua mặt được nó, còn kiểm ở đây thì có.
 */
export function UltrasoundUploadField({ files, onChange, disabled }: Props) {
  const inputId = useId();
  const [rejected, setRejected] = useState<string[]>([]);

  function handleSelect(selected: FileList | null) {
    if (!selected) return;

    const accepted: File[] = [];
    const problems: string[] = [];

    for (const file of Array.from(selected)) {
      if (!ACCEPTED_TYPES.includes(file.type)) {
        problems.push(`${file.name}: chỉ nhận ảnh JPEG hoặc PNG.`);
        continue;
      }
      if (file.size > MAX_FILE_SIZE_BYTES) {
        problems.push(`${file.name}: vượt quá 20MB.`);
        continue;
      }
      accepted.push(file);
    }

    setRejected(problems);
    onChange([...files, ...accepted]);
  }

  function removeAt(index: number) {
    onChange(files.filter((_, position) => position !== index));
  }

  return (
    <div>
      <label htmlFor={inputId} className="mb-1.5 block text-sm font-medium">
        Chọn ảnh siêu âm *
      </label>
      <input
        id={inputId}
        type="file"
        multiple
        accept="image/jpeg,image/png"
        disabled={disabled}
        onChange={(event) => {
          handleSelect(event.target.files);
          // Xoá giá trị để chọn lại đúng file vừa bỏ ra vẫn kích hoạt onChange.
          event.target.value = "";
        }}
        className="block w-full rounded-lg border border-dashed border-border bg-background p-4 text-sm outline-none file:mr-3 file:rounded-md file:border-0 file:bg-accent file:px-3 file:py-1.5 file:text-sm file:font-medium focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
      />
      <p className="mt-1.5 text-xs text-muted-foreground">
        JPEG hoặc PNG, tối đa 20MB mỗi ảnh. Chọn được nhiều ảnh cùng lúc.
      </p>

      {rejected.length > 0 ? (
        <ul className="mt-2 space-y-1" role="alert">
          {rejected.map((message) => (
            <li key={message} className="text-xs text-destructive">
              {message}
            </li>
          ))}
        </ul>
      ) : null}

      {files.length > 0 ? (
        <ul className="mt-4 grid grid-cols-2 gap-3 sm:grid-cols-3">
          {files.map((file, index) => (
            <li
              key={`${file.name}-${file.lastModified}-${file.size}`}
              className="overflow-hidden rounded-lg border border-border"
            >
              {/* URL.createObjectURL đủ dùng cho ô xem trước: ảnh chỉ sống trong lúc form mở,
                  và trình duyệt tự thu hồi khi trang đóng. */}
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={URL.createObjectURL(file)}
                alt={`Ảnh siêu âm ${file.name}`}
                className="aspect-[4/3] w-full bg-black object-contain"
              />
              <div className="flex items-center justify-between gap-2 p-2">
                <span className="truncate text-xs text-muted-foreground" title={file.name}>
                  {file.name}
                </span>
                <button
                  type="button"
                  onClick={() => removeAt(index)}
                  disabled={disabled}
                  aria-label={`Bỏ ảnh ${file.name}`}
                  className="shrink-0 rounded px-1.5 text-sm text-destructive hover:bg-destructive/10 disabled:opacity-50"
                >
                  ✕
                </button>
              </div>
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
