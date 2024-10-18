using Autofac;
using ProjectZeroLib;
using Server.Models;
using Server.Models.Burse;
using Server.Service.Abstract;
using Server.Service.Bot;
using Server.ViewModels;
using Server.Views.Windows;
using System.IO;
using System.Windows;

namespace Server
{
    public partial class App : Application
    {
        private IContainer _container;

        public App()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var builder = new ContainerBuilder();

            builder.RegisterType<ExcelClientsModel>().As<ClientsModel, ExcelClientsModel>().SingleInstance();
            builder.RegisterType<ExcelClientsViewModel>().As<ClientsViewModel, ExcelClientsViewModel>().SingleInstance();

            builder.RegisterType<TcpConnector>().As<Connector, TcpConnector>().SingleInstance();
            builder.RegisterType<TelegramBot>().SingleInstance();
            builder.RegisterType<InstrumentService>().SingleInstance();
            builder.RegisterType<Strategies>().SingleInstance();
            builder.RegisterType<StrategiesViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<BursesModel>().AsSelf().SingleInstance();
            builder.RegisterType<BursesViewModel>().AsSelf().SingleInstance();

            builder.RegisterType<ServerModel>().SingleInstance();
            builder.RegisterType<ServerViewModel>().SingleInstance();

            builder.RegisterType<OkxModel>().AsSelf().SingleInstance();
            builder.RegisterType<OkxViewModel>().AsSelf().SingleInstance();
            builder.RegisterType<BinanceModel>().AsSelf().SingleInstance();
            builder.RegisterType<BinanceViewModel>().AsSelf().SingleInstance();
            builder.RegisterType<BybitModel>().AsSelf().SingleInstance();
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

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            HandleException(e.ExceptionObject as Exception);
        }
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            HandleException(e.Exception);
            e.Handled = true;
        }
        private async void HandleException(Exception ex)
        {
            string path = Path.Combine(Environment.CurrentDirectory, "Backup");
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
