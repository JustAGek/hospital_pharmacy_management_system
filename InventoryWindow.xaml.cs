using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public class MedicineComboViewModel
    {
        public string Barcode { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string MedicineType { get; set; } = "";
        public override string ToString() => $"{NameEn} ({Barcode})";
    }

    public partial class InventoryWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<MedicineComboViewModel> MedicineList { get; set; } = new();
        public ObservableCollection<string> PackagingTypes { get; set; } = new() { "strip", "box" };
        public ObservableCollection<InventoryViewModel> Inventory { get; set; } = new();
        public ObservableCollection<InventoryViewModel> FilteredInventory { get; set; } = new();

        public MedicineComboViewModel? SelectedMedicine { get; set; }
        public string? SelectedPackaging { get; set; }

        private string? editingBarcode = null;
        private string? editingPackaging = null;
        private DateTime? editingExpiry = null;

        public InventoryWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadMedicines();
            LoadInventory();
        }

        private void LoadMedicines()
        {
            MedicineList.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT barcode, name_en, medicine_type FROM medicines ORDER BY name_en";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                MedicineList.Add(new MedicineComboViewModel
                {
                    Barcode = rdr.GetString("barcode"),
                    NameEn = rdr.GetString("name_en"),
                    MedicineType = rdr.GetString("medicine_type")
                });
            }
        }

        private void LoadInventory()
        {
            Inventory.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT i.barcode, m.name_en, m.medicine_type, pt.name AS packaging, i.quantity, i.expiry_date, i.last_updated
                FROM inventory i
                JOIN medicines m ON m.barcode = i.barcode
                JOIN packaging_types pt ON pt.id = i.packaging_type_id
                ORDER BY m.name_en, pt.name, i.expiry_date
            ";
            using var rdr = cmd.ExecuteReader();
            int idx = 1;
            while (rdr.Read())
            {
                Inventory.Add(new InventoryViewModel
                {
                    Index = idx++,
                    Barcode = rdr.GetString("barcode"),
                    NameEn = rdr.GetString("name_en"),
                    MedicineType = rdr.GetString("medicine_type"),
                    Packaging = rdr.GetString("packaging"),
                    Quantity = rdr.GetInt32("quantity"),
                    ExpiryDate = rdr.GetDateTime("expiry_date"),
                    LastUpdated = rdr.GetDateTime("last_updated")
                });
            }
            ApplyFilter(SearchTextBox.Text);
        }

        private void ApplyFilter(string? query)
        {
            FilteredInventory.Clear();
            var lower = (query ?? "").Trim();
            var filtered = string.IsNullOrWhiteSpace(lower)
                ? Inventory
                : Inventory.Where(i =>
                    (i.NameEn?.IndexOf(lower, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                    (i.Barcode?.IndexOf(lower, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                );
            int idx = 1;
            foreach (var inv in filtered)
            {
                inv.Index = idx++;
                FilteredInventory.Add(inv);
            }
            OnPropertyChanged(nameof(FilteredInventory));
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter(SearchTextBox.Text);
        }

        private void SaveInventory_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedMedicine == null || string.IsNullOrWhiteSpace(SelectedPackaging))
            {
                MessageBox.Show("Please select medicine and packaging.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(QuantityTextBox.Text, out int quantity) || quantity < 0)
            {
                MessageBox.Show("Please enter a valid quantity.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (ExpiryDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Please select expiry date.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string barcode = SelectedMedicine.Barcode;
            string packaging = SelectedPackaging;
            DateTime expiry = ExpiryDatePicker.SelectedDate.Value.Date;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            // Get packaging_type_id
            cmd.CommandText = "SELECT id FROM packaging_types WHERE name=@pack";
            cmd.Parameters.AddWithValue("@pack", packaging);
            var packagingTypeId = Convert.ToInt32(cmd.ExecuteScalar());

            if (editingBarcode == null || editingPackaging == null || editingExpiry == null)
            {
                // Insert new
                cmd.CommandText = @"
                    INSERT INTO inventory (barcode, packaging_type_id, quantity, expiry_date)
                    VALUES (@barcode, @packaging_type_id, @quantity, @expiry_date)
                    ON DUPLICATE KEY UPDATE quantity=@quantity, last_updated=NOW()
                ";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@barcode", barcode);
                cmd.Parameters.AddWithValue("@packaging_type_id", packagingTypeId);
                cmd.Parameters.AddWithValue("@quantity", quantity);
                cmd.Parameters.AddWithValue("@expiry_date", expiry);
            }
            else
            {
                // Update
                cmd.CommandText = @"
                    UPDATE inventory
                    SET barcode=@barcode, packaging_type_id=@packaging_type_id, quantity=@quantity, expiry_date=@expiry_date
                    WHERE barcode=@old_barcode AND packaging_type_id=(SELECT id FROM packaging_types WHERE name=@old_packaging) AND expiry_date=@old_expiry
                ";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@barcode", barcode);
                cmd.Parameters.AddWithValue("@packaging_type_id", packagingTypeId);
                cmd.Parameters.AddWithValue("@quantity", quantity);
                cmd.Parameters.AddWithValue("@expiry_date", expiry);
                cmd.Parameters.AddWithValue("@old_barcode", editingBarcode);
                cmd.Parameters.AddWithValue("@old_packaging", editingPackaging);
                cmd.Parameters.AddWithValue("@old_expiry", editingExpiry);
            }
            cmd.ExecuteNonQuery();

            MessageBox.Show((editingBarcode == null) ? "Inventory added." : "Inventory updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForm();
            LoadInventory();
        }

        private void CancelInventory_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void EditInventory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not InventoryViewModel vm) return;
            SelectedMedicine = MedicineList.FirstOrDefault(m => m.Barcode == vm.Barcode);
            OnPropertyChanged(nameof(SelectedMedicine));
            SelectedPackaging = vm.Packaging;
            OnPropertyChanged(nameof(SelectedPackaging));
            QuantityTextBox.Text = vm.Quantity.ToString();
            ExpiryDatePicker.SelectedDate = vm.ExpiryDate;
            editingBarcode = vm.Barcode;
            editingPackaging = vm.Packaging;
            editingExpiry = vm.ExpiryDate;
        }

        private void DeleteInventory_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not InventoryViewModel vm) return;
            if (MessageBox.Show("Delete this inventory record?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM inventory
                WHERE barcode=@barcode AND packaging_type_id=(SELECT id FROM packaging_types WHERE name=@packaging) AND expiry_date=@expiry
            ";
            cmd.Parameters.AddWithValue("@barcode", vm.Barcode);
            cmd.Parameters.AddWithValue("@packaging", vm.Packaging);
            cmd.Parameters.AddWithValue("@expiry", vm.ExpiryDate);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Deleted.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadInventory();
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow("Your Name").Show();
            Close();
        }

        private void ClearForm()
        {
            MedicineComboBox.SelectedIndex = -1;
            SelectedMedicine = null;
            PackagingComboBox.SelectedIndex = -1;
            SelectedPackaging = null;
            QuantityTextBox.Clear();
            ExpiryDatePicker.SelectedDate = null;
            editingBarcode = null;
            editingPackaging = null;
            editingExpiry = null;
            OnPropertyChanged(nameof(SelectedMedicine));
            OnPropertyChanged(nameof(SelectedPackaging));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
