import { useEffect, useRef } from "react";
import { useDiagnosticStore } from "../stores/use-diagnostic-store";
import { getApiErrorMessage } from "@/lib/api-client";
import { analyzeImage } from "../api/cases-diagnosis.api";

export function useBackgroundAi() {
  const store = useDiagnosticStore();
  const { caseId, images, setAiResult, setIsProcessing } = store;
  const processLock = useRef(false);

  useEffect(() => {
    if (!caseId || images.length === 0) return;

    const processNext = async () => {
      if (processLock.current) return;
      
      // Get fresh state to avoid stale closures in setTimeout
      const currentState = useDiagnosticStore.getState();
      const freshAiResults = currentState.aiResults;
      const freshIsProcessing = currentState.isProcessing;

      // Find the first image that has not been processed and is not currently processing
      const nextIndex = images.findIndex((_, index) => !freshAiResults[index] && !freshIsProcessing[index]);
      
      if (nextIndex === -1) return; // All done

      processLock.current = true;
      setIsProcessing(nextIndex, true);

      try {
        const result = await analyzeImage(caseId, images[nextIndex]);
        setAiResult(nextIndex, result);
      } catch (err) {
        setAiResult(nextIndex, {
          sessionId: 'failed',
          detections: [],
          error: getApiErrorMessage(err, "Lỗi hệ thống")
        });
      } finally {
        setIsProcessing(nextIndex, false);
        processLock.current = false;
        
        // Trigger next iteration
        setTimeout(processNext, 100);
      }
    };

    processNext();
  }, [caseId, images, setAiResult, setIsProcessing]);
}
