using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace GoFishCore.WpfUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            System.Runtime.ProfileOptimization.SetProfileRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            System.Runtime.ProfileOptimization.StartProfile("startup.profile");

            App.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;

            base.OnStartup(e);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"{e}", "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
