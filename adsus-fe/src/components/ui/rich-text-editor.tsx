"use client";

import { useEffect } from "react";
import { useEditor, EditorContent } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Underline from "@tiptap/extension-underline";
import Placeholder from "@tiptap/extension-placeholder";
import { Bold, Italic, Underline as UnderlineIcon, List, ListOrdered } from "lucide-react";
import { cn } from "@/lib/utils";

export interface RichTextEditorProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  placeholder?: string;
  className?: string;
}

export function RichTextEditor({
  value,
  onChange,
  disabled = false,
  placeholder = "Nhập nội dung...",
  className,
}: RichTextEditorProps) {
  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        bulletList: {
          keepMarks: true,
          keepAttributes: false,
        },
        orderedList: {
          keepMarks: true,
          keepAttributes: false,
        },
      }),
      Underline,
      Placeholder.configure({
        placeholder,
      }),
    ],
    content: value || "",
    editable: !disabled,
    immediatelyRender: false,
    onUpdate: ({ editor: currentEditor }) => {
      onChange(currentEditor.isEmpty ? "" : currentEditor.getHTML());
    },
  });

  useEffect(() => {
    if (!editor) return;
    const currentHtml = editor.getHTML();
    const isEmptyValue = !value || value.trim() === "";
    const isEditorEmpty = editor.isEmpty;

    if (isEmptyValue && isEditorEmpty) return;

    if (value !== currentHtml) {
      editor.commands.setContent(value || "");
    }
  }, [value, editor]);

  useEffect(() => {
    if (!editor) return;
    editor.setEditable(!disabled);
  }, [disabled, editor]);

  if (!editor) {
    return null;
  }

  return (
    <div
      className={cn(
        "w-full rounded-lg border border-gray-300 dark:border-gray-700 bg-background transition-colors focus-within:ring-2 focus-within:ring-ring focus-within:border-transparent overflow-hidden",
        disabled && "opacity-60 cursor-not-allowed bg-muted/20",
        className,
      )}
    >
      {!disabled && (
        <div className="flex flex-wrap items-center gap-1 border-b border-border bg-muted/30 px-2.5 py-1.5">
          <button
            type="button"
            onClick={() => editor.chain().focus().toggleBold().run()}
            disabled={!editor.can().chain().focus().toggleBold().run()}
            className={cn(
              "rounded p-1.5 text-foreground/80 hover:bg-muted hover:text-foreground transition-colors",
              editor.isActive("bold") && "bg-primary/15 text-primary font-bold",
            )}
            title="In đậm (Ctrl+B)"
            aria-label="In đậm"
          >
            <Bold className="size-4" />
          </button>

          <button
            type="button"
            onClick={() => editor.chain().focus().toggleItalic().run()}
            disabled={!editor.can().chain().focus().toggleItalic().run()}
            className={cn(
              "rounded p-1.5 text-foreground/80 hover:bg-muted hover:text-foreground transition-colors",
              editor.isActive("italic") && "bg-primary/15 text-primary font-bold",
            )}
            title="In nghiêng (Ctrl+I)"
            aria-label="In nghiêng"
          >
            <Italic className="size-4" />
          </button>

          <button
            type="button"
            onClick={() => editor.chain().focus().toggleUnderline().run()}
            disabled={!editor.can().chain().focus().toggleUnderline().run()}
            className={cn(
              "rounded p-1.5 text-foreground/80 hover:bg-muted hover:text-foreground transition-colors",
              editor.isActive("underline") && "bg-primary/15 text-primary font-bold",
            )}
            title="Gạch chân (Ctrl+U)"
            aria-label="Gạch chân"
          >
            <UnderlineIcon className="size-4" />
          </button>

          <div className="mx-1 h-4 w-px bg-border" />

          <button
            type="button"
            onClick={() => editor.chain().focus().toggleBulletList().run()}
            className={cn(
              "rounded p-1.5 text-foreground/80 hover:bg-muted hover:text-foreground transition-colors",
              editor.isActive("bulletList") && "bg-primary/15 text-primary font-bold",
            )}
            title="Danh sách dấu đầu dòng"
            aria-label="Danh sách dấu đầu dòng"
          >
            <List className="size-4" />
          </button>

          <button
            type="button"
            onClick={() => editor.chain().focus().toggleOrderedList().run()}
            className={cn(
              "rounded p-1.5 text-foreground/80 hover:bg-muted hover:text-foreground transition-colors",
              editor.isActive("orderedList") && "bg-primary/15 text-primary font-bold",
            )}
            title="Danh sách có số thứ tự"
            aria-label="Danh sách có số thứ tự"
          >
            <ListOrdered className="size-4" />
          </button>
        </div>
      )}

      <EditorContent
        editor={editor}
        className="prose prose-sm dark:prose-invert max-w-none p-3 min-h-[120px] focus:outline-none [&_.ProseMirror]:outline-none [&_.ProseMirror]:min-h-[100px] [&_.ProseMirror_p.is-editor-empty:first-child::before]:text-muted-foreground [&_.ProseMirror_p.is-editor-empty:first-child::before]:content-[attr(data-placeholder)] [&_.ProseMirror_p.is-editor-empty:first-child::before]:float-left [&_.ProseMirror_p.is-editor-empty:first-child::before]:pointer-events-none [&_.ProseMirror_p.is-editor-empty:first-child::before]:h-0 [&_.ProseMirror_ul]:list-disc [&_.ProseMirror_ul]:pl-5 [&_.ProseMirror_ol]:list-decimal [&_.ProseMirror_ol]:pl-5"
      />
    </div>
  );
}
