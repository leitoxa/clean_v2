/*
 * File Cleanup Manager - Multi-Tab Version
 * 
 * Приложение для автоматической очистки старых файлов с графическим интерфейсом,
 * поддержкой нескольких папок через вкладки и Windows Service.
 * 
 * Author: Serik Muftakhidinov
 * Created: 30.11.2025
 * Version: 2.0.0
 * 
 * Developed with AI assistance from Google Deepmind (Gemini 2.0)
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Windows.Forms;
using System.Configuration.Install;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.Threading;
using System.Web.Script.Serialization;
using Microsoft.VisualBasic.FileIO;
using System.Net;
using System.Collections.Specialized;

[assembly: AssemblyVersion("2.0.0.0")]
[assembly: AssemblyFileVersion("2.0.0.0")]
[assembly: AssemblyTitle("File Cleanup Manager Multi-Tab")]
[assembly: AssemblyDescription("Автоматическая очистка файлов для нескольких папок")]
[assembly: AssemblyCompany("Serik Muftakhidinov")]
[assembly: AssemblyProduct("File Cleanup Manager")]
[assembly: AssemblyCopyright("Copyright © 2025")]

// Конфигурация приложения
public class AppConfig
{
    public List<FolderConfig> Folders { get; set; }
    public string TelegramBotToken { get; set; }
    public string TelegramChatId { get; set; }
    public int IntervalMinutes { get; set; }
    public bool DetailedLog { get; set; }
    
    // Старые поля для миграции
    public string FolderPath { get; set; }
    public int DaysOld { get; set; }
    public bool Recursive { get; set; }
    public bool UseRecycleBin { get; set; }
    public List<string> FileExtensions { get; set; }

    public AppConfig()
    {
        Folders = new List<FolderConfig>();
        TelegramBotToken = "";
        TelegramChatId = "";
        IntervalMinutes = 60;
        DetailedLog = false;
        
        // Инициализация старых полей
        FolderPath = "";
        DaysOld = 7;
        Recursive = false;
        UseRecycleBin = true;
        FileExtensions = new List<string>();
    }
}

// Главный класс
static class Program
{
    public static AppConfig Config = new AppConfig();
    public static string AppDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FileCleanupManager");
    public static string ConfigPath = Path.Combine(AppDataPath, "config.json");
    public static string LogPath = Path.Combine(AppDataPath, "cleanup.log");

    [STAThread]
    static void Main(string[] args)
    {
        LoadConfig();

        // Если запущено как служба (не интерактивно) ИЛИ передан флаг /service
        if (!Environment.UserInteractive || (args.Length > 0 && args[0] == "/service"))
        {
            try
            {
                // Устанавливаем рабочую директорию для службы
                System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
                ServiceBase.Run(new CleanupService());
            }
            catch (Exception ex)
            {
                Log("Critical Service Error: " + ex.Message, "ERROR");
            }
            return;
        }

        if (args.Length > 0)
        {
            if (args[0] == "/install")
            {
                InstallService();
                return;
            }
            if (args[0] == "/uninstall")
            {
                UninstallService();
                return;
            }
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }

    public static void LoadConfig()
    {
        try
        {
            if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);

            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                var serializer = new JavaScriptSerializer();
                Config = serializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                
                // Миграция
                if (Config.Folders == null || Config.Folders.Count == 0)
                {
                    if (!string.IsNullOrEmpty(Config.FolderPath))
                    {
                        Log("Миграция старого конфига...", "INFO");
                        var oldFolder = new FolderConfig();
                        oldFolder.TabName = "Папка 1";
                        oldFolder.FolderPath = Config.FolderPath;
                        oldFolder.DaysOld = Config.DaysOld;
                        oldFolder.Recursive = Config.Recursive;
                        oldFolder.UseRecycleBin = Config.UseRecycleBin;
                        oldFolder.FileExtensions = Config.FileExtensions ?? new List<string>();
                        oldFolder.Enabled = true;
                        
                        Config.Folders = new List<FolderConfig>();
                        Config.Folders.Add(oldFolder);
                        SaveConfig();
                    }
                    else
                    {
                        Config.Folders = new List<FolderConfig>();
                    }
                }
            }
            else
            {
                Config.Folders = new List<FolderConfig>();
                Config.Folders.Add(new FolderConfig("Папка 1", ""));
            }
        }
        catch (Exception ex)
        {
            Log("Ошибка загрузки конфига: " + ex.Message, "ERROR");
            Config.Folders = new List<FolderConfig>();
            Config.Folders.Add(new FolderConfig("Папка 1", ""));
        }
    }

    public static void SaveConfig()
    {
        try
        {
            if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);
            var serializer = new JavaScriptSerializer();
            string json = serializer.Serialize(Config);
            File.WriteAllText(ConfigPath, json);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка сохранения конфига: " + ex.Message);
        }
    }

    public static void Log(string message, string type)
    {
        try
        {
            if (!Directory.Exists(AppDataPath)) Directory.CreateDirectory(AppDataPath);
            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}{3}", DateTime.Now, type, message, Environment.NewLine);
            File.AppendAllText(LogPath, line);
        }
        catch { }
    }

    public static void SendTelegramNotification(string message)
    {
        string token = Config.TelegramBotToken;
        string chatId = Config.TelegramChatId;
        
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(chatId)) return;
        
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            using (var client = new WebClient())
            {
                var values = new NameValueCollection();
                values["chat_id"] = chatId;
                values["text"] = message;
                values["parse_mode"] = "Markdown";
                client.UploadValues(string.Format("https://api.telegram.org/bot{0}/sendMessage", token), values);
            }
        }
        catch (Exception ex)
        {
            Log("Ошибка Telegram: " + ex.Message, "WARN");
        }
    }

    static void InstallService()
    {
        try
        {
            ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
            MessageBox.Show("Служба установлена!", "Успех");
        }
        catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
    }

    static void UninstallService()
    {
        try
        {
            ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
            MessageBox.Show("Служба удалена!", "Успех");
        }
        catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
    }
}

// Служба
public class CleanupService : ServiceBase
{
    private System.Threading.Timer timer;
    public CleanupService() { this.ServiceName = "FileCleanupService"; }

    protected override void OnStart(string[] args)
    {
        // DEBUG LOGGING
        try { File.AppendAllText(@"c:\tmp\service_debug.log", DateTime.Now + " Service OnStart called\n"); } catch {}

        try
        {
            // Устанавливаем рабочую директорию явно
            System.IO.Directory.SetCurrentDirectory(System.AppDomain.CurrentDomain.BaseDirectory);
            try { File.AppendAllText(@"c:\tmp\service_debug.log", DateTime.Now + " WorkDir set: " + System.AppDomain.CurrentDomain.BaseDirectory + "\n"); } catch {}

            Program.Log("Служба запущена", "SERVICE");
            Program.LoadConfig();
            
            int folderCount = 0;
            if (Program.Config.Folders != null) folderCount = Program.Config.Folders.Count;
            try { File.AppendAllText(@"c:\tmp\service_debug.log", DateTime.Now + " Config loaded. Folders: " + folderCount + "\n"); } catch {}
            
            // ВРЕМЕННО: Убрали жесткие проверки, чтобы служба не падала сразу
            // Просто логируем предупреждения
            if (Program.Config.Folders == null || Program.Config.Folders.Count == 0)
            {
                Program.Log("ПРЕДУПРЕЖДЕНИЕ: Нет настроенных папок.", "WARN");
                try { File.AppendAllText(@"c:\tmp\service_debug.log", DateTime.Now + " WARN: No folders configured\n"); } catch {}
            }
            
            int interval = Program.Config.IntervalMinutes * 60 * 1000;
            if (interval <= 0) interval = 3600000;

            timer = new System.Threading.Timer(DoCleanup, null, 0, interval);
            Program.Log("Таймер запущен, интервал: " + interval, "INFO");
            try { File.AppendAllText(@"c:\tmp\service_debug.log", DateTime.Now + " Timer started\n"); } catch {}
        }
        catch (Exception ex)
        {
            Program.Log("Критическая ошибка запуска службы: " + ex.ToString(), "ERROR");
            try { File.AppendAllText(@"c:\tmp\service_debug.log", DateTime.Now + " ERROR: " + ex.ToString() + "\n"); } catch {}
            // Не пробрасываем исключение, чтобы служба не упала и мы могли прочитать лог
            // throw; 
        }
    }

    protected override void OnStop()
    {
        Program.Log("Служба остановлена", "SERVICE");
        Program.SendTelegramNotification("🛑 Служба остановлена");
        if (timer != null) timer.Dispose();
    }

    private void DoCleanup(object state)
    {
        Program.LoadConfig();
        Cleaner.RunCleanupAll();
    }
}

// Установщик
[RunInstaller(true)]
public class MyServiceInstaller : Installer
{
    public MyServiceInstaller()
    {
        var processInstaller = new ServiceProcessInstaller();
        var serviceInstaller = new ServiceInstaller();
        processInstaller.Account = ServiceAccount.LocalSystem;
        serviceInstaller.DisplayName = "File Cleanup Manager";
        serviceInstaller.StartType = ServiceStartMode.Automatic;
        serviceInstaller.ServiceName = "FileCleanupService";
        Installers.Add(processInstaller);
        Installers.Add(serviceInstaller);
    }
}

// Очистка
public static class Cleaner
{
    public static void RunCleanupAll()
    {
        if (Program.Config.Folders == null) return;
        int totalDeleted = 0;
        var results = new List<string>();

        foreach (var folder in Program.Config.Folders)
        {
            if (!folder.Enabled) continue;
            int deleted = RunCleanup(folder);
            totalDeleted += deleted;
            if (deleted > 0) results.Add(string.Format("📂 *{0}*: {1}", folder.TabName, deleted));
        }

        if (totalDeleted > 0)
        {
            Program.SendTelegramNotification("🧹 *Очистка*\n" + string.Join("\n", results.ToArray()));
        }
    }

    public static int RunCleanup(FolderConfig folder)
    {
        if (string.IsNullOrEmpty(folder.FolderPath) || !Directory.Exists(folder.FolderPath)) return 0;

        Program.Log("Очистка: " + folder.TabName, "INFO");
        int deletedCount = 0;
        DateTime threshold = DateTime.Now.AddDays(-folder.DaysOld);

        try
        {
            var searchOption = folder.Recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(folder.FolderPath, "*.*", searchOption);

            foreach (var file in files)
            {
                try
                {
                    if (folder.FileExtensions != null && folder.FileExtensions.Count > 0)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (!folder.FileExtensions.Contains(ext)) continue;
                    }

                    if (File.GetLastWriteTime(file) < threshold)
                    {
                        if (folder.UseRecycleBin)
                             FileSystem.DeleteFile(file, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        else
                            File.Delete(file);
                        
                        deletedCount++;
                        Program.Log("Удален: " + file, "INFO");
                    }
                }
                catch (Exception ex) { Program.Log("Ошибка файла " + file + ": " + ex.Message, "ERROR"); }
            }
        }
        catch (Exception ex) { Program.Log("Ошибка папки " + folder.TabName + ": " + ex.Message, "ERROR"); }

        return deletedCount;
    }
}

// GUI
public class MainForm : Form
{
    private TabControl tabFolders;
    private Button btnAddTab;
    private Button btnRemoveTab;
    
    private CheckBox chkDetailedLog;
    private NumericUpDown numInterval;
    private TextBox txtLog;
    private Label lblServiceStatus;

    public MainForm()
    {
        InitializeComponent();
        LoadSettings();
        UpdateServiceStatus();
    }

    private void InitializeComponent()
    {
        this.Text = "File Cleanup Manager v2.0 (Multi-Tab)";
        this.Size = new Size(650, 750);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(30, 30, 46);
        this.ForeColor = Color.White;
        this.Font = new Font("Segoe UI", 9);

        var lblTitle = new Label();
        lblTitle.Text = "File Cleanup Manager";
        lblTitle.Location = new Point(20, 20);
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(187, 134, 252);
        this.Controls.Add(lblTitle);

        // Вкладки
        tabFolders = new TabControl();
        tabFolders.Location = new Point(20, 60);
        tabFolders.Size = new Size(600, 350);
        this.Controls.Add(tabFolders);

        btnAddTab = CreateButton("+", 530, 30, 40, 25, Color.FromArgb(76, 175, 80));
        btnAddTab.Click += AddTabClick;
        this.Controls.Add(btnAddTab);

        btnRemoveTab = CreateButton("-", 580, 30, 40, 25, Color.FromArgb(244, 67, 54));
        btnRemoveTab.Click += RemoveTabClick;
        this.Controls.Add(btnRemoveTab);

        int y = 420;

        // Глобальные настройки
        AddLabel("Интервал (мин):", 20, y);
        numInterval = new NumericUpDown();
        numInterval.Location = new Point(120, y);
        numInterval.Width = 80;
        numInterval.Minimum = 1;
        numInterval.Maximum = 10000;
        this.Controls.Add(numInterval);

        chkDetailedLog = new CheckBox();
        chkDetailedLog.Text = "Подробный лог";
        chkDetailedLog.Location = new Point(220, y);
        chkDetailedLog.AutoSize = true;
        this.Controls.Add(chkDetailedLog);

        y += 40;

        // Кнопки действий
        var btnSave = CreateButton("Сохранить все", 20, y, 120, 35, Color.FromArgb(76, 175, 80));
        btnSave.Click += SaveClick;
        this.Controls.Add(btnSave);

        var btnTelegram = CreateButton("🔔 Telegram", 150, y, 120, 35, Color.FromArgb(14, 165, 233));
        btnTelegram.Click += TelegramClick;
        this.Controls.Add(btnTelegram);

        var btnTest = CreateButton("Тест текущей", 280, y, 120, 35, Color.FromArgb(255, 152, 0));
        btnTest.Click += TestClick;
        this.Controls.Add(btnTest);
        
        var btnAbout = CreateButton("ℹ О программе", 410, y, 120, 35, Color.FromArgb(103, 58, 183));
        btnAbout.Click += AboutClick;
        this.Controls.Add(btnAbout);

        y += 50;

        // Служба
        var pnlService = new Panel();
        pnlService.Location = new Point(20, y);
        pnlService.Size = new Size(600, 80);
        pnlService.BackColor = Color.FromArgb(45, 45, 68);
        this.Controls.Add(pnlService);

        var lblSvc = new Label { Text = "Служба:", Location = new Point(10, 10), AutoSize = true, ForeColor = Color.LightGray };
        pnlService.Controls.Add(lblSvc);

        lblServiceStatus = new Label { Text = "...", Location = new Point(70, 10), AutoSize = true, ForeColor = Color.White };
        pnlService.Controls.Add(lblServiceStatus);

        var btnInstall = CreateButton("Установить", 10, 40, 100, 30, Color.FromArgb(63, 81, 181));
        btnInstall.Click += (s, e) => { if (CheckConfig()) RunAsAdmin("/install"); };
        pnlService.Controls.Add(btnInstall);

        var btnStart = CreateButton("Запустить", 120, 40, 100, 30, Color.FromArgb(76, 175, 80));
        btnStart.Click += (s, e) => { if (CheckConfig()) RunCmd("net start FileCleanupService"); };
        pnlService.Controls.Add(btnStart);

        var btnStop = CreateButton("Остановить", 230, 40, 100, 30, Color.FromArgb(255, 152, 0));
        btnStop.Click += (s, e) => RunCmd("net stop FileCleanupService");
        pnlService.Controls.Add(btnStop);

        var btnUninst = CreateButton("Удалить", 340, 40, 100, 30, Color.FromArgb(244, 67, 54));
        btnUninst.Click += (s, e) => RunAsAdmin("/uninstall");
        pnlService.Controls.Add(btnUninst);

        y += 90;

        // Лог
        txtLog = new TextBox();
        txtLog.Location = new Point(20, y);
        txtLog.Size = new Size(600, 150);
        txtLog.Multiline = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.BackColor = Color.FromArgb(45, 45, 68);
        txtLog.ForeColor = Color.LightGray;
        this.Controls.Add(txtLog);

        var timer = new System.Windows.Forms.Timer { Interval = 2000 };
        timer.Tick += (s, e) => UpdateServiceStatus();
        timer.Start();
    }

    private void AddTabClick(object sender, EventArgs e)
    {
        var folder = new FolderConfig { TabName = "Папка " + (tabFolders.TabCount + 1) };
        AddTab(folder);
    }

    private void RemoveTabClick(object sender, EventArgs e)
    {
        if (tabFolders.SelectedTab != null)
        {
            if (MessageBox.Show("Удалить вкладку?", "Подтверждение", MessageBoxButtons.YesNo) == DialogResult.Yes)
                tabFolders.TabPages.Remove(tabFolders.SelectedTab);
        }
    }

    private void AddTab(FolderConfig folder)
    {
        var tab = new TabPage(folder.TabName);
        tab.BackColor = Color.FromArgb(45, 45, 68);
        tab.Tag = folder; // Храним конфиг в теге, но обновлять будем при сохранении

        int y = 20;
        
        // Имя вкладки
        var lblName = new Label { Text = "Имя вкладки:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.LightGray };
        tab.Controls.Add(lblName);
        var txtName = new TextBox { Text = folder.TabName, Location = new Point(150, y), Width = 200 };
        txtName.TextChanged += (s, e) => tab.Text = txtName.Text;
        tab.Controls.Add(txtName);
        var chkEnabled = new CheckBox { Text = "Включено", Checked = folder.Enabled, Location = new Point(400, y), AutoSize = true, ForeColor = Color.White };
        tab.Controls.Add(chkEnabled);
        
        y += 40;

        // Путь
        var lblPath = new Label { Text = "Путь к папке:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.LightGray };
        tab.Controls.Add(lblPath);
        var txtPath = new TextBox { Text = folder.FolderPath, Location = new Point(20, y + 20), Width = 450, Name = "txtPath" };
        tab.Controls.Add(txtPath);
        var btnBrowse = CreateButton("...", 480, y + 19, 40, 23, Color.Gray);
        btnBrowse.Click += (s, e) => {
            using (var fbd = new FolderBrowserDialog()) {
                if (fbd.ShowDialog() == DialogResult.OK) txtPath.Text = fbd.SelectedPath;
            }
        };
        tab.Controls.Add(btnBrowse);

        y += 60;

        // Настройки
        var lblDays = new Label { Text = "Дней хранить:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.LightGray };
        tab.Controls.Add(lblDays);
        var numDays = new NumericUpDown { Value = folder.DaysOld, Location = new Point(120, y), Maximum = 3650, Width = 80 };
        tab.Controls.Add(numDays);

        var chkRec = new CheckBox { Text = "Рекурсивно", Checked = folder.Recursive, Location = new Point(220, y), AutoSize = true, ForeColor = Color.White };
        tab.Controls.Add(chkRec);
        
        var chkBin = new CheckBox { Text = "В корзину", Checked = folder.UseRecycleBin, Location = new Point(350, y), AutoSize = true, ForeColor = Color.White };
        tab.Controls.Add(chkBin);

        y += 50;

        // Расширения
        var lblExt = new Label { Text = "Расширения (через запятую):", Location = new Point(20, y), AutoSize = true, ForeColor = Color.LightGray };
        tab.Controls.Add(lblExt);
        var txtExt = new TextBox { Text = string.Join(", ", folder.FileExtensions), Location = new Point(20, y + 20), Width = 450, Name = "txtExt" };
        tab.Controls.Add(txtExt);
        
        var btnScan = CreateButton("🔍", 480, y + 19, 40, 23, Color.FromArgb(63, 81, 181));
        btnScan.Click += (s, e) => ScanExtensions(txtPath.Text, txtExt, chkRec.Checked);
        tab.Controls.Add(btnScan);

        tabFolders.TabPages.Add(tab);
    }

    private void LoadSettings()
    {
        Program.LoadConfig();
        tabFolders.TabPages.Clear();
        
        foreach (var folder in Program.Config.Folders)
        {
            AddTab(folder);
        }
        
        numInterval.Value = Program.Config.IntervalMinutes;
        chkDetailedLog.Checked = Program.Config.DetailedLog;
    }

    private void SaveClick(object sender, EventArgs e)
    {
        Program.Config.Folders.Clear();
        
        foreach (TabPage tab in tabFolders.TabPages)
        {
            var folder = new FolderConfig();
            folder.TabName = tab.Text;
            
            // Получаем контролы
            foreach (Control c in tab.Controls)
            {
                if (c is TextBox && c.Name == "txtPath") folder.FolderPath = c.Text;
                if (c is NumericUpDown) folder.DaysOld = (int)((NumericUpDown)c).Value;
                if (c is CheckBox && c.Text == "Рекурсивно") folder.Recursive = ((CheckBox)c).Checked;
                if (c is CheckBox && c.Text == "В корзину") folder.UseRecycleBin = ((CheckBox)c).Checked;
                if (c is CheckBox && c.Text == "Включено") folder.Enabled = ((CheckBox)c).Checked;
                if (c is TextBox && c.Name == "txtExt") 
                    folder.FileExtensions = new List<string>(c.Text.Split(new[]{','}, StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Trim()));
            }
            Program.Config.Folders.Add(folder);
        }
        
        Program.Config.IntervalMinutes = (int)numInterval.Value;
        Program.Config.DetailedLog = chkDetailedLog.Checked;
        
        Program.SaveConfig();
        MessageBox.Show("Настройки сохранены!");
    }

    private void ScanExtensions(string path, TextBox txtTarget, bool recursive)
    {
        if (!Directory.Exists(path)) { MessageBox.Show("Папка не найдена"); return; }
        try {
            var exts = new HashSet<string>();
            var opt = recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            foreach (var f in Directory.GetFiles(path, "*.*", opt))
            {
                string ext = Path.GetExtension(f).ToLower();
                if (!string.IsNullOrEmpty(ext)) exts.Add(ext);
            }
            if (exts.Count > 0) txtTarget.Text = string.Join(", ", exts);
            else MessageBox.Show("Файлов не найдено");
        } catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
    }

    private void TelegramClick(object sender, EventArgs e)
    {
        Form tgForm = new Form();
        tgForm.Text = "Настройка Telegram";
        tgForm.Size = new Size(400, 250);
        tgForm.StartPosition = FormStartPosition.CenterParent;
        tgForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        tgForm.MaximizeBox = false;
        tgForm.MinimizeBox = false;
        tgForm.BackColor = Color.FromArgb(30, 30, 46);
        tgForm.ForeColor = Color.White;

        int y = 20;
        
        var lblToken = new Label { Text = "Bot Token:", Location = new Point(20, y), AutoSize = true, ForeColor = Color.LightGray };
        tgForm.Controls.Add(lblToken);
        
        var txtToken = new TextBox { Text = Program.Config.TelegramBotToken, Location = new Point(20, y + 25), Width = 340 };
        tgForm.Controls.Add(txtToken);
        y += 60;
        
        var lblChatId = new Label { Text = "Chat ID (ID группы/канала):", Location = new Point(20, y), AutoSize = true, ForeColor = Color.LightGray };
        tgForm.Controls.Add(lblChatId);
        
        var txtChatId = new TextBox { Text = Program.Config.TelegramChatId, Location = new Point(20, y + 25), Width = 340 };
        tgForm.Controls.Add(txtChatId);
        y += 60;
        
        var btnSaveTg = new Button { Text = "Сохранить", Location = new Point(20, y), Size = new Size(100, 30), BackColor = Color.FromArgb(76, 175, 80), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        btnSaveTg.Click += (s, ev) => {
            Program.Config.TelegramBotToken = txtToken.Text;
            Program.Config.TelegramChatId = txtChatId.Text;
            Program.SaveConfig();
            MessageBox.Show("Настройки Telegram сохранены!", "Успех");
            tgForm.Close();
        };
        tgForm.Controls.Add(btnSaveTg);
        
        var btnTestTg = new Button { Text = "Тест", Location = new Point(130, y), Size = new Size(100, 30), BackColor = Color.FromArgb(14, 165, 233), FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        btnTestTg.Click += (s, ev) => {
            Program.Config.TelegramBotToken = txtToken.Text;
            Program.Config.TelegramChatId = txtChatId.Text;
            try 
            {
                Program.SendTelegramNotification("✅ Тестовое сообщение от File Cleanup Manager v2.0");
                MessageBox.Show("Тестовое сообщение отправлено!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка отправки:\n" + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        tgForm.Controls.Add(btnTestTg);
        
        tgForm.ShowDialog();
    }

    private void TestClick(object sender, EventArgs e)
    {
        SaveClick(null, null);
        if (tabFolders.SelectedTab != null)
        {
            var folder = Program.Config.Folders.FirstOrDefault(f => f.TabName == tabFolders.SelectedTab.Text);
            if (folder != null)
            {
                int deleted = Cleaner.RunCleanup(folder);
                MessageBox.Show("Тест завершен. Удалено: " + deleted);
            }
        }
    }

    private bool CheckConfig()
    {
        SaveClick(null, null);
        if (Program.Config.Folders.Count == 0) { MessageBox.Show("Добавьте папки!"); return false; }
        return true;
    }

    private void UpdateServiceStatus()
    {
        try {
            using (var sc = new ServiceController("FileCleanupService")) {
                lblServiceStatus.Text = sc.Status.ToString();
                lblServiceStatus.ForeColor = sc.Status == ServiceControllerStatus.Running ? Color.LightGreen : Color.White;
            }
        } catch { lblServiceStatus.Text = "Не установлена"; lblServiceStatus.ForeColor = Color.Gray; }
    }

    private void RunCmd(string cmd)
    {
        try {
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + cmd) { Verb = "runas", CreateNoWindow = true, UseShellExecute = true });
        } catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
    }
    
    private void RunAsAdmin(string arg)
    {
        try {
            Process.Start(new ProcessStartInfo(Application.ExecutablePath, arg) { Verb = "runas" });
        } catch (Exception ex) { MessageBox.Show("Ошибка: " + ex.Message); }
    }

    private void AboutClick(object sender, EventArgs e)
    {
        string about = "File Cleanup Manager v2.0 (Multi-Tab)\n\n" +
                       "Автоматическая очистка старых файлов\n" +
                       "с поддержкой нескольких папок через вкладки\n\n" +
                       "Автор: Serik Muftakhidinov\n" +
                       "Дата создания: 30.11.2025\n" +
                       "Версия: 2.0.0\n\n" +
                       "Developed with AI assistance\n" +
                       "from Google Deepmind (Gemini 2.0)\n\n" +
                       "© 2025 File Cleanup Manager";
        
        MessageBox.Show(about, "О программе", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void AddLabel(string text, int x, int y)
    {
        this.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Color.LightGray });
    }

    private Button CreateButton(string text, int x, int y, int w, int h, Color bg)
    {
        var btn = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), BackColor = bg, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }
}
