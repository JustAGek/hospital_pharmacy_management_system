using System.Collections.Generic;

namespace WpfApp1
{
    public class SalesPatientModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public List<string> Allergies { get; set; } = new List<string>();
        public override string ToString() => $"{FullName} - {PhoneNumber}";
    }
}
