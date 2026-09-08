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
  id?: string;
  value?: string | Date | null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onChange: (value: any) => void;
  placeholder?: string;
  disabled?: boolean;
  maxDate?: Date;
  minDate?: Date;
  className?: string;
}

export function DatePicker({ id, value, onChange, placeholder = "dd/mm/yyyy", disabled = false, maxDate, minDate, className }: DatePickerProps) {
  const parsedDate = value ? new Date(value) : undefined;
  const [isOpen, setIsOpen] = React.useState(false);
  const [inputValue, setInputValue] = React.useState("");

  // Sync input value when external value changes
  React.useEffect(() => {
    const pDate = value ? new Date(value) : undefined;
    if (pDate && !Number.isNaN(pDate.getTime())) {
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
    // `className` styles the visible input surface, not this positioning wrapper — a
    // caller passing border/rounded/padding here (e.g. to make a pill-shaped date field)
    // used to land on the wrapper AND stack on top of Input's own border+radius+padding,
    // rendering as a smaller boxed input nested inside a taller pill (see dashboard's
    // date-range filter). Forwarding it to Input instead means there's exactly one
    // visible box, matching every other input in the app.
    <div className="relative">
      <Input
        id={id}
        type="text"
        placeholder={placeholder}
        value={inputValue}
        onChange={handleInputChange}
        disabled={disabled}
        // pr-10 last so it always wins the right-padding slot the calendar button sits
        // in, even if a caller's className sets px-* (which would otherwise clobber it).
        className={cn(className, "pr-10")}
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
