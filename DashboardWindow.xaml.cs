using System;
using System.Windows;
using MySql.Data.MySqlClient;

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
            RefreshDashboard();
        }

        private void RefreshDashboard()
        {
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = @"SELECT COUNT(DISTINCT barcode) FROM inventory WHERE quantity>0";
            MedicinesCountText.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();

            cmd.CommandText = @"SELECT IFNULL(SUM(total_amount),0) FROM sales WHERE DATE(sale_time)=CURDATE()";
            SalesOfDayText.Text = Convert.ToDecimal(cmd.ExecuteScalar()).ToString("0");

            cmd.CommandText = @"
SELECT IFNULL(SUM(total_amount),0)
  FROM sales
 WHERE YEAR(sale_time)=YEAR(CURDATE())
   AND MONTH(sale_time)=MONTH(CURDATE())";
            SalesOfMonthText.Text = Convert.ToDecimal(cmd.ExecuteScalar()).ToString("0");

            cmd.CommandText = @"
SELECT COUNT(DISTINCT barcode)
  FROM inventory
 WHERE packaging_type_id=(
           SELECT id FROM packaging_types WHERE name='box'
       )
   AND quantity<2";
            StockShortageText.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();

            cmd.CommandText = @"SELECT COUNT(DISTINCT barcode) FROM inventory WHERE expiry_date<CURDATE()";
            ExpiredProductsText.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();

            cmd.CommandText = @"
SELECT COUNT(DISTINCT barcode)
  FROM inventory
 WHERE expiry_date>CURDATE()
   AND expiry_date<=DATE_ADD(CURDATE(),INTERVAL 3 MONTH)";
            NearExpiryText.Text = Convert.ToInt32(cmd.ExecuteScalar()).ToString();
        }

        private void ClearSelection()
        {
            DashboardBtn.Tag = null;
            POSBtn.Tag = null;
            SalesHistoryBtn.Tag = null;
            ManageUsersBtn.Tag = null;
            PatientsBtn.Tag = null;
            MedicineBtn.Tag = null;
            AllergiesBtn.Tag = null;
            InventoryBtn.Tag = null;
            SuppliersBtn.Tag = null;
            IntakeBtn.Tag = null;
            UserSessionsBtn.Tag = null;
        }

        private void DashboardButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            DashboardBtn.Tag = "Selected";
            RefreshDashboard();
        }

        private void SalesHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session.UserType?.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.",
                                "Access Restricted",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }
            ClearSelection();
            SalesHistoryBtn.Tag = "Selected";
            new SalesHistoryWindow().Show();
            Close();
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
            if (Session.UserType?.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.",
                                "Access Restricted",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
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
            if (Session.UserType?.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.",
                                "Access Restricted",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }
            ClearSelection();
            InventoryBtn.Tag = "Selected";
            new InventoryWindow().Show();
            Close();
        }

        private void SuppliersButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session.UserType?.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.",
                                "Access Restricted",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }
            ClearSelection();
            SuppliersBtn.Tag = "Selected";
            new SupplierWindow().Show();
            Close();
        }

        private void IntakeButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            IntakeBtn.Tag = "Selected";
            new InventoryIntakeWindow().Show();
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
 WHERE session_id = @sid";
            cmd.Parameters.AddWithValue("@sid", Session.SessionId);
            cmd.ExecuteNonQuery();

            new LoginWindow().Show();
            Close();
        }

        private void ChangePassword_Click(object sender, RoutedEventArgs e)
        {
            new ChangePasswordWindow().ShowDialog();
        }
        private void UserSessionsButton_Click(object sender, RoutedEventArgs e)
        {
            if (Session.UserType?.ToLower() != "admin")
            {
                MessageBox.Show("Access denied. You do not have permission to open this page.",
                                "Access Restricted",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }
            ClearSelection();
            UserSessionsBtn.Tag = "Selected";
            new UserSessionsWindow().Show();
            Close();
        }

    }
}
