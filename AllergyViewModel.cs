using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class AllergyViewModel
    {
        public int Index { get; set; }
        public int PatientId { get; set; }
        public string PatientName { get; set; } = "";
        public string PatientPhone { get; set; } = "";
        public string ActiveIngredient { get; set; } = "";
        public string Reaction { get; set; } = "";
    }


    public class PatientComboViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";

        public override string ToString() => $"{FullName} - {PhoneNumber}";
    }
}
