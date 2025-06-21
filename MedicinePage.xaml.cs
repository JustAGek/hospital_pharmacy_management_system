using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class MedicinePage : Window, INotifyPropertyChanged
    {
        public ObservableCollection<MedicineViewModel> Medicines { get; } = new ObservableCollection<MedicineViewModel>();
        public ObservableCollection<MedicineViewModel> FilteredMedicines { get; } = new ObservableCollection<MedicineViewModel>();

        public ObservableCollection<string> MedicineTypes { get; } = new ObservableCollection<string> { "Tablet", "Drink", "Cream", "Other" };
        public ObservableCollection<string> Origins { get; } = new ObservableCollection<string> { "Local", "Imported" };

        private string? _editingBarcode = null;
        private bool isEditing = false;

        public MedicinePage()
        {
            InitializeComponent();
            this.DataContext = this;
            MedicineTypeComboBox.ItemsSource = MedicineTypes;
            OriginComboBox.ItemsSource = Origins;
            LoadMedicines();
        }

        private void LoadMedicines()
        {
            Medicines.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT barcode, name_en, name_ar, active_ingredient, dose, medicine_type,
                       price_per_box, price_per_strip, company, `use`, origin, last_updated
                FROM medicines
                ORDER BY name_en
            ";

            using var rdr = cmd.ExecuteReader();
            int index = 1;
            while (rdr.Read())
            {
                Medicines.Add(new MedicineViewModel
                {
                    Index = index++,
                    Barcode = rdr.GetString("barcode"),
                    NameEn = rdr.GetString("name_en"),
                    NameAr = rdr.GetString("name_ar"),
                    ActiveIngredient = rdr.GetString("active_ingredient"),
                    Dose = rdr.GetString("dose"),
                    MedicineType = rdr.GetString("medicine_type"),
                    PricePerBox = rdr.GetDecimal("price_per_box"),
                    PricePerStrip = rdr.IsDBNull("price_per_strip") ? 0 : rdr.GetDecimal("price_per_strip"),
                    Company = rdr.GetString("company"),
                    Use = rdr.IsDBNull("use") ? "" : rdr.GetString("use"),
                    Origin = rdr.GetString("origin"),
                    LastUpdated = rdr.GetDateTime("last_updated")
                });
            }
            ApplyMedicineSearchFilter(MedicineSearchTextBox?.Text ?? "");
        }

        private void ApplyMedicineSearchFilter(string query)
        {
            FilteredMedicines.Clear();
            string lower = (query ?? "").Trim().ToLower();
            var filtered = Medicines.Where(m =>
                string.IsNullOrEmpty(lower)
                || m.Barcode.ToLower().Contains(lower)
                || m.NameEn.ToLower().Contains(lower)
                || m.NameAr.ToLower().Contains(lower)
            ).ToList();

            int idx = 1;
            foreach (var med in filtered)
            {
                med.Index = idx++;
                FilteredMedicines.Add(med);
            }
            OnPropertyChanged(nameof(FilteredMedicines));
        }

        private void MedicineSearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyMedicineSearchFilter(MedicineSearchTextBox.Text);
        }

        private void SaveMedicine_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string nameEn = MedicineEnglishTextBox.Text.Trim();
                string nameAr = MedicineArabicTextBox.Text.Trim();
                string activeIngredient = ActiveIngredientTextBox.Text.Trim();
                string dose = DoseTextBox.Text.Trim();
                string company = CompanyTextBox.Text.Trim();
                string use = UseTextBox.Text.Trim();
                string type = MedicineTypeComboBox.SelectedItem?.ToString() ?? "";
                string origin = OriginComboBox.SelectedItem?.ToString() ?? "";
                string priceBoxText = PricePerBoxTextBox.Text.Trim();
                string priceStripText = PricePerStripTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(nameEn) || string.IsNullOrWhiteSpace(nameAr) ||
                    string.IsNullOrWhiteSpace(activeIngredient) || string.IsNullOrWhiteSpace(dose) ||
                    string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(type) ||
                    string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(priceBoxText))
                {
                    MessageBox.Show("Please fill all required fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(priceBoxText, out decimal priceBox))
                {
                    MessageBox.Show("Invalid Box Price value.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                decimal? priceStrip = null;
                if (!string.IsNullOrWhiteSpace(priceStripText))
                {
                    if (decimal.TryParse(priceStripText, out decimal pStrip))
                        priceStrip = pStrip;
                    else
                    {
                        MessageBox.Show("Invalid Strip Price value.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                using var conn = DbHelper.GetConnection();
                using var cmd = conn.CreateCommand();

                if (!isEditing)
                {
                    // New barcode
                    string barcode = "MD" + DateTime.Now.Ticks.ToString("X");
                    cmd.CommandText = @"
                        INSERT INTO medicines (barcode, name_en, name_ar, active_ingredient, dose,
                                              medicine_type, price_per_box, price_per_strip,
                                              company, `use`, origin)
                        VALUES (@bc, @en, @ar, @ing, @dose, @type, @ppb, @pps, @company, @use, @origin)
                    ";
                    cmd.Parameters.AddWithValue("@bc", barcode);
                }
                else
                {
                    cmd.CommandText = @"
                        UPDATE medicines
                        SET name_en=@en, name_ar=@ar, active_ingredient=@ing, dose=@dose,
                            medicine_type=@type, price_per_box=@ppb, price_per_strip=@pps,
                            company=@company, `use`=@use, origin=@origin
                        WHERE barcode=@bc
                    ";
                    cmd.Parameters.AddWithValue("@bc", _editingBarcode);
                }

                cmd.Parameters.AddWithValue("@en", nameEn);
                cmd.Parameters.AddWithValue("@ar", nameAr);
                cmd.Parameters.AddWithValue("@ing", activeIngredient);
                cmd.Parameters.AddWithValue("@dose", dose);
                cmd.Parameters.AddWithValue("@type", type);
                cmd.Parameters.AddWithValue("@ppb", priceBox);
                cmd.Parameters.AddWithValue("@pps", (object?)priceStrip ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@company", company);
                cmd.Parameters.AddWithValue("@use", use);
                cmd.Parameters.AddWithValue("@origin", origin);

                cmd.ExecuteNonQuery();

                if (!isEditing)
                {
                    // Insert packaging automatically (strip+box for tablets, box only otherwise)
                    using var packCmd = conn.CreateCommand();
                    if (type == "Tablet")
                    {
                        packCmd.CommandText = @"
                            INSERT IGNORE INTO medicine_packaging (barcode, packaging_type_id)
                            SELECT @bc, id FROM packaging_types WHERE name='strip'
                            UNION ALL
                            SELECT @bc, id FROM packaging_types WHERE name='box'
                        ";
                    }
                    else
                    {
                        packCmd.CommandText = @"
                            INSERT IGNORE INTO medicine_packaging (barcode, packaging_type_id)
                            SELECT @bc, id FROM packaging_types WHERE name='box'
                        ";
                    }
                    packCmd.Parameters.AddWithValue("@bc", isEditing ? _editingBarcode : cmd.Parameters["@bc"].Value);
                    packCmd.ExecuteNonQuery();
                }

                MessageBox.Show(isEditing ? "Medicine updated." : "Medicine saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                isEditing = false;
                _editingBarcode = null;
                ClearFields();
                LoadMedicines();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving medicine:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelMedicine_Click(object sender, RoutedEventArgs e)
        {
            ClearFields();
        }

        private void ClearFields()
        {
            MedicineEnglishTextBox.Clear();
            MedicineArabicTextBox.Clear();
            ActiveIngredientTextBox.Clear();
            DoseTextBox.Clear();
            CompanyTextBox.Clear();
            UseTextBox.Clear();
            PricePerBoxTextBox.Clear();
            PricePerStripTextBox.Clear();
            MedicineTypeComboBox.SelectedIndex = -1;
            OriginComboBox.SelectedIndex = -1;
            isEditing = false;
            _editingBarcode = null;
        }

        private void EditMedicine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not MedicineViewModel vm)
                return;

            MedicineEnglishTextBox.Text = vm.NameEn;
            MedicineArabicTextBox.Text = vm.NameAr;
            ActiveIngredientTextBox.Text = vm.ActiveIngredient;
            DoseTextBox.Text = vm.Dose;
            CompanyTextBox.Text = vm.Company;
            UseTextBox.Text = vm.Use;
            PricePerBoxTextBox.Text = vm.PricePerBox.ToString();
            PricePerStripTextBox.Text = vm.PricePerStrip > 0 ? vm.PricePerStrip.ToString() : "";
            MedicineTypeComboBox.SelectedItem = vm.MedicineType;
            OriginComboBox.SelectedItem = vm.Origin;

            _editingBarcode = vm.Barcode;
            isEditing = true;
        }

        private void DeleteMedicine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not MedicineViewModel vm)
                return;

            var result = MessageBox.Show($"Delete {vm.NameEn}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM medicines WHERE barcode=@bc";
            cmd.Parameters.AddWithValue("@bc", vm.Barcode);
            cmd.ExecuteNonQuery();

            using var packCmd = conn.CreateCommand();
            packCmd.CommandText = "DELETE FROM medicine_packaging WHERE barcode=@bc";
            packCmd.Parameters.AddWithValue("@bc", vm.Barcode);
            packCmd.ExecuteNonQuery();

            MessageBox.Show("Deleted.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadMedicines();
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();

        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
