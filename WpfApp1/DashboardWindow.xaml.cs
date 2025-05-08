using System.Windows;

namespace WpfApp1
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(string fullName)
        {
            InitializeComponent();
            UserNameText.Text = fullName;
        }

        private void ManageUsersButton_Click(object sender, RoutedEventArgs e)
        {
            new RegistrationWindow { Owner = this }.ShowDialog();
        }

        private void PatientsButton_Click(object sender, RoutedEventArgs e)
        {
            new PatientsWindow { Owner = this }.ShowDialog();
        }
    }
}
