import {
  type ConfirmationResult,
  RecaptchaVerifier,
  signInWithPhoneNumber,
} from "firebase/auth";

import { auth } from "@/lib/firebase";

let recaptchaVerifier: RecaptchaVerifier | null = null;

export function ensureRecaptchaContainer(
  containerId: string = "recaptcha-container",
): HTMLElement | null {
  if (typeof window === "undefined" || typeof document === "undefined") {
    return null;
  }

  let container = document.getElementById(containerId);
  if (!container) {
    container = document.createElement("div");
    container.id = containerId;
    container.style.position = "fixed";
    container.style.bottom = "0";
    container.style.right = "0";
    container.style.zIndex = "-1";
    container.style.visibility = "hidden";
    document.body.appendChild(container);
  }
  return container;
}

export function getRecaptchaVerifier(
  containerId: string = "recaptcha-container",
): RecaptchaVerifier {
  if (typeof window === "undefined") {
    throw new Error("RecaptchaVerifier can only be initialized in browser environment.");
  }

  if (!recaptchaVerifier) {
    ensureRecaptchaContainer(containerId);
    recaptchaVerifier = new RecaptchaVerifier(auth, containerId, {
      size: "invisible",
      callback: () => {
        // Invisible reCAPTCHA solved
      },
      "expired-callback": () => {
        clearRecaptchaVerifier();
      },
    });
  }

  return recaptchaVerifier;
}

export function clearRecaptchaVerifier(): void {
  if (recaptchaVerifier) {
    try {
      recaptchaVerifier.clear();
    } catch {
      // ignore cleanup errors
    }
    recaptchaVerifier = null;
  }
}

export function formatToE164(phone: string): string {
  const trimmed = phone.trim().replace(/[\s.-]+/g, "");
  if (trimmed.startsWith("+84")) {
    return trimmed;
  }
  if (trimmed.startsWith("84")) {
    return `+${trimmed}`;
  }
  if (trimmed.startsWith("0")) {
    return `+84${trimmed.slice(1)}`;
  }
  return trimmed;
}

export async function sendPhoneVerificationCode(
  localPhone: string,
): Promise<ConfirmationResult> {
  const formattedPhone = formatToE164(localPhone);
  const verifier = getRecaptchaVerifier();
  try {
    return await signInWithPhoneNumber(auth, formattedPhone, verifier);
  } catch (error) {
    clearRecaptchaVerifier();
    throw error;
  }
}

export async function confirmPhoneVerificationCode(
  confirmationResult: ConfirmationResult,
  code: string,
): Promise<string> {
  const userCredential = await confirmationResult.confirm(code);
  const idToken = await userCredential.user.getIdToken();
  try {
    await auth.signOut();
  } catch {
    // ignore signout errors
  }
  return idToken;
}
