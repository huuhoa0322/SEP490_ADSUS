"use client";

import { useMemo } from "react";
import { PlusIcon, TrashIcon } from "lucide-react";

import type { CreateCaseSymptomInput } from "../types/medical-record.types";
import { useSymptomCategories } from "../hooks/use-symptoms";

interface SymptomSelectorProps {
  value: CreateCaseSymptomInput[];
  onChange: (value: CreateCaseSymptomInput[]) => void;
}

export function SymptomSelector({ value, onChange }: SymptomSelectorProps) {
  const { data: categories, isLoading } = useSymptomCategories();

  // Đưa "Khác" xuống cuối
  const sortedCategories = useMemo(() => {
    if (!categories) return [];
    return [...categories].sort((a, b) => {
      if (a.isOther && !b.isOther) return 1;
      if (!a.isOther && b.isOther) return -1;
      return 0;
    });
  }, [categories]);

  // Thêm một nhóm (block) category mới
  function handleAddCategoryBlock() {
    onChange([...value, { categoryId: "", symptomId: null, otherNote: null }]);
  }

  // Xoá một block
  function handleRemoveBlock(index: number) {
    const newVal = [...value];
    newVal.splice(index, 1);
    onChange(newVal);
  }

  // Cập nhật categoryId cho một block (reset luôn các checkbox đã chọn trong block đó)
  function handleCategoryChange(index: number, newCategoryId: string) {
    const newVal = [...value];
    // Remove all old selected symptoms of this block
    const updatedVal = newVal.filter((item, i) => i !== index || item.categoryId === newCategoryId);
    
    // If it's changing to a new category, we reset the block to just the categoryId
    if (newVal[index].categoryId !== newCategoryId) {
      newVal[index] = { categoryId: newCategoryId, symptomId: null, otherNote: null };
      onChange(newVal);
    }
  }

  // Xử lý toggle checkbox của một symptom trong một category block
  function handleToggleSymptom(blockIndex: number, symptomId: string, isChecked: boolean) {
    const categoryId = value[blockIndex].categoryId;
    const category = categories?.find((c) => c.categoryId === categoryId);
    if (!category) return;

    let newVal = [...value];
    
    if (isChecked) {
      // Nếu block hiện tại đang trống symptomId (chỉ có categoryId), dùng luôn
      if (!newVal[blockIndex].symptomId && newVal[blockIndex].otherNote === null) {
        newVal[blockIndex].symptomId = symptomId;
      } else {
        // Nếu block đã có symptom khác, ta phải chèn thêm 1 item mới chung category
        newVal.splice(blockIndex + 1, 0, { categoryId, symptomId, otherNote: null });
      }
    } else {
      // Tìm và xoá
      const removeIdx = newVal.findIndex(
        (v, i) => i >= blockIndex && v.categoryId === categoryId && v.symptomId === symptomId
      );
      if (removeIdx !== -1) {
        newVal.splice(removeIdx, 1);
        // Nếu xoá hết của category này thì phải giữ lại 1 mảng rỗng để hiện select
        const stillHasCategory = newVal.some((v) => v.categoryId === categoryId);
        if (!stillHasCategory) {
          newVal.splice(blockIndex, 0, { categoryId, symptomId: null, otherNote: null });
        }
      }
    }
    onChange(newVal);
  }

  // Xử lý ô Other trong category bình thường
  function handleOtherNoteChange(blockIndex: number, otherNote: string) {
    const categoryId = value[blockIndex].categoryId;
    let newVal = [...value];
    
    // Tìm phần tử "Other" của category này
    const otherIdx = newVal.findIndex(
      (v, i) => i >= blockIndex && v.categoryId === categoryId && v.symptomId === null && v.otherNote !== null
    );

    if (otherIdx !== -1) {
      newVal[otherIdx].otherNote = otherNote;
    } else {
      // Nếu chưa có, thay thế cái rỗng hoặc thêm mới
      if (!newVal[blockIndex].symptomId && newVal[blockIndex].otherNote === null) {
        newVal[blockIndex].otherNote = otherNote;
      } else {
        newVal.splice(blockIndex + 1, 0, { categoryId, symptomId: null, otherNote });
      }
    }
    onChange(newVal);
  }

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Đang tải danh mục triệu chứng...</div>;
  }

  // Gộp các value item theo categoryId để render thành các block
  const blocks: { categoryId: string; items: CreateCaseSymptomInput[]; startIndex: number }[] = [];
  
  let currentIndex = 0;
  while (currentIndex < value.length) {
    const currentCatId = value[currentIndex].categoryId;
    const items = [];
    const startIndex = currentIndex;
    
    while (currentIndex < value.length && value[currentIndex].categoryId === currentCatId) {
      items.push(value[currentIndex]);
      currentIndex++;
      // Nếu categoryId rỗng thì không gộp chung với các item rỗng tiếp theo
      if (currentCatId === "") break;
    }
    
    blocks.push({ categoryId: currentCatId, items, startIndex });
  }

  // Danh sách các categoryId đã được chọn (để filter không cho chọn trùng)
  const allSelectedCategoryIds = blocks.map(b => b.categoryId).filter(id => id !== "");

  return (
    <div className="space-y-4">
      {blocks.map((block, bIdx) => {
        const selectedCategory = sortedCategories?.find((c) => c.categoryId === block.categoryId);
        const isCategoryOther = selectedCategory?.isOther === true;

        // Các symptomIds đã được chọn trong block này
        const selectedSymptomIds = block.items.map((i) => i.symptomId).filter(Boolean) as string[];
        // Nội dung của text other trong block này (nếu có)
        const otherItem = block.items.find((i) => i.symptomId === null && i.otherNote !== null);
        const otherText = otherItem?.otherNote || "";

        return (
          <div key={bIdx} className="rounded-lg border border-border p-4 bg-muted/20 space-y-3 relative">
            <div className="flex items-center gap-3">
              <select
                value={block.categoryId}
                onChange={(e) => handleCategoryChange(block.startIndex, e.target.value)}
                className="h-10 flex-1 rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
              >
                <option value="">-- Chọn nhóm triệu chứng --</option>
                {sortedCategories?.map((cat) => {
                  // Ẩn các category đã được chọn ở các block khác
                  const isSelectedByOther = allSelectedCategoryIds.includes(cat.categoryId) && cat.categoryId !== block.categoryId;
                  if (isSelectedByOther) return null;

                  return (
                    <option key={cat.categoryId} value={cat.categoryId}>
                      {cat.name}
                    </option>
                  );
                })}
              </select>
              
              <button
                type="button"
                onClick={() => {
                  const newVal = [...value];
                  newVal.splice(block.startIndex, block.items.length);
                  onChange(newVal);
                }}
                className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg border border-destructive text-destructive hover:bg-destructive/10 transition-colors"
                title="Xoá nhóm này"
              >
                <TrashIcon className="h-4 w-4" />
              </button>
            </div>

            {selectedCategory && (
              <div className="pl-2 pt-2 border-l-2 border-muted">
                {isCategoryOther ? (
                  // Nhóm Other Category: Chỉ có 1 textbox
                  <textarea
                    value={otherText}
                    onChange={(e) => {
                       const newVal = [...value];
                       newVal[block.startIndex].otherNote = e.target.value;
                       onChange(newVal);
                    }}
                    placeholder="Mô tả triệu chứng khác..."
                    rows={3}
                    className="w-full rounded-lg border border-border bg-background p-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
                  />
                ) : (() => {
                  // Sắp xếp: Triệu chứng Khác (DB) xuống cuối
                  const sortedSymptoms = [...selectedCategory.symptoms].sort((a, b) => {
                    const aIsOther = a.isOther || a.name.toLowerCase().includes('khác');
                    const bIsOther = b.isOther || b.name.toLowerCase().includes('khác');
                    if (aIsOther && !bIsOther) return 1;
                    if (!aIsOther && bIsOther) return -1;
                    return 0;
                  });

                  // Tìm xem DB có sẵn triệu chứng Khác không
                  const dbOtherSymptom = sortedSymptoms.find(sym => sym.isOther || sym.name.toLowerCase().includes('khác'));
                  const isDbOtherChecked = dbOtherSymptom ? selectedSymptomIds.includes(dbOtherSymptom.symptomId) : false;
                  
                  // Lấy text của triệu chứng Khác (có thể gắn vào DB Khác hoặc Fallback Khác)
                  let currentOtherNote = "";
                  let activeOtherSymptomId: string | null = null;
                  
                  if (dbOtherSymptom && isDbOtherChecked) {
                    const matchedItem = block.items.find(i => i.symptomId === dbOtherSymptom.symptomId);
                    currentOtherNote = matchedItem?.otherNote || "";
                    activeOtherSymptomId = dbOtherSymptom.symptomId;
                  } else if (!!otherItem) {
                    currentOtherNote = otherItem.otherNote || "";
                    activeOtherSymptomId = null;
                  }

                  const showTextBox = isDbOtherChecked || !!otherItem;

                  return (
                    <div className="space-y-3">
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-2">
                        {sortedSymptoms.map((sym) => (
                          <label
                            key={sym.symptomId}
                            className="flex items-start gap-2 cursor-pointer text-sm"
                          >
                            <input
                              type="checkbox"
                              checked={selectedSymptomIds.includes(sym.symptomId)}
                              onChange={(e) =>
                                handleToggleSymptom(block.startIndex, sym.symptomId, e.target.checked)
                              }
                              className="mt-1 shrink-0 rounded border-primary text-primary focus:ring-primary"
                            />
                            <span className="leading-snug">{sym.name}</span>
                          </label>
                        ))}
                        
                        {/* Checkbox Khác... fallback (chỉ hiện nếu DB không có triệu chứng Khác) */}
                        {!dbOtherSymptom && (
                          <label className="flex items-start gap-2 cursor-pointer text-sm">
                            <input
                              type="checkbox"
                              checked={!!otherItem}
                              onChange={(e) => {
                                if (e.target.checked) {
                                  // Khởi tạo mục "Khác" (symptomId = null, otherNote = "")
                                  const newVal = [...value];
                                  if (!newVal[block.startIndex].symptomId && newVal[block.startIndex].otherNote === null) {
                                    newVal[block.startIndex].otherNote = "";
                                  } else {
                                    newVal.splice(block.startIndex + 1, 0, { categoryId: block.categoryId, symptomId: null, otherNote: "" });
                                  }
                                  onChange(newVal);
                                } else {
                                  // Xoá mục "Khác"
                                  const newVal = [...value];
                                  const otherIdx = newVal.findIndex(
                                    (v, i) => i >= block.startIndex && v.categoryId === block.categoryId && v.symptomId === null && v.otherNote !== null
                                  );
                                  if (otherIdx !== -1) {
                                    newVal.splice(otherIdx, 1);
                                    const stillHasCategory = newVal.some((v) => v.categoryId === block.categoryId);
                                    if (!stillHasCategory) {
                                      newVal.splice(block.startIndex, 0, { categoryId: block.categoryId, symptomId: null, otherNote: null });
                                    }
                                  }
                                  onChange(newVal);
                                }
                              }}
                              className="mt-1 shrink-0 rounded border-primary text-primary focus:ring-primary"
                            />
                            <span className="leading-snug italic">Khác...</span>
                          </label>
                        )}
                      </div>
                      
                      {/* Chỉ hiện input khi đã tick vào DB Khác hoặc Fallback Khác */}
                      {showTextBox && (
                        <div className="pt-2 animate-in fade-in slide-in-from-top-2 duration-200">
                          <input
                            type="text"
                            value={currentOtherNote}
                            onChange={(e) => {
                              const note = e.target.value;
                              const newVal = [...value];
                              const idx = newVal.findIndex(
                                (v, i) => i >= block.startIndex && v.categoryId === block.categoryId && v.symptomId === activeOtherSymptomId
                              );
                              if (idx !== -1) {
                                newVal[idx].otherNote = note;
                              }
                              onChange(newVal);
                            }}
                            placeholder="Nhập mô tả triệu chứng khác..."
                            autoFocus
                            className="h-10 w-full rounded-lg border border-border bg-background px-3 text-sm outline-none focus-visible:ring-2 focus-visible:ring-ring"
                          />
                        </div>
                      )}
                    </div>
                  );
                })()}
              </div>
            )}
          </div>
        );
      })}

      <button
        type="button"
        onClick={handleAddCategoryBlock}
        className="flex items-center gap-2 text-sm font-medium text-primary hover:underline"
      >
        <PlusIcon className="h-4 w-4" />
        Thêm nhóm triệu chứng
      </button>
    </div>
  );
}
