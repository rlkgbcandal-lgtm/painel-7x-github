
using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace Painel7X;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += (sender, e) =>
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Painel7X");

                Directory.CreateDirectory(folder);

                File.WriteAllText(
                    Path.Combine(folder, "erro.txt"),
                    e.Exception.ToString());

                MessageBox.Show(
                    e.Exception.ToString(),
                    "Erro do Painel 7X",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }

            e.Handled = true;
        };
    }
}
