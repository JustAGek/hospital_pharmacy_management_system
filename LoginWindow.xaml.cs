using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
        }

        // Clear placeholder text on first focus
        private void RemoveText(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb && tb.Text.StartsWith("Type"))
            {
                tb.Clear();
                tb.GotFocus -= RemoveText;
            }
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // hide previous error
            ErrorTextBlock.Visibility = Visibility.Collapsed;

            string user = UsernameTextBox.Text.Trim();
            string pass = PasswordBox.Password;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                ShowError("Enter username and password.");
                return;
            }

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
              "SELECT full_name, password_hash " +
              "FROM users WHERE username=@u";
            cmd.Parameters.AddWithValue("@u", user);

            using var rdr = cmd.ExecuteReader();
            if (!rdr.Read())
            {
                ShowError("This User doesn't exist");
                return;
            }

            string fullName = rdr.GetString(0);
            string storedHash = rdr.GetString(1);
            rdr.Close();

            if (!AuthHelper.VerifyPassword(pass, storedHash))
            {
                ShowError("Wrong Password");
                return;
            }

            // Get user type and ID
            string userType = "";
            int userId = -1;

            using (var typeCmd = conn.CreateCommand())
            {
                typeCmd.CommandText = "SELECT user_type, id FROM users WHERE username=@u";
                typeCmd.Parameters.AddWithValue("@u", user);
                using var tr = typeCmd.ExecuteReader();
                if (tr.Read())
                {
                    userType = tr.GetString(0);
                    userId = tr.GetInt32(1);
                }

            }

            Session.UserFullName = fullName;
            Session.UserType = userType.ToLower();
            Session.UserId = userId;
            Session.LoginTime = DateTime.Now;

            // Track login time for pharmacist
            if (userType == "pharmacist")
            {
                using var sess = conn.CreateCommand();
                sess.CommandText = "INSERT INTO user_sessions(user_id, login_time) VALUES(@id, NOW())";
                sess.Parameters.AddWithValue("@id", userId);
                sess.ExecuteNonQuery();
            }


            // open dashboard
            var dash = new DashboardWindow(fullName);
            dash.Show();
            Close();
        }

        private void ShowError(string msg)
        {
            ErrorTextBlock.Text = msg;
            ErrorTextBlock.Visibility = Visibility.Visible;
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var reg = new RegistrationWindow();
            reg.Show();
            Close();
        }
    }
}
