﻿using System.Windows;

namespace WpfApp1
{
    public partial class DashboardWindow : Window
    {
        public DashboardWindow(string fullName)
        {
            InitializeComponent();
            UserNameText.Text = fullName;
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
            ClearSelection();
            ManageUsersBtn.Tag = "Selected";
            // open ManageUsersWindow…
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
            ClearSelection();
            InventoryBtn.Tag = "Selected";
            new InventoryWindow().Show();
            Close();
        }
        private void SuppliersButton_Click(object sender, RoutedEventArgs e)
        {
            ClearSelection();
            SuppliersBtn.Tag = "Selected";
            new SupplierWindow().Show();
            Close();
        }


    }
}
