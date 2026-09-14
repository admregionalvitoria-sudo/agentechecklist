import React from "react";
import { Check, X, AlertTriangle, Monitor, Keyboard, Mouse, Globe, Cpu } from "lucide-react";
import { motion, AnimatePresence } from "framer-motion";
import { cn } from "@/lib/utils";

export interface ChecklistQuestionState {
  id: string;
  title: string;
  category: string;
  iconName: "monitor" | "keyboard" | "mouse" | "globe" | "cpu";
  isOk: boolean | null; // true = SIM, false = NÃO, null = Não respondido
  problemDescription: string;
}

interface ChecklistItemCardProps {
  question: ChecklistQuestionState;
  onSelectOption: (id: string, isOk: boolean) => void;
  onDescriptionChange: (id: string, description: string) => void;
}

const iconMap = {
  monitor: Monitor,
  keyboard: Keyboard,
  mouse: Mouse,
  globe: Globe,
  cpu: Cpu,
};

export const ChecklistItemCard: React.FC<ChecklistItemCardProps> = ({
  question,
  onSelectOption,
  onDescriptionChange,
}) => {
  const IconComponent = iconMap[question.iconName] || Cpu;
  const isSimSelected = question.isOk === true;
  const isNaoSelected = question.isOk === false;

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: "easeOut" }}
      className={cn(
        "glass-card glass-card-hover rounded-3xl p-6 relative overflow-hidden border transition-all duration-300 shadow-liquid-card",
        isSimSelected && "border-[#0091D6]/70 bg-gradient-to-br from-sky-50/70 via-white/80 to-sky-100/40 shadow-glow-cyan",
        isNaoSelected && "border-[#EF5E31]/70 bg-gradient-to-br from-orange-50/70 via-white/80 to-orange-100/40 shadow-glow-orange",
        question.isOk === null && "border-slate-200/90 hover:border-senai-cyan/40"
      )}
    >
      {/* Top Status Accent Liquid Light Bar */}
      <div
        className={cn(
          "absolute top-0 left-0 right-0 h-1.5 transition-all duration-300",
          isSimSelected && "bg-gradient-to-r from-senai-cyan via-senai-blue to-senai-cyan shadow-[0_0_15px_#0091D6]",
          isNaoSelected && "bg-gradient-to-r from-senai-orange via-rose-500 to-senai-orange shadow-[0_0_15px_#EF5E31]",
          question.isOk === null && "bg-slate-200/70"
        )}
      />

      <div className="flex flex-col gap-5">
        {/* Card Header: Icon + Category + Title */}
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <div
              className={cn(
                "flex h-12 w-12 items-center justify-center rounded-2xl transition-all duration-300 shadow-md backdrop-blur-md",
                isSimSelected && "bg-gradient-to-br from-[#0091D6] to-[#1A4B9F] text-white shadow-glow-cyan scale-105",
                isNaoSelected && "bg-gradient-to-br from-[#EF5E31] to-rose-600 text-white shadow-glow-orange scale-105",
                question.isOk === null && "bg-senai-blue/10 text-senai-blue border border-senai-blue/20"
              )}
            >
              <IconComponent className="h-6 w-6 stroke-[2.2]" />
            </div>
            <div>
              <span className="text-xs font-montserrat font-bold uppercase tracking-wider text-senai-blue/80">
                {question.category}
              </span>
              <h3 className="text-base md:text-lg font-montserrat font-bold text-slate-900 tracking-tight leading-snug">
                {question.title}
              </h3>
            </div>
          </div>

          {/* Quick Response Badge Indicator */}
          {question.isOk !== null && (
            <span
              className={cn(
                "hidden sm:inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[11px] font-montserrat font-extrabold uppercase tracking-wider backdrop-blur-md shadow-sm border",
                isSimSelected && "bg-sky-100/90 text-senai-cyan border-sky-300/80",
                isNaoSelected && "bg-orange-100/90 text-senai-orange border-orange-300/80"
              )}
            >
              {isSimSelected ? (
                <>
                  <Check className="h-3.5 w-3.5 stroke-[3]" /> OPERACIONAL (OK)
                </>
              ) : (
                <>
                  <X className="h-3.5 w-3.5 stroke-[3]" /> COM DEFEITO
                </>
              )}
            </span>
          )}
        </div>

        {/* Custom Touch Liquid Glass SIM / NÃO Selection System */}
        <div className="grid grid-cols-2 gap-4 pt-2">
          {/* SIM BUTTON */}
          <motion.button
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.97 }}
            onClick={() => onSelectOption(question.id, true)}
            className={cn(
              "relative overflow-hidden flex items-center justify-center gap-3 py-3.5 px-6 rounded-2xl font-montserrat font-bold text-base md:text-lg cursor-pointer transition-all duration-300 select-none border",
              isSimSelected
                ? "bg-gradient-to-r from-[#0091D6] to-[#1A4B9F] text-white border-sky-300 shadow-glow-cyan scale-[1.02] ring-2 ring-[#0091D6]/40"
                : "bg-white/70 text-slate-700 border-slate-200/90 hover:border-sky-400 hover:bg-sky-50/60 hover:text-senai-blue backdrop-blur-md"
            )}
          >
            <div
              className={cn(
                "flex h-7 w-7 items-center justify-center rounded-xl transition-all duration-300",
                isSimSelected ? "bg-white/25 text-white" : "bg-sky-100 text-senai-cyan"
              )}
            >
              <Check className="h-4.5 w-4.5 stroke-[3]" />
            </div>
            <span>SIM</span>

            {/* Liquid shine highlight on select */}
            {isSimSelected && (
              <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent animate-liquid-shine" />
            )}
          </motion.button>

          {/* NÃO BUTTON */}
          <motion.button
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.97 }}
            onClick={() => onSelectOption(question.id, false)}
            className={cn(
              "relative overflow-hidden flex items-center justify-center gap-3 py-3.5 px-6 rounded-2xl font-montserrat font-bold text-base md:text-lg cursor-pointer transition-all duration-300 select-none border",
              isNaoSelected
                ? "bg-gradient-to-r from-[#EF5E31] to-rose-600 text-white border-orange-300 shadow-glow-orange scale-[1.02] ring-2 ring-[#EF5E31]/40"
                : "bg-white/70 text-slate-700 border-slate-200/90 hover:border-orange-400 hover:bg-orange-50/60 hover:text-senai-orange backdrop-blur-md"
            )}
          >
            <div
              className={cn(
                "flex h-7 w-7 items-center justify-center rounded-xl transition-all duration-300",
                isNaoSelected ? "bg-white/25 text-white" : "bg-orange-100 text-senai-orange"
              )}
            >
              <X className="h-4.5 w-4.5 stroke-[3]" />
            </div>
            <span>NÃO</span>

            {/* Liquid shine highlight on select */}
            {isNaoSelected && (
              <div className="absolute inset-0 bg-gradient-to-r from-transparent via-white/20 to-transparent animate-liquid-shine" />
            )}
          </motion.button>
        </div>

        {/* Dynamic Animated Problem Description Textarea (Appears when NÃO is active) */}
        <AnimatePresence>
          {isNaoSelected && (
            <motion.div
              initial={{ opacity: 0, height: 0, marginTop: 0 }}
              animate={{ opacity: 1, height: "auto", marginTop: 8 }}
              exit={{ opacity: 0, height: 0, marginTop: 0 }}
              transition={{ duration: 0.3, ease: "easeInOut" }}
              className="overflow-hidden"
            >
              <div className="rounded-2xl bg-orange-50/80 border border-orange-200/90 p-4 flex flex-col gap-2 backdrop-blur-md">
                <div className="flex items-center gap-2 text-xs font-montserrat font-bold text-senai-orange uppercase tracking-wide">
                  <AlertTriangle className="h-4 w-4 text-senai-orange animate-bounce" />
                  <span>Descreva o problema observado (Obrigatório):</span>
                </div>
                <textarea
                  value={question.problemDescription}
                  onChange={(e) => onDescriptionChange(question.id, e.target.value)}
                  placeholder="Exemplo: Tecla de espaço travada ou monitor piscando..."
                  rows={3}
                  className="w-full rounded-xl border border-orange-300/80 bg-white/95 p-3 text-sm text-slate-800 placeholder-slate-400 focus:border-senai-orange focus:outline-none focus:ring-2 focus:ring-[#EF5E31]/30 transition-all resize-none font-roboto shadow-inner"
                />
                {!question.problemDescription.trim() && (
                  <span className="text-[11px] font-inter font-semibold text-senai-orange italic">
                    * Preencha a descrição para poder concluir o checklist.
                  </span>
                )}
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </motion.div>
  );
};
