/*
 * File Cleanup Manager - Native Windows Application
 * 
 * Приложение для автоматической очистки старых файлов с графическим интерфейсом
 * и поддержкой Windows Service.
 * 
 * Author: Serik Muftakhidinov
 * Created: 29.11.2025
 * Version: 1.2.5
 * 
 * Developed with AI assistance from Google Deepmind (Gemini 2.0)
 * 
 * Features:
 * - Modern Dark-themed GUI
 * - Windows Service support
 * - File filtering by extensions
 * - Recursive search
 * - Configurable retention period
 * - Automatic cleanup scheduling
 * - Operation logging
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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

// Для безопасного удаления через корзину
using Microsoft.VisualBasic.FileIO;
using System.Net;
using System.Collections.Specialized;

[assembly: AssemblyVersion("1.2.5.0")]
[assembly: AssemblyFileVersion("1.2.5.0")]
[assembly: AssemblyTitle("File Cleanup Manager")]
[assembly: AssemblyDescription("Автоматическая очистка файлов с уведомлениями в Telegram")]
[assembly: AssemblyCompany("Serik Muftakhidinov")]
[assembly: AssemblyProduct("File Cleanup Manager")]
[assembly: AssemblyCopyright("Copyright © 2025")]

// Конфигурация
public class AppConfig
{
    public string FolderPath { get; set; }
    public int DaysOld { get; set; }
    public bool Recursive { get; set; }
    public bool UseRecycleBin { get; set; }
    public bool DetailedLog { get; set; }
    public int IntervalMinutes { get; set; }
    public List<string> FileExtensions { get; set; }
    public string TelegramBotToken { get; set; }
    public string TelegramChatId { get; set; }

    public AppConfig()
    {
        FolderPath = "";
        DaysOld = 7;
        Recursive = false;
        UseRecycleBin = true;
        DetailedLog = false;
        IntervalMinutes = 60;
        FileExtensions = new List<string>();
        TelegramBotToken = "";
        TelegramChatId = "";
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

        if (args.Length > 0)
        {
            if (args[0] == "/service")
            {
                ServiceBase.Run(new CleanupService());
                return;
            }
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
                if (Config.DetailedLog) Log("Конфигурация загружена: " + json, "DEBUG");
            }
        }
        catch (Exception ex)
        {
            Log("Ошибка загрузки конфига: " + ex.Message, "ERROR");
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
            if (!Directory.Exists(AppDataPath))
            {
                try { Directory.CreateDirectory(AppDataPath); }
                catch { return; }
            }

            string line = string.Format("{0:yyyy-MM-dd HH:mm:ss} [{1}] {2}{3}", 
                DateTime.Now, type, message, Environment.NewLine);
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
            // Fix for Windows Server 2012 / Windows 7 (TLS 1.2)
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            
            Log("SecurityProtocol: " + ServicePointManager.SecurityProtocol.ToString(), "DEBUG");

            try 
            {
                var hostEntry = Dns.GetHostEntry("api.telegram.org");
                if (hostEntry.AddressList.Length > 0)
                {
                    Log("DNS Resolved api.telegram.org: " + hostEntry.AddressList[0].ToString(), "DEBUG");
                }
            }
            catch (Exception dnsEx)
            {
                Log("DNS Error: " + dnsEx.Message, "ERROR");
                throw new Exception("DNS Resolution Failed: " + dnsEx.Message);
            }

            using (var client = new WebClient())
            {
                var values = new NameValueCollection();
                values["chat_id"] = chatId;
                values["text"] = message;
                values["parse_mode"] = "Markdown";
                
                Log("Sending request to Telegram API...", "DEBUG");
                byte[] response = client.UploadValues(string.Format("https://api.telegram.org/bot{0}/sendMessage", token), values);
                string responseString = Encoding.UTF8.GetString(response);
                Log("Telegram response: " + responseString, "INFO");
            }
        }
        catch (WebException webEx)
        {
            string responseText = "";
            if (webEx.Response != null)
            {
                using (var reader = new StreamReader(webEx.Response.GetResponseStream()))
                {
                    responseText = reader.ReadToEnd();
                }
            }
            string err = string.Format("WebException: {0}\nResponse: {1}\nProtocol: {2}", 
                webEx.Message, responseText, ServicePointManager.SecurityProtocol);
            Log(err, "ERROR");
            throw new Exception(err);
        }
        catch (Exception ex)
        {
            Log("General Error: " + ex.ToString(), "ERROR");
            throw;
        }
    }

    static void InstallService()
    {
        try
        {
            ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
            MessageBox.Show("Служба успешно установлена!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка установки службы: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    static void UninstallService()
    {
        try
        {
            ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
            MessageBox.Show("Служба успешно удалена!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка удаления службы: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

// Служба
public class CleanupService : ServiceBase
{
    private System.Threading.Timer timer;

    public CleanupService()
    {
        this.ServiceName = "FileCleanupService";
    }

    protected override void OnStart(string[] args)
    {
        try
        {
            Program.Log("Служба запущена", "SERVICE");
            Program.LoadConfig();
            
            // Проверка настроек
            if (string.IsNullOrEmpty(Program.Config.FolderPath))
            {
                Program.Log("ОШИБКА: Не указана папка для очистки! Настройте приложение перед запуском службы.", "ERROR");
                throw new Exception("Папка для очистки не настроена. Откройте приложение и укажите папку.");
            }
            
            if (!Directory.Exists(Program.Config.FolderPath))
            {
                Program.Log("ОШИБКА: Папка не существует: " + Program.Config.FolderPath, "ERROR");
                throw new Exception("Указанная папка не существует: " + Program.Config.FolderPath);
            }
            
            Program.Log("Конфигурация проверена: " + Program.Config.FolderPath, "INFO");
            
            try
            {
                Program.SendTelegramNotification("✅ Служба File Cleanup Service запущена");
            }
            catch (Exception ex)
            {
                Program.Log("Ошибка отправки Telegram уведомления: " + ex.Message, "WARN");
            }
            
            int interval = Program.Config.IntervalMinutes * 60 * 1000;
            if (interval <= 0) interval = 3600000;

            timer = new System.Threading.Timer(DoCleanup, null, 0, interval);
            Program.Log("Служба успешно запущена, интервал: " + interval + "мс", "INFO");
        }
        catch (Exception ex)
        {
            Program.Log("Критическая ошибка запуска службы: " + ex.ToString(), "ERROR");
            throw;
        }
    }

    protected override void OnStop()
    {
        try
        {
            Program.Log("Служба остановлена", "SERVICE");
            try
            {
                Program.SendTelegramNotification("🛑 Служба File Cleanup Service остановлена");
            }
            catch (Exception ex)
            {
                Program.Log("Ошибка отправки Telegram уведомления: " + ex.Message, "WARN");
            }
            if (timer != null) timer.Dispose();
        }
        catch (Exception ex)
        {
            Program.Log("Ошибка остановки службы: " + ex.ToString(), "ERROR");
        }
    }

    private void DoCleanup(object state)
    {
        Program.LoadConfig();
        Cleaner.RunCleanup(Program.Config);
    }
}

// Установщик службы
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
        serviceInstaller.Description = "Автоматическая очистка старых файлов";

        Installers.Add(processInstaller);
        Installers.Add(serviceInstaller);
    }
}

// Очистка
public static class Cleaner
{
    public static void RunCleanup(AppConfig config)
    {
        if (string.IsNullOrEmpty(config.FolderPath) || !Directory.Exists(config.FolderPath))
        {
            Program.Log(string.Format("Папка не найдена: {0}", config.FolderPath), "WARN");
            return;
        }

        Program.Log(string.Format("Начало очистки: {0}", config.FolderPath), "INFO");
        int deletedCount = 0;
        DateTime threshold = DateTime.Now.AddDays(-config.DaysOld);

        try
        {
            System.IO.SearchOption searchOption = config.Recursive ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(config.FolderPath, "*.*", searchOption);

            foreach (var file in files)
            {
                try
                {
                    if (config.DetailedLog) Program.Log("Проверка файла: " + file, "DEBUG");

                    // Фильтр расширений
                    if (config.FileExtensions != null && config.FileExtensions.Count > 0)
                    {
                        string ext = Path.GetExtension(file).ToLower();
                        if (!config.FileExtensions.Contains(ext)) 
                        {
                            if (config.DetailedLog) Program.Log("Пропуск (расширение): " + file, "DEBUG");
                            continue;
                        }
                    }

                    DateTime lastWriteTime = File.GetLastWriteTime(file);
                    if (lastWriteTime < threshold)
                    {
                        if (config.UseRecycleBin)
                        {
                             FileSystem.DeleteFile(file, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                        }
                        else
                        {
                            File.Delete(file);
                        }
                        
                        deletedCount++;
                        Program.Log(string.Format("Удален файл: {0}", file), "INFO");
                    }
                    else
                    {
                        if (config.DetailedLog) Program.Log("Пропуск (слишком новый): " + file, "DEBUG");
                    }
                }
                catch (Exception ex)
                {
                    Program.Log(string.Format("Ошибка удаления файла {0}: {1}", file, ex.Message), "ERROR");
                }
            }
        }
        catch (Exception ex)
        {
            Program.Log(string.Format("Критическая ошибка сканирования: {0}", ex.Message), "ERROR");
        }

        Program.Log(string.Format("Очистка завершена. Удалено файлов: {0}", deletedCount), "INFO");
        
        if (deletedCount > 0)
        {
            string msg = string.Format("🧹 *Очистка завершена*\n📂 Папка: `{0}`\n🗑️ Удалено файлов: *{1}*", config.FolderPath, deletedCount);
            Program.SendTelegramNotification(msg);
        }
    }
}

// GUI
public class MainForm : Form
{
    private TextBox txtFolder;
    private NumericUpDown numDays;
    private CheckBox chkRecursive;
    private CheckBox chkRecycleBin;
    private CheckBox chkDetailedLog;
    private NumericUpDown numInterval;
    private TextBox txtExtensions;
    private TextBox txtLog;
    private Label lblServiceStatus;
    private Button btnInstallService;
    private Button btnStartService;
    private Button btnStopService;
    private Button btnUninstallService;

    public MainForm()
    {
        InitializeComponent();
        LoadSettings();
        UpdateServiceStatus();
    }

    private void InitializeComponent()
    {
        this.Text = "File Cleanup Manager (C# Native)";
        this.Size = new Size(600, 700);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(30, 30, 46);
        this.ForeColor = Color.White;
        this.Font = new Font("Segoe UI", 9);

        var titleFont = new Font("Segoe UI", 14, FontStyle.Bold);
        var labelColor = Color.FromArgb(187, 134, 252);

        var lblTitle = new Label();
        lblTitle.Text = "File Cleanup Manager";
        lblTitle.Location = new Point(20, 20);
        lblTitle.AutoSize = true;
        lblTitle.Font = titleFont;
        lblTitle.ForeColor = labelColor;
        this.Controls.Add(lblTitle);

        int y = 70;

        // Папка
        AddLabel("Папка для очистки:", 20, y);
        txtFolder = new TextBox();
        txtFolder.Location = new Point(20, y + 25);
        txtFolder.Width = 450;
        txtFolder.BackColor = Color.FromArgb(45, 45, 68);
        txtFolder.ForeColor = Color.White;
        txtFolder.BorderStyle = BorderStyle.FixedSingle;
        
        var btnBrowse = CreateButton("...", 480, y + 24, 40, 25, Color.Gray);
        btnBrowse.Click += BrowseFolder;
        
        this.Controls.Add(txtFolder);
        this.Controls.Add(btnBrowse);
        y += 60;

        // Дни
        AddLabel("Удалять старше (дней):", 20, y);
        numDays = new NumericUpDown();
        numDays.Location = new Point(20, y + 25);
        numDays.Width = 100;
        numDays.Minimum = 0;
        numDays.Maximum = 3650;
        numDays.BackColor = Color.FromArgb(45, 45, 68);
        numDays.ForeColor = Color.White;
        this.Controls.Add(numDays);
        
        // Рекурсивно
        chkRecursive = new CheckBox();
        chkRecursive.Text = "Искать во вложенных папках";
        chkRecursive.Location = new Point(150, y + 25);
        chkRecursive.AutoSize = true;
        this.Controls.Add(chkRecursive);

        // Корзина
        chkRecycleBin = new CheckBox();
        chkRecycleBin.Text = "Удалять в корзину";
        chkRecycleBin.Location = new Point(350, y + 25);
        chkRecycleBin.AutoSize = true;
        this.Controls.Add(chkRecycleBin);
        
        // Подробный лог
        chkDetailedLog = new CheckBox();
        chkDetailedLog.Text = "Подробный лог";
        chkDetailedLog.Location = new Point(480, y + 25);
        chkDetailedLog.AutoSize = true;
        this.Controls.Add(chkDetailedLog);
        
        y += 60;

        // Расширения
        AddLabel("Фильтр расширений (через запятую):", 20, y);
        txtExtensions = new TextBox();
        txtExtensions.Location = new Point(20, y + 25);
        txtExtensions.Width = 450;
        txtExtensions.BackColor = Color.FromArgb(45, 45, 68);
        txtExtensions.ForeColor = Color.White;
        txtExtensions.BorderStyle = BorderStyle.FixedSingle;
        this.Controls.Add(txtExtensions);

        var btnScan = CreateButton("🔍", 480, y + 24, 40, 25, Color.FromArgb(63, 81, 181));
        btnScan.Click += ScanExtensionsClick;
        this.Controls.Add(btnScan);
        y += 60;

        // Интервал
        AddLabel("Интервал проверки (минут):", 20, y);
        numInterval = new NumericUpDown();
        numInterval.Location = new Point(20, y + 25);
        numInterval.Width = 100;
        numInterval.Minimum = 1;
        numInterval.Maximum = 10000;
        numInterval.BackColor = Color.FromArgb(45, 45, 68);
        numInterval.ForeColor = Color.White;
        this.Controls.Add(numInterval);
        y += 60;

        // Кнопки
        var btnSave = CreateButton("Сохранить", 20, y, 120, 35, Color.FromArgb(76, 175, 80));
        btnSave.Click += SaveClick;
        this.Controls.Add(btnSave);

        var btnTelegram = CreateButton("🔔 Telegram", 150, y, 120, 35, Color.FromArgb(14, 165, 233));
        btnTelegram.Click += TelegramClick;
        this.Controls.Add(btnTelegram);

        var btnTest = CreateButton("Тест", 280, y, 100, 35, Color.FromArgb(255, 152, 0));
        btnTest.Click += TestClick;
        this.Controls.Add(btnTest);
        
        var btnAbout = CreateButton("О программе", 390, y, 120, 35, Color.FromArgb(99, 102, 241));
        btnAbout.Click += AboutClick;
        this.Controls.Add(btnAbout);
        y += 50;

        // Панель службы
        var pnlService = new Panel();
        pnlService.Location = new Point(20, y);
        pnlService.Size = new Size(540, 100);
        pnlService.BackColor = Color.FromArgb(45, 45, 68);
        this.Controls.Add(pnlService);
        
        var lblSvcTitle = new Label();
        lblSvcTitle.Text = "Управление службой";
        lblSvcTitle.Location = new Point(10, 10);
        lblSvcTitle.AutoSize = true;
        lblSvcTitle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        lblSvcTitle.ForeColor = labelColor;
        pnlService.Controls.Add(lblSvcTitle);

        lblServiceStatus = new Label();
        lblServiceStatus.Text = "Статус: ...";
        lblServiceStatus.Location = new Point(10, 35);
        lblServiceStatus.AutoSize = true;
        lblServiceStatus.ForeColor = Color.Gray;
        pnlService.Controls.Add(lblServiceStatus);

        btnInstallService = CreateButton("Установить", 10, 60, 100, 30, Color.FromArgb(63, 81, 181));
        btnInstallService.Click += InstallClick;
        pnlService.Controls.Add(btnInstallService);

        btnStartService = CreateButton("Запустить", 120, 60, 100, 30, Color.FromArgb(76, 175, 80));
        btnStartService.Click += StartClick;
        pnlService.Controls.Add(btnStartService);

        btnStopService = CreateButton("Остановить", 230, 60, 100, 30, Color.FromArgb(255, 152, 0));
        btnStopService.Click += StopClick;
        pnlService.Controls.Add(btnStopService);

        btnUninstallService = CreateButton("Удалить", 340, 60, 100, 30, Color.FromArgb(244, 67, 54));
        btnUninstallService.Click += UninstallClick;
        pnlService.Controls.Add(btnUninstallService);

        y += 110;

        // Лог
        AddLabel("Лог операций:", 20, y);
        txtLog = new TextBox();
        txtLog.Location = new Point(20, y + 25);
        txtLog.Width = 540;
        txtLog.Height = 150;
        txtLog.Multiline = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.BackColor = Color.FromArgb(45, 45, 68);
        txtLog.ForeColor = Color.LightGray;
        txtLog.BorderStyle = BorderStyle.FixedSingle;
        txtLog.ReadOnly = true;
        txtLog.Font = new Font("Consolas", 9);
        this.Controls.Add(txtLog);

        var timer = new System.Windows.Forms.Timer();
        timer.Interval = 2000;
        timer.Tick += TimerTick;
        timer.Start();
    }

    private void AddLabel(string text, int x, int y)
    {
        var lbl = new Label();
        lbl.Text = text;
        lbl.Location = new Point(x, y);
        lbl.AutoSize = true;
        lbl.ForeColor = Color.LightGray;
        this.Controls.Add(lbl);
    }

    private Button CreateButton(string text, int x, int y, int w, int h, Color bg)
    {
        var btn = new Button();
        btn.Text = text;
        btn.Location = new Point(x, y);
        btn.Size = new Size(w, h);
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = bg;
        btn.ForeColor = Color.White;
        btn.Cursor = Cursors.Hand;
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void LoadSettings()
    {
        Program.LoadConfig();
        txtFolder.Text = Program.Config.FolderPath;
        numDays.Value = Program.Config.DaysOld;
        chkRecursive.Checked = Program.Config.Recursive;
        chkRecycleBin.Checked = Program.Config.UseRecycleBin;
        chkDetailedLog.Checked = Program.Config.DetailedLog;
        numInterval.Value = Program.Config.IntervalMinutes;
        if (Program.Config.FileExtensions != null)
            txtExtensions.Text = string.Join(", ", Program.Config.FileExtensions.ToArray());
    }

    private void SaveClick(object sender, EventArgs e)
    {
        Program.Config.FolderPath = txtFolder.Text;
        Program.Config.DaysOld = (int)numDays.Value;
        Program.Config.Recursive = chkRecursive.Checked;
        Program.Config.UseRecycleBin = chkRecycleBin.Checked;
        Program.Config.DetailedLog = chkDetailedLog.Checked;
        Program.Config.IntervalMinutes = (int)numInterval.Value;
        
        Program.Config.FileExtensions = new List<string>(
            txtExtensions.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
        );

        Program.SaveConfig();
        MessageBox.Show("Настройки сохранены!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void TestClick(object sender, EventArgs e)
    {
        SaveClick(sender, e);
        txtLog.AppendText("Запуск тестовой очистки...\r\n");
        Cleaner.RunCleanup(Program.Config);
        txtLog.AppendText("Тестовая очистка завершена. Проверьте cleanup.log\r\n");
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
        
        var lblToken = new Label();
        lblToken.Text = "Bot Token:";
        lblToken.Location = new Point(20, y);
        lblToken.AutoSize = true;
        tgForm.Controls.Add(lblToken);
        
        var txtToken = new TextBox();
        txtToken.Text = Program.Config.TelegramBotToken;
        txtToken.Location = new Point(20, y + 25);
        txtToken.Width = 340;
        tgForm.Controls.Add(txtToken);
        y += 60;
        
        var lblChatId = new Label();
        lblChatId.Text = "Chat ID:";
        lblChatId.Location = new Point(20, y);
        lblChatId.AutoSize = true;
        tgForm.Controls.Add(lblChatId);
        
        var txtChatId = new TextBox();
        txtChatId.Text = Program.Config.TelegramChatId;
        txtChatId.Location = new Point(20, y + 25);
        txtChatId.Width = 340;
        tgForm.Controls.Add(txtChatId);
        y += 60;
        
        var btnSaveTg = new Button();
        btnSaveTg.Text = "Сохранить";
        btnSaveTg.Location = new Point(20, y);
        btnSaveTg.Size = new Size(100, 30);
        btnSaveTg.BackColor = Color.FromArgb(76, 175, 80);
        btnSaveTg.ForeColor = Color.White;
        btnSaveTg.FlatStyle = FlatStyle.Flat;
        btnSaveTg.Click += (s, ev) => {
            Program.Config.TelegramBotToken = txtToken.Text;
            Program.Config.TelegramChatId = txtChatId.Text;
            Program.SaveConfig();
            MessageBox.Show("Настройки Telegram сохранены!", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
            tgForm.Close();
        };
        tgForm.Controls.Add(btnSaveTg);
        
        var btnTestTg = new Button();
        btnTestTg.Text = "Тест";
        btnTestTg.Location = new Point(130, y);
        btnTestTg.Size = new Size(100, 30);
        btnTestTg.BackColor = Color.FromArgb(14, 165, 233);
        btnTestTg.ForeColor = Color.White;
        btnTestTg.FlatStyle = FlatStyle.Flat;
        btnTestTg.Click += (s, ev) => {
            Program.Config.TelegramBotToken = txtToken.Text;
            Program.Config.TelegramChatId = txtChatId.Text;
            try 
            {
                Program.SendTelegramNotification("✅ Тестовое сообщение от File Cleanup Manager");
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

    private void ScanExtensionsClick(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(txtFolder.Text) || !Directory.Exists(txtFolder.Text))
        {
            MessageBox.Show("Выберите папку для сканирования!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try 
        {
            var extensions = new Dictionary<string, int>();
            System.IO.SearchOption searchOption = chkRecursive.Checked ? System.IO.SearchOption.AllDirectories : System.IO.SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(txtFolder.Text, "*.*", searchOption);
            
            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (!string.IsNullOrEmpty(ext))
                {
                    if (extensions.ContainsKey(ext)) extensions[ext]++;
                    else extensions[ext] = 1;
                }
            }

            if (extensions.Count == 0)
            {
                MessageBox.Show("Файлы с расширениями не найдены.", "Информация");
                return;
            }

            using (var form = new ScanResultForm(extensions))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    var current = new List<string>(txtExtensions.Text.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                    foreach (var ext in form.SelectedExtensions)
                    {
                        if (!current.Contains(ext)) current.Add(ext);
                    }
                    txtExtensions.Text = string.Join(", ", current.ToArray());
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка сканирования: " + ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BrowseFolder(object sender, EventArgs e)
    {
        using (var fbd = new FolderBrowserDialog())
        {
            if (fbd.ShowDialog() == DialogResult.OK)
                txtFolder.Text = fbd.SelectedPath;
        }
    }

    private void InstallClick(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(Program.Config.FolderPath))
        {
            MessageBox.Show("⚠️ Необходимо настроить папку для очистки!\n\n" +
                "Укажите папку и нажмите 'Сохранить' перед установкой службы.",
                "Настройки не заданы",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        RunAsAdmin("/install");
    }

    private void StartClick(object sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(Program.Config.FolderPath))
        {
            MessageBox.Show("⚠️ Необходимо настроить папку для очистки!\n\n" +
                "Укажите папку и нажмите 'Сохранить' перед запуском службы.\n\n" +
                "Служба не запустится без корректных настроек!",
                "Настройки не заданы",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        if (!Directory.Exists(Program.Config.FolderPath))
        {
            MessageBox.Show("⚠️ Папка не существует!\n\n" +
                "Указанная папка: " + Program.Config.FolderPath + "\n\n" +
                "Создайте папку или укажите другой путь.",
                "Папка не найдена",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }
        RunCmd("net start FileCleanupService");
    }

    private void StopClick(object sender, EventArgs e)
    {
        RunCmd("net stop FileCleanupService");
    }

    private void UninstallClick(object sender, EventArgs e)
    {
        RunAsAdmin("/uninstall");
    }

    private void RunAsAdmin(string arg)
    {
        try
        {
            var psi = new ProcessStartInfo(Application.ExecutablePath, arg);
            psi.Verb = "runas";
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка: " + ex.Message);
        }
    }

    private void RunCmd(string cmd)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + cmd);
            psi.Verb = "runas";
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Ошибка: " + ex.Message);
        }
    }

    private void TimerTick(object sender, EventArgs e)
    {
        UpdateServiceStatus();
    }

    private void UpdateServiceStatus()
    {
        try
        {
            using (var sc = new ServiceController("FileCleanupService"))
            {
                lblServiceStatus.Text = "Статус: " + sc.Status.ToString();
                lblServiceStatus.ForeColor = sc.Status == ServiceControllerStatus.Running ? Color.LightGreen : Color.Orange;
                
                btnInstallService.Enabled = false;
                btnUninstallService.Enabled = true;
                btnStartService.Enabled = sc.Status == ServiceControllerStatus.Stopped;
                btnStopService.Enabled = sc.Status == ServiceControllerStatus.Running;
            }
        }
        catch
        {
            lblServiceStatus.Text = "Статус: Не установлена";
            lblServiceStatus.ForeColor = Color.Gray;
            
            btnInstallService.Enabled = true;
            btnUninstallService.Enabled = false;
            btnStartService.Enabled = false;
            btnStopService.Enabled = false;
        }
    }
    
    private void AboutClick(object sender, EventArgs e)
    {
        Form aboutForm = new Form();
        aboutForm.Text = "О программе";
        aboutForm.Size = new Size(400, 300);
        aboutForm.StartPosition = FormStartPosition.CenterParent;
        aboutForm.FormBorderStyle = FormBorderStyle.FixedDialog;
        aboutForm.MaximizeBox = false;
        aboutForm.MinimizeBox = false;
        aboutForm.BackColor = Color.FromArgb(30, 30, 46);
        
        int y = 30;
        
        // Заголовок
        var lblTitle = new Label();
        lblTitle.Text = "File Cleanup Manager";
        lblTitle.Location = new Point(0, y);
        lblTitle.Size = new Size(400, 40);
        lblTitle.TextAlign = ContentAlignment.MiddleCenter;
        lblTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(187, 134, 252);
        aboutForm.Controls.Add(lblTitle);
        y += 50;
        
        // Версия
        var lblVersion = new Label();
        lblVersion.Text = "Версия 1.2.5";
        lblVersion.Location = new Point(0, y);
        lblVersion.Size = new Size(400, 25);
        lblVersion.TextAlign = ContentAlignment.MiddleCenter;
        lblVersion.Font = new Font("Segoe UI", 11);
        lblVersion.ForeColor = Color.FromArgb(224, 224, 224);
        aboutForm.Controls.Add(lblVersion);
        y += 40;
        
        // Автор
        var lblAuthor = new Label();
        lblAuthor.Text = "Created with Antigravity\nby Serik Muftakhidinov";
        lblAuthor.Location = new Point(0, y);
        lblAuthor.Size = new Size(400, 50);
        lblAuthor.TextAlign = ContentAlignment.MiddleCenter;
        lblAuthor.Font = new Font("Segoe UI", 10);
        lblAuthor.ForeColor = Color.FromArgb(153, 153, 170);
        aboutForm.Controls.Add(lblAuthor);
        y += 60;
        
        // Copyright
        var lblCopyright = new Label();
        lblCopyright.Text = "All rights reserved.\n© 2025";
        lblCopyright.Location = new Point(0, y);
        lblCopyright.Size = new Size(400, 40);
        lblCopyright.TextAlign = ContentAlignment.MiddleCenter;
        lblCopyright.Font = new Font("Segoe UI", 9);
        lblCopyright.ForeColor = Color.FromArgb(102, 102, 170);
        aboutForm.Controls.Add(lblCopyright);
        y += 50;
        
        // Кнопка закрыть
        var btnClose = new Button();
        btnClose.Text = "Закрыть";
        btnClose.Location = new Point(140, y);
        btnClose.Size = new Size(120, 35);
        btnClose.FlatStyle = FlatStyle.Flat;
        btnClose.BackColor = Color.FromArgb(99, 102, 241);
        btnClose.ForeColor = Color.White;
        btnClose.Cursor = Cursors.Hand;
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, ev) => aboutForm.Close();
        aboutForm.Controls.Add(btnClose);
        
        aboutForm.ShowDialog();
    }
}

public class ScanResultForm : Form
{
    public List<string> SelectedExtensions { get; private set; }
    private CheckedListBox chkList;

    public ScanResultForm(Dictionary<string, int> extensions)
    {
        this.Text = "Выберите расширения";
        this.Size = new Size(300, 400);
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.FromArgb(30, 30, 46);
        this.ForeColor = Color.White;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;

        chkList = new CheckedListBox();
        chkList.Location = new Point(10, 10);
        chkList.Size = new Size(265, 300);
        chkList.BackColor = Color.FromArgb(45, 45, 68);
        chkList.ForeColor = Color.White;
        chkList.BorderStyle = BorderStyle.FixedSingle;
        chkList.CheckOnClick = true;

        foreach (var kvp in extensions.OrderByDescending(x => x.Value))
        {
            chkList.Items.Add(string.Format("{0} ({1} файлов)", kvp.Key, kvp.Value));
        }

        this.Controls.Add(chkList);

        var btnOk = new Button();
        btnOk.Text = "Добавить";
        btnOk.Location = new Point(10, 320);
        btnOk.Size = new Size(100, 30);
        btnOk.BackColor = Color.FromArgb(76, 175, 80);
        btnOk.FlatStyle = FlatStyle.Flat;
        btnOk.ForeColor = Color.White;
        btnOk.Click += (s, e) => {
            SelectedExtensions = new List<string>();
            foreach (string item in chkList.CheckedItems)
            {
                SelectedExtensions.Add(item.Split(' ')[0]);
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        };
        this.Controls.Add(btnOk);

        var btnCancel = new Button();
        btnCancel.Text = "Отмена";
        btnCancel.Location = new Point(120, 320);
        btnCancel.Size = new Size(100, 30);
        btnCancel.BackColor = Color.FromArgb(244, 67, 54);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.ForeColor = Color.White;
        btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        this.Controls.Add(btnCancel);
    }
}
