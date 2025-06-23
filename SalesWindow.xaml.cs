using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;
using System.Windows.Documents;
using System.Windows.Media;
using System.Printing;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace WpfApp1
{
    public partial class SalesWindow : Window
    {
        public SalesMainViewModel ViewModel { get; } = new SalesMainViewModel();
        public int CurrentUserId => Session.UserId;

        public SalesWindow()
        {
            InitializeComponent();
            DataContext = ViewModel;
            ViewModel.StockProvider = GetAvailableStock;
            // delegate used by the VM to surface non‑fatal warnings
            ViewModel.ShowWarning = s => MessageBox.Show(s, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);

            LoadPatients();
            LoadMedicines();
            LoadPackagingTypes();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
            ViewModel.CartItems.CollectionChanged += (_, __) => UpdateStockView();
        }

        // Overload for "Edit" path
        // in SalesWindow.xaml.cs
        public SalesWindow(int saleId, bool isEdit) : this()
        {
            LoadSaleIntoCart(saleId);
        }



        private void LoadSaleIntoCart(int saleId)
        {
            using var conn = DbHelper.GetConnection();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT patient_id, discount, amount_paid
  FROM sales
 WHERE sale_id = @i";
                cmd.Parameters.AddWithValue("@i", saleId);

                using var r = cmd.ExecuteReader();
                if (r.Read())
                {
                    int? pid = r.IsDBNull(0) ? (int?)null : r.GetInt32(0);
                    ViewModel.SelectedPatient = pid.HasValue
                        ? ViewModel.PatientsList.FirstOrDefault(p => p.Id == pid.Value)
                        : null;

                    ViewModel.Discount = r.GetDecimal("discount");
                    ViewModel.AmountPaid = r.GetDecimal("amount_paid");
                }
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT si.barcode,
       pt.name   AS packaging,
       si.quantity,
       si.unit_price
  FROM sale_items si
  JOIN packaging_types pt
    ON pt.id = si.packaging_type_id
 WHERE si.sale_id = @i";
                cmd.Parameters.AddWithValue("@i", saleId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    ViewModel.CartItems.Add(new SalesCartItemModel
                    {
                        Barcode = r.GetString("barcode"),
                        Packaging = r.GetString("packaging"),
                        Quantity = r.GetInt32("quantity"),
                        UnitPrice = r.GetDecimal("unit_price"),
                        NameEn = ViewModel.FilteredProducts.First(m => m.Barcode == r.GetString("barcode")).NameEn,
                        ExpiryDate = GetNearestExpiry(r.GetString("barcode"), r.GetString("packaging")) ?? DateTime.Today
                    });
                }
            }

            UpdateStockView();
        }



        #region Data‑loading helpers
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

            // bring the allergy list into memory – we only need it once
            foreach (var p in patients)
            {
                using var conn = DbHelper.GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT active_ingredient FROM patient_allergies WHERE patient_id=@pid";
                cmd.Parameters.AddWithValue("@pid", p.Id);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                    p.Allergies.Add(rdr.GetString("active_ingredient"));

                ViewModel.PatientsList.Add(p);
            }
        }

        private void LoadMedicines()
        {
            var list = new List<SalesMedicineModel>();

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT barcode, name_en, name_ar, medicine_type,
                       active_ingredient, price_per_box,
                       price_per_strip, strips_per_box
                  FROM medicines
                 ORDER BY name_en";
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
            ViewModel.PackagingTypes.Clear();
            ViewModel.PackagingTypes.Add("box");
            ViewModel.PackagingTypes.Add("strip");
        }

        #endregion

        #region VM event wiring
        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.SelectedPackaging))
            {
                UpdateStockView();
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedMedicine))
            {
                Dispatcher.BeginInvoke((Action)UpdateStockView, DispatcherPriority.Background);
            }
        }

        #endregion

        #region Stock helpers
        private int GetPackagingTypeId(string name)
        {
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id FROM packaging_types WHERE name=@n";
            cmd.Parameters.AddWithValue("@n", name);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        private int GetAvailableStock(string barcode, string packaging)
        {
            int ptid = GetPackagingTypeId(packaging);

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT IFNULL(SUM(quantity),0)
                  FROM inventory
                 WHERE barcode=@bc
                   AND packaging_type_id=@pt
                   AND expiry_date > DATE_ADD(CURDATE(),INTERVAL 3 MONTH)
                   AND quantity>0";
            cmd.Parameters.AddWithValue("@bc", barcode);
            cmd.Parameters.AddWithValue("@pt", ptid);

            int dbStock = Convert.ToInt32(cmd.ExecuteScalar());
            int inCart = ViewModel.CartItems
                                   .Where(i => i.Barcode == barcode && i.Packaging == packaging)
                                   .Sum(i => i.Quantity);

            return Math.Max(0, dbStock - inCart);
        }

        private DateTime? GetNearestExpiry(string barcode, string packaging)
        {
            int ptid = GetPackagingTypeId(packaging);

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT MIN(expiry_date) FROM inventory WHERE barcode=@bc AND packaging_type_id=@pt AND quantity>0";
            cmd.Parameters.AddWithValue("@bc", barcode);
            cmd.Parameters.AddWithValue("@pt", ptid);

            var val = cmd.ExecuteScalar();
            return val == DBNull.Value ? null : (DateTime?)Convert.ToDateTime(val);
        }
        #endregion

        #region Patient safety
        private bool PatientHasAllergy(string activeIngredient, string patientName, int patientId)
        {
            if (patientId == 0 || string.IsNullOrEmpty(activeIngredient))
                return false;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM patient_allergies WHERE patient_id=@pid AND active_ingredient=@ai";
            cmd.Parameters.AddWithValue("@pid", patientId);
            cmd.Parameters.AddWithValue("@ai", activeIngredient);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private bool SelectedPatientIsAllergicTo(SalesMedicineModel med)
            => med != null && ViewModel.SelectedPatient != null && PatientHasAllergy(med.ActiveIngredient, "", ViewModel.SelectedPatient.Id);
        #endregion

        #region UI event handlers
        private void AddToCart_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPatientIsAllergicTo(ViewModel.SelectedMedicine))
            {
                MessageBox.Show($"Patient is allergic to {ViewModel.SelectedMedicine.ActiveIngredient}.", "Allergy", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ViewModel.SelectedPackaging == "strip")
                TrySplitBoxToStrips();

            ViewModel.AddToCart(GetAvailableStock, GetNearestExpiry, PatientHasAllergy);
            UpdateStockView();
        }

        private void ViewStock_Click(object sender, RoutedEventArgs e) => UpdateStockView();
        #endregion

        #region Inventory splitting/merging helpers
        private void TrySplitBoxToStrips()
        {
            var med = ViewModel.SelectedMedicine;
            if (med == null || !med.StripsPerBox.HasValue) return;

            int needed = ViewModel.QuantityToAdd;
            if (GetAvailableStock(med.Barcode, "strip") >= needed) return;

            int perBox = med.StripsPerBox.Value;
            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();

            int boxId = GetPackagingTypeId("box");
            int stripId = GetPackagingTypeId("strip");

            var find = conn.CreateCommand();
            find.Transaction = trx;
            find.CommandText = @"SELECT expiry_date,quantity
                                    FROM inventory
                                   WHERE barcode=@barcode AND packaging_type_id=@boxid
                                     AND expiry_date>DATE_ADD(CURDATE(),INTERVAL 3 MONTH)
                                     AND quantity>0
                                ORDER BY expiry_date ASC
                                   LIMIT 1";
            find.Parameters.AddWithValue("@barcode", med.Barcode);
            find.Parameters.AddWithValue("@boxid", boxId);

            using var rdr = find.ExecuteReader();
            if (rdr.Read())
            {
                DateTime exp = rdr.GetDateTime("expiry_date");
                int qty = rdr.GetInt32("quantity");
                rdr.Close();

                if (qty > 0)
                {
                    var delBox = conn.CreateCommand();
                    delBox.Transaction = trx;
                    delBox.CommandText = "UPDATE inventory SET quantity = quantity - 1 WHERE barcode=@bc AND packaging_type_id=@boxid AND expiry_date=@exp AND quantity>0";
                    delBox.Parameters.AddWithValue("@bc", med.Barcode);
                    delBox.Parameters.AddWithValue("@boxid", boxId);
                    delBox.Parameters.AddWithValue("@exp", exp);
                    delBox.ExecuteNonQuery();

                    var addStrips = conn.CreateCommand();
                    addStrips.Transaction = trx;
                    addStrips.CommandText = "INSERT INTO inventory(barcode,packaging_type_id,quantity,expiry_date) VALUES(@bc,@stripid,@strips,@exp) ON DUPLICATE KEY UPDATE quantity=quantity+@strips";
                    addStrips.Parameters.AddWithValue("@bc", med.Barcode);
                    addStrips.Parameters.AddWithValue("@stripid", stripId);
                    addStrips.Parameters.AddWithValue("@strips", perBox);
                    addStrips.Parameters.AddWithValue("@exp", exp);
                    addStrips.ExecuteNonQuery();

                    trx.Commit();
                }
            }
            else rdr.Close();
        }

        private void TryMergeAllStripsToBox()
        {
            foreach (var med in ViewModel.FilteredProducts.Where(m => m.StripsPerBox.HasValue))
            {
                int per = med.StripsPerBox.Value;
                int stripId = GetPackagingTypeId("strip");
                int boxId = GetPackagingTypeId("box");

                using var conn = DbHelper.GetConnection();
                using var trx = conn.BeginTransaction();

                var cmd = conn.CreateCommand();
                cmd.Transaction = trx;
                cmd.CommandText = "SELECT expiry_date,quantity FROM inventory WHERE barcode=@bc AND packaging_type_id=@stripid";
                cmd.Parameters.AddWithValue("@bc", med.Barcode);
                cmd.Parameters.AddWithValue("@stripid", stripId);

                using var rdr = cmd.ExecuteReader();
                var data = new List<(DateTime exp, int qty)>();
                while (rdr.Read()) data.Add((rdr.GetDateTime(0), rdr.GetInt32(1)));
                rdr.Close();

                foreach (var grp in data.GroupBy(x => x.exp))
                {
                    int total = grp.Sum(x => x.qty);
                    int full = total / per;
                    int left = total % per;
                    if (full > 0)
                    {
                        DateTime exp = grp.Key;

                        var del = conn.CreateCommand();
                        del.Transaction = trx;
                        del.CommandText = "DELETE FROM inventory WHERE barcode=@bc AND packaging_type_id=@stripid AND expiry_date=@exp";
                        del.Parameters.AddWithValue("@bc", med.Barcode);
                        del.Parameters.AddWithValue("@stripid", stripId);
                        del.Parameters.AddWithValue("@exp", exp);
                        del.ExecuteNonQuery();

                        var addBox = conn.CreateCommand();
                        addBox.Transaction = trx;
                        addBox.CommandText = "INSERT INTO inventory(barcode,packaging_type_id,quantity,expiry_date) VALUES(@bc,@boxid,@qty,@exp) ON DUPLICATE KEY UPDATE quantity=quantity+@qty";
                        addBox.Parameters.AddWithValue("@bc", med.Barcode);
                        addBox.Parameters.AddWithValue("@boxid", boxId);
                        addBox.Parameters.AddWithValue("@qty", full);
                        addBox.Parameters.AddWithValue("@exp", exp);
                        addBox.ExecuteNonQuery();

                        if (left > 0)
                        {
                            var addLeft = conn.CreateCommand();
                            addLeft.Transaction = trx;
                            addLeft.CommandText = "INSERT INTO inventory(barcode,packaging_type_id,quantity,expiry_date) VALUES(@bc,@stripid,@qty,@exp) ON DUPLICATE KEY UPDATE quantity=@qty";
                            addLeft.Parameters.AddWithValue("@bc", med.Barcode);
                            addLeft.Parameters.AddWithValue("@stripid", stripId);
                            addLeft.Parameters.AddWithValue("@qty", left);
                            addLeft.Parameters.AddWithValue("@exp", exp);
                            addLeft.ExecuteNonQuery();
                        }
                    }
                }
                trx.Commit();
            }
        }
        #endregion

        #region Inventory deduction after sale
        private void DeductInventoryAfterSale()
        {
            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();

            foreach (var item in ViewModel.CartItems)
            {
                int ptid = GetPackagingTypeId(item.Packaging);

                // FIFO – take earliest expiry first
                var cmd = conn.CreateCommand();
                cmd.Transaction = trx;
                cmd.CommandText = "SELECT expiry_date,quantity FROM inventory WHERE barcode=@bc AND packaging_type_id=@ptid AND quantity>0 ORDER BY expiry_date ASC";
                cmd.Parameters.AddWithValue("@bc", item.Barcode);
                cmd.Parameters.AddWithValue("@ptid", ptid);

                using var rdr = cmd.ExecuteReader();
                var rows = new List<(DateTime exp, int qty)>();
                while (rdr.Read()) rows.Add((rdr.GetDateTime(0), rdr.GetInt32(1)));
                rdr.Close();

                int toDeduct = item.Quantity;
                foreach (var (exp, qty) in rows)
                {
                    if (toDeduct <= 0) break;
                    int dec = Math.Min(qty, toDeduct);

                    var upd = conn.CreateCommand();
                    upd.Transaction = trx;
                    upd.CommandText = "UPDATE inventory SET quantity=quantity-@dec WHERE barcode=@bc AND packaging_type_id=@ptid AND expiry_date=@exp";
                    upd.Parameters.AddWithValue("@dec", dec);
                    upd.Parameters.AddWithValue("@bc", item.Barcode);
                    upd.Parameters.AddWithValue("@ptid", ptid);
                    upd.Parameters.AddWithValue("@exp", exp);
                    upd.ExecuteNonQuery();

                    toDeduct -= dec;
                }
            }
            trx.Commit();
        }
        #endregion

        #region Sale completion & cancellation
        private void CompleteSale_Click(object sender, RoutedEventArgs e)
        {
            // final allergy gate
            if (ViewModel.SelectedPatient != null)
            {
                foreach (var cartItem in ViewModel.CartItems)
                {
                    var med = ViewModel.FilteredProducts.FirstOrDefault(m => m.Barcode == cartItem.Barcode);
                    if (med != null && PatientHasAllergy(med.ActiveIngredient, "", ViewModel.SelectedPatient.Id))
                    {
                        MessageBox.Show($"Cannot complete sale – patient allergic to {med.ActiveIngredient}.",
                                         "Allergy", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }
            }

            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();

            var cmd = conn.CreateCommand();
            cmd.Transaction = trx;
            cmd.CommandText = @"INSERT INTO sales(user_id,patient_id,sale_time,discount,total_amount,amount_paid)
                                VALUES(@uid,@pid,NOW(),@disc,@tot,@paid)";
            cmd.Parameters.AddWithValue("@uid", CurrentUserId);
            cmd.Parameters.AddWithValue("@pid", ViewModel.SelectedPatient?.Id);
            cmd.Parameters.AddWithValue("@disc", ViewModel.Discount);
            cmd.Parameters.AddWithValue("@tot", ViewModel.Total);
            cmd.Parameters.AddWithValue("@paid", ViewModel.AmountPaid);
            cmd.ExecuteNonQuery();
            int saleId = (int)cmd.LastInsertedId;

            foreach (var item in ViewModel.CartItems)
            {
                int ptid = GetPackagingTypeId(item.Packaging);
                var sicmd = conn.CreateCommand();
                sicmd.Transaction = trx;
                sicmd.CommandText = @"INSERT INTO sale_items(sale_id,barcode,packaging_type_id,quantity,unit_price)
                                     VALUES(@sid,@bc,@ptid,@qty,@price)";
                sicmd.Parameters.AddWithValue("@sid", saleId);
                sicmd.Parameters.AddWithValue("@bc", item.Barcode);
                sicmd.Parameters.AddWithValue("@ptid", ptid);
                sicmd.Parameters.AddWithValue("@qty", item.Quantity);
                sicmd.Parameters.AddWithValue("@price", item.UnitPrice);
                sicmd.ExecuteNonQuery();
            }
            trx.Commit();

            DeductInventoryAfterSale();
            TryMergeAllStripsToBox();
            PrintReceipt(saleId);
            MessageBox.Show("Sale completed!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForm();
        }
        private FlowDocument BuildReceiptDocument(int saleId)
        {
            string cashier = Session.UserFullName;
            string patient = ViewModel.SelectedPatient?.FullName ?? "Walk-in";
            DateTime saleTime;
            decimal discount, total, paid;

            using (var conn = DbHelper.GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT sale_time, discount, total_amount, amount_paid
FROM sales
WHERE sale_id = @sid";
                cmd.Parameters.AddWithValue("@sid", saleId);
                using var r = cmd.ExecuteReader();
                r.Read();
                saleTime = r.GetDateTime(0);
                discount = r.GetDecimal(1);
                total = r.GetDecimal(2);
                paid = r.GetDecimal(3);
            }

            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5) };
            table.Columns.Add(new TableColumn { Width = new GridLength(120) });
            table.Columns.Add(new TableColumn { Width = new GridLength(40) });
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });
            table.Columns.Add(new TableColumn { Width = new GridLength(70) });

            var headerRow = new TableRow();
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Item"))) { FontWeight = FontWeights.Bold, Padding = new Thickness(2) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Qty"))) { FontWeight = FontWeights.Bold, Padding = new Thickness(2) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Unit"))) { FontWeight = FontWeights.Bold, Padding = new Thickness(2) });
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("Total"))) { FontWeight = FontWeights.Bold, Padding = new Thickness(2) });
            var headerGroup = new TableRowGroup();
            headerGroup.Rows.Add(headerRow);
            table.RowGroups.Add(headerGroup);

            var bodyGroup = new TableRowGroup();
            using (var conn = DbHelper.GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
SELECT si.quantity, si.unit_price, m.name_en
FROM sale_items si
JOIN medicines m ON m.barcode = si.barcode
WHERE si.sale_id = @sid";
                cmd.Parameters.AddWithValue("@sid", saleId);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    int q = rdr.GetInt32(0);
                    decimal u = rdr.GetDecimal(1);
                    string desc = rdr.GetString(2);
                    var row = new TableRow();
                    row.Cells.Add(new TableCell(new Paragraph(new Run(desc))) { Padding = new Thickness(2) });
                    row.Cells.Add(new TableCell(new Paragraph(new Run(q.ToString()))) { Padding = new Thickness(2) });
                    row.Cells.Add(new TableCell(new Paragraph(new Run(u.ToString("C2")))) { Padding = new Thickness(2) });
                    row.Cells.Add(new TableCell(new Paragraph(new Run((q * u).ToString("C2")))) { Padding = new Thickness(2) });
                    bodyGroup.Rows.Add(row);
                }
            }
            table.RowGroups.Add(bodyGroup);

            var doc = new FlowDocument
            {
                FontFamily = new FontFamily("Consolas"),
                FontSize = 14,
                PagePadding = new Thickness(0),
                PageWidth = 300,
                ColumnWidth = 300
            };

            doc.Blocks.Add(new Paragraph(new Run("PHARMACY RECEIPT"))
            {
                TextAlignment = TextAlignment.Center,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 4, 0, 2)
            });
            doc.Blocks.Add(new Paragraph(new Run($"Date   : {saleTime:yyyy-MM-dd HH:mm}")) { Margin = new Thickness(0) });
            doc.Blocks.Add(new Paragraph(new Run($"Cashier: {cashier}")) { Margin = new Thickness(0) });
            doc.Blocks.Add(new Paragraph(new Run($"Patient: {patient}")) { Margin = new Thickness(0, 0, 0, 4) });
            doc.Blocks.Add(table);

            var summary = new Paragraph { TextAlignment = TextAlignment.Right, Margin = new Thickness(0) };
            summary.Inlines.Add(new Run($"Discount: {discount:0.##}%\n"));
            summary.Inlines.Add(new Run($"Total   : {total:C2}\n") { FontWeight = FontWeights.Bold });
            summary.Inlines.Add(new Run($"Paid    : {paid:C2}\n"));
            summary.Inlines.Add(new Run($"Change  : {(paid - total):C2}\n"));
            doc.Blocks.Add(summary);

            return doc;
        }

        private string SaveFlowDocumentToPdf(FlowDocument doc)
        {
            var tempPdf = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"Receipt_{Guid.NewGuid():N}.pdf");
            var server = new LocalPrintServer();
            var queue = server.GetPrintQueue("Microsoft Print to PDF");
            var writer = PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(((IDocumentPaginatorSource)doc).DocumentPaginator);
            return tempPdf;
        }

        private void PrintReceipt(int saleId)
        {
            var pdfPath = SaveFlowDocumentToPdf(BuildReceiptDocument(saleId));
        }





        private void CancelSale_Click(object sender, RoutedEventArgs e)
        {

            TryMergeAllStripsToBox();

            // Empty the cart
            ViewModel.ClearCart();

            ViewModel.SelectedMedicine = null;
            ViewModel.SelectedPackaging = string.Empty;
            ViewModel.AvailableStock = 0;   
            UpdateStockView();
        }

        #endregion

        private void UpdateStockView()
        {
            if (ViewModel.SelectedMedicine == null || string.IsNullOrWhiteSpace(ViewModel.SelectedPackaging))
                ViewModel.AvailableStock = 0;
            else
                ViewModel.AvailableStock = GetAvailableStock(ViewModel.SelectedMedicine.Barcode, ViewModel.SelectedPackaging);
        }

        private void ClearForm()
        {
            ViewModel.ClearCart();
            UpdateStockView();

            ViewModel.SearchNameEn = string.Empty;
            ViewModel.SearchNameAr = string.Empty;
            ViewModel.SearchBarcode = string.Empty;
            ViewModel.SelectedMedicine = null;
            ViewModel.SelectedPackaging = null;
            ViewModel.QuantityToAdd = 1;
            ViewModel.Discount = 0;
            ViewModel.AmountPaid = 0;
            ViewModel.SelectedPatient = null;
        }


        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();
        }
    }
}