using System;
using System.ComponentModel;

namespace WpfApp1
{
    public class SalesCartItemModel : INotifyPropertyChanged
    {
        public string Barcode { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string Packaging { get; set; } = "";
        public string ActiveIngredient { get; set; } = "";
        public DateTime ExpiryDate { get; set; }
        public decimal UnitPrice { get; set; }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity == value) return;
                _quantity = value;
                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(LineTotal));
            }
        }

        public decimal LineTotal => UnitPrice * Quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
