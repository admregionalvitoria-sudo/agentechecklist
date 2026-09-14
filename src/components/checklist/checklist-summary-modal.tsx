import React from "react";
import { motion, AnimatePresence } from "framer-motion";
import { CheckCircle, FileText, Download, X } from "lucide-react";
import { InteractiveHoverButton } from "@/components/ui/interactive-hover-button";

interface ChecklistSummaryModalProps {
  isOpen: boolean;
  onClose: () => void;
  machineName: string;
  userName: string;
  logContent: string;
}

export const ChecklistSummaryModal: React.FC<ChecklistSummaryModalProps> = ({
  isOpen,
  onClose,
  machineName,
  userName,
  logContent,
}) => {
  if (!isOpen) return null;

  const handleDownloadLog = () => {
    const element = document.createElement("a");
    const file = new Blob([logContent], { type: "text/plain;charset=utf-8" });
    element.href = URL.createObjectURL(file);
    element.download = `${machineName}.txt`;
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
  };

  return (
    <AnimatePresence>
      <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/50 backdrop-blur-xl">
        <motion.div
          initial={{ opacity: 0, scale: 0.88, y: 25 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          exit={{ opacity: 0, scale: 0.88, y: 25 }}
          transition={{ duration: 0.35, ease: "easeOut" }}
          className="glass-card max-w-lg w-full rounded-3xl p-8 shadow-2xl border border-white/90 relative overflow-hidden backdrop-blur-2xl"
        >
          {/* Top Liquid Light Accent Bar */}
          <div className="absolute top-0 left-0 right-0 h-2 bg-gradient-to-r from-senai-blue via-senai-cyan to-senai-orange shadow-[0_0_20px_#0091D6]" />

          {/* Header Icon & Text */}
          <div className="flex flex-col items-center text-center gap-3 mb-6 pt-2">
            <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br from-senai-cyan to-senai-blue text-white shadow-glow-cyan">
              <CheckCircle className="h-10 w-10 stroke-[2.5]" />
            </div>
            <span className="text-xs font-montserrat font-extrabold uppercase tracking-widest text-senai-orange">
              REGISTRO GRAVADO COM SUCESSO
            </span>
            <h2 className="text-2xl font-montserrat font-black text-slate-900 tracking-tight">
              Checklist Concluído!
            </h2>
            <p className="text-sm font-inter text-slate-600 leading-relaxed">
              O relatório dos equipamentos foi registrado para o usuário{" "}
              <strong className="text-senai-blue font-bold">{userName}</strong> na máquina{" "}
              <strong className="text-senai-blue font-bold">{machineName}</strong>.
            </p>
          </div>

          {/* Log Preview Box */}
          <div className="mb-6 rounded-2xl bg-slate-900/95 p-4 font-mono text-xs text-slate-200 border border-slate-700/80 shadow-inner relative max-h-48 overflow-y-auto backdrop-blur-md">
            <div className="flex items-center justify-between pb-2 border-b border-slate-700/80 mb-2 text-slate-400 font-sans text-[11px]">
              <span className="flex items-center gap-1.5 font-montserrat font-bold text-senai-cyan">
                <FileText className="h-3.5 w-3.5" /> {machineName}.txt
              </span>
              <span className="text-[10px] font-semibold tracking-wider text-slate-400">UTF-8</span>
            </div>
            <pre className="whitespace-pre-wrap leading-relaxed font-mono">{logContent}</pre>
          </div>

          {/* Action Buttons */}
          <div className="flex flex-col sm:flex-row items-center gap-3">
            <button
              onClick={handleDownloadLog}
              className="w-full sm:w-auto flex-1 flex items-center justify-center gap-2 py-3.5 px-4 rounded-2xl border border-slate-300/80 bg-white/90 text-slate-800 font-montserrat font-bold text-sm hover:bg-slate-50 hover:border-senai-blue hover:text-senai-blue transition-all cursor-pointer shadow-sm"
            >
              <Download className="h-4 w-4 text-senai-blue" />
              Baixar Cópia (.txt)
            </button>
            <InteractiveHoverButton
              text="Finalizar Sessão"
              onClick={onClose}
              className="w-full sm:w-auto flex-1"
            />
          </div>
        </motion.div>
      </div>
    </AnimatePresence>
  );
};
