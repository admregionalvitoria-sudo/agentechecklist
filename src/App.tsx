import React, { useState } from "react";
import { ChecklistHeader } from "@/components/checklist/checklist-header";
import { ChecklistItemCard, ChecklistQuestionState } from "@/components/checklist/checklist-item-card";
import { ChecklistSummaryModal } from "@/components/checklist/checklist-summary-modal";
import { InteractiveHoverButton } from "@/components/ui/interactive-hover-button";
import { ShieldAlert, Building2 } from "lucide-react";

export function App() {
  // Mock logged-in user & machine details (simulates Environment.UserName / MachineName)
  const [userName] = useState("aluno.senai");
  const [machineName] = useState("LAB-INFORMATICA-01");
  const [selectedLocation, setSelectedLocation] = useState("PORTO");
  const isNotebook = selectedLocation.toUpperCase().includes("NOTEBOOK");

  // Checklist Questions Data
  const desktopQuestions: ChecklistQuestionState[] = [
    { id: "tela", title: "Tela / Monitor com exibição perfeita?", category: "Display & Imagem", iconName: "monitor", isOk: null, problemDescription: "" },
    { id: "teclado", title: "Teclado completo e com todas as teclas funcionando?", category: "Periféricos de Entrada", iconName: "keyboard", isOk: null, problemDescription: "" },
    { id: "mouse", title: "Mouse óptico com cliques e scroll operacionais?", category: "Periféricos de Entrada", iconName: "mouse", isOk: null, problemDescription: "" },
    { id: "internet", title: "Conexão com a Internet / Rede SENAI ativa?", category: "Conectividade", iconName: "globe", isOk: null, problemDescription: "" },
    { id: "computador", title: "Gabinete / Computador liga sem ruídos ou lentidão?", category: "Hardware Principal", iconName: "cpu", isOk: null, problemDescription: "" },
  ];

  const notebookQuestions: ChecklistQuestionState[] = [
    { id: "tela", title: "Tela / Monitor com exibição perfeita?", category: "Display & Imagem", iconName: "monitor", isOk: null, problemDescription: "" },
    { id: "teclado", title: "Teclado completo e com todas as teclas funcionando?", category: "Periféricos de Entrada", iconName: "keyboard", isOk: null, problemDescription: "" },
    { id: "touchpad", title: "Touch Pad / Mouse com cliques e navegação operacionais?", category: "Periféricos de Entrada", iconName: "mouse", isOk: null, problemDescription: "" },
    { id: "internet", title: "Conexão com a Internet / Rede SENAI ativa?", category: "Conectividade", iconName: "globe", isOk: null, problemDescription: "" },
  ];

  const [questions, setQuestions] = useState<ChecklistQuestionState[]>(desktopQuestions);

  const handleLocationChange = (loc: string) => {
    setSelectedLocation(loc);
    const isNb = loc.toUpperCase().includes("NOTEBOOK");
    setQuestions(isNb ? notebookQuestions : desktopQuestions);
  };

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
    log += `Local / Aba: ${selectedLocation}\n`;
    log += `Computador: ${machineName}\n`;
    log += `Usuário: ${userName}\n`;
    log += `Data/Hora: ${formattedDate}\n`;
    log += "-----------------------------------------\n";

    let statusTela = "OK";
    let statusTeclado = "OK";
    let statusMouse = "OK";
    let statusTouchPad = "OK";
    let statusInternet = "OK";
    let statusGabinete = "OK";
    let temDefeito = false;
    const statusList: string[] = [];

    questions.forEach((q) => {
      const name = q.id.toUpperCase();
      let val = "OK";
      if (q.isOk === true) {
        log += `${name}: OK\n`;
        statusList.push(`${name}: OK`);
      } else {
        temDefeito = true;
        const desc = q.problemDescription.trim() || "Sem descrição";
        val = `NÃO OK (${desc})`;
        log += `${name}: NÃO OK - ${desc}\n`;
        statusList.push(`${name}: NO-OK (${desc})`);
      }

      if (q.id === "tela") statusTela = val;
      else if (q.id === "teclado") statusTeclado = val;
      else if (q.id === "touchpad") statusTouchPad = val;
      else if (q.id === "mouse") statusMouse = val;
      else if (q.id === "internet") statusInternet = val;
      else if (q.id === "computador") statusGabinete = val;
    });

    log += "=========================================\n";

    setGeneratedLog(log);
    setIsModalOpen(true);

    const statusGeral = temDefeito ? "ATENÇÃO / DEFEITO" : "OK";

    // Envio para o Google Sheets Webhook
    const GOOGLE_WEBHOOK_URL =
      "https://script.google.com/macros/s/AKfycbyvVnnAmbv_zVtjBilNd8qu5S4LWfN_K6QZga-aE5j3UKs3NOmSBHn1SKjaCCOeSrpA/exec";
    fetch(GOOGLE_WEBHOOK_URL, {
      method: "POST",
      mode: "no-cors",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        location: selectedLocation,
        aba: selectedLocation,
        unidade: selectedLocation,
        computador: machineName,
        usuario: userName,
        dataHora: formattedDate,
        tela: statusTela,
        teclado: statusTeclado,
        mouse: statusMouse,
        touchpad: statusTouchPad,
        internet: statusInternet,
        gabinete: statusGabinete,
        statusGeral: statusGeral,
        resumoItens: statusList.join("; "),
      }),
    }).catch((err) => console.error("Erro ao enviar para o Google Sheets:", err));
  };

  return (
    <div className="relative min-h-screen w-screen bg-white text-slate-800 flex flex-col p-4 md:p-8 overflow-x-hidden selection:bg-senai-blue/20">
      {/* Background Ambient Liquid Glass Blobs with Official Colors (#1A4B9F, #0091D6, #EF5E31) */}
      <div className="pointer-events-none fixed inset-0 z-0 overflow-hidden">
        <div className="absolute -top-40 -left-40 h-[500px] w-[500px] rounded-full bg-[#1A4B9F]/12 blur-3xl animate-float" />
        <div className="absolute top-1/3 -right-40 h-[500px] w-[500px] rounded-full bg-[#0091D6]/12 blur-3xl animate-float" style={{ animationDelay: '2.5s' }} />
        <div className="absolute -bottom-40 left-1/3 h-[500px] w-[500px] rounded-full bg-[#EF5E31]/10 blur-3xl animate-float" style={{ animationDelay: '5s' }} />
      </div>

      <div className="relative z-10 max-w-7xl mx-auto w-full flex flex-col flex-1">
        {/* Institutional Header */}
        <ChecklistHeader
          userName={userName}
          machineName={machineName}
          totalQuestions={questions.length}
          answeredQuestions={answeredCount}
          locationName={selectedLocation}
          onLocationChange={handleLocationChange}
        />

        {/* Main Content Area: Responsive Grid */}
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
        <footer className="glass-panel sticky bottom-4 z-30 w-full rounded-3xl p-5 mt-6 shadow-glass border border-white/80 flex flex-col sm:flex-row items-center justify-between gap-4 backdrop-blur-2xl">
          <div className="flex items-center gap-3 text-slate-700 text-xs font-inter font-semibold">
            <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-senai-blue/15 text-senai-blue shadow-sm">
              <Building2 className="h-4.5 w-4.5" />
            </div>
            <span>
              Serviço Nacional de Aprendizagem Industrial — <strong className="font-montserrat font-bold text-senai-blue">SENAI</strong>
            </span>
          </div>

          <div className="flex items-center gap-4 w-full sm:w-auto">
            {!isFormValid && (
              <div className="hidden lg:flex items-center gap-2 text-xs font-montserrat font-extrabold text-senai-orange bg-orange-50/90 px-3.5 py-2.5 rounded-2xl border border-orange-200/80 backdrop-blur-md shadow-sm">
                <ShieldAlert className="h-4 w-4 animate-bounce text-senai-orange" />
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
