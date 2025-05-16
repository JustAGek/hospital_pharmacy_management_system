using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{

    public partial class MedicinePage : Window
    {
        public ObservableCollection<MedicineViewModel> Medicines { get; } = new ObservableCollection<MedicineViewModel>();

        public MedicinePage()
        {
            InitializeComponent();
            MedicineListGrid.ItemsSource = Medicines;
            LoadMedicines();
        }

        private void LoadMedicines()
        {
            Medicines.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT m.barcode, m.name_en, m.name_ar, m.medicine_type,
                       m.price_per_box, m.use
                FROM medicines m
                JOIN medicine_packaging mp ON mp.barcode = m.barcode AND mp.packaging_type_id = 3; -- box
            ";

            using var rdr = cmd.ExecuteReader();
            int index = 1;
            while (rdr.Read())
            {
                Medicines.Add(new MedicineViewModel
                {
                    Index = index++,
                    Barcode = rdr.GetString("barcode"),
                    MedicineType = rdr.GetString("medicine_type"),
                    NameEn = rdr.GetString("name_en"),
                    NameAr = rdr.GetString("name_ar"),
                    PricePerBox = rdr.GetDecimal("price_per_box"),
                    Use = rdr.IsDBNull("use") ? "" : rdr.GetString("use"),
                    Availability = "Yes"
                });
            }
        }

        private void SaveMedicine_Click(object sender, RoutedEventArgs e)
        {
            string nameEn = MedicineEnglishTextBox.Text.Trim();
            string nameAr = MedicineArabicTextBox.Text.Trim();
            string description = MedicineDescriptionTextBox.Text.Trim();
            string priceText = MedicinePriceTextBox.Text.Trim();
            string type = (MedicineTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            bool available = true; // can link to toggle later

            if (string.IsNullOrEmpty(nameEn) || string.IsNullOrEmpty(nameAr) || string.IsNullOrEmpty(type) || string.IsNullOrEmpty(priceText))
            {
                MessageBox.Show("Please fill all fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(priceText, out var price))
            {
                MessageBox.Show("Invalid price value.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            string barcode = "MD" + DateTime.Now.Ticks.ToString().Substring(10); // generate dummy barcode

            cmd.CommandText = @"
                INSERT INTO medicines (barcode, name_en, name_ar, active_ingredient, dose,
                                       medicine_type, price_per_box, price_per_strip,
                                       company, use, origin)
                VALUES (@bc, @en, @ar, 'IngredientX', '10mg', @type, @ppb, NULL, 'CompanyX', @desc, 'Local');
            ";

            cmd.Parameters.AddWithValue("@bc", barcode);
            cmd.Parameters.AddWithValue("@en", nameEn);
            cmd.Parameters.AddWithValue("@ar", nameAr);
            cmd.Parameters.AddWithValue("@type", type);
            cmd.Parameters.AddWithValue("@ppb", price);
            cmd.Parameters.AddWithValue("@desc", description);
            cmd.ExecuteNonQuery();

            using var pack = conn.CreateCommand();
            pack.CommandText = "INSERT INTO medicine_packaging (barcode, packaging_type_id) VALUES (@bc, 3)"; // 3 = box
            pack.Parameters.AddWithValue("@bc", barcode);
            pack.ExecuteNonQuery();

            MessageBox.Show("Medicine saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadMedicines();
        }

        private void CancelMedicine_Click(object sender, RoutedEventArgs e)
        {
            MedicineEnglishTextBox.Clear();
            MedicineArabicTextBox.Clear();
            MedicineDescriptionTextBox.Clear();
            MedicinePriceTextBox.Clear();
            MessageBox.Show("Cleared.", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void EditMedicine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not MedicineViewModel vm)
                return;

            MessageBox.Show($"Edit not yet implemented for {vm.Barcode} - {vm.NameEn}", "Edit", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeleteMedicine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not MedicineViewModel vm)
                return;

            var result = MessageBox.Show($"Are you sure you want to delete {vm.NameEn}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM medicines WHERE barcode=@bc";
            cmd.Parameters.AddWithValue("@bc", vm.Barcode);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadMedicines();
        }
    }


}

