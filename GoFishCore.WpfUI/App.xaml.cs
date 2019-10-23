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
        public static System.Threading.SynchronizationContext UIContext { get; private set; }
        protected override void OnStartup(StartupEventArgs e)
        {
            System.Runtime.ProfileOptimization.SetProfileRoot(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            System.Runtime.ProfileOptimization.StartProfile("startup.profile");

            App.Current.DispatcherUnhandledException += Current_DispatcherUnhandledException;
            UIContext = System.Threading.SynchronizationContext.Current;
            base.OnStartup(e);
        }

        private void Current_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show(string.Join("\n", MergedExceptions(e.Exception).Select(x => x.ToString())), "Unhandled Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        /// <summary>Returns the exception aswell as all inner exceptions.</summary>
        public static IEnumerable<Exception> MergedExceptions(Exception e)
        {
            return enumerate(e ?? throw new ArgumentNullException(nameof(e)));
            IEnumerable<Exception> enumerate(Exception e)
            {
                var current = e;
                do { yield return current; current = current.InnerException; }
                while (current != null);
            }
        }
    }
}
