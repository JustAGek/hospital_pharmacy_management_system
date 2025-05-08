using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class MedicineWindow : Window
    {
        public ObservableCollection<MedicineViewModel> Medicines { get; }
            = new ObservableCollection<MedicineViewModel>();

        public MedicineWindow()
        {
            InitializeComponent();
            MedicineGrid.ItemsSource = Medicines;
            LoadMedicines();
        }

        private void LoadMedicines()
        {
            Medicines.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                  m.barcode,
                  CONCAT(m.name_en,' ',m.dose) AS DisplayName,
                  m.medicine_type     AS MedicineType,
                  m.price_per_box     AS PricePerBox,
                  m.price_per_strip   AS PricePerStrip,
                  i.quantity          AS Quantity,
                  i.expiry_date       AS ExpiryDate,
                  m.active_ingredient AS ActiveIngredient,
                  m.company           AS Company
                FROM medicines m
                JOIN medicine_packaging mp ON mp.barcode = m.barcode
                  AND mp.packaging_type_id = (
                    SELECT id FROM packaging_types WHERE name='box'
                  )
                JOIN inventory i ON i.barcode = m.barcode
                  AND i.packaging_type_id = mp.packaging_type_id;
            ";

            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                Medicines.Add(new MedicineViewModel
                {
                    Barcode = rdr.GetString("barcode"),
                    DisplayName = rdr.GetString("DisplayName"),
                    MedicineType = rdr.GetString("MedicineType"),
                    PricePerBox = rdr.GetDecimal("PricePerBox"),
                    PricePerStrip = rdr.IsDBNull("PricePerStrip") ? 0 : rdr.GetDecimal("PricePerStrip"),
                    Quantity = rdr.GetInt32("Quantity"),
                    ExpiryDate = rdr.GetDateTime("ExpiryDate"),
                    ActiveIngredient = rdr.GetString("ActiveIngredient"),
                    Company = rdr.GetString("Company")
                });
            }
        }

        // stubs for buttons:
        private void AddMedicine_Click(object sender, RoutedEventArgs e)
        {
            // TODO: open add‐medicine dialog
            MessageBox.Show("Add Medicine clicked");
        }

        private void LoadStock_Click(object sender, RoutedEventArgs e)
        {
            // TODO: reload this row's stock from DB
            LoadMedicines();
        }

        private void EditMedicine_Click(object sender, RoutedEventArgs e)
        {
            var vm = ((FrameworkElement)sender).DataContext as MedicineViewModel;
            // TODO: open edit screen for vm.Barcode
            MessageBox.Show($"Edit {vm.DisplayName}");
        }

        private void DeleteMedicine_Click(object sender, RoutedEventArgs e)
        {
            var vm = ((FrameworkElement)sender).DataContext as MedicineViewModel;
            // TODO: confirm & delete from DB, then reload
            MessageBox.Show($"Delete {vm.DisplayName}");
        }
    }

    public class MedicineViewModel
    {
        public string Barcode { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string MedicineType { get; set; } = "";
        public string ActiveIngredient { get; set; } = "";
        public string Company { get; set; } = "";
        public decimal PricePerBox { get; set; }
        public decimal PricePerStrip { get; set; }
        public int Quantity { get; set; }
        public DateTime ExpiryDate { get; set; }
    }

}
