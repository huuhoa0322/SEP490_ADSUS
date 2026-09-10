import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"
import { Slot } from "radix-ui"

import { cn } from "@/lib/utils"

const badgeVariants = cva(
  "group/badge inline-flex h-5 w-fit shrink-0 items-center justify-center gap-1 overflow-hidden rounded-4xl border border-transparent px-2 py-0.5 text-xs font-medium whitespace-nowrap transition-all focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 has-data-[icon=inline-end]:pr-1.5 has-data-[icon=inline-start]:pl-1.5 aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 [&>svg]:pointer-events-none [&>svg]:size-3!",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground [a]:hover:bg-primary/80",
        secondary:
          "bg-secondary text-secondary-foreground [a]:hover:bg-secondary/80",
        destructive:
          "bg-destructive/10 text-destructive focus-visible:ring-destructive/20 dark:bg-destructive/20 dark:focus-visible:ring-destructive/40 [a]:hover:bg-destructive/20",
        outline:
          "border-border text-foreground [a]:hover:bg-muted [a]:hover:text-muted-foreground",
        ghost:
          "hover:bg-muted hover:text-muted-foreground dark:hover:bg-muted/50",
        link: "text-primary underline-offset-4 hover:underline",
        "soft-primary":
          "bg-[#ECEDF7] text-[#2E37A4] border-[#2E37A4]/20 [a]:hover:bg-[#ECEDF7]/80",
        "soft-success":
          "bg-[#E4F5F3] text-[#1E9E6B] border-[#1E9E6B]/20 [a]:hover:bg-[#E4F5F3]/80",
        "soft-warning":
          "bg-[#FEFBF5] text-[#E2B93B] border-[#E2B93B]/20 [a]:hover:bg-[#FEFBF5]/80",
        "soft-danger":
          "bg-[#FEF4F4] text-[#EF1E1E] border-[#EF1E1E]/20 [a]:hover:bg-[#FEF4F4]/80",
        "soft-teal":
          "bg-[#E4F5F3] text-[#1E9E6B] border-[#1E9E6B]/20 [a]:hover:bg-[#E4F5F3]/80",
        softPrimary:
          "bg-[#ECEDF7] text-[#2E37A4] border-[#2E37A4]/20 [a]:hover:bg-[#ECEDF7]/80",
        softSuccess:
          "bg-[#E4F5F3] text-[#1E9E6B] border-[#1E9E6B]/20 [a]:hover:bg-[#E4F5F3]/80",
        softWarning:
          "bg-[#FEFBF5] text-[#E2B93B] border-[#E2B93B]/20 [a]:hover:bg-[#FEFBF5]/80",
        softDanger:
          "bg-[#FEF4F4] text-[#EF1E1E] border-[#EF1E1E]/20 [a]:hover:bg-[#FEF4F4]/80",
        softTeal:
          "bg-[#E4F5F3] text-[#1E9E6B] border-[#1E9E6B]/20 [a]:hover:bg-[#E4F5F3]/80",
      },
    },
    defaultVariants: {
      variant: "default",
    },
  }
)

function Badge({
  className,
  variant = "default",
  asChild = false,
  ...props
}: React.ComponentProps<"span"> &
  VariantProps<typeof badgeVariants> & { asChild?: boolean }) {
  const Comp = asChild ? Slot.Root : "span"

  return (
    <Comp
      data-slot="badge"
      data-variant={variant}
      className={cn(badgeVariants({ variant }), className)}
      {...props}
    />
  )
}

export { Badge, badgeVariants }
