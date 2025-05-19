using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class MedicineViewModel
    {
        public int Index { get; set; }
        public string Barcode { get; set; } = "";
        public string NameEn { get; set; } = "";
        public string NameAr { get; set; } = "";
        public string ActiveIngredient { get; set; } = "";
        public string Dose { get; set; } = "";
        public string MedicineType { get; set; } = "";
        public decimal PricePerBox { get; set; }
        public decimal PricePerStrip { get; set; }
        public string Company { get; set; } = "";
        public string Use { get; set; } = "";
        public string Origin { get; set; } = "";
        public string Availability { get; set; } = "";
        public DateTime LastUpdated { get; set; }
    }
}