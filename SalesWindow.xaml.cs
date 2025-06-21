using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class SalesWindow : Window
    {
        public SalesMainViewModel ViewModel { get; } = new SalesMainViewModel();
        public int CurrentUserId { get; set; } = 1; // Use 1 for now

        public SalesWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.ShowWarning = s => MessageBox.Show(s, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            LoadPatients();
            LoadMedicines();
            LoadPackagingTypes();
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.CartItems.CollectionChanged += (s, e) => UpdateStockView();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedMedicine) ||
                e.PropertyName == nameof(ViewModel.SelectedPackaging))
            {
                UpdateStockView();
            }
        }

        private void LoadPatients()
        {
            ViewModel.PatientsList.Clear();
            var patients = new List<SalesPatientModel>();
            using (var conn = DbHelper.GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id, full_name, phone_number FROM patients ORDER BY full_name";
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    patients.Add(new SalesPatientModel
                    {
                        Id = rdr.GetInt32("id"),
                        FullName = rdr.GetString("full_name"),
                        PhoneNumber = rdr.GetString("phone_number")
                    });
                }
            }
            foreach (var patient in patients)
            {
                using var conn = DbHelper.GetConnection();
                using var allergyCmd = conn.CreateCommand();
                allergyCmd.CommandText = "SELECT active_ingredient FROM patient_allergies WHERE patient_id=@pid";
                allergyCmd.Parameters.AddWithValue("@pid", patient.Id);
                using var allergyRdr = allergyCmd.ExecuteReader();
                while (allergyRdr.Read())
                    patient.Allergies.Add(allergyRdr.GetString("active_ingredient"));
                ViewModel.PatientsList.Add(patient);
            }
        }

        private void LoadMedicines()
        {
            var list = new List<SalesMedicineModel>();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT barcode, name_en, name_ar, medicine_type, active_ingredient, price_per_box, price_per_strip, strips_per_box FROM medicines ORDER BY name_en";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                list.Add(new SalesMedicineModel
                {
                    Barcode = rdr.GetString("barcode"),
                    NameEn = rdr.GetString("name_en"),
                    NameAr = rdr.GetString("name_ar"),
                    MedicineType = rdr.GetString("medicine_type"),
                    ActiveIngredient = rdr.GetString("active_ingredient"),
                    PricePerBox = rdr.GetDecimal("price_per_box"),
                    PricePerStrip = rdr.IsDBNull("price_per_strip") ? 0 : rdr.GetDecimal("price_per_strip"),
                    StripsPerBox = rdr.IsDBNull("strips_per_box") ? (int?)null : rdr.GetInt32("strips_per_box")
                });
            }
            ViewModel.SetAllProducts(list);
        }

        private void LoadPackagingTypes()
        {
            ViewModel.PackagingTypes = new ObservableCollection<string> { "box", "strip" };
        }

        private int GetPackagingTypeId(string packagingName)
        {
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM packaging_types WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", packagingName);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private int GetAvailableStock(string barcode, string packaging)
        {
            int packagingTypeId = GetPackagingTypeId(packaging);
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT IFNULL(SUM(quantity), 0)
                FROM inventory
                WHERE barcode = @barcode
                  AND packaging_type_id = @ptid
                  AND expiry_date > DATE_ADD(CURDATE(), INTERVAL 3 MONTH)
                  AND quantity > 0";
            cmd.Parameters.AddWithValue("@barcode", barcode);
            cmd.Parameters.AddWithValue("@ptid", packagingTypeId);

            int dbStock = Convert.ToInt32(cmd.ExecuteScalar());

            int cartQty = ViewModel.CartItems
                .Where(i => i.Barcode == barcode && i.Packaging == packaging)
                .Sum(i => i.Quantity);

            return Math.Max(0, dbStock - cartQty);
        }

        private DateTime? GetNearestExpiry(string barcode, string packaging)
        {
            int packagingTypeId = GetPackagingTypeId(packaging);
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MIN(expiry_date) FROM inventory WHERE barcode=@barcode AND packaging_type_id=@ptid AND quantity>0";
            cmd.Parameters.AddWithValue("@barcode", barcode);
            cmd.Parameters.AddWithValue("@ptid", packagingTypeId);
            var val = cmd.ExecuteScalar();
            return val == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(val);
        }

        private bool PatientHasAllergy(string activeIngredient, string patientName, int patientId)
        {
            if (string.IsNullOrEmpty(activeIngredient) || patientId == 0)
                return false;
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM patient_allergies WHERE patient_id=@pid AND active_ingredient=@ai";
            cmd.Parameters.AddWithValue("@pid", patientId);
            cmd.Parameters.AddWithValue("@ai", activeIngredient);
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            return count > 0;
        }

        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedPackaging == "strip")
            {
                TrySplitBoxToStrips();
            }
            ViewModel.AddToCart(GetAvailableStock, GetNearestExpiry, PatientHasAllergy);
            UpdateStockView();
        }

        private void TrySplitBoxToStrips()
        {
            var med = ViewModel.SelectedMedicine;
            if (med == null || !med.StripsPerBox.HasValue)
                return;

            int stripsNeeded = ViewModel.QuantityToAdd;
            int stripsInStock = GetAvailableStock(med.Barcode, "strip");

            if (stripsInStock >= stripsNeeded) return;

            int stripsPerBox = med.StripsPerBox.Value;
            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();

            int boxPackagingId = GetPackagingTypeId("box");
            int stripPackagingId = GetPackagingTypeId("strip");

            var findBoxCmd = conn.CreateCommand();
            findBoxCmd.Transaction = trx;
            findBoxCmd.CommandText = @"
                SELECT expiry_date, quantity
                FROM inventory
                WHERE barcode=@barcode AND packaging_type_id=@boxid
                  AND expiry_date > DATE_ADD(CURDATE(), INTERVAL 3 MONTH)
                  AND quantity > 0
                ORDER BY expiry_date ASC
                LIMIT 1";
            findBoxCmd.Parameters.AddWithValue("@barcode", med.Barcode);
            findBoxCmd.Parameters.AddWithValue("@boxid", boxPackagingId);

            using var boxRdr = findBoxCmd.ExecuteReader();
            if (boxRdr.Read())
            {
                DateTime expiry = boxRdr.GetDateTime("expiry_date");
                int boxQty = boxRdr.GetInt32("quantity");
                boxRdr.Close();

                if (boxQty > 0)
                {
                    var removeBoxCmd = conn.CreateCommand();
                    removeBoxCmd.Transaction = trx;
                    removeBoxCmd.CommandText = @"
                        UPDATE inventory
                        SET quantity = quantity - 1
                        WHERE barcode=@barcode AND packaging_type_id=@boxid AND expiry_date=@exp AND quantity > 0";
                    removeBoxCmd.Parameters.AddWithValue("@barcode", med.Barcode);
                    removeBoxCmd.Parameters.AddWithValue("@boxid", boxPackagingId);
                    removeBoxCmd.Parameters.AddWithValue("@exp", expiry);
                    removeBoxCmd.ExecuteNonQuery();

                    var addStripsCmd = conn.CreateCommand();
                    addStripsCmd.Transaction = trx;
                    addStripsCmd.CommandText = @"
                        INSERT INTO inventory (barcode, packaging_type_id, quantity, expiry_date)
                        VALUES (@barcode, @stripid, @strips, @exp)
                        ON DUPLICATE KEY UPDATE quantity = quantity + @strips";
                    addStripsCmd.Parameters.AddWithValue("@barcode", med.Barcode);
                    addStripsCmd.Parameters.AddWithValue("@stripid", stripPackagingId);
                    addStripsCmd.Parameters.AddWithValue("@strips", stripsPerBox);
                    addStripsCmd.Parameters.AddWithValue("@exp", expiry);
                    addStripsCmd.ExecuteNonQuery();
                    trx.Commit();
                }
            }
            else
            {
                boxRdr.Close();
            }
        }

        private void UpdateStockView()
        {
            if (ViewModel.SelectedMedicine == null || string.IsNullOrWhiteSpace(ViewModel.SelectedPackaging))
                ViewModel.AvailableStock = 0;
            else
                ViewModel.AvailableStock = GetAvailableStock(ViewModel.SelectedMedicine.Barcode, ViewModel.SelectedPackaging);
        }

        private void TryMergeAllStripsToBox()
        {
            foreach (var med in ViewModel.FilteredProducts.Where(m => m.StripsPerBox.HasValue))
            {
                int stripsPerBox = med.StripsPerBox.Value;
                int stripPackagingId = GetPackagingTypeId("strip");
                int boxPackagingId = GetPackagingTypeId("box");

                using var conn = DbHelper.GetConnection();
                using var trx = conn.BeginTransaction();

                var cmd = conn.CreateCommand();
                cmd.Transaction = trx;
                cmd.CommandText = @"
                    SELECT expiry_date, quantity
                    FROM inventory
                    WHERE barcode=@barcode AND packaging_type_id=@stripid";
                cmd.Parameters.AddWithValue("@barcode", med.Barcode);
                cmd.Parameters.AddWithValue("@stripid", stripPackagingId);

                using var rdr = cmd.ExecuteReader();
                var stripQuantities = new List<(DateTime expiry, int qty)>();
                while (rdr.Read())
                {
                    var expiry = rdr.GetDateTime("expiry_date");
                    var qty = rdr.GetInt32("quantity");
                    stripQuantities.Add((expiry, qty));
                }
                rdr.Close();

                foreach (var group in stripQuantities.GroupBy(x => x.expiry))
                {
                    int totalStrips = group.Sum(x => x.qty);
                    int fullBoxes = totalStrips / stripsPerBox;
                    int leftStrips = totalStrips % stripsPerBox;

                    if (fullBoxes > 0)
                    {
                        var expiry = group.Key;
                        var delCmd = conn.CreateCommand();
                        delCmd.Transaction = trx;
                        delCmd.CommandText = @"
                            DELETE FROM inventory WHERE barcode=@barcode AND packaging_type_id=@stripid AND expiry_date=@exp";
                        delCmd.Parameters.AddWithValue("@barcode", med.Barcode);
                        delCmd.Parameters.AddWithValue("@stripid", stripPackagingId);
                        delCmd.Parameters.AddWithValue("@exp", expiry);
                        delCmd.ExecuteNonQuery();

                        var addBoxCmd = conn.CreateCommand();
                        addBoxCmd.Transaction = trx;
                        addBoxCmd.CommandText = @"
                            INSERT INTO inventory (barcode, packaging_type_id, quantity, expiry_date)
                            VALUES (@barcode, @boxid, @qty, @exp)
                            ON DUPLICATE KEY UPDATE quantity = quantity + @qty";
                        addBoxCmd.Parameters.AddWithValue("@barcode", med.Barcode);
                        addBoxCmd.Parameters.AddWithValue("@boxid", boxPackagingId);
                        addBoxCmd.Parameters.AddWithValue("@qty", fullBoxes);
                        addBoxCmd.Parameters.AddWithValue("@exp", expiry);
                        addBoxCmd.ExecuteNonQuery();

                        if (leftStrips > 0)
                        {
                            var addLeftStrips = conn.CreateCommand();
                            addLeftStrips.Transaction = trx;
                            addLeftStrips.CommandText = @"
                                INSERT INTO inventory (barcode, packaging_type_id, quantity, expiry_date)
                                VALUES (@barcode, @stripid, @qty, @exp)
                                ON DUPLICATE KEY UPDATE quantity = @qty";
                            addLeftStrips.Parameters.AddWithValue("@barcode", med.Barcode);
                            addLeftStrips.Parameters.AddWithValue("@stripid", stripPackagingId);
                            addLeftStrips.Parameters.AddWithValue("@qty", leftStrips);
                            addLeftStrips.Parameters.AddWithValue("@exp", expiry);
                            addLeftStrips.ExecuteNonQuery();
                        }
                    }
                }
                trx.Commit();
            }
        }

        private void DeductInventoryAfterSale()
        {
            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();

            foreach (var item in ViewModel.CartItems)
            {
                int packagingTypeId = GetPackagingTypeId(item.Packaging);

                // Find all inventory rows for the barcode and packaging ordered by expiry (FIFO)
                var cmd = conn.CreateCommand();
                cmd.Transaction = trx;
                cmd.CommandText = @"
                    SELECT expiry_date, quantity
                    FROM inventory
                    WHERE barcode=@barcode AND packaging_type_id=@ptid AND quantity>0
                    ORDER BY expiry_date ASC";
                cmd.Parameters.AddWithValue("@barcode", item.Barcode);
                cmd.Parameters.AddWithValue("@ptid", packagingTypeId);

                int qtyToDeduct = item.Quantity;
                using var rdr = cmd.ExecuteReader();
                var stockRows = new List<(DateTime expiry, int qty)>();
                while (rdr.Read())
                {
                    stockRows.Add((rdr.GetDateTime("expiry_date"), rdr.GetInt32("quantity")));
                }
                rdr.Close();

                foreach (var (expiry, inStockQty) in stockRows)
                {
                    if (qtyToDeduct <= 0) break;
                    int deduct = Math.Min(inStockQty, qtyToDeduct);

                    var updateCmd = conn.CreateCommand();
                    updateCmd.Transaction = trx;
                    updateCmd.CommandText = @"
                        UPDATE inventory
                        SET quantity = quantity - @deduct
                        WHERE barcode=@barcode AND packaging_type_id=@ptid AND expiry_date=@exp";
                    updateCmd.Parameters.AddWithValue("@deduct", deduct);
                    updateCmd.Parameters.AddWithValue("@barcode", item.Barcode);
                    updateCmd.Parameters.AddWithValue("@ptid", packagingTypeId);
                    updateCmd.Parameters.AddWithValue("@exp", expiry);
                    updateCmd.ExecuteNonQuery();

                    qtyToDeduct -= deduct;
                }
            }
            trx.Commit();
        }

        private void CompleteSale_Click(object sender, RoutedEventArgs e)
        {
            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();

            var cmd = conn.CreateCommand();
            cmd.Transaction = trx;
            cmd.CommandText = @"INSERT INTO sales 
                (user_id, patient_id, sale_time, discount, total_amount, amount_paid) 
                VALUES (@user_id, @patient_id, NOW(), @discount, @total, @paid)";
            cmd.Parameters.AddWithValue("@user_id", CurrentUserId);
            cmd.Parameters.AddWithValue("@patient_id", ViewModel.SelectedPatient?.Id);
            cmd.Parameters.AddWithValue("@discount", ViewModel.Discount);
            cmd.Parameters.AddWithValue("@total", ViewModel.Total);
            cmd.Parameters.AddWithValue("@paid", ViewModel.AmountPaid);
            cmd.ExecuteNonQuery();

            int saleId = (int)cmd.LastInsertedId;

            foreach (var item in ViewModel.CartItems)
            {
                int packagingTypeId = GetPackagingTypeId(item.Packaging);

                var saleItemCmd = conn.CreateCommand();
                saleItemCmd.Transaction = trx;
                saleItemCmd.CommandText = @"INSERT INTO sale_items 
                    (sale_id, barcode, packaging_type_id, quantity, unit_price) 
                    VALUES (@sid, @barcode, @ptid, @qty, @price)";
                saleItemCmd.Parameters.AddWithValue("@sid", saleId);
                saleItemCmd.Parameters.AddWithValue("@barcode", item.Barcode);
                saleItemCmd.Parameters.AddWithValue("@ptid", packagingTypeId);
                saleItemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                saleItemCmd.Parameters.AddWithValue("@price", item.UnitPrice);
                saleItemCmd.ExecuteNonQuery();
            }

            trx.Commit();

            DeductInventoryAfterSale();
            TryMergeAllStripsToBox();

            MessageBox.Show("Sale Completed!");
            ViewModel.ClearCart();
            UpdateStockView();
        }

        private void CancelSale_Click(object sender, RoutedEventArgs e)
        {
            TryMergeAllStripsToBox();
            ViewModel.ClearCart();
            UpdateStockView();
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();
        }
    }
}
