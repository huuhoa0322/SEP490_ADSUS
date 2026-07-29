import type { ReactNode } from "react";

import { AppHeader } from "@/components/shared/app-header";
import { AuthGuard } from "@/features/auth/components/auth-guard";

/**
 * Layout for every page that requires a signed-in user.
 * Dropping a new page into (protected) makes it guarded automatically — no extra wiring
 * to remember.
 */
export default function ProtectedLayout({ children }: { children: ReactNode }) {
  return (
    <AuthGuard>
      <div className="flex min-h-screen flex-col bg-muted/30">
        <AppHeader />
        <main className="flex-1">{children}</main>
      </div>
    </AuthGuard>
  );
}
