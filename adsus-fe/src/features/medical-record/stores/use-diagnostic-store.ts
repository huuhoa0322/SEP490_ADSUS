import { create } from "zustand";

interface DiagnosticStore {
  caseId: string | null;
  images: File[];
  currentIndex: number;
  setDiagnosticSession: (caseId: string, files: File[]) => void;
  nextImage: () => void;
  clearSession: () => void;
}

export const useDiagnosticStore = create<DiagnosticStore>((set) => ({
  caseId: null,
  images: [],
  currentIndex: 0,
  setDiagnosticSession: (caseId, files) =>
    set({ caseId, images: files, currentIndex: 0 }),
  nextImage: () =>
    set((state) => ({ currentIndex: state.currentIndex + 1 })),
  clearSession: () => set({ caseId: null, images: [], currentIndex: 0 }),
}));
