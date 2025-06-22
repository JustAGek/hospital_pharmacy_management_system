using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{
    public static class Session
    {
        public static string UserFullName { get; set; } = "";
        public static string UserType { get; set; } = "";

        public static int SessionId { get; set; }
        public static int UserId { get; set; }
        public static DateTime LoginTime { get; set; }
    }
}