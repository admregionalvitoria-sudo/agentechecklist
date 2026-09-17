using System;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

[assembly: AssemblyVersion(ChecklistLogin.AutoUpdater.CurrentVersion + ".0")]
[assembly: AssemblyFileVersion(ChecklistLogin.AutoUpdater.CurrentVersion + ".0")]

namespace ChecklistLogin
{
    // ─────────────────────────────────────────────────────────────────────────
    // AUTO-UPDATER — verifica e aplica novas versões em segundo plano
    // Estratégia: lê version.txt direto do repo GitHub e baixa o .exe raw
    // Para publicar nova versão: basta compilar, copiar para release/ e git push
    // ─────────────────────────────────────────────────────────────────────────
    public static class RemoteAppearance
    {
        private static readonly string CacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChecklistLogin");
        private const string Endpoint = "https://log-acesso.vercel.app/api/appearance?scope=";
        private static int _refreshing;
        public static Dictionary<string, string> Current = new Dictionary<string, string>();
        private static string _panelUrl = "https://log-acesso.vercel.app/api/appearance";
        public static void SetConfig(string panelUrl) { if (!string.IsNullOrWhiteSpace(panelUrl)) _panelUrl = panelUrl; }
        private static string CacheFile(string location) { return Path.Combine(CacheFolder, "appearance-" + Regex.Replace(location, "[^A-Za-z0-9]", "_") + ".json"); }
        private static bool TryFetchAppearance(string scope, out string response)
        {
            response = null;

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(_panelUrl + "?scope=" + Uri.EscapeDataString(scope) + "&_t=" + DateTime.UtcNow.Ticks);
                request.Timeout = 10000;
                request.ReadWriteTimeout = 10000;
                request.Headers[HttpRequestHeader.CacheControl] = "no-cache";
                request.Headers[HttpRequestHeader.Pragma] = "no-cache";

                using (var result = request.GetResponse())
                using (var reader = new StreamReader(result.GetResponseStream()))
                {
                    char[] buffer = new char[1200001]; int total = 0, count;
                    while (total < buffer.Length && (count = reader.Read(buffer, total, buffer.Length - total)) > 0) total += count;
                    if (total > 1200000) throw new Exception("Response too large");
                    response = new string(buffer, 0, total);
                }

                var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(response);
                object wrappedPayload;
                if (root != null && root.TryGetValue("payload", out wrappedPayload) && wrappedPayload is string)
                {
                    return true;
                }
            }
            catch (WebException ex)
            {
                var result = ex.Response as HttpWebResponse;
                if (result == null || result.StatusCode != HttpStatusCode.NotFound) throw;
            }

