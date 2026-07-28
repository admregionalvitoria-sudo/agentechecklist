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
      initial={{ opacity: 0, y: 15 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, ease: "easeOut" }}
      className={cn(
        "glass-card glass-card-hover rounded-3xl p-6 relative overflow-hidden border transition-all duration-300",
        isSimSelected && "border-emerald-300/80 bg-emerald-50/30",
        isNaoSelected && "border-rose-300/80 bg-rose-50/30",
        question.isOk === null && "border-slate-200/90"
      )}
    >
      {/* Top Status Accent Indicator Bar */}
      <div
        className={cn(
          "absolute top-0 left-0 right-0 h-1.5 transition-all duration-300",
          isSimSelected && "bg-emerald-500 shadow-[0_0_12px_#10B981]",
          isNaoSelected && "bg-rose-500 shadow-[0_0_12px_#EF4444]",
          question.isOk === null && "bg-slate-200"
        )}
      />

      <div className="flex flex-col gap-5">
        {/* Card Header: Icon + Title */}
        <div className="flex items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <div
              className={cn(
                "flex h-12 w-12 items-center justify-center rounded-2xl transition-all duration-300 shadow-sm",
                isSimSelected && "bg-emerald-500 text-white shadow-glow-sim",
                isNaoSelected && "bg-rose-500 text-white shadow-glow-nao",
                question.isOk === null && "bg-senai-blue/10 text-senai-blue"
              )}
            >
              <IconComponent className="h-6 w-6 stroke-[2.2]" />
            </div>
            <div>
              <span className="text-xs font-bold uppercase tracking-wider text-senai-blue/70">
                {question.category}
              </span>
              <h3 className="text-lg md:text-xl font-bold text-slate-800 tracking-tight">
                {question.title}
              </h3>
            </div>
          </div>

          {/* Quick Response Badge Indicator */}
          {question.isOk !== null && (
            <span
              className={cn(
                "hidden sm:inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-black uppercase tracking-wider",
                isSimSelected && "bg-emerald-100 text-emerald-800 border border-emerald-300",
                isNaoSelected && "bg-rose-100 text-rose-800 border border-rose-300"
              )}
            >
              {isSimSelected ? (
                <>
                  <Check className="h-3.5 w-3.5" /> OK
                </>
              ) : (
                <>
                  <X className="h-3.5 w-3.5" /> COM DEFEITO
                </>
              )}
            </span>
          )}
        </div>

        {/* Custom Touch SIM / NÃO Selection System */}
        <div className="grid grid-cols-2 gap-4 pt-2">
          {/* SIM BUTTON */}
          <motion.button
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.97 }}
            onClick={() => onSelectOption(question.id, true)}
            className={cn(
              "relative flex items-center justify-center gap-3 py-3.5 px-6 rounded-2xl font-bold text-base md:text-lg cursor-pointer transition-all duration-250 select-none border",
              isSimSelected
                ? "bg-emerald-500 text-white border-emerald-600 shadow-glow-sim scale-[1.02] ring-2 ring-emerald-400/40"
                : "bg-white/80 text-slate-700 border-slate-200 hover:border-emerald-400 hover:bg-emerald-50/50 hover:text-emerald-700"
            )}
          >
            <div
              className={cn(
                "flex h-7 w-7 items-center justify-center rounded-xl transition-all duration-250",
                isSimSelected ? "bg-white/20 text-white" : "bg-emerald-100 text-emerald-600"
              )}
            >
              <Check className="h-4.5 w-4.5 stroke-[3]" />
            </div>
            <span>SIM</span>
          </motion.button>

          {/* NÃO BUTTON */}
          <motion.button
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.97 }}
            onClick={() => onSelectOption(question.id, false)}
            className={cn(
              "relative flex items-center justify-center gap-3 py-3.5 px-6 rounded-2xl font-bold text-base md:text-lg cursor-pointer transition-all duration-250 select-none border",
              isNaoSelected
                ? "bg-rose-500 text-white border-rose-600 shadow-glow-nao scale-[1.02] ring-2 ring-rose-400/40"
                : "bg-white/80 text-slate-700 border-slate-200 hover:border-rose-400 hover:bg-rose-50/50 hover:text-rose-700"
            )}
          >
            <div
              className={cn(
                "flex h-7 w-7 items-center justify-center rounded-xl transition-all duration-250",
                isNaoSelected ? "bg-white/20 text-white" : "bg-rose-100 text-rose-600"
              )}
            >
              <X className="h-4.5 w-4.5 stroke-[3]" />
            </div>
            <span>NÃO</span>
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
              <div className="rounded-2xl bg-rose-50/80 border border-rose-200 p-4 flex flex-col gap-2">
                <div className="flex items-center gap-2 text-xs font-bold text-rose-700 uppercase tracking-wide">
                  <AlertTriangle className="h-4 w-4 text-rose-600" />
                  <span>Descreva detalhadamente o problema (Obrigatório):</span>
                </div>
                <textarea
                  value={question.problemDescription}
                  onChange={(e) => onDescriptionChange(question.id, e.target.value)}
                  placeholder="Exemplo: Tecla de espaço não responde ou tela piscando..."
                  rows={3}
                  className="w-full rounded-xl border border-rose-300 bg-white p-3 text-sm text-slate-800 placeholder-slate-400 focus:border-rose-500 focus:outline-none focus:ring-2 focus:ring-rose-400/30 transition-all resize-none font-sans"
                />
                {!question.problemDescription.trim() && (
                  <span className="text-[11px] font-semibold text-rose-600 italic">
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
