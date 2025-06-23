using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class SalesHistoryWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<SaleHistoryItem> SalesList { get; } = new();
        public ObservableCollection<CashierModel> CashiersList { get; } = new();
        public ObservableCollection<PatientModel> PatientsList { get; } = new();

        private decimal _salesTotal;
        public decimal SalesTotal
        {
            get => _salesTotal;
            set
            {
                if (_salesTotal != value)
                {
                    _salesTotal = value;
                    OnPropertyChanged(nameof(SalesTotal));
                }
            }
        }

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { _fromDate = value; OnPropertyChanged(nameof(FromDate)); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { _toDate = value; OnPropertyChanged(nameof(ToDate)); }
        }

        private CashierModel? _selectedCashier;
        public CashierModel? SelectedCashier
        {
            get => _selectedCashier;
            set { _selectedCashier = value; OnPropertyChanged(nameof(SelectedCashier)); }
        }

        private PatientModel? _selectedPatient;
        public PatientModel? SelectedPatient
        {
            get => _selectedPatient;
            set { _selectedPatient = value; OnPropertyChanged(nameof(SelectedPatient)); }
        }

        public SalesHistoryWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadCashiers();
            LoadPatients();
            FetchSales();
        }

        private void LoadCashiers()
        {
            CashiersList.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, full_name FROM users";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                CashiersList.Add(new CashierModel { Id = r.GetInt32(0), Name = r.GetString(1) });
        }

        private void LoadPatients()
        {
            PatientsList.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, full_name FROM patients";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                PatientsList.Add(new PatientModel { Id = r.GetInt32(0), FullName = r.GetString(1) });
        }

        private void FetchSales()
        {
            SalesList.Clear();
            decimal sum = 0;
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT s.sale_id, s.sale_time,
       u.full_name AS cashier,
       IFNULL(p.full_name,'Walk-in') AS patient,
       s.discount, s.total_amount, s.amount_paid
  FROM sales s
  LEFT JOIN users u   ON u.id = s.user_id
  LEFT JOIN patients p ON p.id = s.patient_id
 WHERE (@from IS NULL OR s.sale_time >= @from)
   AND (@to   IS NULL OR s.sale_time <= @to)
   AND (@cid  IS NULL OR s.user_id = @cid)
   AND (@pid  IS NULL OR s.patient_id = @pid)
ORDER BY s.sale_time DESC";
            cmd.Parameters.AddWithValue("@from", FromDate);
            cmd.Parameters.AddWithValue("@to", ToDate?.AddDays(1).AddSeconds(-1));
            cmd.Parameters.AddWithValue("@cid", SelectedCashier?.Id);
            cmd.Parameters.AddWithValue("@pid", SelectedPatient?.Id);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var item = new SaleHistoryItem
                {
                    SaleId = r.GetInt32("sale_id"),
                    SaleTime = r.GetDateTime("sale_time"),
                    CashierName = r.GetString("cashier"),
                    PatientName = r.GetString("patient"),
                    Discount = r.GetDecimal("discount"),
                    Total = r.GetDecimal("total_amount"),
                    Paid = r.GetDecimal("amount_paid")
                };
                SalesList.Add(item);
                sum += item.Total;
            }
            SalesTotal = sum;
        }


        private void FetchButton_Click(object sender, RoutedEventArgs e) => FetchSales();

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            FromDate = ToDate = null;
            SelectedCashier = null;
            SelectedPatient = null;
            FetchSales();
        }

        private void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            var item = (SaleHistoryItem)((FrameworkElement)sender).DataContext;
            var result = MessageBox.Show(
                "Are you sure you want to delete this order?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
            RestoreInventory(item.SaleId);
            RemoveSale(item.SaleId);
        }

        private void EditSale_Click(object sender, RoutedEventArgs e)
        {
            var item = (SaleHistoryItem)((FrameworkElement)sender).DataContext;
            var pos = new SalesWindow(item.SaleId, true);
            pos.Show();
            RestoreInventory(item.SaleId);
            RemoveSale(item.SaleId);
            Close();
        }

        private void RestoreInventory(int saleId)
        {
            var lines = new List<(string bc, int pt, int qty, DateTime exp)>();
            using (var conn = DbHelper.GetConnection())
            using (var trx = conn.BeginTransaction())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.Transaction = trx;
                    cmd.CommandText = @"
SELECT si.barcode,
       si.packaging_type_id,
       si.quantity,
       inv.expiry_date
  FROM sale_items si
  JOIN inventory inv
    ON inv.barcode           = si.barcode
   AND inv.packaging_type_id = si.packaging_type_id
 WHERE si.sale_id = @sid
 ORDER BY inv.expiry_date ASC";
                    cmd.Parameters.AddWithValue("@sid", saleId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lines.Add((
                                reader.GetString(0),
                                reader.GetInt32(1),
                                reader.GetInt32(2),
                                reader.GetDateTime(3)
                            ));
                        }
                    }
                }
                foreach (var (bc, pt, qty, exp) in lines)
                {
                    using (var upd = conn.CreateCommand())
                    {
                        upd.Transaction = trx;
                        upd.CommandText = @"
INSERT INTO inventory(barcode,packaging_type_id,quantity,expiry_date)
VALUES(@bc,@pt,@qty,@exp)
ON DUPLICATE KEY UPDATE quantity = quantity + @qty";
                        upd.Parameters.AddWithValue("@bc", bc);
                        upd.Parameters.AddWithValue("@pt", pt);
                        upd.Parameters.AddWithValue("@qty", qty);
                        upd.Parameters.AddWithValue("@exp", exp);
                        upd.ExecuteNonQuery();
                    }
                }
                trx.Commit();
            }
        }

        private void RemoveSale(int saleId)
        {
            using var conn = DbHelper.GetConnection();
            using var trx = conn.BeginTransaction();
            using var d1 = conn.CreateCommand();
            d1.Transaction = trx;
            d1.CommandText = "DELETE FROM sale_items WHERE sale_id=@sid";
            d1.Parameters.AddWithValue("@sid", saleId);
            d1.ExecuteNonQuery();
            using var d2 = conn.CreateCommand();
            d2.Transaction = trx;
            d2.CommandText = "DELETE FROM sales WHERE sale_id=@sid";
            d2.Parameters.AddWithValue("@sid", saleId);
            d2.ExecuteNonQuery();
            trx.Commit();
            FetchSales();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class SaleHistoryItem
    {
        public int SaleId { get; set; }
        public DateTime SaleTime { get; set; }
        public string CashierName { get; set; } = "";
        public string PatientName { get; set; } = "";
        public decimal Discount { get; set; }
        public decimal Total { get; set; }
        public decimal Paid { get; set; }
    }



    public class CashierModel { public int Id { get; set; } public string Name { get; set; } = ""; }
    public class PatientModel { public int Id { get; set; } public string FullName { get; set; } = ""; }
}

