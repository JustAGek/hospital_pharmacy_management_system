using System.Windows;
using MySqlX.XDevAPI;

namespace WpfApp1
{
    public partial class DashboardWindow : Window
    {


        public string UserFullName => $"👤 {Session.UserFullName}";
        public string user_full_name => $" {Session.UserFullName}";
        public string LoginTimeFormatted => $"🕒 {Session.LoginTime:HH:mm:ss - dd MMM yyyy}";
        public DashboardWindow(string fullName)
        {
            InitializeComponent();
            Session.UserFullName = fullName;
            DataContext = this;
        }

        private void ClearSelection()
        {
            DashboardBtn.Tag = null;
            POSBtn.Tag = null;
            ManageUsersBtn.Tag = null;
            PatientsBtn.Tag = null;
            MedicineBtn.Tag = null;
            AllergiesBtn.Tag = null;
            InventoryBtn.Tag = null;
            IntakeBtn.Tag = null;
            SuppliersBtn.Tag = null;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            DashboardBtn.Tag = "Selected";
            // Optionally refresh dashboard stats/cards here
        }

        private void POSButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            POSBtn.Tag = "Selected";
            new SalesWindow().Show();
            Close();
        }

        private void ManageUsersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session.UserType.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ClearSelection();
            ManageUsersBtn.Tag = "Selected";
            new ManageUsersWindow().Show();
            Close();
        }



        private void PatientsButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            PatientsBtn.Tag = "Selected";
            new PatientsWindow().Show();
            Close();
        }

        private void MedicineButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            MedicineBtn.Tag = "Selected";
            new MedicinePage().Show();
            Close();
        }

        private void AllergiesButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            AllergiesBtn.Tag = "Selected";
            new AllergiesWindow().Show();
            Close();
        }

        private void InventoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session.UserType.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            new InventoryWindow().Show();
            Close();
        }

        private void SuppliersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session.UserType.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            new SupplierWindow().Show();
            Close();
        }


        private void AvatarButton_Click(object sender, RoutedEventArgs e)
        {
            LogoutPopup.IsOpen = true;
        }

        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE user_sessions
                        SET logout_time = NOW()
                        WHERE session_id = @sid";
            cmd.Parameters.AddWithValue("@sid", Session.SessionId);
            cmd.ExecuteNonQuery();

            new LoginWindow().Show();
            Close();
        }


        private void IntakeButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            IntakeBtn.Tag = "Selected";
            new InventoryIntakeWindow().Show();
            Close();
        }


        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            new ChangePasswordWindow().ShowDialog(); // modal
        }

    }
}
