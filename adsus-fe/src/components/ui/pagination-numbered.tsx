'use client';

import React from 'react';
import { cn } from '@/lib/utils';

export interface PaginationNumberedProps {
  currentPage: number;
  totalPages: number;
  setPage: (page: number) => void;
  className?: string;
}

export function PaginationNumbered({ currentPage, totalPages, setPage, className }: PaginationNumberedProps) {
  if (totalPages <= 1) return null;

  let pages: number[] = [];
  if (totalPages <= 5) {
    pages = Array.from({ length: totalPages }, (_, i) => i + 1);
  } else if (currentPage <= 3) {
    pages = [1, 2, 3, 4, 5];
  } else if (currentPage >= totalPages - 2) {
    pages = [totalPages - 4, totalPages - 3, totalPages - 2, totalPages - 1, totalPages];
  } else {
    pages = [currentPage - 2, currentPage - 1, currentPage, currentPage + 1, currentPage + 2];
  }

  return (
    <div className={cn("flex items-center space-x-2", className)}>
      <button
        className="px-4 py-2 text-sm font-medium rounded-full border border-border disabled:opacity-50 hover:bg-secondary transition-colors"
        onClick={() => setPage(currentPage - 1)}
        disabled={currentPage <= 1}
      >
        Trước
      </button>
      
      <div className="flex items-center space-x-1">
        {pages.map((p) => (
          <button
            key={p}
            onClick={() => setPage(p)}
            className={cn(
              "flex h-9 min-w-9 items-center justify-center rounded-full text-sm font-medium px-2 transition-colors",
              currentPage === p
                ? "bg-emerald-500 text-white hover:bg-emerald-600 border border-emerald-500"
                : "border border-border text-foreground hover:bg-secondary"
            )}
          >
            {p}
          </button>
        ))}
      </div>

      <button
        className="px-4 py-2 text-sm font-medium rounded-full border border-border disabled:opacity-50 hover:bg-secondary transition-colors"
        onClick={() => setPage(currentPage + 1)}
        disabled={currentPage >= totalPages}
      >
        Sau
      </button>
    </div>
  );
}
