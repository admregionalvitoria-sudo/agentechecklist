import React, { useState } from "react";
import { ChecklistHeader } from "@/components/checklist/checklist-header";
import { ChecklistItemCard, ChecklistQuestionState } from "@/components/checklist/checklist-item-card";
import { ChecklistSummaryModal } from "@/components/checklist/checklist-summary-modal";
import { InteractiveHoverButton } from "@/components/ui/interactive-hover-button";
import { CheckCircle2, ShieldAlert, Sparkles, Building2 } from "lucide-react";

export function App() {
  // Mock logged-in user & machine details (simulates Environment.UserName / MachineName)
  const [userName] = useState("aluno.senai");
  const [machineName] = useState("LAB-INFORMATICA-01");

  // Checklist Questions Data
  const [questions, setQuestions] = useState<ChecklistQuestionState[]>([
    {
      id: "tela",
      title: "Tela / Monitor com exibição perfeita?",
      category: "Display & Imagem",
      iconName: "monitor",
      isOk: null,
      problemDescription: "",
    },
    {
      id: "teclado",
      title: "Teclado completo e com todas as teclas funcionando?",
      category: "Periféricos de Entrada",
      iconName: "keyboard",
      isOk: null,
      problemDescription: "",
    },
    {
      id: "mouse",
      title: "Mouse óptico com cliques e scroll operacionais?",
      category: "Periféricos de Entrada",
      iconName: "mouse",
      isOk: null,
      problemDescription: "",
    },
    {
      id: "internet",
      title: "Conexão com a Internet / Rede SENAI ativa?",
      category: "Conectividade",
      iconName: "globe",
      isOk: null,
      problemDescription: "",
    },
    {
      id: "computador",
      title: "Gabinete / Computador liga sem ruídos ou lentidão?",
      category: "Hardware Principal",
      iconName: "cpu",
      isOk: null,
      problemDescription: "",
    },
  ]);

  const [isModalOpen, setIsModalOpen] = useState(false);
  const [generatedLog, setGeneratedLog] = useState("");

  // Select Option Handler (SIM / NÃO)
  const handleSelectOption = (id: string, isOk: boolean) => {
    setQuestions((prev) =>
      prev.map((q) => (q.id === id ? { ...q, isOk } : q))
    );
  };

  // Description Change Handler
  const handleDescriptionChange = (id: string, problemDescription: string) => {
    setQuestions((prev) =>
      prev.map((q) => (q.id === id ? { ...q, problemDescription } : q))
    );
  };

  // Validation: All items must be answered, and if false, description must not be empty
  const answeredCount = questions.filter((q) => q.isOk !== null).length;
  const isFormValid =
    questions.length > 0 &&
    questions.every((q) => {
      if (q.isOk === null) return false;
      if (q.isOk === true) return true;
      return q.problemDescription.trim().length > 0;
    });

  // Submit Handler
  const handleSubmit = () => {
    if (!isFormValid) return;

    const now = new Date();
    const formattedDate = `${String(now.getDate()).padStart(2, "0")}/${String(
      now.getMonth() + 1
    ).padStart(2, "0")}/${now.getFullYear()} ${String(
      now.getHours()
    ).padStart(2, "0")}:${String(now.getMinutes()).padStart(2, "0")}:${String(
      now.getSeconds()
    ).padStart(2, "0")}`;

    let log = "=========================================\n";
    log += `Computador: ${machineName}\n`;
    log += `Usuário: ${userName}\n`;
    log += `Data/Hora: ${formattedDate}\n`;
    log += "-----------------------------------------\n";

    questions.forEach((q) => {
      const name = q.id.toUpperCase();
      if (q.isOk === true) {
        log += `${name}: OK\n`;
      } else {
        const desc = q.problemDescription.trim() || "Sem descrição";
        log += `${name}: NÃO OK - ${desc}\n`;
      }
    });

    log += "=========================================\n";

    setGeneratedLog(log);
    setIsModalOpen(true);
  };

  return (
    <div className="relative min-h-screen w-screen bg-slate-50 text-slate-800 flex flex-col p-4 md:p-8 overflow-x-hidden selection:bg-senai-blue/20">
      {/* Background Ambient Liquid Glass Blobs */}
      <div className="pointer-events-none fixed inset-0 z-0 overflow-hidden">
        <div className="absolute -top-40 -left-40 h-96 w-96 rounded-full bg-senai-blue/10 blur-3xl animate-float" />
        <div className="absolute top-1/3 -right-40 h-96 w-96 rounded-full bg-senai-orange/10 blur-3xl animate-float" style={{ animationDelay: '2s' }} />
        <div className="absolute -bottom-40 left-1/3 h-96 w-96 rounded-full bg-senai-cyan/10 blur-3xl animate-float" style={{ animationDelay: '4s' }} />
      </div>

      <div className="relative z-10 max-w-7xl mx-auto w-full flex flex-col flex-1">
        {/* Institutional Header */}
        <ChecklistHeader
          userName={userName}
          machineName={machineName}
          totalQuestions={questions.length}
          answeredQuestions={answeredCount}
        />

        {/* Main Content Area: Responsive Grid 100% Fill */}
        <main className="flex-1 w-full my-4">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {questions.map((question) => (
              <ChecklistItemCard
                key={question.id}
                question={question}
                onSelectOption={handleSelectOption}
                onDescriptionChange={handleDescriptionChange}
              />
            ))}
          </div>
        </main>

        {/* Footer Actions Panel */}
        <footer className="glass-panel sticky bottom-4 z-30 w-full rounded-3xl p-5 mt-6 shadow-glass flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-3 text-slate-600 text-xs font-medium">
            <Building2 className="h-5 w-5 text-senai-blue" />
            <span>
              Serviço Nacional de Aprendizagem Industrial — <strong>SENAI</strong>
            </span>
          </div>

          <div className="flex items-center gap-4 w-full sm:w-auto">
            {!isFormValid && (
              <div className="hidden lg:flex items-center gap-2 text-xs font-bold text-senai-orange bg-orange-50 px-3 py-2 rounded-xl border border-orange-200">
                <ShieldAlert className="h-4 w-4" />
                <span>Responda todos os itens para liberar a conclusão</span>
              </div>
            )}

            <InteractiveHoverButton
              text="Concluir Checklist"
              disabled={!isFormValid}
              onClick={handleSubmit}
              className="w-full sm:w-auto min-w-[240px]"
            />
          </div>
        </footer>
      </div>

      {/* Confirmation Modal */}
      <ChecklistSummaryModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        machineName={machineName}
        userName={userName}
        logContent={generatedLog}
      />
    </div>
  );
}

export default App;
