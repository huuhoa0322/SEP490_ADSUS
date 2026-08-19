import { create } from "zustand";

export interface AiResultData {
  sessionId: string;
  detections: any[];
  error?: string;
}

interface DiagnosticStore {
  caseId: string | null;
  images: File[];
  currentIndex: number;
  aiResults: Record<number, AiResultData>;
  isProcessing: Record<number, boolean>;
  
  setDiagnosticSession: (caseId: string, files: File[]) => void;
  nextImage: () => void;
  prevImage: () => void;
  goToImage: (index: number) => void;
  setAiResult: (index: number, result: AiResultData) => void;
  setIsProcessing: (index: number, processing: boolean) => void;
  removeImage: (index: number) => void;
  clearSession: () => void;
}

export const useDiagnosticStore = create<DiagnosticStore>((set) => ({
  caseId: null,
  images: [],
  currentIndex: 0,
  aiResults: {},
  isProcessing: {},
  setDiagnosticSession: (caseId, files) =>
    set({ caseId, images: files, currentIndex: 0, aiResults: {}, isProcessing: {} }),
  nextImage: () =>
    set((state) => ({ currentIndex: state.currentIndex + 1 })),
  prevImage: () =>
    set((state) => ({ currentIndex: Math.max(0, state.currentIndex - 1) })),
  goToImage: (index) =>
    set({ currentIndex: index }),
  setAiResult: (index, result) =>
    set((state) => ({ aiResults: { ...state.aiResults, [index]: result } })),
  setIsProcessing: (index, processing) =>
    set((state) => ({ isProcessing: { ...state.isProcessing, [index]: processing } })),
  removeImage: (index) => set((state) => {
    const newImages = [...state.images];
    newImages.splice(index, 1);
    
    const newAiResults: Record<number, AiResultData> = {};
    const newIsProcessing: Record<number, boolean> = {};
    
    for (let i = 0; i < state.images.length; i++) {
      if (i === index) continue;
      const newIndex = i > index ? i - 1 : i;
      if (state.aiResults[i] !== undefined) newAiResults[newIndex] = state.aiResults[i];
      if (state.isProcessing[i] !== undefined) newIsProcessing[newIndex] = state.isProcessing[i];
    }
    
    let newIndex = state.currentIndex;
    if (newImages.length === 0) {
      newIndex = 0;
    } else if (newIndex >= newImages.length) {
      newIndex = newImages.length - 1;
    } else if (newIndex > index) {
      newIndex--;
    }
    
    return {
      images: newImages,
      aiResults: newAiResults,
      isProcessing: newIsProcessing,
      currentIndex: newIndex
    };
  }),
  clearSession: () => set({ caseId: null, images: [], currentIndex: 0, aiResults: {}, isProcessing: {} }),
}));
