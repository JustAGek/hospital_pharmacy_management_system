using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class RegistrationWindow : Window
    {
        public RegistrationWindow()
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

        private void CreateAccountButton_Click(object sender, RoutedEventArgs e)
        {
            // hide previous success
            SuccessTextBlock.Visibility = Visibility.Collapsed;

            string fullName = FullNameTextBox.Text.Trim();
            string username = UsernameTextBox.Text.Trim();
            string password = PasswordBox.Password;
            string userType = ((ComboBoxItem)UserTypeComboBox.SelectedItem).Content.ToString();

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(username) ||
                string.IsNullOrWhiteSpace(password))
            {
                // you could show an inline error similarly
                return;
            }

            string hash = BCrypt.Net.BCrypt.HashPassword(password);

            try
            {
                using var conn = DbHelper.GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                  @"INSERT INTO users (full_name,username,password_hash,user_type)
                    VALUES (@fn,@un,@pw,@ut)";
                cmd.Parameters.AddWithValue("@fn", fullName);
                cmd.Parameters.AddWithValue("@un", username);
                cmd.Parameters.AddWithValue("@pw", hash);
                cmd.Parameters.AddWithValue("@ut", userType);
                cmd.ExecuteNonQuery();
            }
            catch (MySqlException ex) when (ex.Number == 1062)
            {
                // Duplicate username
                // You can show inline error here if you like
                return;
            }

            // Show inline success message
            SuccessTextBlock.Visibility = Visibility.Visible;
        }

        private void BackToLoginButton_Click(object sender, RoutedEventArgs e)
        {
            new LoginWindow().Show();
            Close();
        }
    }
}
