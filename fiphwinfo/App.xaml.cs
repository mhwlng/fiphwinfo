using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using log4net;
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;
using TheArtOfDev.HtmlRenderer.Core;
using SharpDX.DirectInput;
using System.Collections.Specialized;
using System.Linq;


// ReSharper disable StringLiteralTypo

namespace fiphwinfo
{

    /// <summary>
    /// Simple application. Check the XAML for comments.
    /// </summary>
    public partial class App : Application
    {
        public static bool IsShuttingDown { get; set; }


        public static Task HWInfoTask;
        private static CancellationTokenSource _hwInfoTokenSource = new CancellationTokenSource();

        private static Mutex _mutex;

        private TaskbarIcon _notifyIcon;

        public static readonly FipHandler FipHandler = new FipHandler();


        public static readonly ILog Log =
            LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static CssData CssData;
        
        public static string ExePath;

        private static CachedSound _clickSound = null;

        public static void PlayClickSound()
        {
            if (_clickSound != null)
            {
                try
                {
                    AudioPlaybackEngine.Instance.PlaySound(_clickSound);
                }
                catch (Exception ex)
                {
                    Log.Error( $"PlaySound: {ex}");
                }
            }
        }

        private static void GetExePath()
        {
            var strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            ExePath = Path.GetDirectoryName(strExeFilePath);
        }

        private static void RunProcess(string fileName)
        {
            var process = new Process();
            // Configure the process using the StartInfo properties.
            process.StartInfo.FileName = fileName;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.Start();
            process.WaitForExit();
        }


        protected override void OnStartup(StartupEventArgs evtArgs)
        {
            const string appName = "fiphwinfo";

            _mutex = new Mutex(true, appName, out var createdNew);

            if (!createdNew)
            {
                //app is already running! Exiting the application  
                Current.Shutdown();
            }

            GetExePath();

            base.OnStartup(evtArgs);

            log4net.Config.XmlConfigurator.Configure();

           
            _clickSound = null;

            if (File.Exists(Path.Combine(ExePath, "appSettings.config")) &&
                ConfigurationManager.GetSection("appSettings") is NameValueCollection appSection)
            {
                if (File.Exists(Path.Combine(ExePath, "Sounds", appSection["clickSound"])))
                {
                    try
                    {
                        _clickSound = new CachedSound(Path.Combine(ExePath, "Sounds", appSection["clickSound"]));
                    }
                    catch (Exception ex)
                    {
                        _clickSound = null;

                        Log.Error($"CachedSound: {ex}");
                    }

                }
            }

            //create the notifyicon (it's a resource declared in NotifyIconResources.xaml
            _notifyIcon = (TaskbarIcon)FindResource("NotifyIcon");

            _notifyIcon.IconSource = new BitmapImage(new Uri("pack://application:,,,/fiphwinfo;component/fiphwinfo.ico"));
            _notifyIcon.ToolTipText = "fiphwinfo";

            var splashScreen = new SplashScreenWindow();
            splashScreen.Show();

            Task.Run(() =>
            {
                var config = new TemplateServiceConfiguration
                {
                    TemplateManager = new ResolvePathTemplateManager(new[] { Path.Combine(ExePath, "Templates") }),
                    DisableTempFileLocking = true,
                    BaseTemplateType = typeof(HtmlSupportTemplateBase<>) /*,
                    Namespaces = new HashSet<string>(){
                        "System",
                        "System.Linq",
                        "System.Collections",
                        "System.Collections.Generic"
                        }*/
                };

                splashScreen.Dispatcher.Invoke(() => splashScreen.ProgressText.Text = "Loading cshtml templates...");

                Engine.Razor = RazorEngineService.Create(config);

                Engine.Razor.Compile("cardcaption.cshtml", null);
                Engine.Razor.Compile("layout.cshtml", null);

                Engine.Razor.Compile("hwinfo.cshtml", null);

                CssData = TheArtOfDev.HtmlRenderer.WinForms.HtmlRender.ParseStyleSheet(
                    File.ReadAllText(Path.Combine(ExePath, "Templates\\styles.css")), true);
                
                splashScreen.Dispatcher.Invoke(() => splashScreen.ProgressText.Text = "Getting sensor data from HWInfo...");

                HWInfo.ReadMem("HWINFO.INC");
                
                if (HWInfo.SensorData.Any())
                {
                    HWInfo.SaveDataToFile(@"Data\hwinfo.json");
                }

                Dispatcher.Invoke(() =>
                {
                    var window = Current.MainWindow = new MainWindow();
                    window.ShowActivated = false;
                });


                splashScreen.Dispatcher.Invoke(() => splashScreen.ProgressText.Text = "Initializing FIP...");
                if (!FipHandler.Initialize())
                {
                    Current.Shutdown();
                }

                Log.Info("fiphwinfo started");

                Dispatcher.Invoke(() =>
                {
                    var window = Current.MainWindow;
                    window?.Hide();
                });

                Dispatcher.Invoke(() => { splashScreen.Close(); });
                
                var hwInfoToken = _hwInfoTokenSource.Token;

                HWInfoTask = Task.Run(async () =>
                {
                    var result = await MQTT.Connect();
                    
                    Log.Info("HWInfo task started");

                    while (true)
                    {
                        if (hwInfoToken.IsCancellationRequested)
                        {
                            hwInfoToken.ThrowIfCancellationRequested();
                        }

                        HWInfo.ReadMem("HWINFO.INC");

                        FipHandler.RefreshHWInfoPages();

                        await Task.Delay(5 * 1000, _hwInfoTokenSource.Token); // repeat every 5 seconds
                    }

                }, hwInfoToken);

            });

        }
      

        protected override void OnExit(ExitEventArgs e)
        {
            FipHandler.Close();

            _notifyIcon.Dispose(); //the icon would clean up automatically, but this is cleaner

            _hwInfoTokenSource.Cancel();

            var hwInfoToken = _hwInfoTokenSource.Token;

            try
            {
                HWInfoTask?.Wait(hwInfoToken);
            }
            catch (OperationCanceledException)
            {
                Log.Info("HWInfo background task ended");
            }
            finally
            {
                _hwInfoTokenSource.Dispose();
            }


            Log.Info("exiting");

            base.OnExit(e);
        }
    }
}
