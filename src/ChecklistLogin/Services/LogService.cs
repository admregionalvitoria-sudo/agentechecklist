using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

            foreach (var item in items)
            {
                if (item.IsOk == true)
                {
                    sb.AppendLine($"{item.Name}: OK");
                }
                else if (item.IsOk == false)
                {
                    string desc = string.IsNullOrWhiteSpace(item.ProblemDescription)
                        ? "Sem descrição"
                        : item.ProblemDescription.Trim();
                    sb.AppendLine($"{item.Name}: NÃO OK - {desc}");
                }
                else
                {
                    sb.AppendLine($"{item.Name}: NÃO RESPONDIDO");
                }
            }

            sb.AppendLine("=========================================");
            sb.AppendLine(); // Empty line separator between records

            string primaryFolderPath = config.LogFolderPath;
            
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
