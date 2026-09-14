using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ChecklistLogin.Models;
using ChecklistLogin.ViewModels;

namespace ChecklistLogin.Services
{
    public class LogResult
    {
        public bool Success { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public bool UsedFallback { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public static class LogService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        public static LogResult SaveLog(AppConfig config, string machineName, string userName, IEnumerable<ChecklistItemViewModel> items)
        {
            var result = new LogResult();
            string fileName = $"{machineName}.txt";

            // Format Log Entry
            var sb = new StringBuilder();
            sb.AppendLine("=========================================");
            sb.AppendLine($"Computador: {machineName}");
            sb.AppendLine($"Usuário: {userName}");
            sb.AppendLine($"Data/Hora: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine("-----------------------------------------");

            var statusList = new List<string>();

            string statusTela = "OK", statusTeclado = "OK", statusMouse = "OK", statusInternet = "OK", statusGabinete = "OK";
            bool temDefeito = false;

            foreach (var item in items)
            {
                string itemNameLower = item.Name.ToLowerInvariant();
                string val = "OK";

                if (item.IsOk == true)
                {
                    sb.AppendLine($"{item.Name}: OK");
                    statusList.Add($"{item.Name}: OK");
                }
                else if (item.IsOk == false)
                {
                    temDefeito = true;
                    string desc = string.IsNullOrWhiteSpace(item.ProblemDescription)
                        ? "Sem descrição"
                        : item.ProblemDescription.Trim();
                    sb.AppendLine($"{item.Name}: NÃO OK - {desc}");
                    val = $"NÃO OK ({desc})";
                    statusList.Add($"{item.Name}: NO-OK ({desc})");
                }
                else
                {
                    val = "PENDENTE";
                    sb.AppendLine($"{item.Name}: NÃO RESPONDIDO");
                    statusList.Add($"{item.Name}: PENDENTE");
                }

                if (itemNameLower.Contains("tela") || itemNameLower.Contains("monitor")) statusTela = val;
                else if (itemNameLower.Contains("teclado")) statusTeclado = val;
                else if (itemNameLower.Contains("mouse") || itemNameLower.Contains("touch")) statusMouse = val;
                else if (itemNameLower.Contains("internet") || itemNameLower.Contains("rede")) statusInternet = val;
                else if (itemNameLower.Contains("computador") || itemNameLower.Contains("gabinete")) statusGabinete = val;
            }

            sb.AppendLine("=========================================");
            sb.AppendLine(); // Empty line separator between records

            // Disparo assíncrono para o Google Sheets em segundo plano
            if (!string.IsNullOrWhiteSpace(config?.GoogleWebhookUrl))
            {
                string location = config?.Location ?? "PORTO";
                string statusGeral = temDefeito ? "ATENÇÃO / DEFEITO" : "OK";
                string resumo = string.Join("; ", statusList);
                _ = SendToGoogleSheetsAsync(config.GoogleWebhookUrl, location, machineName, userName, DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"), statusTela, statusTeclado, statusMouse, statusInternet, statusGabinete, statusGeral, resumo);
            }

            string primaryFolderPath = config?.LogFolderPath ?? @"C:\Logs\Checklist";
            
            // 1. Try Primary Configured Path
            if (TryWriteToFolder(primaryFolderPath, fileName, sb.ToString(), out string targetFile, out string errorStr))
            {
                result.Success = true;
                result.FilePath = targetFile;
                result.UsedFallback = false;
                return result;
            }

            // 2. Fallback to %APPDATA%\ChecklistLogin\Logs
            string fallbackPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChecklistLogin",
                "Logs"
            );

            if (TryWriteToFolder(fallbackPath, fileName, sb.ToString(), out targetFile, out string fallbackError))
            {
                result.Success = true;
                result.FilePath = targetFile;
                result.UsedFallback = true;
                return result;
            }

            // 3. Fallback to Local Directory
            string localFallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (TryWriteToFolder(localFallback, fileName, sb.ToString(), out targetFile, out string localError))
            {
                result.Success = true;
                result.FilePath = targetFile;
                result.UsedFallback = true;
                return result;
            }

            result.Success = false;
            result.ErrorMessage = $"Não foi possível salvar o log em nenhum local: {errorStr} | {fallbackError}";
            return result;
        }

        private static async Task SendToGoogleSheetsAsync(string webhookUrl, string location, string machineName, string userName, string dataHora, string tela, string teclado, string mouse, string internet, string gabinete, string statusGeral, string resumoItens)
        {
            try
            {
                string jsonPayload = $"{{\"location\":\"{EscapeJson(location)}\",\"aba\":\"{EscapeJson(location)}\",\"unidade\":\"{EscapeJson(location)}\",\"computador\":\"{EscapeJson(machineName)}\",\"usuario\":\"{EscapeJson(userName)}\",\"dataHora\":\"{EscapeJson(dataHora)}\",\"tela\":\"{EscapeJson(tela)}\",\"teclado\":\"{EscapeJson(teclado)}\",\"mouse\":\"{EscapeJson(mouse)}\",\"internet\":\"{EscapeJson(internet)}\",\"gabinete\":\"{EscapeJson(gabinete)}\",\"statusGeral\":\"{EscapeJson(statusGeral)}\",\"resumoItens\":\"{EscapeJson(resumoItens)}\"}}";
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                await httpClient.PostAsync(webhookUrl, content);
            }
            catch
            {
                // Silently ignore Google Sheets webhook failures to not block local TXT logging
            }
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        private static bool TryWriteToFolder(string folderPath, string fileName, string content, out string filePath, out string errorMsg)
        {
            filePath = string.Empty;
            errorMsg = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(folderPath))
                {
                    errorMsg = "Caminho de pasta vazio.";
                    return false;
                }

                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                filePath = Path.Combine(folderPath, fileName);
                File.AppendAllText(filePath, content, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                errorMsg = ex.Message;
                return false;
            }
        }
    }
}
