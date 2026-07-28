import React from "react";
import { User, Laptop, ShieldCheck, CheckCircle2 } from "lucide-react";

interface ChecklistHeaderProps {
  userName: string;
  machineName: string;
  totalQuestions: number;
  answeredQuestions: number;
}

export const ChecklistHeader: React.FC<ChecklistHeaderProps> = ({
  userName,
  machineName,
  totalQuestions,
  answeredQuestions,
}) => {
  const progressPercent = Math.round((answeredQuestions / totalQuestions) * 100);

  return (
    <header className="glass-panel sticky top-0 z-30 w-full rounded-3xl p-5 md:p-6 mb-6 shadow-glass">
      <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
        {/* Top Left: Logo SENAI + Institutional Header */}
        <div className="flex items-center gap-5">
          <div className="relative group flex items-center justify-center p-2 bg-white/90 rounded-2xl shadow-sm border border-slate-100">
            <img
              src="/logo/logo-senai.png"
              onError={(e) => {
                (e.target as HTMLImageElement).src = "/logo/logo-senai.svg";
              }}
              alt="Logo SENAI"
              className="h-10 md:h-12 w-auto object-contain transition-transform duration-300 group-hover:scale-105"
            />
          </div>

          <div className="flex flex-col">
            <div className="flex items-center gap-2">
              <span className="h-2.5 w-2.5 rounded-full bg-senai-orange animate-pulse" />
              <span className="text-xs font-black uppercase tracking-widest text-senai-blue">
                SISTEMA DE VERIFICAÇÃO INSTITUCIONAL
              </span>
            </div>
            <h1 className="text-xl md:text-2xl font-black text-slate-900 tracking-tight">
              Checklist de Equipamentos
            </h1>
          </div>
        </div>

        {/* Top Right: Badges (User & Computer) + Progress Indicator */}
        <div className="flex flex-wrap items-center gap-3 w-full md:w-auto justify-start md:justify-end">
          {/* User Badge */}
          <div className="flex items-center gap-2 px-3.5 py-2 rounded-2xl bg-white/80 border border-slate-200/80 text-xs font-bold shadow-sm">
            <div className="flex h-6 w-6 items-center justify-center rounded-lg bg-senai-blue/10 text-senai-blue">
              <User className="h-3.5 w-3.5" />
            </div>
            <div className="flex flex-col">
              <span className="text-[10px] text-slate-400 font-semibold uppercase">Usuário</span>
              <span className="text-slate-800 font-bold">{userName}</span>
            </div>
          </div>

          {/* Machine Badge */}
          <div className="flex items-center gap-2 px-3.5 py-2 rounded-2xl bg-white/80 border border-slate-200/80 text-xs font-bold shadow-sm">
            <div className="flex h-6 w-6 items-center justify-center rounded-lg bg-senai-cyan/15 text-senai-blue">
              <Laptop className="h-3.5 w-3.5" />
            </div>
            <div className="flex flex-col">
              <span className="text-[10px] text-slate-400 font-semibold uppercase">Computador</span>
              <span className="text-slate-800 font-bold">{machineName}</span>
            </div>
          </div>

          {/* Progress Widget */}
          <div className="flex items-center gap-3 px-4 py-2 rounded-2xl bg-senai-blue text-white shadow-md">
            <CheckCircle2 className="h-5 w-5 text-senai-cyan" />
            <div className="flex flex-col">
              <span className="text-[10px] text-blue-200 font-bold uppercase">Progresso</span>
              <span className="text-xs font-black">
                {answeredQuestions} de {totalQuestions} ({progressPercent}%)
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Progress Bar Track */}
      <div className="w-full bg-slate-200/70 h-2 rounded-full mt-4 overflow-hidden p-0.5">
        <div
          className="bg-gradient-to-r from-senai-blue via-senai-cyan to-senai-orange h-full rounded-full transition-all duration-500 ease-out shadow-sm"
          style={{ width: `${progressPercent}%` }}
        />
      </div>
    </header>
  );
};
