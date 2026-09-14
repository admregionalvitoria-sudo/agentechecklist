using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ChecklistLogin
{
    // ─────────────────────────────────────────────────────────────────────────
    // AUTO-UPDATER — verifica e aplica novas versões em segundo plano
    // Estratégia: lê version.txt direto do repo GitHub e baixa o .exe raw
    // Para publicar nova versão: basta compilar, copiar para release/ e git push
    // ─────────────────────────────────────────────────────────────────────────
    public static class AutoUpdater
    {
        // Versão atual do executável — deve coincidir com o conteúdo de version.txt no repo
        public const string CurrentVersion = "2.0.2";

        // URL raw do arquivo version.txt no repositório GitHub
        private const string VersionUrl =
            "https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/main/version.txt";

        // URL raw do executável ChecklistLogin.exe no repositório GitHub
        private const string ExeUrl =
            "https://raw.githubusercontent.com/admregionalvitoria-sudo/agentechecklist/main/release/ChecklistLogin.exe";

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
        public static void CheckAndUpdateAsync()
        {
            ThreadPool.QueueUserWorkItem(_ => RunUpdateCheck());
        }

        private static void RunUpdateCheck()
        {
            try
            {
                // 1. Lê version.txt direto do repositório GitHub (sem autenticação)
                string remoteVersion = FetchText(VersionUrl);
                if (string.IsNullOrWhiteSpace(remoteVersion)) return;
                remoteVersion = remoteVersion.Trim().TrimStart('v', 'V');

                // 2. Compara com a versão local
                if (!IsNewerVersion(remoteVersion, CurrentVersion)) return;

                // 3. Baixa o novo ChecklistLogin.exe diretamente do repositório
                string exePath  = Process.GetCurrentProcess().MainModule.FileName;
                string exeDir   = Path.GetDirectoryName(exePath);
                string tempPath = Path.Combine(exeDir, "ChecklistLogin_update.tmp");

                DownloadFile(ExeUrl, tempPath);

                // 4. Valida arquivo (> 10 KB)
                if (!File.Exists(tempPath) || new FileInfo(tempPath).Length < 10240)
                {
                    try { File.Delete(tempPath); } catch { }
                    return;
                }

                // 5. Troca atômica: renomeia atual para .old, coloca novo no lugar
                //    No Windows um EXE em execução não pode ser sobrescrito, mas PODE ser renomeado.
                string oldPath = exePath + ".old";
                try { File.Delete(oldPath); } catch { }

                File.Move(exePath, oldPath);   // exe_atual -> exe_atual.old
                File.Move(tempPath, exePath);  // novo_tmp  -> exe_atual

                // Atualização aplicada — entrará em vigor no próximo logon/reinício do agente
            }
            catch { /* Falhas são silenciosas — app continua funcionando normalmente */ }
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
                string xmlPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ChecklistTask.xml");

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
      <StateChange>SessionConnect</StateChange>
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
</Task>", exePath);

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
                }

                if (File.Exists(xmlPath))
                {
                    File.Delete(xmlPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Install error: " + ex.Message);
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

        public AppConfig()
        {
            LogFolderPath = @"C:\Logs\Checklist";
            Location = "PORTO";
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
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            config.LogFolderPath = path;
                        }
                    }

                    Match mLoc = Regex.Match(text, "\"location\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (mLoc.Success && !string.IsNullOrWhiteSpace(mLoc.Groups[1].Value))
                    {
                        config.Location = mLoc.Groups[1].Value.Trim();
                    }
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

    public class MainWindow : Window
    {
        private bool _isExplicitShutdown = false;
        private List<ChecklistQuestion> _questions = new List<ChecklistQuestion>();
        private Button _btnSubmit;
        private TextBlock _progressText;
        private Border _progressBarFill;
        private AppConfig _config;

        public MainWindow(EventWaitHandle showEventWaitHandle)
        {
            _config = AppConfig.Load();
            InitUI();

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
                        Application.Current?.Dispatcher?.Invoke(() => ReopenChecklist());
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
                Application.Current?.Dispatcher?.Invoke(() => ReopenChecklist());
            }
        }

        public void ReopenChecklist()
        {
            _isExplicitShutdown = false;
            ResetForm();
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

        private void InitUI()
        {
            // Window Configuration (Full Screen Glassmorphic Container)
            Title = "Sistema de Checklist SENAI — Verificação de Equipamentos";
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            ShowInTaskbar = true;
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));

            // Intercept Alt+F4 and Escape
            PreviewKeyDown += (s, e) =>
            {
                if ((e.Key == Key.System && e.SystemKey == Key.F4) || e.Key == Key.Escape)
                {
                    e.Handled = true;
                }
            };

            Grid mainGrid = new Grid { Margin = new Thickness(24) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Question Cards
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // Footer

            // 1. HEADER WITH SENAI LOGO & METADATA WIDGETS
            Border headerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(24, 16, 24, 16),
                Margin = new Thickness(0, 0, 0, 20)
            };

            Grid headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // SENAI Institutional Logo Block (Em Destaque Amplo)
            Image logoImage = new Image
            {
                Height = 72,
                Margin = new Thickness(0, 0, 24, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            RenderOptions.SetBitmapScalingMode(logoImage, BitmapScalingMode.HighQuality);

            bool logoLoaded = false;
            try
            {
                // 1. Tentar carregar a partir dos recursos embutidos do assembly
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("logo.png"))
                {
                    if (stream != null)
                    {
                        BitmapImage bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = stream;
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.EndInit();
                        bmp.Freeze();
                        logoImage.Source = bmp;
                        logoLoaded = true;
                    }
                }

                // 2. Fallback: Tentar carregar diretamente do arquivo em disco (logo/logo.png)
                if (!logoLoaded)
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    string[] possiblePaths = new string[]
                    {
                        System.IO.Path.Combine(baseDir, "logo", "logo.png"),
                        System.IO.Path.Combine(baseDir, "logo.png"),
                        @"C:\Program Files\ChecklistLogin\logo\logo.png",
                        @"c:\Users\Porto\Documents\agente cheklist\logo\logo.png"
                    };

                    foreach (var p in possiblePaths)
                    {
                        if (File.Exists(p))
                        {
                            BitmapImage bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(p, UriKind.Absolute);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            logoImage.Source = bmp;
                            logoLoaded = true;
                            break;
                        }
                    }
                }
            }
            catch { }

            if (logoLoaded)
            {
                Grid.SetColumn(logoImage, 0);
                headerGrid.Children.Add(logoImage);
            }
            else
            {
                // Fallback visual de texto institucional SENAI
                Border logoBlock = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A4B9F")),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(20, 8, 20, 8),
                    Margin = new Thickness(0, 0, 20, 0)
                };
                StackPanel logoStack = new StackPanel();
                TextBlock logoText = new TextBlock
                {
                    Text = "SENAI",
                    FontSize = 26,
                    FontWeight = FontWeights.Black,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Border accentBar = new Border
                {
                    Height = 3.5,
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF5E31")),
                    CornerRadius = new CornerRadius(2),
                    Margin = new Thickness(0, 2, 0, 0)
                };
                logoStack.Children.Add(logoText);
                logoStack.Children.Add(accentBar);
                logoBlock.Child = logoStack;
                Grid.SetColumn(logoBlock, 0);
                headerGrid.Children.Add(logoBlock);
            }

            // Title & Subtitle Stack
            StackPanel titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock tagText = new TextBlock
            {
                Text = "SISTEMA DE VERIFICAÇÃO INSTITUCIONAL",
                FontSize = 11,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A4B9F")),
                Margin = new Thickness(0, 0, 0, 2)
            };
            TextBlock mainTitle = new TextBlock
            {
                Text = "Checklist de Equipamentos",
                FontSize = 24,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"))
            };
            titleStack.Children.Add(tagText);
            titleStack.Children.Add(mainTitle);
            Grid.SetColumn(titleStack, 1);

            // Metadata Badges (Location, User, Machine & Progress)
            StackPanel badgesPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            
            // Location Badge
            Border locationBadge = CreateBadge("📍 Local", _config.Location ?? "PORTO", "#EF5E31");
            // User Badge
            Border userBadge = CreateBadge("👤 Usuário", Environment.UserName, "#1A4B9F");
            // Machine Badge
            Border pcBadge = CreateBadge("💻 Computador", Environment.MachineName, "#0091D6");

            // Progress Badge
            Border progressBadge = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A4B9F")),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(8, 0, 0, 0)
            };
            StackPanel progStack = new StackPanel();
            progStack.Children.Add(new TextBlock { Text = "PROGRESSO", FontSize = 9, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#93C5FD")) });
            _progressText = new TextBlock { Text = "0 de 0 (0%)", FontSize = 13, FontWeight = FontWeights.Black, Foreground = Brushes.White };
            progStack.Children.Add(_progressText);
            progressBadge.Child = progStack;

            badgesPanel.Children.Add(locationBadge);
            badgesPanel.Children.Add(userBadge);
            badgesPanel.Children.Add(pcBadge);
            badgesPanel.Children.Add(progressBadge);
            Grid.SetColumn(badgesPanel, 2);

            headerGrid.Children.Add(titleStack);
            headerGrid.Children.Add(badgesPanel);

            // Progress Bar Line at bottom of Header
            Grid headerFullGrid = new Grid();
            headerFullGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            headerFullGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetRow(headerGrid, 0);
            headerFullGrid.Children.Add(headerGrid);

            Border progressTrack = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(0, 12, 0, 0)
            };
            _progressBarFill = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A4B9F")),
                Height = 6,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            progressTrack.Child = _progressBarFill;
            Grid.SetRow(progressTrack, 1);
            headerFullGrid.Children.Add(progressTrack);

            headerBorder.Child = headerFullGrid;
            Grid.SetRow(headerBorder, 0);
            mainGrid.Children.Add(headerBorder);

            // 2. CHECKLIST ITEMS GRID (100% Responsive Screen Coverage)
            ScrollViewer scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Margin = new Thickness(0, 0, 0, 20)
            };

            WrapPanel cardsPanel = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            bool isNotebook = (_config.Location ?? "").ToUpperInvariant().Contains("NOTEBOOK");

            AddQuestion(cardsPanel, "tela", "Tela / Monitor com exibição perfeita?", "DISPLAY & IMAGEM");
            AddQuestion(cardsPanel, "teclado", "Teclado completo e com todas as teclas funcionando?", "PERIFÉRICOS DE ENTRADA");

            if (isNotebook)
            {
                AddQuestion(cardsPanel, "touchpad", "Touch Pad / Mouse com cliques e navegação operacionais?", "PERIFÉRICOS DE ENTRADA");
                AddQuestion(cardsPanel, "internet", "Conexão com a Internet / Rede SENAI ativa?", "CONECTIVIDADE");
            }
            else
            {
                AddQuestion(cardsPanel, "mouse", "Mouse óptico com cliques e scroll operacionais?", "PERIFÉRICOS DE ENTRADA");
                AddQuestion(cardsPanel, "internet", "Conexão com a Internet / Rede SENAI ativa?", "CONECTIVIDADE");
                AddQuestion(cardsPanel, "computador", "Gabinete / Computador liga sem ruídos ou lentidão?", "HARDWARE PRINCIPAL");
            }

            scrollViewer.Content = cardsPanel;
            Grid.SetRow(scrollViewer, 1);
            mainGrid.Children.Add(scrollViewer);

            // 3. FOOTER & INTERACTIVE HOVER BUTTON
            Border footerBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderThickness = new Thickness(1),
                CornerRadius = new RadiusHelper().Radius20,
                Padding = new Thickness(24, 16, 24, 16)
            };

            Grid footerGrid = new Grid();
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel infoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            infoStack.Children.Add(new TextBlock
            {
                Text = "Serviço Nacional de Aprendizagem Industrial — ",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                FontWeight = FontWeights.Medium
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = "SENAI",
                FontSize = 13,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A4B9F")),
                FontWeight = FontWeights.Black
            });

            _btnSubmit = new Button
            {
                Content = "Concluir Checklist",
                Height = 50,
                Width = 260,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                IsEnabled = false,
                Cursor = Cursors.No
            };
            UpdateSubmitButtonStyle();

            _btnSubmit.Click += BtnSubmit_Click;

            Grid.SetColumn(infoStack, 0);
            Grid.SetColumn(_btnSubmit, 1);
            footerGrid.Children.Add(infoStack);
            footerGrid.Children.Add(_btnSubmit);

            footerBorder.Child = footerGrid;
            Grid.SetRow(footerBorder, 2);
            mainGrid.Children.Add(footerBorder);

            Content = mainGrid;
        }

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

        private void AddQuestion(WrapPanel container, string id, string title, string category)
        {
            var q = new ChecklistQuestion
            {
                Id = id,
                Title = title,
                Category = category
            };

            // Card Container
            q.CardBorder = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(20),
                Margin = new Thickness(10),
                Width = 420
            };

            StackPanel cardStack = new StackPanel();

            // Category Tag
            TextBlock catText = new TextBlock
            {
                Text = category,
                FontSize = 10,
                FontWeight = FontWeights.Black,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#164194")),
                Margin = new Thickness(0, 0, 0, 4)
            };
            cardStack.Children.Add(catText);

            // Title
            TextBlock titleText = new TextBlock
            {
                Text = title,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            cardStack.Children.Add(titleText);

            // SIM / NÃO Custom Touch Buttons
            Grid btnGrid = new Grid();
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            btnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            q.BtnSim = new Button
            {
                Content = "✔  SIM",
                Height = 46,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            StyleButtonUnselected(q.BtnSim, "#F0F9FF", "#0091D6", "#BAE6FD");

            q.BtnNao = new Button
            {
                Content = "✖  NÃO",
                Height = 46,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand
            };
            StyleButtonUnselected(q.BtnNao, "#FFF7ED", "#EF5E31", "#FFEDD5");

            Grid.SetColumn(q.BtnSim, 0);
            Grid.SetColumn(q.BtnNao, 1);
            btnGrid.Children.Add(q.BtnSim);
            btnGrid.Children.Add(q.BtnNao);
            cardStack.Children.Add(btnGrid);

            // Dynamic Problem Description Textarea
            q.DescPanel = new StackPanel
            {
                Visibility = Visibility.Collapsed,
                Margin = new Thickness(0, 14, 0, 0)
            };

            TextBlock label = new TextBlock
            {
                Text = "Descreva detalhadamente o problema (Obrigatório):",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF5E31")),
                Margin = new Thickness(0, 0, 0, 6)
            };

            q.DescTextBox = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                MinHeight = 54,
                Padding = new Thickness(10),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EF5E31")),
                BorderThickness = new Thickness(1.5),
                FontSize = 13
            };

            q.DescTextBox.TextChanged += (s, e) =>
            {
                q.ProblemDescription = q.DescTextBox.Text;
                ValidateForm();
            };

            q.DescPanel.Children.Add(label);
            q.DescPanel.Children.Add(q.DescTextBox);
            cardStack.Children.Add(q.DescPanel);

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

            q.CardBorder.Child = cardStack;
            container.Children.Add(q.CardBorder);
            _questions.Add(q);
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
                string location = _config?.Location ?? "PORTO";
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
