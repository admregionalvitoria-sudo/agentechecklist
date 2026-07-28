import React from "react";
import { ArrowRight, Check } from "lucide-react";
import { cn } from "@/lib/utils";

interface InteractiveHoverButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  text?: string;
  className?: string;
  icon?: React.ReactNode;
}

export const InteractiveHoverButton = React.forwardRef<
  HTMLButtonElement,
  InteractiveHoverButtonProps
>(({ text = "Concluir Checklist", className, disabled, icon, ...props }, ref) => {
  return (
    <button
      ref={ref}
      disabled={disabled}
      className={cn(
        "group relative cursor-pointer overflow-hidden rounded-2xl border border-senai-blue/20 p-2 text-center font-bold transition-all duration-300 shadow-glass focus:outline-none focus:ring-4 focus:ring-senai-blue/20",
        disabled
          ? "cursor-not-allowed bg-slate-100 text-slate-400 border-slate-200 shadow-none opacity-60"
          : "bg-gradient-to-r from-[#164194] to-[#2563EB] text-white hover:border-senai-cyan/50 hover:shadow-glow-senai active:scale-[0.98]",
        className
      )}
      {...props}
    >
      <div className="flex items-center justify-center gap-3 px-6 py-3">
        <span className="inline-block font-sans text-base md:text-lg tracking-wide transition-all duration-300 group-hover:translate-x-1">
          {text}
        </span>
        <div className="relative flex h-8 w-8 items-center justify-center rounded-xl bg-white/20 backdrop-blur-md transition-all duration-300 group-hover:scale-110 group-hover:bg-white group-hover:text-senai-blue">
          {icon || (
            <ArrowRight className="h-5 w-5 transition-transform duration-300 group-hover:translate-x-0.5" />
          )}
        </div>
      </div>
      
      {/* Liquid Glass Highlight Sweep Effect */}
      {!disabled && (
        <div className="absolute -inset-full top-0 block h-full w-1/2 -skew-x-12 bg-gradient-to-r from-transparent via-white/20 to-transparent group-hover:animate-[shine_1.5s_ease-in-out_infinite]" />
      )}
    </button>
  );
});

InteractiveHoverButton.displayName = "InteractiveHoverButton";
