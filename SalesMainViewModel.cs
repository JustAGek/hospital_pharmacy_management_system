using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using MySql.Data.MySqlClient;
using System.Runtime.CompilerServices;

namespace WpfApp1
{
    public class SalesMainViewModel : INotifyPropertyChanged
    {
        #region Collections exposed to the view
        private readonly ObservableCollection<SalesMedicineModel> _allProducts = new();
        public ObservableCollection<SalesMedicineModel> FilteredProducts { get; } = new();
        public ObservableCollection<SalesPatientModel> PatientsList { get; } = new();
        public ObservableCollection<string> PackagingTypes { get; } = new();
        public ObservableCollection<SalesCartItemModel> CartItems { get; } = new();
        public Func<string, string, int>? StockProvider { get; set; }

        #endregion

        #region Search boxes (Name‑En / Name‑Ar / Barcode)
        private string _searchNameEn = string.Empty;
        public string SearchNameEn
        {
            get => _searchNameEn;
            set { if (Set(ref _searchNameEn, value)) FilterProducts("en"); }
        }

        private string _searchNameAr = string.Empty;
        public string SearchNameAr
        {
            get => _searchNameAr;
            set { if (Set(ref _searchNameAr, value)) FilterProducts("ar"); }
        }

        private string _searchBarcode = string.Empty;
        public string SearchBarcode
        {
            get => _searchBarcode;
            set { if (Set(ref _searchBarcode, value)) FilterProducts("barcode"); }
        }
        #endregion

        #region Selected entities (Medicine / Patient / Packaging)
        private SalesMedicineModel? _selectedMedicine;
        public SalesMedicineModel? SelectedMedicine
        {
            get => _selectedMedicine;
            set
            {
                if (!Set(ref _selectedMedicine, value)) return;

                // only run these helpers when there *is* a selection
                if (value != null)
                {
                    UpdatePackagingTypes();
                    UpdateSearchFieldsFromSelection();
                }

                OnPropertyChanged(nameof(SelectedProductPrice));
            }
        }

        private SalesPatientModel? _selectedPatient;
        public SalesPatientModel? SelectedPatient
        {
            get => _selectedPatient;
            set => Set(ref _selectedPatient, value);
        }

        private string _selectedPackaging = string.Empty;
        public string SelectedPackaging
        {
            get => _selectedPackaging;
            set
            {
                if (Set(ref _selectedPackaging, value))
                    OnPropertyChanged(nameof(SelectedProductPrice));
                RefreshAvailableStock();
            }
        }
        #endregion

        #region Stock helpers bound to the UI
        private int _availableStock;
        public int AvailableStock
        {
            get => _availableStock;
            set => Set(ref _availableStock, value);
        }
        public int QuantityToAdd { get; set; } = 1; // bound to the small quantity TextBox
        #endregion

        #region Money / totals (Discount is now PERCENTAGE)
        private decimal _discount;   // 0 ‑> none,   10 -> 10% off
        public decimal Discount
        {
            get => _discount;
            set
            {
                // Clamp to 0‑100 just in case the user types something odd
                value = Math.Min(Math.Max(value, 0), 100);
                if (Set(ref _discount, value))
                {
                    OnPropertyChanged(nameof(Total));
                    OnPropertyChanged(nameof(ChangeDue));
                }
            }
        }

        private decimal _amountPaid;
        public decimal AmountPaid
        {
            get => _amountPaid;
            set { if (Set(ref _amountPaid, value)) OnPropertyChanged(nameof(ChangeDue)); }
        }

        /// <summary>Sum of <c>UnitPrice × Quantity</c> for every cart line.</summary>
        public decimal Subtotal => CartItems.Sum(l => l.LineTotal);

        /// <summary>Subtotal minus a percentage‑based discount.</summary>
        public decimal Total => Subtotal - (Subtotal * (Discount / 100m));

        /// <summary>What the customer will get back.</summary>
        public decimal ChangeDue => AmountPaid - Total;

        /// <summary>Live count of items in the cart (strips+boxes combined).</summary>
        public int CartQuantity => CartItems.Sum(l => l.Quantity);
        #endregion

        #region Helpers for the UI (price / packaging …)
        public decimal SelectedProductPrice
            => _selectedMedicine == null || string.IsNullOrWhiteSpace(_selectedPackaging)
               ? 0m
               : _selectedPackaging == "strip"
                    ? _selectedMedicine.PricePerStrip
                    : _selectedMedicine.PricePerBox;
        #endregion

        #region Public API called by code‑behind
        public void SetAllProducts(System.Collections.Generic.List<SalesMedicineModel> meds)
        {
            _allProducts.Clear();
            foreach (var m in meds) _allProducts.Add(m);

            FilteredProducts.Clear();
            foreach (var m in meds) FilteredProducts.Add(m);
        }

        private void RefreshAvailableStock()
        {
            if (StockProvider != null && _selectedMedicine != null && !string.IsNullOrWhiteSpace(_selectedPackaging))
                AvailableStock = StockProvider(_selectedMedicine.Barcode, _selectedPackaging);
            else
                AvailableStock = 0;
        }

