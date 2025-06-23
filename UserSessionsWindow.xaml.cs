using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class UserSessionsWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<UserSessionItem> SessionsList { get; } = new();
        public ObservableCollection<UserModel> UsersList { get; } = new();

        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { _fromDate = value; OnPropertyChanged(nameof(FromDate)); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { _toDate = value; OnPropertyChanged(nameof(ToDate)); }
        }

        private UserModel? _selectedUser;
        public UserModel? SelectedUser
        {
            get => _selectedUser;
            set { _selectedUser = value; OnPropertyChanged(nameof(SelectedUser)); }
        }

        public UserSessionsWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadUsers();
            FetchSessions();
        }

        private void LoadUsers()
        {
            UsersList.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, full_name FROM users ORDER BY full_name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                UsersList.Add(new UserModel { Id = r.GetInt32(0), FullName = r.GetString(1) });
        }

        private void FetchSessions()
        {
            SessionsList.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT us.session_id, us.login_time, us.logout_time, u.full_name
FROM user_sessions us
JOIN users u ON u.id = us.user_id
WHERE (@uid IS NULL OR us.user_id = @uid)
  AND (@from IS NULL OR us.login_time >= @from)
  AND (@to   IS NULL OR us.login_time <= @to)
ORDER BY us.session_id DESC";
            cmd.Parameters.AddWithValue("@uid", SelectedUser?.Id);
            cmd.Parameters.AddWithValue("@from", FromDate);
            cmd.Parameters.AddWithValue("@to", ToDate?.AddDays(1).AddSeconds(-1));
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                SessionsList.Add(new UserSessionItem
                {
                    SessionId = r.GetInt32("session_id"),
                    UserFullName = r.GetString("full_name"),
                    LoginTime = r.GetDateTime("login_time"),
                    LogoutTime = r.IsDBNull(r.GetOrdinal("logout_time")) ? (DateTime?)null : r.GetDateTime("logout_time")
                });
            }
        }

        private void FetchButton_Click(object sender, RoutedEventArgs e) => FetchSessions();

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedUser = null;
            FromDate = null;
            ToDate = null;
            FetchSessions();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class UserSessionItem
    {
        public int SessionId { get; set; }
        public string UserFullName { get; set; } = "";
        public DateTime LoginTime { get; set; }
        public DateTime? LogoutTime { get; set; }
    }

    public class UserModel { public int Id { get; set; } public string FullName { get; set; } = ""; }
}
