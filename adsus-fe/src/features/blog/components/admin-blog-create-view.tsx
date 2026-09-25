"use client";

import { ArrowLeft, Send } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";

import { getApiErrorMessage } from "@/lib/api-client";

import { useCreateBlogPost } from "../hooks/use-admin-blog";

/**
 * Admin Blog Create View - requires ADMIN role.
 * SCR-27 - Táº¡o bÃ i viáº¿t má»›i (báº£n Draft)
 */
export function AdminBlogCreateView() {
  const router = useRouter();
  const createMutation = useCreateBlogPost();

  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (!title.trim() || !content.trim()) {
      return;
    }

    try {
      const result = await createMutation.mutateAsync({
        title: title.trim(),
        content: content.trim(),
      });
      router.push(`/admin/blog/${result.id}`);
    } catch (e) {
      // Error handled by mutation
    }
  };

  return (
    <div className="mx-auto w-full max-w-3xl px-6 py-8">
      <Link
        href="/admin/blog"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-primary"
      >
        <ArrowLeft className="size-4" />
        Danh sÃ¡ch bÃ i viáº¿t
      </Link>

      <h1 className="mt-5 font-heading text-[32px] font-bold tracking-[-0.02em] text-foreground">
        Táº¡o bÃ i viáº¿t má»›i
      </h1>
      <p className="mt-2 text-[15px] leading-relaxed text-muted-foreground">
        BÃ i viáº¿t sáº½ Ä‘Æ°á»£c táº¡o á»Ÿ tráº¡ng thÃ¡i báº£n nhÃ¡p. Báº¡n cÃ³ thá»ƒ chá»‰nh sá»­a vÃ  xuáº¥t báº£n sau.
      </p>

      <form onSubmit={handleSubmit} className="mt-8 space-y-5">
        {/* Title */}
        <label className="flex flex-col gap-2.5">
          <span className="font-heading text-[13px] font-600 uppercase tracking-wider text-foreground">
            TiÃªu Ä‘á» bÃ i viáº¿t
          </span>
          <input
            type="text"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            required
            className="h-14 w-full rounded-full border border-border bg-background px-5 text-[15px] outline-none transition-colors focus:border-[var(--success)]"
            placeholder="Nháº­p tiÃªu Ä‘á» bÃ i viáº¿t..."
          />
        </label>

        {/* Content */}
        <label className="flex flex-col gap-2.5">
          <span className="font-heading text-[13px] font-600 uppercase tracking-wider text-foreground">
            Ná»™i dung bÃ i viáº¿t
          </span>
          <RichTextEditor value={content} onChange={setContent} />
        </label>

        {/* Error */}
        {createMutation.isError && (
          <div role="alert" className="flex items-start gap-2.5 rounded-2xl border border-destructive/25 bg-destructive/5 px-4 py-3 text-sm text-destructive">
            {getApiErrorMessage(createMutation.error, "KhÃ´ng táº¡o Ä‘Æ°á»£c bÃ i viáº¿t.")}
          </div>
        )}

        {/* Actions */}
        <div className="flex items-center justify-end gap-3 pt-2">
          <Link
            href="/admin/blog"
            className="rounded-full border border-border px-5 py-2.5 text-sm font-600 text-muted-foreground transition-colors hover:bg-secondary"
          >
            Há»§y
          </Link>
          <button
            type="submit"
            disabled={createMutation.isPending || !title.trim() || !content.trim()}
            className="flex items-center gap-2 rounded-full bg-[var(--success)] px-6 py-2.5 font-heading text-sm font-600 uppercase tracking-wider text-text-white shadow-lg shadow-[var(--success)]/25 transition-all hover:bg-[var(--success)]/90 disabled:cursor-not-allowed disabled:opacity-60"
          >
            <Send className="size-4" />
            {createMutation.isPending ? "Äang táº¡o..." : "Táº¡o bÃ i viáº¿t"}
          </button>
        </div>
      </form>
    </div>
  );
}

