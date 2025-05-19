using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public class PatientViewModel
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public string Sex { get; set; } = "";
        public DateTime RegistrationTime { get; set; }
    }
}
