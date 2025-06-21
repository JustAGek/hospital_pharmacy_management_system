using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class ManageUsersWindow : Window
    {
        public ObservableCollection<UserViewModel> Users { get; set; } = new();
        private int? editingUserId = null;

        public ManageUsersWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadUsers();
        }

        private void LoadUsers()
        {
            Users.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, full_name, username, user_type FROM users ORDER BY full_name";
            using var rdr = cmd.ExecuteReader();
            int idx = 1;
            while (rdr.Read())
            {
                Users.Add(new UserViewModel
                {
                    Index = idx++,
                    Id = rdr.GetInt32("id"),
                    FullName = rdr.GetString("full_name"),
                    Username = rdr.GetString("username"),
                    UserType = rdr.GetString("user_type")
                });
            }
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            var win = new RegistrationWindow();
            win.ShowDialog();
            LoadUsers();
        }

        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not UserViewModel vm) return;
            MessageBox.Show($"Editing user: {vm.Username}. You could redirect to a dedicated edit form.", "Edit User");
            // Implement dedicated user editing if needed
        }

        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not UserViewModel vm)
                return;

            if (Session.UserId == vm.Id)
            {
                MessageBox.Show("You cannot delete the currently logged-in user.", "Operation Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete user: {vm.FullName}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using var conn = DbHelper.GetConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM users WHERE id = @id";
                cmd.Parameters.AddWithValue("@id", vm.Id);
                int rowsAffected = cmd.ExecuteNonQuery();

                if (rowsAffected > 0)
                {
                    MessageBox.Show("User deleted successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadUsers(); // Refresh the list
                }
                else
                {
                    MessageBox.Show("User could not be deleted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while deleting user:\n" + ex.Message, "Exception", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow("Ahmed Mohammed EL Sherbeny").Show();
            Close();

        }

    }

    public class UserViewModel
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public string Username { get; set; } = "";
        public string UserType { get; set; } = "";
    }
}
