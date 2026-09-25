import React, { useCallback } from 'react';
import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Placeholder from '@tiptap/extension-placeholder';
import Underline from '@tiptap/extension-underline';
import { Bold, Italic, Underline as UnderlineIcon, Strikethrough, List, ListOrdered, ImageIcon, Heading2, Quote } from 'lucide-react';
import { cn } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';

interface RichTextEditorProps {
  value: string;
  onChange: (value: string) => void;
}

const MenuBar = ({ editor }: { editor: any }) => {
  if (!editor) return null;

  const addImage = useCallback(() => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = 'image/*';
    input.onchange = async () => {
      if (input.files?.length) {
        const file = input.files[0];
        try {
          const formData = new FormData();
          formData.append('file', file);
          
          // Endpoint update blog-posts/upload-image
          const res = await apiClient.post<any>('/api/v1/admin/blog-posts/upload-image', formData, {
            headers: { 'Content-Type': 'multipart/form-data' }
          });
          
          if (res.data?.data?.url) {
            editor.chain().focus().setImage({ src: res.data.data.url }).run();
          } else {
            alert('Lỗi upload ảnh!');
          }
        } catch (error) {
          console.error(error);
          alert('Upload ảnh thất bại!');
        }
      }
    };
    input.click();
  }, [editor]);

  return (
    <div className="flex flex-wrap items-center gap-1 border-b border-border bg-muted/20 p-2">
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleBold().run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('bold') && "bg-muted text-primary")}
      >
        <Bold size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleItalic().run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('italic') && "bg-muted text-primary")}
      >
        <Italic size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleUnderline().run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('underline') && "bg-muted text-primary")}
      >
        <UnderlineIcon size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleStrike().run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('strike') && "bg-muted text-primary")}
      >
        <Strikethrough size={16} />
      </button>
      
      <div className="w-px h-4 bg-border mx-1" />
      
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('heading', { level: 2 }) && "bg-muted text-primary")}
      >
        <Heading2 size={16} />
      </button>
      
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleBulletList().run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('bulletList') && "bg-muted text-primary")}
      >
        <List size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleOrderedList().run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('orderedList') && "bg-muted text-primary")}
      >
        <ListOrdered size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().toggleBlockquote().run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive('blockquote') && "bg-muted text-primary")}
      >
        <Quote size={16} />
      </button>
      
      <div className="w-px h-4 bg-border mx-1" />
      
      <button
        type="button"
        onClick={addImage}
        className="p-2 rounded-md hover:bg-muted transition-colors"
      >
        <ImageIcon size={16} />
      </button>
    </div>
  );
};

export const RichTextEditor = ({ value, onChange }: RichTextEditorProps) => {
  const editor = useEditor({
    extensions: [
      StarterKit,
      Underline,
      Image.configure({
        HTMLAttributes: {
          class: 'rounded-lg max-w-full h-auto object-cover my-4',
        },
      }),
      Placeholder.configure({
        placeholder: 'Viết nội dung bài viết...',
      }),
    ],
    content: value,
    onUpdate: ({ editor }) => {
      onChange(editor.getHTML());
    },
    editorProps: {
      attributes: {
        class: 'prose prose-sm sm:prose-base focus:outline-none min-h-[300px] p-4 max-w-none',
      },
    },
  });

  return (
    <div className="flex flex-col rounded-2xl border border-border bg-background overflow-hidden focus-within:border-[var(--success)] transition-colors">
      <MenuBar editor={editor} />
      <EditorContent editor={editor} />
    </div>
  );
};
