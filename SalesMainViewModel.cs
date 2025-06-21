using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace WpfApp1
{
    public class SalesMainViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<SalesMedicineModel> _allProducts = new();
        public ObservableCollection<SalesMedicineModel> FilteredProducts { get; set; } = new();
        public ObservableCollection<SalesPatientModel> PatientsList { get; set; } = new();
        public ObservableCollection<string> PackagingTypes { get; set; } = new();
        public ObservableCollection<SalesCartItemModel> CartItems { get; set; } = new();

        private string _searchNameEn;
        public string SearchNameEn
        {
            get => _searchNameEn;
            set
            {
                if (_searchNameEn != value)
                {
                    _searchNameEn = value;
                    OnPropertyChanged(nameof(SearchNameEn));
                    FilterProducts("en");
                }
            }
        }

        private string _searchNameAr;
        public string SearchNameAr
        {
            get => _searchNameAr;
            set
            {
                if (_searchNameAr != value)
                {
                    _searchNameAr = value;
                    OnPropertyChanged(nameof(SearchNameAr));
                    FilterProducts("ar");
                }
            }
        }

        private string _searchBarcode;
        public string SearchBarcode
        {
            get => _searchBarcode;
            set
            {
                if (_searchBarcode != value)
                {
                    _searchBarcode = value;
                    OnPropertyChanged(nameof(SearchBarcode));
                    FilterProducts("barcode");
                }
            }
        }

        private SalesMedicineModel _selectedMedicine;
        public SalesMedicineModel SelectedMedicine
        {
            get => _selectedMedicine;
            set
            {
                if (_selectedMedicine != value)
                {
                    _selectedMedicine = value;
                    OnPropertyChanged(nameof(SelectedMedicine));
                    UpdatePackagingTypes();
                    UpdateSearchFieldsFromSelection();
                    OnPropertyChanged(nameof(SelectedProductPrice));
                }
            }
        }

        private SalesPatientModel _selectedPatient;
        public SalesPatientModel SelectedPatient
        {
            get => _selectedPatient;
            set
            {
                if (_selectedPatient != value)
                {
                    _selectedPatient = value;
                    OnPropertyChanged(nameof(SelectedPatient));
                }
            }
        }

        private void UpdateSearchFieldsFromSelection()
        {
            if (SelectedMedicine != null)
            {
                _searchNameEn = SelectedMedicine.NameEn;
                _searchNameAr = SelectedMedicine.NameAr;
                _searchBarcode = SelectedMedicine.Barcode;
                OnPropertyChanged(nameof(SearchNameEn));
                OnPropertyChanged(nameof(SearchNameAr));
                OnPropertyChanged(nameof(SearchBarcode));
            }
        }

        private void FilterProducts(string triggerField)
        {
            string nameEn = _searchNameEn?.Trim().ToLower() ?? "";
            string nameAr = _searchNameAr?.Trim().ToLower() ?? "";
            string barcode = _searchBarcode?.Trim() ?? "";

            FilteredProducts.Clear();
            var filtered = _allProducts.AsEnumerable();
            if (triggerField == "en" && !string.IsNullOrEmpty(nameEn))
                filtered = filtered.Where(p => !string.IsNullOrEmpty(p.NameEn) && p.NameEn.ToLower().Contains(nameEn));
            else if (triggerField == "ar" && !string.IsNullOrEmpty(nameAr))
                filtered = filtered.Where(p => !string.IsNullOrEmpty(p.NameAr) && p.NameAr.ToLower().Contains(nameAr));
            else if (triggerField == "barcode" && !string.IsNullOrEmpty(barcode))
                filtered = filtered.Where(p => !string.IsNullOrEmpty(p.Barcode) && p.Barcode.Contains(barcode));

            foreach (var m in filtered)
                FilteredProducts.Add(m);

            if (FilteredProducts.Count == 1)
            {
                SelectedMedicine = FilteredProducts.First();
            }
        }

        public void SetAllProducts(System.Collections.Generic.List<SalesMedicineModel> meds)
        {
            _allProducts.Clear();
            foreach (var m in meds) _allProducts.Add(m);
            FilteredProducts.Clear();
            foreach (var m in meds) FilteredProducts.Add(m);
        }

        private int _availableStock;
        public int AvailableStock
        {
            get => _availableStock;
            set
            {
                if (_availableStock != value)
                {
                    _availableStock = value;
                    OnPropertyChanged(nameof(AvailableStock));
                }
            }
        }

        private string _selectedPackaging;
        public string SelectedPackaging
        {
            get => _selectedPackaging;
            set
            {
                if (_selectedPackaging != value)
                {
                    _selectedPackaging = value;
                    OnPropertyChanged(nameof(SelectedPackaging));
                    OnPropertyChanged(nameof(SelectedProductPrice));
                }
            }
        }

        public int QuantityToAdd { get; set; } = 1;

        public decimal Discount
        {
            get => _discount;
            set
            {
                _discount = value;
                OnPropertyChanged(nameof(Discount));
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(ChangeDue));
            }
        }
        private decimal _discount;

        public decimal Subtotal => CartItems.Sum(i => i.LineTotal);
        public decimal Total => Math.Max(0, Subtotal - Discount);

        public decimal AmountPaid
        {
            get => _amountPaid;
            set
            {
                _amountPaid = value;
                OnPropertyChanged(nameof(AmountPaid));
                OnPropertyChanged(nameof(ChangeDue));
            }
        }
        private decimal _amountPaid;

        public decimal ChangeDue => AmountPaid - Total;

        public decimal SelectedProductPrice
        {
            get
            {
                if (SelectedMedicine == null || string.IsNullOrWhiteSpace(SelectedPackaging))
                    return 0;
                if (SelectedPackaging == "strip")
                    return SelectedMedicine.PricePerStrip;
                else
                    return SelectedMedicine.PricePerBox;
            }
        }

        public void UpdatePackagingTypes()
        {
            PackagingTypes.Clear();
            if (SelectedMedicine == null)
                return;
            if (SelectedMedicine.MedicineType.ToLower().Contains("tablet"))
            {
                PackagingTypes.Add("box");
                PackagingTypes.Add("strip");
            }
            else
            {
                PackagingTypes.Add("box");
            }
            SelectedPackaging = PackagingTypes.First();
        }

        public void AddToCart(Func<string, string, int> getAvailableStock, Func<string, string, DateTime?> getNearestExpiry, Func<string, string, int, bool> checkAllergy)
        {
            if (SelectedMedicine == null)
            {
                ShowWarning("Please select a medicine.");
                return;
            }
            if (string.IsNullOrWhiteSpace(SelectedPackaging))
            {
                ShowWarning("Please select packaging type.");
                return;
            }
            if (QuantityToAdd < 1)
            {
                ShowWarning("Quantity must be at least 1.");
                return;
            }

            int available = getAvailableStock(SelectedMedicine.Barcode, SelectedPackaging);
            if (available < QuantityToAdd)
            {
                ShowWarning($"Cannot add more than available stock. (Available: {available})");
                return;
            }

            DateTime? expiry = getNearestExpiry(SelectedMedicine.Barcode, SelectedPackaging);
            if (expiry != null && expiry.Value <= DateTime.Now.AddMonths(3))
            {
                ShowWarning("Cannot sell expired or near expiry medicine.");
                return;
            }

            if (SelectedPatient != null && checkAllergy(SelectedMedicine.ActiveIngredient, SelectedPatient.FullName, SelectedPatient.Id))
            {
                ShowWarning($"Patient is allergic to: {SelectedMedicine.ActiveIngredient}");
                return;
            }

            var price = SelectedPackaging == "strip" ? SelectedMedicine.PricePerStrip : SelectedMedicine.PricePerBox;
            var existing = CartItems.FirstOrDefault(i => i.Barcode == SelectedMedicine.Barcode && i.Packaging == SelectedPackaging);
            if (existing != null)
            {
                existing.Quantity += QuantityToAdd;
            }
            else
            {
                CartItems.Add(new SalesCartItemModel
                {
                    Barcode = SelectedMedicine.Barcode,
                    NameEn = SelectedMedicine.NameEn,
                    Packaging = SelectedPackaging,
                    Quantity = QuantityToAdd,
                    UnitPrice = price,
                    ActiveIngredient = SelectedMedicine.ActiveIngredient
                });
            }
            OnPropertyChanged(nameof(CartItems));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(ChangeDue));
        }

        public Action<string> ShowWarning = s => { };

        public void RemoveCartItem(SalesCartItemModel item)
        {
            CartItems.Remove(item);
            OnPropertyChanged(nameof(CartItems));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(ChangeDue));
        }

        public void ClearCart()
        {
            CartItems.Clear();
            OnPropertyChanged(nameof(CartItems));
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Total));
            OnPropertyChanged(nameof(ChangeDue));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
