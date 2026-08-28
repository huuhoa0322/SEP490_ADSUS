import * as React from "react"
import { format, parse, isValid } from "date-fns"
import { Calendar as CalendarIcon } from "lucide-react"

import { cn } from "@/lib/utils"
import { Input } from "@/components/ui/input"
import { Button } from "@/components/ui/button"
import { Calendar } from "@/components/ui/calendar"
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover"

interface DatePickerProps {
  value?: string | Date | null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onChange: (value: any) => void;
  placeholder?: string;
  disabled?: boolean;
  maxDate?: Date;
  minDate?: Date;
  className?: string;
}

export function DatePicker({ value, onChange, placeholder = "dd/mm/yyyy", disabled = false, maxDate, minDate, className }: DatePickerProps) {
  const parsedDate = value ? new Date(value) : undefined;
  const [isOpen, setIsOpen] = React.useState(false);
  const [inputValue, setInputValue] = React.useState("");

  // Sync input value when external value changes
  React.useEffect(() => {
    const pDate = value ? new Date(value) : undefined;
    if (pDate && !isNaN(pDate.getTime())) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setInputValue(format(pDate, "dd/MM/yyyy"));
    } else {
       
      setInputValue("");
    }
     
  }, [value]);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    let val = e.target.value;
    
    // Auto format dd/MM/yyyy
    if (val.length > inputValue.length) {
      val = val.replace(/[^\d/]/g, '');
      if (val.length === 2 && !val.includes('/')) {
        val += '/';
      } else if (val.length === 5 && val.split('/').length === 2) {
        val += '/';
      }
    }

    if (val.length > 10) {
      val = val.slice(0, 10);
    }

    setInputValue(val);

    if (val.length === 10) {
      // Try parsing the manually typed full date (e.g., 27/08/2026)
      const parsed = parse(val, "dd/MM/yyyy", new Date());
      if (isValid(parsed)) {
        // Validate min/max constraints
        if (maxDate && parsed > maxDate) return;
        if (minDate && parsed < minDate) return;

        if (value instanceof Date || value === undefined) {
          onChange(parsed);
        } else {
          onChange(format(parsed, "yyyy-MM-dd"));
        }
      }
    } else if (val === "") {
      onChange(value instanceof Date ? undefined : "");
    }
  };

  return (
    <div className={cn("relative", className)}>
      <Input
        type="text"
        placeholder={placeholder}
        value={inputValue}
        onChange={handleInputChange}
        disabled={disabled}
        className="pr-10"
      />
      <Popover open={isOpen} onOpenChange={setIsOpen}>
        <PopoverTrigger asChild>
          <Button
            variant="ghost"
            className="absolute right-0 top-0 h-full px-3 py-2 hover:bg-transparent text-muted-foreground"
            disabled={disabled}
            type="button"
          >
            <CalendarIcon className="h-4 w-4" />
          </Button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="end">
          <Calendar
            mode="single"
            selected={parsedDate}
            captionLayout="dropdown"
            startMonth={new Date(1900, 0)}
            endMonth={new Date(2100, 11)}
            disabled={(date) => {
              if (maxDate && date > maxDate) return true;
              if (minDate && date < minDate) return true;
              return false;
            }}
            onSelect={(date) => {
              if (date) {
                 if (value instanceof Date || value === undefined) {
                   onChange(date);
                 } else {
                   onChange(format(date, "yyyy-MM-dd"));
                 }
                 setInputValue(format(date, "dd/MM/yyyy"));
              } else {
                 onChange(value instanceof Date ? undefined : "");
                 setInputValue("");
              }
              setIsOpen(false);
            }}
          />
        </PopoverContent>
      </Popover>
    </div>
  )
}