            return false;
        }
        private static Dictionary<string, string> Parse(string payload)
        {
            if (payload == null || System.Text.Encoding.UTF8.GetByteCount(payload) > 900000) throw new Exception("Appearance too large");
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, string>>(payload);
            if (data == null || !data.ContainsKey("title") || string.IsNullOrWhiteSpace(data["title"]) || data["title"].Length > 120) throw new Exception("Invalid title");
            if (!data.ContainsKey("accent") || !Regex.IsMatch(data["accent"], "^#[0-9a-fA-F]{6}$")) throw new Exception("Invalid color");
            if (data.ContainsKey("notice") && data["notice"].Length > 2000) throw new Exception("Invalid notice");
            if (data.ContainsKey("subtitle") && data["subtitle"].Length > 120) throw new Exception("Invalid subtitle");
            if (data.ContainsKey("image") && data["image"].Length > 280000) throw new Exception("Invalid image");
            if (data.ContainsKey("supportUrl")) {
                Uri link;
                if (data["supportUrl"].Length > 500 || (data["supportUrl"] != "" && (!Uri.TryCreate(data["supportUrl"], UriKind.Absolute, out link) || link.Scheme != "https"))) throw new Exception("Invalid support URL");
            }
            if (data.ContainsKey("supportTitle") && data["supportTitle"].Length > 70) throw new Exception("Invalid support title");
            if (data.ContainsKey("qrImage") && data["qrImage"].Length > 100000) throw new Exception("Invalid QR image");
            if (data.ContainsKey("appVersion") && !string.IsNullOrWhiteSpace(data["appVersion"])) {
                if (!Regex.IsMatch(data["appVersion"].Trim(), @"^\d+(\.\d+){1,3}$")) throw new Exception("Invalid appVersion");
            }
            if (data.ContainsKey("exeUrl") && !string.IsNullOrWhiteSpace(data["exeUrl"])) {
                Uri link;
                if (data["exeUrl"].Length > 500 || (!Uri.TryCreate(data["exeUrl"], UriKind.Absolute, out link) || link.Scheme != "https")) throw new Exception("Invalid exeUrl");
            }
            if (data.ContainsKey("exeSha256") && !string.IsNullOrWhiteSpace(data["exeSha256"])) {
                if (!Regex.IsMatch(data["exeSha256"].Trim(), "^[a-fA-F0-9]{64}$")) throw new Exception("Invalid exeSha256");
            }
            if (data.ContainsKey("slides")) ValidateSlides(data["slides"]);
            return data;
        }
        public static List<AnnouncementSlide> ValidateSlides(string json) {
            var slides = new JavaScriptSerializer().Deserialize<List<AnnouncementSlide>>(json);
            if (slides == null || slides.Count > 6) throw new Exception("Invalid slides");
            foreach (var slide in slides) {
                if (slide == null || slide.image == null || slide.image.Length > 280000 || (!string.IsNullOrEmpty(slide.kind) && slide.kind != "image" && slide.kind != "video") || (!MediaCache.IsAllowed(slide.image, slide.kind) && !(slide.kind != "video" && (slide.image.StartsWith("data:image/jpeg;base64,") || slide.image.StartsWith("data:image/png;base64,") || slide.image.StartsWith("data:image/webp;base64,")))) || slide.title == null || slide.title.Length > 100 || slide.seconds < 5 || slide.seconds > 60) throw new Exception("Invalid slide");
            }
            return slides;
        }
        public static string Get(string key, string fallback) { string value; return Current.TryGetValue(key, out value) ? value : fallback; }
        private static void CheckForExecutableUpdate(Dictionary<string, string> parsed)
        {
            try
            {
                string appVer, exeUrl, exeHash, restartStr;
                if (parsed != null &&
                    parsed.TryGetValue("appVersion", out appVer) && !string.IsNullOrWhiteSpace(appVer) &&
                    parsed.TryGetValue("exeUrl", out exeUrl) && !string.IsNullOrWhiteSpace(exeUrl))
                {
                    parsed.TryGetValue("exeSha256", out exeHash);
                    bool restart = parsed.TryGetValue("forceRestart", out restartStr) && (restartStr == "true" || restartStr == "True");
                    AutoUpdater.CheckAndUpdateAsync(appVer, exeUrl, exeHash, restart);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("CheckForExecutableUpdate: " + ex.Message);
            }
        }
        public static bool TryPrime(string location)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

                string response;
                foreach (string scope in new string[] { location, "global" })
                {
                    if (!TryFetchAppearance(scope, out response)) continue;

                    string payload = response;
                    try {
                        var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(response);
                        object wrappedPayload;
                        if (root != null && root.TryGetValue("payload", out wrappedPayload) && wrappedPayload is string) {
                            payload = (string)wrappedPayload;
                        }
                    } catch { }

                    var parsed = Parse(payload);
                    Directory.CreateDirectory(CacheFolder);
                    File.WriteAllText(CacheFile(location) + ".tmp", payload);
                    if (File.Exists(CacheFile(location))) File.Replace(CacheFile(location) + ".tmp", CacheFile(location), null);
                    else File.Move(CacheFile(location) + ".tmp", CacheFile(location));
                    Current = parsed;
                    CheckForExecutableUpdate(parsed);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Appearance prime: " + ex.Message);
            }

            return false;
        }
        public static void LoadCache(string location)
        {
            try { Current = Parse(File.ReadAllText(CacheFile(location))); } catch { Current = new Dictionary<string, string>(); }
        }
        public static void Refresh(string location, Action done)
        {
            if (Interlocked.Exchange(ref _refreshing, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(delegate {
                try {
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;
                    string response = null;
                    foreach (string scope in new string[] { location, "global" }) {
                        if (TryFetchAppearance(scope, out response)) {
                            break;
                        }
                    }
                    if (response == null) return;
                    string payload = response;
                    try {
                        var root = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(response);
                        object wrappedPayload;
                        if (root != null && root.TryGetValue("payload", out wrappedPayload) && wrappedPayload is string) {
                            payload = (string)wrappedPayload;
                        }
                    } catch { }
                    string cacheFilePath = CacheFile(location);
                    if (File.Exists(cacheFilePath))
                    {
                        try {
                            string existing = File.ReadAllText(cacheFilePath);
                            if (string.Equals(existing, payload, StringComparison.Ordinal))
                            {
                                return;
                            }
                        } catch { }
                    }

                    var parsed = Parse(payload);
                    Directory.CreateDirectory(CacheFolder);
                    File.WriteAllText(CacheFile(location) + ".tmp", payload);
                    if (File.Exists(CacheFile(location))) File.Replace(CacheFile(location) + ".tmp", CacheFile(location), null);
                    else File.Move(CacheFile(location) + ".tmp", CacheFile(location));
                    Application.Current.Dispatcher.Invoke(new Action(delegate { Current = parsed; done(); }));
                    string image;
                    if (parsed.TryGetValue("image", out image)) {
                        try { MediaCache.Download(new AnnouncementSlide { image = image, kind = "image", title = "", seconds = 10 }, false); } catch (Exception ex) { Debug.WriteLine("Media: " + ex.Message); }
                    }
                    string qrImage;
                    if (parsed.TryGetValue("qrImage", out qrImage)) {
                        try { MediaCache.Download(new AnnouncementSlide { image = qrImage, kind = "image", title = "", seconds = 10 }, false); } catch (Exception ex) { Debug.WriteLine("Media: " + ex.Message); }
                    }
                    string slides;
                    if (parsed.TryGetValue("slides", out slides)) {
                        var media = ValidateSlides(slides);
                        foreach (var slide in media) {
                            try { MediaCache.Download(slide, false); } catch (Exception ex) { Debug.WriteLine("Media: " + ex.Message); }
                        }
                        Application.Current.Dispatcher.Invoke(new Action(delegate { done(); }));
                        MediaCache.Cleanup();
                    }
                    CheckForExecutableUpdate(parsed);
                } catch (Exception ex) { Debug.WriteLine("Appearance: " + ex.Message); }
                finally { Interlocked.Exchange(ref _refreshing, 0); }
            });
        }
    }

    public static class AutoUpdater
    {
        // Versão atual do executável — deve coincidir com o conteúdo de version.txt no repo
        public const string CurrentVersion = "2.4.4";

        // URL raw do arquivo version.txt no repositório GitHub (fallback)
        private const string VersionUrl =
            "https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/main/version.txt";

        // URL raw do executável ChecklistLogin.exe no repositório GitHub (fallback)
        private const string ExeUrl =
            "https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/main/release/ChecklistLogin.exe";

        private static int _updating = 0;

        // Limpa arquivos .old deixados por actualizações anteriores
        public static void CleanupOldFiles()
        {
            try
            {
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                foreach (string f in Directory.GetFiles(exeDir, "*.old"))
                {
                    try { File.Delete(f); } catch { }
                }
            }
            catch { }
        }

        // Inicia a checagem em segundo plano — não bloqueia a thread principal
        public static void CheckAndUpdateAsync(string remoteVersion = null, string exeUrl = null, string expectedHash = null, bool forceRestart = false)
        {
            if (Interlocked.Exchange(ref _updating, 1) != 0) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    RunUpdateCheck(remoteVersion, exeUrl, expectedHash, forceRestart);
                }
                finally
                {
                    Interlocked.Exchange(ref _updating, 0);
                }
            });
        }

        private static void RunUpdateCheck(string targetVersion = null, string targetExeUrl = null, string targetHash = null, bool forceRestart = false)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;

                string remoteVersion = targetVersion;
                string exeUrl = targetExeUrl;
                string expectedHash = targetHash;

                // 1. Se não foi fornecida versão específica (ex: arranque antes de consultar API), tenta fallback GitHub
                if (string.IsNullOrWhiteSpace(remoteVersion) || string.IsNullOrWhiteSpace(exeUrl))
                {
                    remoteVersion = FetchText(VersionUrl);
                    if (string.IsNullOrWhiteSpace(remoteVersion)) return;
                    remoteVersion = remoteVersion.Trim().TrimStart('v', 'V');
                    exeUrl = ExeUrl;
                    expectedHash = FetchText(ExeUrl + ".sha256");
                }

                remoteVersion = remoteVersion.Trim().TrimStart('v', 'V');

                // 2. Compara com a versão local
                if (!IsNewerVersion(remoteVersion, CurrentVersion)) return;

                // 3. Baixa o novo ChecklistLogin.exe
                string exePath  = Process.GetCurrentProcess().MainModule.FileName;
                string exeDir   = Path.GetDirectoryName(exePath);
                string tempPath = Path.Combine(exeDir, "ChecklistLogin_update.tmp");

                DownloadFile(exeUrl, tempPath);

                // 4. Valida arquivo (> 10 KB)
                if (!File.Exists(tempPath) || new FileInfo(tempPath).Length < 10240)
                {
                    try { File.Delete(tempPath); } catch { }
                    return;
                }

                // 5. Valida SHA-256 se fornecido
                if (!string.IsNullOrWhiteSpace(expectedHash))
                {
                    if (!ValidateDownload(tempPath, expectedHash)) {
                        try { File.Delete(tempPath); } catch { }
                        return;
                    }
                }

                // 6. Troca atômica: renomeia atual para .old, coloca novo no lugar
                //    No Windows um EXE em execução não pode ser sobrescrito, mas PODE ser renomeado.
                string oldPath = exePath + ".old";
                try { File.Delete(oldPath); } catch { }

                File.Move(exePath, oldPath);   // exe_atual -> exe_atual.old
                try { File.Move(tempPath, exePath); }
                catch { if (!File.Exists(exePath)) File.Move(oldPath, exePath); throw; }

                // 7. Se forceRestart solicitado e app estiver em execução
                if (forceRestart && Application.Current != null && Application.Current.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(delegate
                    {
                        try
                        {
                            var mw = Application.Current.MainWindow as MainWindow;
                            if (mw != null && mw.CanSafelyRestart())
                            {
                                mw.RestartWithNewBinary();
                            }
                        }
                        catch { }
                    }));
                }
            }
            catch { /* Falhas são silenciosas — app continua funcionando normalmente */ }
        }

        public static bool ValidateDownload(string path, string expectedHash)
        {
            if (expectedHash == null || !Regex.IsMatch(expectedHash.Trim(), "^[a-fA-F0-9]{64}$")) return false;
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) {
                return string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), expectedHash.Trim(), StringComparison.OrdinalIgnoreCase);
            }
        }

        // Baixa texto simples de uma URL (para ler version.txt)
        private static string FetchText(string url)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "ChecklistLogin-Updater/" + CurrentVersion;
                req.Timeout   = 10000;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch { return null; }
        }

        private static void DownloadFile(string url, string destination)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "ChecklistLogin-Updater/" + CurrentVersion;
                req.Timeout   = 120000; // 2 minutos para download

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream src  = resp.GetResponseStream())
                using (FileStream dst = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buf = new byte[81920];
                    int read;
                    while ((read = src.Read(buf, 0, buf.Length)) > 0)
                        dst.Write(buf, 0, read);
                }
            }
            catch
            {
                try { if (File.Exists(destination)) File.Delete(destination); } catch { }
            }
        }

        // Retorna true se remoteVersion é estritamente maior que localVersion
        private static bool IsNewerVersion(string remote, string local)
        {
            try
            {
                Version r = new Version(remote);
                Version l = new Version(local);
                return r > l;
            }
            catch { return false; }
        }
    }


    public class Program
    {
        private static Mutex _singleInstanceMutex;
        private const string EventName = @"Local\ChecklistLoginAppShowEvent";

        [STAThread]
        public static void Main(string[] args)
        {
            // Limpa arquivos .old de atualizações anteriores (operação rápida, sem impacto)
            AutoUpdater.CleanupOldFiles();

            try
            {
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }
            catch { }

            // Process CLI Switches (--install / --uninstall)
            if (args != null && args.Length > 0)
            {
                string arg0 = args[0].ToLowerInvariant();
                if (arg0 == "--install" || arg0 == "/install")
                {
                    string logDir = (args.Length > 1) ? args[1] : @"C:\Logs\Checklist";
                    string location = (args.Length > 2) ? args[2] : "PORTO";
                    InstallTaskAndPermissions(logDir, location);
                    return;
                }
                else if (arg0 == "--uninstall" || arg0 == "/uninstall")
                {
                    UninstallTask();
                    return;
                }
            }

            // Per-Session Mutex (Local\ scope so each user session gets its own instance)
            const string mutexName = @"Local\ChecklistLoginAppSessionMutex";
            bool createdNew;
            _singleInstanceMutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                // App is already running in this user session. Signal existing instance to show itself immediately.
                try
                {
                    using (var showEvent = EventWaitHandle.OpenExisting(EventName))
                    {
                        showEvent.Set();
                    }
                }
                catch { }
                return;
            }

            EventWaitHandle showEventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);

            // Dispara verificação de atualização em segundo plano — não bloqueia a UI
            AutoUpdater.CheckAndUpdateAsync();

            // Launch WPF GUI App
            var app = new Application();
            var mainWindow = new MainWindow(showEventWaitHandle);
            app.Run(mainWindow);
        }

        public static void SaveConfigFile(string logDir, string location)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(location)) location = "PORTO";
                if (string.IsNullOrWhiteSpace(logDir)) logDir = @"C:\Logs\Checklist";

                string json = "{\n" +
                              "  \"logFolderPath\": \"" + logDir.Replace("\\", "\\\\") + "\",\n" +
                              "  \"location\": \"" + location + "\",\n" +
                              "  \"googleWebhookUrl\": \"https://script.google.com/macros/s/AKfycbyvVnnAmbv_zVtjBilNd8qu5S4LWfN_K6QZga-aE5j3UKs3NOmSBHn1SKjaCCOeSrpA/exec\"\n" +
                              "}";

                // Save in App base dir
                string baseFile = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                File.WriteAllText(baseFile, json, Encoding.UTF8);

                // Save in ProgramData
                string progDataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ChecklistLogin");
                if (!Directory.Exists(progDataDir)) Directory.CreateDirectory(progDataDir);
                File.WriteAllText(System.IO.Path.Combine(progDataDir, "config.json"), json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SaveConfigFile error: " + ex.Message);
                throw;
            }
        }

        public static void InstallTaskAndPermissions(string logDir, string location = "PORTO")
        {
            try
            {
                // 0. Grava/atualiza o arquivo de configuração config.json
                SaveConfigFile(logDir, location);

                // 1. Ensure log folder exists and grant Users write permissions natively in C#
                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                try
                {
                    DirectoryInfo dInfo = new DirectoryInfo(logDir);
                    DirectorySecurity dSecurity = dInfo.GetAccessControl();
                    SecurityIdentifier usersGroup = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                    dSecurity.AddAccessRule(new FileSystemAccessRule(
                        usersGroup,
                        FileSystemRights.FullControl,
                        InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                        PropagationFlags.None,
                        AccessControlType.Allow));
                    dInfo.SetAccessControl(dSecurity);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("ACL error: " + ex.Message);
                }

                // 1b. Conceder permissão de Modify na pasta do executável para que o auto-update
                //     possa substituir o .exe sem necessitar de privilégios de Administrador
                try
                {
                    string exeDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
                    if (!string.IsNullOrWhiteSpace(exeDir) && Directory.Exists(exeDir))
                    {
                        DirectoryInfo appDirInfo = new DirectoryInfo(exeDir);
                        DirectorySecurity appDirSec = appDirInfo.GetAccessControl();
                        SecurityIdentifier users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
                        appDirSec.AddAccessRule(new FileSystemAccessRule(
                            users,
                            FileSystemRights.Modify,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.None,
                            AccessControlType.Allow));
                        appDirInfo.SetAccessControl(appDirSec);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("AppDir ACL error: " + ex.Message);
                }

                // 2. Set HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run for All Users
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            key.SetValue("ChecklistLogin", "\"" + exePath + "\"");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Registry error: " + ex.Message);
                }

                // 3. Generate XML Task Definition for ALL USERS, USER SWITCHING, and HIGH PRIORITY
                string taskName = "ChecklistLoginTask";
                string xmlPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChecklistTask-" + Guid.NewGuid().ToString("N") + ".xml");

                string xmlContent = string.Format(@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.4"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>Agente de Checklist de Login SENAI para Todos os Usuarios e Troca de Usuario</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
    <SessionStateChangeTrigger>
      <Enabled>true</Enabled>
      <StateChange>ConsoleConnect</StateChange>
    </SessionStateChangeTrigger>
    <SessionStateChangeTrigger>
      <Enabled>true</Enabled>
      <StateChange>SessionUnlock</StateChange>
    </SessionStateChangeTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <GroupId>S-1-5-32-545</GroupId>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>false</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <IdleSettings>
      <StopOnIdleEnd>false</StopOnIdleEnd>
      <RestartOnIdle>false</RestartOnIdle>
    </IdleSettings>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <WakeToRun>false</WakeToRun>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>1</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{0}</Command>
    </Exec>
  </Actions>
</Task>", System.Security.SecurityElement.Escape(exePath));

                File.WriteAllText(xmlPath, xmlContent, Encoding.Unicode);

                // Register Task silently via schtasks.exe using XML file
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = string.Format("/Create /TN \"{0}\" /XML \"{1}\" /F", taskName, xmlPath),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                    if (p.ExitCode != 0) throw new Exception("Falha ao registrar tarefa: " + p.ExitCode);
                }

                if (File.Exists(xmlPath))
                {
                    File.Delete(xmlPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Install error: " + ex.Message);
                Environment.ExitCode = 1;
            }
        }

        public static void UninstallTask()
        {
            try
            {
                // Remove HKLM Run Key
                try
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (key != null)
                        {
                            key.DeleteValue("ChecklistLogin", false);
                        }
                    }
                }
                catch { }

                // Remove Scheduled Task
                string taskName = "ChecklistLoginTask";
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "schtasks.exe",
                    Arguments = string.Format("/Delete /TN \"{0}\" /F", taskName),
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process p = Process.Start(psi))
                {
                    p.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Uninstall error: " + ex.Message);
            }
        }
    }

    public class AppConfig
    {
        public string LogFolderPath { get; set; }
        public string Location { get; set; }
        public string CloudName { get; set; }
        public string UploadPreset { get; set; }
        public string PanelUrl { get; set; }

        public AppConfig()
        {
            LogFolderPath = @"C:\Logs\Checklist";
            Location = "PORTO";
            CloudName = "j35zooeo";
            UploadPreset = "ml_default";
            PanelUrl = "https://log-acesso.vercel.app/api/appearance";
        }

        public static AppConfig Load()
        {
            var config = new AppConfig();
            try
            {
                string baseDirConfig = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                string commonConfig = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ChecklistLogin", "config.json");
                string appDataConfig = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChecklistLogin", "config.json");

                string configPath = null;
                if (File.Exists(baseDirConfig)) configPath = baseDirConfig;
                else if (File.Exists(commonConfig)) configPath = commonConfig;
                else if (File.Exists(appDataConfig)) configPath = appDataConfig;

                if (configPath != null)
                {
                    string text = File.ReadAllText(configPath);
                    Match mPath = Regex.Match(text, "\"logFolderPath\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (mPath.Success)
                    {
                        string path = mPath.Groups[1].Value.Replace("\\\\", "\\");
                        if (!string.IsNullOrWhiteSpace(path)) config.LogFolderPath = path;
                    }

                    Match mLoc = Regex.Match(text, "\"location\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (mLoc.Success && !string.IsNullOrWhiteSpace(mLoc.Groups[1].Value)) config.Location = mLoc.Groups[1].Value.Trim();

                    Match mCloud = Regex.Match(text, "\"cloudName\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (mCloud.Success && !string.IsNullOrWhiteSpace(mCloud.Groups[1].Value)) config.CloudName = mCloud.Groups[1].Value.Trim();

                    Match mPreset = Regex.Match(text, "\"uploadPreset\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (mPreset.Success && !string.IsNullOrWhiteSpace(mPreset.Groups[1].Value)) config.UploadPreset = mPreset.Groups[1].Value.Trim();

                    Match mPanel = Regex.Match(text, "\"panelUrl\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (mPanel.Success && !string.IsNullOrWhiteSpace(mPanel.Groups[1].Value)) config.PanelUrl = mPanel.Groups[1].Value.Trim();
                }
            }
            catch { }

            return config;
        }
    }

    public class ChecklistQuestion
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Category { get; set; }
        public bool? IsOk { get; set; }
        public string ProblemDescription { get; set; }

        public Button BtnSim { get; set; }
        public Button BtnNao { get; set; }
        public Border CardBorder { get; set; }
        public StackPanel DescPanel { get; set; }
        public TextBox DescTextBox { get; set; }

        public bool IsValid
        {
            get
            {
                if (IsOk == null) return false;
                if (IsOk == true) return true;
                return !string.IsNullOrWhiteSpace(ProblemDescription);
            }
        }
    }

    public partial class MainWindow : Window
    {
        private bool _isExplicitShutdown = false;
        private List<ChecklistQuestion> _questions = new List<ChecklistQuestion>();
        private Button _btnSubmit;
        private TextBlock _progressText;
        private Border _progressBarFill;
        private TextBlock _customTitle, _customSubtitle, _customNotice;
        private Image _customImage;
        private Border _customPanel;
        private DispatcherTimer _appearanceTimer;
        private DateTime _lastAppearanceRefreshUtc = DateTime.MinValue;
        private void ApplyAppearance() { ApplyModernAppearance(); }
        private AppConfig _config;

        // Atualiza a aparência remotamente com taxa reduzida para tempo real.
        private void RefreshAppearanceIfDue(string location)
        {
            if ((DateTime.UtcNow - _lastAppearanceRefreshUtc).TotalSeconds < 3) return;
            _lastAppearanceRefreshUtc = DateTime.UtcNow;
            RemoteAppearance.Refresh(location, ApplyAppearance);
        }

        public MainWindow(EventWaitHandle showEventWaitHandle, bool previewOnly = false, string previewLocation = null)
        {
            _config = AppConfig.Load();
            if (previewOnly && previewLocation != null) _config.Location = previewLocation;
            // Propaga panelUrl e cloudName lidos do config.json para os módulos estáticos
            RemoteAppearance.SetConfig(_config.PanelUrl);
            var location = _config.Location ?? "PORTO";
            if (!RemoteAppearance.TryPrime(location))
            {
                RemoteAppearance.LoadCache(location);
            }
            InitUI();
            ApplyAppearance();
            if (previewOnly) return;
            RemoteAppearance.Refresh(location, ApplyAppearance);

            _appearanceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _appearanceTimer.Tick += delegate
            {
                // Mantém o cache sempre atualizado em tempo real (a cada 5s)
                RefreshAppearanceIfDue(location);
            };
            _appearanceTimer.Start();

            // Ao ganhar foco (reabertura/sessão), busca imediatamente a aparência.
            Activated += delegate
            {
                if (_isExplicitShutdown) return;
                RefreshAppearanceIfDue(location);
            };

            Closed += delegate
            {
                if (_appearanceTimer != null)
                {
                    _appearanceTimer.Stop();
                }
            };

            // Escutar eventos de troca de sessão/logon do Windows
            try
            {
                SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
            }
            catch { }

            // Escutar sinal IPC de instâncias secundárias via EventWaitHandle
            if (showEventWaitHandle != null)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    while (true)
                    {
                        showEventWaitHandle.WaitOne();
                        if (Application.Current != null && Application.Current.Dispatcher != null)
                        {
                            Application.Current.Dispatcher.Invoke(new Action(() => ReopenChecklist()));
                        }
                    }
                });
            }
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.ConsoleConnect ||
                e.Reason == SessionSwitchReason.SessionUnlock ||
                e.Reason == SessionSwitchReason.SessionLogon ||
                e.Reason == SessionSwitchReason.RemoteConnect ||
                e.Reason == SessionSwitchReason.SessionLock)
            {
                if (Application.Current != null && Application.Current.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(new Action(() => ReopenChecklist()));
                }
            }
        }

        public void ReopenChecklist()
        {
            _isExplicitShutdown = false;
            ResetForm();
            RefreshAppearanceIfDue(_config.Location ?? "PORTO");
            Show();
            WindowState = WindowState.Maximized;
            Activate();
            Topmost = true;
        }

        private void ResetForm()
        {
            _isExplicitShutdown = false;
            foreach (var q in _questions)
            {
                q.IsOk = null;
                q.ProblemDescription = "";
                if (q.DescTextBox != null) q.DescTextBox.Text = "";
                if (q.DescPanel != null) q.DescPanel.Visibility = Visibility.Collapsed;
                if (q.BtnSim != null) StyleButtonUnselected(q.BtnSim, "#F0F9FF", "#0091D6", "#BAE6FD");
                if (q.BtnNao != null) StyleButtonUnselected(q.BtnNao, "#FFF7ED", "#EF5E31", "#FFEDD5");
                if (q.CardBorder != null) q.CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            }
            ValidateForm();
        }

        public bool CanSafelyRestart()
        {
            if (_questions == null || _questions.Count == 0) return true;
            foreach (var q in _questions)
            {
                if (q.IsOk != null) return false;
            }
            return true;
        }

        public void RestartWithNewBinary()
        {
            try
            {
                _isExplicitShutdown = true;
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(exePath);
                Application.Current.Shutdown();
            }
            catch { }
        }

        private void InitUI() { InitModernUi(); }

        private Border CreateBadge(string label, string value, string hexColor)
        {
            Border b = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 8, 0)
            };
            StackPanel s = new StackPanel();
            s.Children.Add(new TextBlock { Text = label, FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")) });
            s.Children.Add(new TextBlock { Text = value, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)) });
            b.Child = s;
            return b;
        }

        private void AddQuestion(Panel container, string id, string title, string category)
        {
            var q = new ChecklistQuestion { Id = id, Title = title, Category = category };
            q.CardBorder = Surface(null, new Thickness(14, 8, 14, 8));
            q.CardBorder.Margin = new Thickness(0, 0, 0, 8); q.CardBorder.MinHeight = 84;
            var layout = new Grid { VerticalAlignment = VerticalAlignment.Center };
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(136) });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string glyph = id == "tela" ? "\uE7F4" : id == "teclado" ? "\uE765" : id == "internet" ? "\uE701" : id == "computador" ? "\uE950" : "\uE962";
            var icon = new TextBlock { Text = glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 22, Foreground = Brush("#164194"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            layout.Children.Add(new Border { Background = Brush("#F0F5FC"), CornerRadius = new CornerRadius(10), Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Child = icon });
            var copy = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            copy.Children.Add(Label((_questions.Count + 1).ToString("00") + " · VERIFICAÇÃO", 8, "#8A98AC", false));
            copy.Children.Add(Label(title, 15, "#142842", true)); copy.Children.Add(Label(category, 10, "#718096", false)); Grid.SetColumn(copy, 1); layout.Children.Add(copy);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            q.BtnSim = new Button { Content = "✓ Sim", Width = 62, Height = 34, FontSize = 12, Margin = new Thickness(0, 0, 6, 0), Cursor = Cursors.Hand };
            q.BtnNao = new Button { Content = "× Não", Width = 62, Height = 34, FontSize = 12, Cursor = Cursors.Hand };
            StyleButtonUnselected(q.BtnSim, "#F0F9FF", "#0091D6", "#BAE6FD"); StyleButtonUnselected(q.BtnNao, "#FFF7ED", "#EF5E31", "#FFEDD5");
            buttons.Children.Add(q.BtnSim); buttons.Children.Add(q.BtnNao); Grid.SetColumn(buttons, 2); layout.Children.Add(buttons);
            q.DescPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 8, 0, 0) };
            q.DescPanel.Children.Add(Label("Descreva o problema para continuar:", 10, "#C45C38", true));
            q.DescTextBox = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44, MaxHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(8), FontSize = 12, BorderBrush = Brush("#E7B9A8"), BorderThickness = new Thickness(1), Background = Brushes.White, Foreground = Ink };
            q.DescTextBox.TextChanged += delegate { q.ProblemDescription = q.DescTextBox.Text; ValidateForm(); };
            q.DescPanel.Children.Add(q.DescTextBox); Grid.SetRow(q.DescPanel, 1); Grid.SetColumn(q.DescPanel, 1); Grid.SetColumnSpan(q.DescPanel, 2); layout.Children.Add(q.DescPanel);
            // SIM Click Event
            q.BtnSim.Click += (s, e) =>
            {
                q.IsOk = true;
                StyleButtonSelected(q.BtnSim, "#0091D6"); // SENAI Cyan SIM
                StyleButtonUnselected(q.BtnNao, "#FFF7ED", "#EF5E31", "#FFEDD5");
                q.CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0091D6"));
                q.DescPanel.Visibility = Visibility.Collapsed;
                ValidateForm();
            };

            // NÃO Click Event
            q.BtnNao.Click += (s, e) =>
            {
                q.IsOk = false;
                StyleButtonSelected(q.BtnNao, "#EF5E31"); // SENAI Orange NÃO
                StyleButtonUnselected(q.BtnSim, "#F0F9FF", "#0091D6", "#BAE6FD");
                q.CardBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF5E31"));
                q.DescPanel.Visibility = Visibility.Visible;
                ValidateForm();
            };

            q.CardBorder.Child = layout;
            container.Children.Add(q.CardBorder); _questions.Add(q);
        }

        private void StyleButtonUnselected(Button btn, string bgHex, string fgHex, string borderHex)
        {
            Style s = new Style(typeof(Button));
            ControlTemplate t = new ControlTemplate(typeof(Button));
            FrameworkElementFactory b = new FrameworkElementFactory(typeof(Border));
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            b.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgHex)));
            b.SetValue(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(borderHex)));
            b.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(cp);
            t.VisualTree = b;

            s.Setters.Add(new Setter(Button.TemplateProperty, t));
            s.Setters.Add(new Setter(Button.ForegroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(fgHex))));
            btn.Style = s;
        }

        private void StyleButtonSelected(Button btn, string activeHex)
        {
            Style s = new Style(typeof(Button));
            ControlTemplate t = new ControlTemplate(typeof(Button));
            FrameworkElementFactory b = new FrameworkElementFactory(typeof(Border));
            b.SetValue(Border.CornerRadiusProperty, new CornerRadius(12));
            b.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(activeHex)));
            b.SetValue(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString(activeHex)));
            b.SetValue(Border.BorderThicknessProperty, new Thickness(2));

            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            b.AppendChild(cp);
            t.VisualTree = b;

            s.Setters.Add(new Setter(Button.TemplateProperty, t));
            s.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));
            btn.Style = s;
        }

        private void ValidateForm()
        {
            int answeredCount = 0;
            bool allValid = true;

            foreach (var q in _questions)
            {
                if (q.IsOk != null) answeredCount++;
                if (!q.IsValid) allValid = false;
            }

            int percent = (int)(((double)answeredCount / _questions.Count) * 100);
            _progressText.Text = string.Format("{0} de {1} ({2}%)", answeredCount, _questions.Count, percent);
            
            // Update progress bar width dynamically
            double maxProgressWidth = 300;
            _progressBarFill.Width = (maxProgressWidth * percent) / 100.0;

            _btnSubmit.IsEnabled = allValid;
            _btnSubmit.Cursor = allValid ? Cursors.Hand : Cursors.No;
            UpdateSubmitButtonStyle();
        }

        private void UpdateSubmitButtonStyle()
        {
            if (_btnSubmit == null) return;

            Style btnStyle = new Style(typeof(Button));
            ControlTemplate template = new ControlTemplate(typeof(Button));

            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));

            if (_btnSubmit.IsEnabled)
            {
                LinearGradientBrush grad = new LinearGradientBrush();
                grad.StartPoint = new Point(0, 0);
                grad.EndPoint = new Point(1, 1);
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#1A4B9F"), 0));
                grad.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString("#0091D6"), 1));
                borderFactory.SetValue(Border.BackgroundProperty, grad);
            }
            else
            {
                borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CBD5E1")));
            }

            FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);

            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            btnStyle.Setters.Add(new Setter(Button.TemplateProperty, template));
            btnStyle.Setters.Add(new Setter(Button.ForegroundProperty, _btnSubmit.IsEnabled ? Brushes.White : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"))));

            _btnSubmit.Style = btnStyle;
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            SaveLog();
            _isExplicitShutdown = true;
            Hide();
        }

        private void SaveLog()
        {
            try
            {
                string machineName = Environment.MachineName;
                string userName = Environment.UserName;
                string fileName = machineName + ".txt";
                string dataHora = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=========================================");
                sb.AppendLine("Computador: " + machineName);
                sb.AppendLine("Usuário: " + userName);
                sb.AppendLine("Data/Hora: " + dataHora);
                sb.AppendLine("-----------------------------------------");

                List<string> statusList = new List<string>();

                string statusTela = "OK";
                string statusTeclado = "OK";
                string statusMouse = "OK";
                string statusTouchPad = "OK";
                string statusInternet = "OK";
                string statusGabinete = "OK";
                bool temDefeito = false;

                foreach (var q in _questions)
                {
                    string name = q.Id.ToUpper();
                    string id = (q.Id ?? "").ToLower();
                    string val = "OK";

                    if (q.IsOk == true)
                    {
                        sb.AppendLine(name + ": OK");
                        statusList.Add(name + ": OK");
                    }
                    else if (q.IsOk == false)
                    {
                        temDefeito = true;
                        string desc = string.IsNullOrWhiteSpace(q.ProblemDescription) ? "Sem descrição" : q.ProblemDescription.Trim();
                        sb.AppendLine(name + ": NÃO OK - " + desc);
                        val = "NÃO OK (" + desc + ")";
                        statusList.Add(name + ": NO-OK (" + desc + ")");
                    }
                    else
                    {
                        val = "PENDENTE";
                        statusList.Add(name + ": PENDENTE");
                    }

                    if (id.Contains("tela") || id.Contains("monitor")) statusTela = val;
                    else if (id.Contains("teclado")) statusTeclado = val;
                    else if (id.Contains("touch")) statusTouchPad = val;
                    else if (id.Contains("mouse")) statusMouse = val;
                    else if (id.Contains("internet") || id.Contains("rede")) statusInternet = val;
                    else if (id.Contains("gabinete") || id.Contains("computador")) statusGabinete = val;
                }

                sb.AppendLine("=========================================");
                sb.AppendLine();

                string statusGeral = temDefeito ? "ATENÇÃO / DEFEITO" : "OK";
                string resumoItens = string.Join("; ", statusList);

                // Dispara o log para a planilha online do Google em segundo plano
                string location = (_config != null && !string.IsNullOrEmpty(_config.Location)) ? _config.Location : "PORTO";
                SendToGoogleWebhook(location, machineName, userName, dataHora, statusTela, statusTeclado, statusMouse, statusTouchPad, statusInternet, statusGabinete, statusGeral, resumoItens);

                string primaryFolder = _config.LogFolderPath;
                if (TrySaveLogFile(primaryFolder, fileName, sb.ToString()))
                {
                    return;
                }

                // Fallback 1: %APPDATA%\ChecklistLogin\Logs
                string fallback1 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChecklistLogin", "Logs");
                if (TrySaveLogFile(fallback1, fileName, sb.ToString()))
                {
                    return;
                }

                // Fallback 2: Local app folder
                string fallback2 = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
                TrySaveLogFile(fallback2, fileName, sb.ToString());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao gravar log: " + ex.Message, "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SendToGoogleWebhook(string location, string machineName, string userName, string dataHora, string tela, string teclado, string mouse, string touchPad, string internet, string gabinete, string statusGeral, string resumoItens)
        {
            try
            {
                string webhookUrl = "https://script.google.com/macros/s/AKfycbyvVnnAmbv_zVtjBilNd8qu5S4LWfN_K6QZga-aE5j3UKs3NOmSBHn1SKjaCCOeSrpA/exec";
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        using (var client = new System.Net.WebClient())
                        {
                            client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                            client.Encoding = Encoding.UTF8;
                            string jsonPayload = string.Format(
                                "{{\"location\":\"{0}\",\"aba\":\"{0}\",\"unidade\":\"{0}\",\"computador\":\"{1}\",\"usuario\":\"{2}\",\"dataHora\":\"{3}\",\"tela\":\"{4}\",\"teclado\":\"{5}\",\"mouse\":\"{6}\",\"touchpad\":\"{7}\",\"internet\":\"{8}\",\"gabinete\":\"{9}\",\"statusGeral\":\"{10}\",\"resumoItens\":\"{11}\"}}",
                                EscapeJson(location),
                                EscapeJson(machineName),
                                EscapeJson(userName),
                                EscapeJson(dataHora),
                                EscapeJson(tela),
                                EscapeJson(teclado),
                                EscapeJson(mouse),
                                EscapeJson(touchPad),
                                EscapeJson(internet),
                                EscapeJson(gabinete),
                                EscapeJson(statusGeral),
                                EscapeJson(resumoItens)
                            );
                            client.UploadString(webhookUrl, "POST", jsonPayload);
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private static string EscapeJson(string str)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        private bool TrySaveLogFile(string folderPath, string fileName, string content)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath)) return false;
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                string filePath = System.IO.Path.Combine(folderPath, fileName);
                File.AppendAllText(filePath, content, Encoding.UTF8);
                return true;
            }
            catch
            {
                return false;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (!_isExplicitShutdown)
            {
                e.Cancel = true;
                WindowState = WindowState.Maximized;
                Topmost = true;
                Activate();
                MessageBox.Show(
                    "Você precisa responder e concluir todo o checklist de equipamentos antes de fechar a aplicação.",
                    "Atenção — SENAI",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }

    public class RadiusHelper
    {
        public CornerRadius Radius20 { get { return new CornerRadius(20); } }
    }
}
