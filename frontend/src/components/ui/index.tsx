import { cn } from "@/lib/utils";
import { cva, type VariantProps } from "class-variance-authority";
import { forwardRef, ButtonHTMLAttributes } from "react";

const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg text-sm font-medium transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 disabled:pointer-events-none disabled:opacity-50",
  {
    variants: {
      variant: {
        default: "bg-indigo-600 text-white hover:bg-indigo-500 shadow-lg shadow-indigo-500/20",
        secondary: "bg-zinc-800 text-zinc-100 hover:bg-zinc-700 border border-zinc-700",
        ghost: "hover:bg-zinc-800 text-zinc-300",
        destructive: "bg-red-600 text-white hover:bg-red-500",
        outline: "border border-zinc-700 bg-transparent hover:bg-zinc-800 text-zinc-200",
      },
      size: {
        default: "h-10 px-4 py-2",
        sm: "h-8 px-3 text-xs",
        lg: "h-12 px-6 text-base",
        icon: "h-10 w-10",
      },
    },
    defaultVariants: { variant: "default", size: "default" },
  }
);

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement>, VariantProps<typeof buttonVariants> {}

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, ...props }, ref) => (
    <button className={cn(buttonVariants({ variant, size, className }))} ref={ref} {...props} />
  )
);
Button.displayName = "Button";

export function Card({ className, children, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return (
    <div className={cn("rounded-xl border border-zinc-800 bg-zinc-900/50 backdrop-blur-sm", className)} {...props}>
      {children}
    </div>
  );
}

export function CardHeader({ className, children, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("flex flex-col gap-1.5 p-6 pb-4", className)} {...props}>{children}</div>;
}

export function CardTitle({ className, children, ...props }: React.HTMLAttributes<HTMLHeadingElement>) {
  return <h3 className={cn("text-lg font-semibold text-zinc-100", className)} {...props}>{children}</h3>;
}

export function CardDescription({ className, children, ...props }: React.HTMLAttributes<HTMLParagraphElement>) {
  return <p className={cn("text-sm text-zinc-400", className)} {...props}>{children}</p>;
}

export function CardContent({ className, children, ...props }: React.HTMLAttributes<HTMLDivElement>) {
  return <div className={cn("p-6 pt-0", className)} {...props}>{children}</div>;
}

const badgeVariants = cva("inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium", {
  variants: {
    variant: {
      default: "bg-indigo-500/10 text-indigo-400 border border-indigo-500/20",
      success: "bg-emerald-500/10 text-emerald-400 border border-emerald-500/20",
      warning: "bg-amber-500/10 text-amber-400 border border-amber-500/20",
      danger: "bg-red-500/10 text-red-400 border border-red-500/20",
      neutral: "bg-zinc-500/10 text-zinc-400 border border-zinc-500/20",
    },
  },
  defaultVariants: { variant: "default" },
});

export function Badge({ className, variant, ...props }: React.HTMLAttributes<HTMLSpanElement> & VariantProps<typeof badgeVariants>) {
  return <span className={cn(badgeVariants({ variant }), className)} {...props} />;
}

export function Input({ className, ...props }: React.InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cn(
        "flex h-10 w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm text-zinc-100 placeholder:text-zinc-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent transition-all",
        className
      )}
      {...props}
    />
  );
}

export function Textarea({ className, ...props }: React.TextareaHTMLAttributes<HTMLTextAreaElement>) {
  return (
    <textarea
      className={cn(
        "flex min-h-[80px] w-full rounded-lg border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm text-zinc-100 placeholder:text-zinc-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent transition-all",
        className
      )}
      {...props}
    />
  );
}

export function StatCard({ title, value, subtitle, icon: Icon }: { title: string; value: string | number; subtitle?: string; icon?: React.ComponentType<{ className?: string }> }) {
  return (
    <Card className="hover:border-zinc-700 transition-colors">
      <CardContent className="p-6">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-zinc-400">{title}</p>
            <p className="text-3xl font-bold text-zinc-100 mt-1">{value}</p>
            {subtitle && <p className="text-xs text-zinc-500 mt-1">{subtitle}</p>}
          </div>
          {Icon && (
            <div className="h-12 w-12 rounded-xl bg-indigo-500/10 flex items-center justify-center">
              <Icon className="h-6 w-6 text-indigo-400" />
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
