import React, { useCallback } from 'react';
import { useEditor, EditorContent } from '@tiptap/react';
import StarterKit from '@tiptap/starter-kit';
import Image from '@tiptap/extension-image';
import Placeholder from '@tiptap/extension-placeholder';
import Underline from '@tiptap/extension-underline';
import TextAlign from '@tiptap/extension-text-align';
import { Indent } from './tiptap-indent';
import { Bold, Italic, Underline as UnderlineIcon, Strikethrough, List, ListOrdered, ImageIcon, Heading2, Quote, AlignLeft, AlignCenter, AlignRight, AlignJustify, Indent as IndentIcon, Outdent } from 'lucide-react';
import { cn } from '@/lib/utils';
import { apiClient } from '@/lib/api-client';

interface RichTextEditorProps {
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
  placeholder?: string;
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
        onClick={() => editor.chain().focus().setTextAlign('left').run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive({ textAlign: 'left' }) && "bg-muted text-primary")}
      >
        <AlignLeft size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().setTextAlign('center').run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive({ textAlign: 'center' }) && "bg-muted text-primary")}
      >
        <AlignCenter size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().setTextAlign('right').run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive({ textAlign: 'right' }) && "bg-muted text-primary")}
      >
        <AlignRight size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().setTextAlign('justify').run()}
        className={cn("p-2 rounded-md hover:bg-muted transition-colors", editor.isActive({ textAlign: 'justify' }) && "bg-muted text-primary")}
      >
        <AlignJustify size={16} />
      </button>

      <div className="w-px h-4 bg-border mx-1" />

      <button
        type="button"
        onClick={() => editor.chain().focus().indent().run()}
        className="p-2 rounded-md hover:bg-muted transition-colors"
      >
        <IndentIcon size={16} />
      </button>
      <button
        type="button"
        onClick={() => editor.chain().focus().outdent().run()}
        className="p-2 rounded-md hover:bg-muted transition-colors"
      >
        <Outdent size={16} />
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

export const RichTextEditor = ({ value, onChange, disabled = false, placeholder = 'Viết nội dung bài viết...' }: RichTextEditorProps) => {
  const editor = useEditor({
    editable: !disabled,
    extensions: [
      StarterKit,
      Underline,
      TextAlign.configure({
        types: ['heading', 'paragraph'],
      }),
      Indent,
      Image.configure({
        HTMLAttributes: {
          class: 'rounded-lg max-w-full h-auto object-cover my-4',
        },
      }),
      Placeholder.configure({
        placeholder: placeholder,
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
      handlePaste: (view, event) => {
        const items = Array.from(event.clipboardData?.items || []);
        const imageItem = items.find(item => item.type.startsWith('image/'));
        
        if (imageItem) {
          const file = imageItem.getAsFile();
          if (file) {
            const uploadImage = async () => {
              try {
                const formData = new FormData();
                formData.append('file', file);
                const res = await apiClient.post<any>('/api/v1/admin/blog-posts/upload-image', formData, {
                  headers: { 'Content-Type': 'multipart/form-data' }
                });
                
                if (res.data?.data?.url) {
                  const { schema } = view.state;
                  const node = schema.nodes.image.create({ src: res.data.data.url });
                  const transaction = view.state.tr.replaceSelectionWith(node);
                  view.dispatch(transaction);
                }
              } catch (error) {
                console.error('Paste image upload failed:', error);
                alert('Upload ảnh thất bại!');
              }
            };
            
            uploadImage();
            return true;
          }
        }
        return false;
      },
    },
  });

  return (
    <div className={cn("flex flex-col rounded-2xl border border-border bg-background overflow-hidden transition-colors", !disabled && "focus-within:border-[var(--success)]", disabled && "opacity-70 cursor-not-allowed")}>
      {!disabled && <MenuBar editor={editor} />}
      <EditorContent editor={editor} className={cn(disabled && "pointer-events-none")} />
    </div>
  );
};
