namespace WpfApp1
{
    public class SalesMedicineModel
    {
        public string Barcode { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string NameAr { get; set; } = "";
        public string MedicineType { get; set; } = "";
        public string ActiveIngredient { get; set; } = "";
        public decimal PricePerBox { get; set; }
        public decimal PricePerStrip { get; set; }
        public int? StripsPerBox { get; set; }
        public override string ToString() => $"{NameEn} - {NameAr} ({Barcode})";
    }
}
