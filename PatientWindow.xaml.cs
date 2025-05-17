using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class PatientWindow : Window
    {
        public ObservableCollection<PatientViewModel> Patients { get; } = new ObservableCollection<PatientViewModel>();

        public PatientWindow()
        {
            InitializeComponent();
            PatientListGrid.ItemsSource = Patients;
            LoadPatients();
        }

        private void LoadPatients(string filter = "")
        {
            Patients.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            if (!string.IsNullOrEmpty(filter))
            {
                cmd.CommandText = "SELECT id, full_name, birth_date, sexual_orientation FROM patients WHERE full_name LIKE @name";
                cmd.Parameters.AddWithValue("@name", $"%{filter}%");
            }
            else
            {
                cmd.CommandText = "SELECT id, full_name, birth_date, sexual_orientation FROM patients";
            }

            using var rdr = cmd.ExecuteReader();
            int index = 1;
            while (rdr.Read())
            {
                Patients.Add(new PatientViewModel
                {
                    Index = index++,
                    Id = rdr.GetInt32("id"),
                    FullName = rdr.GetString("full_name"),
                    BirthDate = rdr.GetDateTime("birth_date"),
                    SexualOrientation = rdr.GetString("sexual_orientation")
                });
            }
        }

        private void SavePatient_Click(object sender, RoutedEventArgs e)
        {
            string fullName = PatientFullNameTextBox.Text.Trim();
            DateTime? birthDate = PatientDOBPicker.SelectedDate;
            string orientation = ((PatientSexualOrientationComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()) ?? string.Empty;


            if (string.IsNullOrEmpty(fullName) || birthDate == null || string.IsNullOrEmpty(orientation))
            {
                MessageBox.Show("Please fill all fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO patients (full_name, birth_date, sexual_orientation)
                VALUES (@fn, @dob, @so);
            ";
            cmd.Parameters.AddWithValue("@fn", fullName);
            cmd.Parameters.AddWithValue("@dob", birthDate.Value);
            cmd.Parameters.AddWithValue("@so", orientation);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Patient saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadPatients();
        }

        private void CancelPatient_Click(object sender, RoutedEventArgs e)
        {
            PatientFullNameTextBox.Clear();
            PatientDOBPicker.SelectedDate = null;
            PatientSexualOrientationComboBox.SelectedIndex = -1;
            MessageBox.Show("Cleared.", "Reset", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DeletePatient_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PatientViewModel vm)
                return;

            var result = MessageBox.Show($"Are you sure you want to delete {vm.FullName}?",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM patients WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", vm.Id);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Deleted successfully.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadPatients();
        }

        private void SearchPatient_Click(object sender, RoutedEventArgs e)
        {
            string searchText = PatientSearchTextBox.Text.Trim();
            LoadPatients(searchText);
        }

        private void ResetPatientSearch_Click(object sender, RoutedEventArgs e)
        {
            PatientSearchTextBox.Clear();
            LoadPatients();
        }
    }

    public class PatientViewModel
    {
        public int Index { get; set; }
        public int Id { get; set; }
        public string FullName { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public string SexualOrientation { get; set; } = "";
    }
}
