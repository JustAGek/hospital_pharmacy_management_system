using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class InventoryIntakeWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<string> PackagingTypes { get; } = new() { "box", "strip" };
        public ObservableCollection<IntakeLineViewModel> Lines { get; } = new();
        public ObservableCollection<IntakeLineViewModel> FilteredLines { get; } = new();

        public string EditingBarcode { get; set; } = "";
        public string SelectedPackaging { get; set; } = "box";
        public int EditingQuantity { get; set; }
        public DateTime EditingExpiry { get; set; } = DateTime.Today;
        public int EditingSupplier { get; set; }
        public decimal EditingCost { get; set; }
        public string EditingInvoice { get; set; } = "";

        private IntakeLineViewModel? _lineBeingEdited = null;

        public InventoryIntakeWindow()
        {
            InitializeComponent();
            DataContext = this;
            RefreshFiltered();
        }

        void Back_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();
        }

        void AddLine_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EditingBarcode) || EditingQuantity <= 0) return;

            if (_lineBeingEdited == null)
            {
                Lines.Add(new IntakeLineViewModel
                {
                    Barcode = EditingBarcode.Trim(),
                    Packaging = SelectedPackaging,
                    Quantity = EditingQuantity,
                    ExpiryDate = EditingExpiry,
                    SupplierId = EditingSupplier,
                    CostPerUnit = EditingCost,
                    InvoiceNumber = EditingInvoice.Trim()
                });
            }
            else
            {
                _lineBeingEdited.Barcode = EditingBarcode.Trim();
                _lineBeingEdited.Packaging = SelectedPackaging;
                _lineBeingEdited.Quantity = EditingQuantity;
                _lineBeingEdited.ExpiryDate = EditingExpiry;
                _lineBeingEdited.SupplierId = EditingSupplier;
                _lineBeingEdited.CostPerUnit = EditingCost;
                _lineBeingEdited.InvoiceNumber = EditingInvoice.Trim();
            }

            ClearForm();
            RefreshFiltered();
        }

        void EditLine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not IntakeLineViewModel vm) return;
            _lineBeingEdited = vm;
            EditingBarcode = vm.Barcode;
            SelectedPackaging = vm.Packaging;
            EditingQuantity = vm.Quantity;
            EditingExpiry = vm.ExpiryDate;
            EditingSupplier = vm.SupplierId;
            EditingCost = vm.CostPerUnit;
            EditingInvoice = vm.InvoiceNumber;
            OnPropChanged(null);
        }

        void DeleteLine_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not IntakeLineViewModel vm) return;
            Lines.Remove(vm);
            RefreshFiltered();
        }

        void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshFiltered();
        }

        void Save_Click(object sender, RoutedEventArgs e)
        {
            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();

            foreach (var line in Lines)
            {
                if (string.IsNullOrWhiteSpace(line.Barcode) || line.Quantity <= 0) continue;

                int ptid;
                using (var ptCmd = conn.CreateCommand())
                {
                    ptCmd.Transaction = trx;
                    ptCmd.CommandText = "SELECT id FROM packaging_types WHERE name=@n LIMIT 1";
                    ptCmd.Parameters.AddWithValue("@n", line.Packaging);
                    ptid = Convert.ToInt32(ptCmd.ExecuteScalar());
                }

                using (var intake = conn.CreateCommand())
                {
                    intake.Transaction = trx;
                    intake.CommandText = @"
INSERT INTO inventory_intake
      (barcode, packaging_type_id, expiry_date, quantity,
       supplier_id, user_id, cost_per_unit, invoice_number)
VALUES(@bc, @pt, @exp, @qty,
       @sup, @uid, @cost, @inv)";
                    intake.Parameters.AddWithValue("@bc", line.Barcode);
                    intake.Parameters.AddWithValue("@pt", ptid);
                    intake.Parameters.AddWithValue("@exp", line.ExpiryDate);
                    intake.Parameters.AddWithValue("@qty", line.Quantity);
                    intake.Parameters.AddWithValue("@sup", line.SupplierId);
                    intake.Parameters.AddWithValue("@uid", Session.UserId);
                    intake.Parameters.AddWithValue("@cost", line.CostPerUnit);
                    intake.Parameters.AddWithValue("@inv", line.InvoiceNumber);
                    intake.ExecuteNonQuery();
                }

                using (var stock = conn.CreateCommand())
                {
                    stock.Transaction = trx;
                    stock.CommandText = @"
INSERT INTO inventory
      (barcode, packaging_type_id, quantity, expiry_date)
VALUES(@bc, @pt, @qty, @exp)
ON DUPLICATE KEY UPDATE
    quantity     = quantity + @qty,
    last_updated = NOW()";
                    stock.Parameters.AddWithValue("@bc", line.Barcode);
                    stock.Parameters.AddWithValue("@pt", ptid);
                    stock.Parameters.AddWithValue("@qty", line.Quantity);
                    stock.Parameters.AddWithValue("@exp", line.ExpiryDate);
                    stock.ExecuteNonQuery();
                }
            }

            trx.Commit();
            MessageBox.Show("Intake posted to inventory and intake log.", "Saved",
                            MessageBoxButton.OK, MessageBoxImage.Information);
            Lines.Clear();
            RefreshFiltered();
        }

        void ClearForm()
        {
            EditingBarcode = "";
            SelectedPackaging = "box";
            EditingQuantity = 0;
            EditingExpiry = DateTime.Today;
            EditingSupplier = 0;
            EditingCost = 0;
            EditingInvoice = "";
            _lineBeingEdited = null;
            OnPropChanged(null);
        }

        void RefreshFiltered()
        {
            FilteredLines.Clear();
            var q = (SearchTextBox.Text ?? "").Trim();
            var src = string.IsNullOrWhiteSpace(q)
                      ? Lines
                      : Lines.Where(l => l.Barcode.Contains(q, StringComparison.OrdinalIgnoreCase)
                                      || l.InvoiceNumber.Contains(q, StringComparison.OrdinalIgnoreCase));
            int idx = 1;
            foreach (var l in src)
            {
                l.Index = idx++;
                FilteredLines.Add(l);
            }
            OnPropChanged(nameof(FilteredLines));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnPropChanged(string? p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public class IntakeLineViewModel : INotifyPropertyChanged
    {
        public int Index { get; set; }
        string _barcode = "";
        string _pack = "box";
        DateTime _exp = DateTime.Today;
        int _qty;
        int _sup;
        decimal _cost;
        string _inv = "";

        public string Barcode { get => _barcode; set { _barcode = value; OnChanged(nameof(Barcode)); } }
        public string Packaging { get => _pack; set { _pack = value; OnChanged(nameof(Packaging)); } }
        public int Quantity { get => _qty; set { _qty = value; OnChanged(nameof(Quantity)); } }
        public DateTime ExpiryDate { get => _exp; set { _exp = value; OnChanged(nameof(ExpiryDate)); } }
        public int SupplierId { get => _sup; set { _sup = value; OnChanged(nameof(SupplierId)); } }
        public decimal CostPerUnit { get => _cost; set { _cost = value; OnChanged(nameof(CostPerUnit)); } }
        public string InvoiceNumber { get => _inv; set { _inv = value; OnChanged(nameof(InvoiceNumber)); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        void OnChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
