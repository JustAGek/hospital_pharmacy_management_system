namespace WpfApp1
{
    public class InventoryViewModel
    {
        public int Index { get; set; }
        public string Barcode { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string MedicineType { get; set; } = "";
        public string Packaging { get; set; } = "";
        public int Quantity { get; set; }
        public DateTime ExpiryDate { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
