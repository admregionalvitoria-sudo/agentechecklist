import React from "react";
import { User, Laptop, CheckCircle2, ShieldCheck } from "lucide-react";

interface ChecklistHeaderProps {
  userName: string;
  machineName: string;
  totalQuestions: number;
  answeredQuestions: number;
  locationName?: string;
  onLocationChange?: (location: string) => void;
}

export const ChecklistHeader: React.FC<ChecklistHeaderProps> = ({
  userName,
  machineName,
  totalQuestions,
  answeredQuestions,
  locationName = "PORTO",
  onLocationChange,
}) => {
  const progressPercent = Math.round((answeredQuestions / totalQuestions) * 100);

  return (
    <header className="glass-panel sticky top-4 z-30 w-full rounded-3xl p-5 md:p-6 mb-6 shadow-glass border border-white/80 transition-all duration-300">
      <div className="flex flex-col md:flex-row items-start md:items-center justify-between gap-6">
        {/* Top Left: Logo SENAI + Institutional Header */}
        <div className="flex items-center gap-5">
          <div className="relative group flex items-center justify-center p-2.5 bg-white/90 backdrop-blur-md rounded-2xl shadow-sm border border-slate-100">
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
              <span className="h-2.5 w-2.5 rounded-full bg-senai-orange animate-pulse shadow-[0_0_10px_#EF5E31]" />
              <span className="text-xs font-montserrat font-extrabold uppercase tracking-widest text-senai-blue">
                SISTEMA DE VERIFICAÇÃO INSTITUCIONAL
              </span>
            </div>
            <h1 className="text-xl md:text-2xl font-montserrat font-black text-slate-900 tracking-tight">
              Checklist de Equipamentos
            </h1>
          </div>
        </div>

        {/* Top Right: Badges (User & Computer & Location) + Progress Indicator */}
        <div className="flex flex-wrap items-center gap-3 w-full md:w-auto justify-start md:justify-end">
          {/* Location Badge / Selector */}
          <div className="flex items-center gap-2.5 px-4 py-2.5 rounded-2xl liquid-glass-badge border border-slate-200/80 text-xs font-inter font-bold shadow-sm">
            <div className="flex h-7 w-7 items-center justify-center rounded-xl bg-orange-500/15 text-orange-600">
              <ShieldCheck className="h-4 w-4" />
            </div>
            <div className="flex flex-col">
              <span className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Local / Planilha</span>
              {onLocationChange ? (
                <select
                  value={locationName}
                  onChange={(e) => onLocationChange(e.target.value)}
                  className="bg-transparent text-slate-800 font-bold text-xs focus:outline-none cursor-pointer pr-1"
                >
                  <option value="PORTO">PORTO</option>
                  <option value="NOTEBOOK PORTO">NOTEBOOK PORTO</option>
                  <option value="BEIRA MAR">BEIRA MAR</option>
                  <option value="NOTEBOOK BEIRA MAR">NOTEBOOK BEIRA MAR</option>
                </select>
              ) : (
                <span className="text-slate-800 font-bold text-xs">{locationName}</span>
              )}
            </div>
          </div>

          {/* User Badge */}
          <div className="flex items-center gap-2.5 px-4 py-2.5 rounded-2xl liquid-glass-badge border border-slate-200/80 text-xs font-inter font-bold shadow-sm">
            <div className="flex h-7 w-7 items-center justify-center rounded-xl bg-senai-blue/15 text-senai-blue">
              <User className="h-4 w-4" />
            </div>
            <div className="flex flex-col">
              <span className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Usuário</span>
              <span className="text-slate-800 font-bold text-xs">{userName}</span>
            </div>
          </div>

          {/* Machine Badge */}
          <div className="flex items-center gap-2.5 px-4 py-2.5 rounded-2xl liquid-glass-badge border border-slate-200/80 text-xs font-inter font-bold shadow-sm">
            <div className="flex h-7 w-7 items-center justify-center rounded-xl bg-senai-cyan/20 text-senai-cyan">
              <Laptop className="h-4 w-4" />
            </div>
            <div className="flex flex-col">
              <span className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Computador</span>
              <span className="text-slate-800 font-bold text-xs">{machineName}</span>
            </div>
          </div>

          {/* Progress Widget */}
          <div className="flex items-center gap-3 px-4 py-2.5 rounded-2xl bg-gradient-to-r from-senai-blue to-senai-cyan text-white shadow-glow-blue">
            <CheckCircle2 className="h-5 w-5 text-white animate-pulse" />
            <div className="flex flex-col">
              <span className="text-[10px] text-blue-100 font-bold uppercase tracking-wider">Progresso</span>
              <span className="text-xs font-montserrat font-black">
                {answeredQuestions} de {totalQuestions} ({progressPercent}%)
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Progress Bar Track with Liquid Gradient */}
      <div className="w-full bg-slate-200/80 h-2.5 rounded-full mt-4 overflow-hidden p-0.5 border border-white/60 shadow-inner">
        <div
          className="bg-gradient-to-r from-senai-blue via-senai-cyan to-senai-orange h-full rounded-full transition-all duration-500 ease-out shadow-[0_0_12px_rgba(0,145,214,0.6)]"
          style={{ width: `${progressPercent}%` }}
        />
      </div>
    </header>
  );
};
