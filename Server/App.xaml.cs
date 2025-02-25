using Autofac;
using ProjectZeroLib.Enums;
using Server.Models;
using Server.Models.Burse;
using Server.Service;
using Server.Service.Abstract;
using Server.Service.Bot;
using Server.ViewModels;
using Server.ViewModels.Burse;
using Server.Views.Windows;
using Strategies.Instruments;
using Strategies.Strategies;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Server
{
    public partial class App : Application
    {
        private IContainer _container = null!;

        /// <summary>
        /// Создает обработчики необработанных исключений во всех потоках.
        /// </summary>
        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        /// <summary>
        /// Регистрирует зависимости в контейнере и разрешает MainWindow
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStartup(StartupEventArgs args)
        {
            base.OnStartup(args);

            var builder = new ContainerBuilder();

            builder.RegisterType<ExcelClientsModel>().As<ClientsModel, ExcelClientsModel>().SingleInstance();
            builder.RegisterType<ExcelClientsViewModel>().As<ClientsViewModel, ExcelClientsViewModel>().SingleInstance();

            builder.RegisterType<TcpConnector>().As<Connector, TcpConnector>().SingleInstance();
            builder.RegisterType<TelegramBot>().SingleInstance();
            builder.RegisterType<InstrumentRepository>().SingleInstance().WithParameter("logging", false);
            builder.RegisterType<StrategiesRepository>().SingleInstance();
            builder.RegisterType<StrategiesViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<BursesModel>().AsSelf().SingleInstance();
            builder.RegisterType<BursesViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<ServerModel>().SingleInstance();
            builder.RegisterType<ServerViewModel>().SingleInstance();

            builder.RegisterType<OkxModel>().AsSelf().SingleInstance().WithParameter("name", BurseName.Okx);
            builder.RegisterType<OkxViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<BinanceModel>().AsSelf().SingleInstance().WithParameter("name", BurseName.Binance);
            builder.RegisterType<BinanceViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<BybitModel>().AsSelf().SingleInstance().WithParameter("name", BurseName.Bybit);
            builder.RegisterType<BybitViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<QuikModel>().AsSelf().SingleInstance();
            builder.RegisterType<QuikViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<MainModel>().AsSelf().SingleInstance();
            builder.RegisterType<MainViewModel>().AsSelf().SingleInstance();
            builder.RegisterType<MainView>()
                .OnActivating(eventArgs => eventArgs.Instance.DataContext = eventArgs.Context.Resolve<MainViewModel>());

            _container = builder.Build();

            using var scope = _container.BeginLifetimeScope();
            
            var test = scope.Resolve<MainModel>();

            var mainWindow = scope.Resolve<MainView>();
            mainWindow.ShowDialog();
        }

        /// <summary>
        /// Обрабатывает ошибки во всех потоках.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleException(ex);
            }
        }

        /// <summary>
        /// Обрабатывает ошибки в потоке UI.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception);
            e.Handled = true;
        }

        /// <summary>
        /// Сохраняет справку об ошибке в файл, сохраняет логи и присылает уведомление в телеграм.
        /// </summary>
        /// <param name="ex"></param>
        private async void HandleException(Exception ex)
        {
            var path = Path.Combine(Environment.CurrentDirectory, "Backup");

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            using StreamWriter sw = new(path + "\\error.txt", true);
            sw.WriteLine($"{DateTime.Now}: {ex.Message}\n{ex.TargetSite}\n{ex.StackTrace}");
            var main = _container.Resolve<MainModel>();
            main.SaveLogs();

            var telegram = _container.Resolve<TelegramBot>();
            await telegram.SendShutdownErrorMessage();

            Current.Shutdown();
        }
    }
}
