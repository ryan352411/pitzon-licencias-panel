using System.Windows;
using RefaccionariaPOS.Security;
using RefaccionariaPOS.Views;

namespace RefaccionariaPOS
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            if (!DemoLicenseService.Validate(out string demoMessage))
            {
                MessageBox.Show(demoMessage, "Demo vencido", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            LoginView pantallaLogin = new LoginView();
            MainWindow = pantallaLogin;
            pantallaLogin.Show();
        }
    }
}
