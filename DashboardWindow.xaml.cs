using System.Windows;
using MySqlX.XDevAPI;

namespace WpfApp1
{
    public partial class DashboardWindow : Window
    {
        public string UserFullName { get; set; }
        public DashboardWindow(string fullName)
        {
            InitializeComponent();
            UserFullName = fullName;
            DataContext = this;
            ApplyRoleVisibility();
        }

        private void ClearSelection()
        {
            DashboardBtn.Tag = null;
            ManageUsersBtn.Tag = null;
            PatientsBtn.Tag = null;
            MedicineBtn.Tag = null;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            DashboardBtn.Tag = "Selected";
            // refresh dashboard if needed
        }

        private void ManageUsersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session.UserType.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.", "Access Restricted", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

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
            cmd.CommandText = @"
            UPDATE user_sessions
            SET logout_time = NOW()
            WHERE user_id = (SELECT id FROM users WHERE full_name=@name)
            AND logout_time IS NULL
            ORDER BY login_time DESC LIMIT 1";
            cmd.Parameters.AddWithValue("@name", UserFullName); // Set on login
            cmd.ExecuteNonQuery();

            new LoginWindow().Show();
            Close();
        }
        private void ApplyRoleVisibility()
        {
            if (Session.UserType == "pharmacist")
            {
                ManageUsersBtn.Visibility = Visibility.Collapsed;
                InventoryBtn.Visibility = Visibility.Collapsed;
                SuppliersBtn.Visibility = Visibility.Collapsed;
            }
        }

    }
}
