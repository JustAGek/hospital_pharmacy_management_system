using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace WpfApp1
{
    public partial class AllergiesWindow : Window, INotifyPropertyChanged
    {
        public ObservableCollection<PatientComboViewModel> PatientsList { get; set; } = new();
        public ObservableCollection<AllergyViewModel> Allergies { get; set; } = new();
        public ObservableCollection<AllergyViewModel> FilteredAllergies { get; set; } = new();

        public ObservableCollection<string> IngredientList { get; set; } = new();

        public PatientComboViewModel? SelectedPatient { get; set; }
        public string SelectedIngredient { get; set; } = "";

        private int? editingPatientId = null;
        private string? editingIngredient = null;

        public AllergiesWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadPatients();
            LoadAllergies();
            LoadIngredients();
        }

        private void LoadPatients()
        {
            PatientsList.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT id, full_name, phone_number FROM patients ORDER BY full_name";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                PatientsList.Add(new PatientComboViewModel
                {
                    Id = rdr.GetInt32("id"),
                    FullName = rdr.GetString("full_name"),
                    PhoneNumber = rdr.GetString("phone_number")
                });
            }
        }

        private void LoadAllergies()
        {
            Allergies.Clear();
            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT pa.patient_id, p.full_name, p.phone_number, pa.active_ingredient, pa.reaction
                FROM patient_allergies pa
                JOIN patients p ON p.id = pa.patient_id
                ORDER BY pa.patient_id DESC, pa.active_ingredient ASC
            ";
            using var rdr = cmd.ExecuteReader();
            int idx = 1;
            while (rdr.Read())
            {
                Allergies.Add(new AllergyViewModel
                {
                    Index = idx++,
                    PatientId = rdr.GetInt32("patient_id"),
                    PatientName = rdr.GetString("full_name"),
                    PatientPhone = rdr.GetString("phone_number"),
                    ActiveIngredient = rdr.GetString("active_ingredient"),
                    Reaction = rdr.GetString("reaction")
                });
            }
            ApplySearchFilter(SearchTextBox.Text);
        }

        private void LoadIngredients()
        {
            IngredientList.Clear();
            HashSet<string> allIngredients = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT active_ingredient FROM medicines ORDER BY active_ingredient";
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                // Split on +, ; or , and trim
                var ingString = rdr.GetString("active_ingredient");
                var split = ingString
                    .Split(new[] { '+', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim());
                foreach (var ing in split)
                {
                    if (!string.IsNullOrWhiteSpace(ing))
                        allIngredients.Add(ing);
                }
            }
            foreach (var ing in allIngredients.OrderBy(x => x))
                IngredientList.Add(ing);
        }


        private void SaveAllergy_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedPatient == null)
            {
                MessageBox.Show("Please select a patient.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string ingredient = SelectedIngredient?.Trim() ?? IngredientComboBox.Text.Trim();
            string reaction = ReactionTextBox.Text.Trim();

            if (string.IsNullOrEmpty(ingredient) || string.IsNullOrEmpty(reaction))
            {
                MessageBox.Show("Please fill all fields.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();

            if (editingPatientId == null || editingIngredient == null)
            {
                cmd.CommandText = @"
                    INSERT INTO patient_allergies (patient_id, active_ingredient, reaction)
                    VALUES (@pid, @ingredient, @reaction)
                ";
                cmd.Parameters.AddWithValue("@pid", SelectedPatient.Id);
                cmd.Parameters.AddWithValue("@ingredient", ingredient);
                cmd.Parameters.AddWithValue("@reaction", reaction);
            }
            else
            {
                cmd.CommandText = @"
                    UPDATE patient_allergies
                    SET patient_id=@pid, active_ingredient=@ingredient, reaction=@reaction
                    WHERE patient_id=@oldpid AND active_ingredient=@oldingredient
                ";
                cmd.Parameters.AddWithValue("@pid", SelectedPatient.Id);
                cmd.Parameters.AddWithValue("@ingredient", ingredient);
                cmd.Parameters.AddWithValue("@reaction", reaction);
                cmd.Parameters.AddWithValue("@oldpid", editingPatientId.Value);
                cmd.Parameters.AddWithValue("@oldingredient", editingIngredient);
            }
            cmd.ExecuteNonQuery();

            MessageBox.Show((editingPatientId == null || editingIngredient == null) ? "Allergy added." : "Allergy updated.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            ClearForm();
            LoadAllergies();
        }

        private void CancelAllergy_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void EditAllergy_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not AllergyViewModel vm) return;
            SelectedPatient = PatientsList.FirstOrDefault(p => p.Id == vm.PatientId);
            OnPropertyChanged(nameof(SelectedPatient));
            SelectedIngredient = vm.ActiveIngredient;
            OnPropertyChanged(nameof(SelectedIngredient));
            ReactionTextBox.Text = vm.Reaction;
            editingPatientId = vm.PatientId;
            editingIngredient = vm.ActiveIngredient;
        }

        private void DeleteAllergy_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not AllergyViewModel vm) return;
            if (MessageBox.Show($"Delete allergy for {vm.PatientName}?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            using var conn = DbHelper.GetConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM patient_allergies WHERE patient_id=@pid AND active_ingredient=@ingredient";
            cmd.Parameters.AddWithValue("@pid", vm.PatientId);
            cmd.Parameters.AddWithValue("@ingredient", vm.ActiveIngredient);
            cmd.ExecuteNonQuery();

            MessageBox.Show("Deleted.", "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            LoadAllergies();
        }

        private void BackToDashboard_Click(object sender, RoutedEventArgs e)
        {
            new DashboardWindow(Session.UserFullName).Show();
            Close();
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter(SearchTextBox.Text);
        }

        private void ApplySearchFilter(string? query)
        {
            FilteredAllergies.Clear();
            var lower = (query ?? "").Trim();
            var filtered = Allergies.Where(a =>
                string.IsNullOrEmpty(lower)
                || (a.PatientName?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (a.PatientPhone?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (a.ActiveIngredient?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
                || (a.Reaction?.IndexOf(lower, System.StringComparison.OrdinalIgnoreCase) ?? -1) >= 0
            ).ToList();
            int idx = 1;
            foreach (var a in filtered)
            {
                a.Index = idx++;
                FilteredAllergies.Add(a);
            }
            OnPropertyChanged(nameof(FilteredAllergies));
        }

        private void ClearForm()
        {
            PatientComboBox.SelectedIndex = -1;
            SelectedPatient = null;
            IngredientComboBox.SelectedIndex = -1;
            SelectedIngredient = "";
            ReactionTextBox.Clear();
            editingPatientId = null;
            editingIngredient = null;
            OnPropertyChanged(nameof(SelectedPatient));
            OnPropertyChanged(nameof(SelectedIngredient));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
