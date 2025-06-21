using System.Windows;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class ChangePasswordWindow : Window
    {
        public ChangePasswordWindow()
        {
            InitializeComponent();
        }

        private void Change_Click(object sender, RoutedEventArgs e)
        {
            string oldPass = OldPasswordBox.Password.Trim();
            string newPass = NewPasswordBox.Password.Trim();
            string confirmPass = ConfirmPasswordBox.Password.Trim();

            if (string.IsNullOrEmpty(oldPass) || string.IsNullOrEmpty(newPass) || string.IsNullOrEmpty(confirmPass))
            {
                MessageBox.Show("Please fill in all fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPass != confirmPass)
            {
                MessageBox.Show("New passwords do not match.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT password_hash FROM users WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", Session.UserId);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                MessageBox.Show("User not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string hash = reader.GetString("password_hash");
            reader.Close();

            if (!AuthHelper.VerifyPassword(oldPass, hash))
            {
                MessageBox.Show("Old password is incorrect.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string newHash = AuthHelper.HashPassword(newPass);

            cmd.CommandText = "UPDATE users SET password_hash = @newpass WHERE id = @id";
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("@newpass", newHash);
            cmd.Parameters.AddWithValue("@id", Session.UserId);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Password changed successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}