        public void AddToCart(
            Func<string, string, int> getAvailableStock,
            Func<string, string, DateTime?> getNearestExpiry,
            Func<string, string, int, bool> checkAllergy)
        {
            if (_selectedMedicine == null) { Warn("Please select a medicine."); return; }
            if (string.IsNullOrWhiteSpace(_selectedPackaging)) { Warn("Please select packaging type."); return; }
            if (QuantityToAdd < 1) { Warn("Quantity must be at least 1."); return; }

            int available = getAvailableStock(_selectedMedicine.Barcode, _selectedPackaging);
            if (available < QuantityToAdd) { Warn($"Cannot add more than available stock. (Available: {available})"); return; }

            var safeExp = GetNearestSafeExpiry(_selectedMedicine.Barcode, _selectedPackaging);
            if (safeExp == null) { Warn("Cannot sell expired or near‑expiry medicine."); return; }

            if (_selectedPatient != null &&
                checkAllergy(_selectedMedicine.ActiveIngredient, _selectedPatient.FullName, _selectedPatient.Id))
            {
                Warn($"Patient is allergic to: {_selectedMedicine.ActiveIngredient}");
                return;
            }

            decimal price = _selectedPackaging == "strip" ? _selectedMedicine.PricePerStrip : _selectedMedicine.PricePerBox;

            var existing = CartItems.FirstOrDefault(i => i.Barcode == _selectedMedicine.Barcode && i.Packaging == _selectedPackaging);
            if (existing != null)
                existing.Quantity += QuantityToAdd;
            else
                CartItems.Add(new SalesCartItemModel
                {
                    Barcode = _selectedMedicine.Barcode,
                    NameEn = _selectedMedicine.NameEn,
                    Packaging = _selectedPackaging,
                    Quantity = QuantityToAdd,
                    UnitPrice = price,
                    ActiveIngredient = _selectedMedicine.ActiveIngredient,
                    ExpiryDate = safeExp.Value
                });

            RaiseCartChanged();
        }

        public void RemoveCartItem(SalesCartItemModel item)
        {
            CartItems.Remove(item);
            RaiseCartChanged();
        }

        public void ClearCart()
        {
            CartItems.Clear();
            RaiseCartChanged();
        }
        #endregion

        #region Internals (filtering, expiry, etc.)
        private void UpdatePackagingTypes()
        {
            PackagingTypes.Clear();
            if (_selectedMedicine == null) return;

            if (_selectedMedicine.MedicineType?.ToLower().Contains("tablet") == true)
            {
                PackagingTypes.Add("box");
                PackagingTypes.Add("strip");
            }
            else
            {
                PackagingTypes.Add("box");
            }

            SelectedPackaging = PackagingTypes.FirstOrDefault() ?? string.Empty;
        }

        private void UpdateSearchFieldsFromSelection()
        {
            if (_selectedMedicine == null) return;
            // These assignments will trigger FilterProducts again but that’s harmless
            SearchNameEn = _selectedMedicine.NameEn;
            SearchNameAr = _selectedMedicine.NameAr;
            SearchBarcode = _selectedMedicine.Barcode;
        }

        private void FilterProducts(string field)
        {
            var nameEn = _searchNameEn.Trim().ToLower();
            var nameAr = _searchNameAr.Trim().ToLower();
            var barcode = _searchBarcode.Trim();

            var query = _allProducts.AsEnumerable();
            if (field == "en" && nameEn.Length > 0) query = query.Where(p => p.NameEn?.ToLower().Contains(nameEn) == true);
            if (field == "ar" && nameAr.Length > 0) query = query.Where(p => p.NameAr?.ToLower().Contains(nameAr) == true);
            if (field == "barcode" && barcode.Length > 0) query = query.Where(p => p.Barcode?.Contains(barcode) == true);

            FilteredProducts.Clear();
            foreach (var m in query) FilteredProducts.Add(m);

            if (FilteredProducts.Count == 1) SelectedMedicine = FilteredProducts.First();
        }

        private DateTime? GetNearestSafeExpiry(string barcode, string packaging)
        {
            int ptid;
            using (var conn = DbHelper.GetConnection())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM packaging_types WHERE name=@n LIMIT 1";
                cmd.Parameters.AddWithValue("@n", packaging);
                ptid = Convert.ToInt32(cmd.ExecuteScalar());
            }

            using var conn2 = DbHelper.GetConnection();
            using var cmd2 = conn2.CreateCommand();
            cmd2.CommandText = @"SELECT MIN(expiry_date)
                                   FROM inventory
                                  WHERE barcode=@bc
                                    AND packaging_type_id=@pt
                                    AND quantity>0
                                    AND expiry_date > DATE_ADD(NOW(),INTERVAL 3 MONTH)";
            cmd2.Parameters.AddWithValue("@bc", barcode);
            cmd2.Parameters.AddWithValue("@pt", ptid);
            var res = cmd2.ExecuteScalar();
            return res == null || res == DBNull.Value ? null : Convert.ToDateTime(res);
        }
        #endregion

        #region INotifyPropertyChanged helpers
        public event PropertyChangedEventHandler? PropertyChanged;

        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name!);
            return true;
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private void RaiseCartChanged()
        {
            OnPropertyChanged(nameof(CartItems));
            OnPropertyChanged(nameof(CartQuantity));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(ChangeDue));
        }
        #endregion

        #region Warning helper
        private void Warn(string msg) => ShowWarning(msg);
        public Action<string> ShowWarning = msg => System.Windows.MessageBox.Show(msg, "Warning", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        #endregion
    }

}
