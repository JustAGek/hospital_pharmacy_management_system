using System.ComponentModel;

namespace WpfApp1
{
    public class SalesCartItemModel : INotifyPropertyChanged
    {
        public string Barcode { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string Packaging { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string ActiveIngredient { get; set; } = "";
        public int? StripsPerBox { get; set; }
        public DateTime ExpiryDate { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
