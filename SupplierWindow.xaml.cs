using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class SupplierWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<SupplierViewModel> Suppliers { get; set; } = new();
        public ObservableCollection<SupplierViewModel> FilteredSuppliers { get; set; } = new();

        private int? editingSupplierId = null;

        public SupplierWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadSuppliers();
        }

        private void LoadSuppliers()
        {
            Suppliers.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT supplier_id, name, contact_name, phone_number, address, email FROM suppliers ORDER BY name";
            using var rdr = cmd.ExecuteReader();
            int idx = 1;
            while (rdr.Read())
            {
                Suppliers.Add(new SupplierViewModel
                {
                    Index = idx++,
                    SupplierId = rdr.GetInt32("supplier_id"),
                    Name = rdr.GetString("name"),
                    ContactName = rdr.IsDBNull("contact_name") ? "" : rdr.GetString("contact_name"),
                    PhoneNumber = rdr.IsDBNull("phone_number") ? "" : rdr.GetString("phone_number"),
                    Address = rdr.IsDBNull("address") ? "" : rdr.GetString("address"),
                    Email = rdr.IsDBNull("email") ? "" : rdr.GetString("email")
                });
            }
            ApplySearchFilter(SearchTextBox.Text);
        }

        private void SaveSupplier_Click(object sender, RoutedEventArgs e)
        {
            string name = NameTextBox.Text.Trim();
            string contact = ContactNameTextBox.Text.Trim();
            string phone = PhoneTextBox.Text.Trim();
            string address = AddressTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Supplier name is required.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            if (editingSupplierId == null)
            {
                cmd.CommandText = @"
                    INSERT INTO suppliers (name, contact_name, phone_number, address, email)
                    VALUES (@name, @contact, @phone, @address, @email)
                ";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE suppliers SET
                        name=@name, contact_name=@contact, phone_number=@phone, address=@address, email=@email
                    WHERE supplier_id=@id
                ";
                cmd.Parameters.AddWithValue("@id", editingSupplierId.Value);
            }

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@contact", string.IsNullOrEmpty(contact) ? (object)DBNull.Value : contact);
            cmd.Parameters.AddWithValue("@phone", string.IsNullOrEmpty(phone) ? (object)DBNull.Value : phone);
            cmd.Parameters.AddWithValue("@address", string.IsNullOrEmpty(address) ? (object)DBNull.Value : address);
            cmd.Parameters.AddWithValue("@email", string.IsNullOrEmpty(email) ? (object)DBNull.Value : email);

            cmd.ExecuteNonQuery();

            MessageBox.Show(editingSupplierId == null ? "Supplier added." : "Supplier updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForm();
            LoadSuppliers();
        }

        private void CancelSupplier_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void EditSupplier_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not SupplierViewModel vm) return;
            NameTextBox.Text = vm.Name;
            ContactNameTextBox.Text = vm.ContactName;
            PhoneTextBox.Text = vm.PhoneNumber;
            AddressTextBox.Text = vm.Address;
            EmailTextBox.Text = vm.Email;
            editingSupplierId = vm.SupplierId;
        }

        private void DeleteSupplier_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not SupplierViewModel vm) return;
            if (MessageBox.Show($"Delete supplier '{vm.Name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM suppliers WHERE supplier_id=@id";
            cmd.Parameters.AddWithValue("@id", vm.SupplierId);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Deleted.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadSuppliers();
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter(SearchTextBox.Text);
        }

        private void ApplySearchFilter(string? query)
        {
            FilteredSuppliers.Clear();
            var lower = (query ?? "").Trim();
            var filtered = string.IsNullOrWhiteSpace(lower)
                ? Suppliers
                : Suppliers.Where(s =>
                    (s.Name?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (s.ContactName?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (s.PhoneNumber?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (s.Email?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                );
            int idx = 1;
            foreach (var s in filtered)
            {
                s.Index = idx++;
                FilteredSuppliers.Add(s);
            }
            OnPropertyChanged(nameof(FilteredSuppliers));
        }

        private void ClearForm()
        {
            NameTextBox.Clear();
            ContactNameTextBox.Clear();
            PhoneTextBox.Clear();
            AddressTextBox.Clear();
            EmailTextBox.Clear();
            editingSupplierId = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
