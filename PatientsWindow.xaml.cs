using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class PatientsWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<PatientViewModel> Patients { get; set; } = new ObservableCollection<PatientViewModel>();
        public ObservableCollection<PatientViewModel> FilteredPatients { get; set; } = new ObservableCollection<PatientViewModel>();
        public ObservableCollection<string> SexList { get; } = new ObservableCollection<string> { "Male", "Female" };

        private int? editingPatientId = null;

        public PatientsWindow()
        {
            InitializeComponent();
            this.DataContext = this;
            PatientSexComboBox.ItemsSource = SexList;
            LoadPatients();
        }

        private void LoadPatients()
        {
            Patients.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, full_name, phone_number, birth_date, sexual_orientation, registration_time
                FROM patients
                ORDER BY full_name
            ";

            using var rdr = cmd.ExecuteReader();
            int idx = 1;
            while (rdr.Read())
            {
                Patients.Add(new PatientViewModel
                {
                    Index = idx++,
                    Id = rdr.GetInt32("id"),
                    FullName = rdr.GetString("full_name"),
                    PhoneNumber = rdr.GetString("phone_number"),
                    BirthDate = rdr.GetDateTime("birth_date"),
                    Sex = rdr.GetString("sexual_orientation"),
                    RegistrationTime = rdr.GetDateTime("registration_time"),
                });
            }
            ApplySearchFilter(SearchTextBox.Text);
        }

        private void ApplySearchFilter(string query)
        {
            FilteredPatients.Clear();
            var lower = (query ?? "").Trim().ToLower();
            var filtered = Patients.Where(p =>
                string.IsNullOrEmpty(lower) ||
                p.FullName.ToLower().Contains(lower) ||
                p.PhoneNumber.ToLower().Contains(lower)
            ).ToList();
            int idx = 1;
            foreach (var p in filtered)
            {
                p.Index = idx++;
                FilteredPatients.Add(p);
            }
            OnPropertyChanged(nameof(FilteredPatients));
        }

        private void SavePatient_Click(object sender, RoutedEventArgs e)
        {
            string name = PatientNameTextBox.Text.Trim();
            string phone = PatientPhoneTextBox.Text.Trim();
            string sex = PatientSexComboBox.SelectedItem?.ToString() ?? "";
            DateTime? birthDate = PatientBirthDatePicker.SelectedDate;

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(sex) || birthDate == null)
            {
                MessageBox.Show("Please fill all fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            if (editingPatientId == null)
            {
                cmd.CommandText = @"
                    INSERT INTO patients (full_name, phone_number, birth_date, sexual_orientation)
                    VALUES (@name, @phone, @birth, @sex)
                ";
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE patients SET full_name=@name, phone_number=@phone, birth_date=@birth, sexual_orientation=@sex
                    WHERE id=@id
                ";
                cmd.Parameters.AddWithValue("@id", editingPatientId.Value);
            }

            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@phone", phone);
            cmd.Parameters.AddWithValue("@birth", birthDate.Value.Date);
            cmd.Parameters.AddWithValue("@sex", sex);

            cmd.ExecuteNonQuery();

            MessageBox.Show(editingPatientId == null ? "Patient added." : "Patient updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForm();
            LoadPatients();
        }

        private void CancelPatient_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void EditPatient_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PatientViewModel vm) return;

            PatientNameTextBox.Text = vm.FullName;
            PatientPhoneTextBox.Text = vm.PhoneNumber;
            PatientBirthDatePicker.SelectedDate = vm.BirthDate;
            PatientSexComboBox.SelectedItem = vm.Sex;
            editingPatientId = vm.Id;
        }

        private void DeletePatient_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not PatientViewModel vm) return;

            var result = MessageBox.Show($"Delete {vm.FullName}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM patients WHERE id=@id";
            cmd.Parameters.AddWithValue("@id", vm.Id);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Deleted.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadPatients();
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            this.Close();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter(SearchTextBox.Text);
        }

        private void ClearForm()
        {
            PatientNameTextBox.Clear();
            PatientPhoneTextBox.Clear();
            PatientBirthDatePicker.SelectedDate = null;
            PatientSexComboBox.SelectedIndex = -1;
            editingPatientId = null;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
