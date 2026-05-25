using System;
using System.Windows;
using RefaccionariaPOS.Views;

namespace RefaccionariaPOS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Forzamos a que la PRIMERA ventana en abrirse sea el Login real
            LoginView pantallaLogin = new LoginView();
            pantallaLogin.Show();
        }
    }
}