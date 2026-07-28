import React from "react";
import { InteractiveHoverButton } from "@/components/ui/interactive-hover-button";

export function Demo() {
  return (
    <div className="p-8 flex flex-col items-center justify-center gap-4 bg-slate-50 min-h-screen">
      <h1 className="text-2xl font-bold text-senai-blue">SENAI Component Demo</h1>
      <InteractiveHoverButton 
        text="Concluir Checklist" 
        onClick={() => alert("Checklist concluído com sucesso!")} 
      />
      <InteractiveHoverButton 
        text="Botão Desabilitado" 
        disabled={true} 
      />
    </div>
  );
}

export default Demo;
