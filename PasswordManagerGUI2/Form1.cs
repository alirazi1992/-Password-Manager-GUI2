using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;

namespace PasswordManagerGUI
{
    public partial class Form1 : Form
    {
        private string _dbPath;
        private string _connectionString;

        // Demo key only. For a real app, use a securely-derived key & random IV per record.
        private static readonly string Key = "MySuperSecretKey123";

        public Form1()
        {
            InitializeComponent();

            _dbPath = Path.Combine(AppContext.BaseDirectory, "passwords.db");
            _connectionString = "Data Source=" + _dbPath;

            try
            {
                EnsureDatabase();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error:\n" + ex, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- DB bootstrap ----------
        private void EnsureDatabase()
        {
            if (!File.Exists(_dbPath))
                using (File.Create(_dbPath)) { }

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = @"
CREATE TABLE IF NOT EXISTS Accounts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Website TEXT NOT NULL,
    Username TEXT NOT NULL,
    Password TEXT NOT NULL
);";
                using (var cmd = new SqliteCommand(sql, conn))
                    cmd.ExecuteNonQuery();
            }
        }

        // ---------- CRUD ----------
        private void btnAdd_Click(object sender, EventArgs e)
        {
            try
            {
                var website = (txtWebsite.Text ?? "").Trim();
                var username = (txtUsername.Text ?? "").Trim();
                var password = (txtPassword.Text ?? "").Trim();

                if (website.Length == 0 || username.Length == 0 || password.Length == 0)
                {
                    MessageBox.Show("Please fill Website, Username, and Password.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var encrypted = Encrypt(password);

                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = "INSERT INTO Accounts (Website, Username, Password) VALUES ($w,$u,$p)";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("$w", website);
                        cmd.Parameters.AddWithValue("$u", username);
                        cmd.Parameters.AddWithValue("$p", encrypted);
                        cmd.ExecuteNonQuery();
                    }
                }

                ClearInputs();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Add failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvAccounts.CurrentRow == null)
                {
                    MessageBox.Show("Select a row first.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                object idObj = dgvAccounts.CurrentRow.Cells["colId"].Value;
                if (idObj == null || idObj == DBNull.Value)
                {
                    MessageBox.Show("Selected row has no Id.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int id = Convert.ToInt32(idObj);

                if (MessageBox.Show("Delete this account?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = "DELETE FROM Accounts WHERE Id=$id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("$id", id);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadData();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Delete failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnReveal_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvAccounts.CurrentRow == null)
                {
                    MessageBox.Show("Select a row first.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                object idObj = dgvAccounts.CurrentRow.Cells["colId"].Value;
                if (idObj == null || idObj == DBNull.Value)
                {
                    MessageBox.Show("Selected row has no Id.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int id = Convert.ToInt32(idObj);

                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();
                    const string sql = "SELECT Password FROM Accounts WHERE Id=$id";
                    using (var cmd = new SqliteCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("$id", id);
                        var encrypted = cmd.ExecuteScalar() as string;
                        if (!string.IsNullOrEmpty(encrypted))
                        {
                            string decrypted = Decrypt(encrypted);
                            MessageBox.Show("Password: " + decrypted, "Reveal",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            MessageBox.Show("No password stored.", "Info",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reveal failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- NEW: Update ----------
        private void btnUpdate_Click(object sender, EventArgs e)
        {
            try
            {
                if (dgvAccounts.CurrentRow == null)
                {
                    MessageBox.Show("Select a row first.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                object idObj = dgvAccounts.CurrentRow.Cells["colId"].Value;
                if (idObj == null || idObj == DBNull.Value)
                {
                    MessageBox.Show("Selected row has no Id.", "Info",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int id = Convert.ToInt32(idObj);

                string website = (txtWebsite.Text ?? "").Trim();
                string username = (txtUsername.Text ?? "").Trim();
                string password = (txtPassword.Text ?? "").Trim();

                if (website.Length == 0 || username.Length == 0)
                {
                    MessageBox.Show("Website and Username cannot be empty.", "Validation",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var conn = new SqliteConnection(_connectionString))
                {
                    conn.Open();

                    string sql;
                    if (string.IsNullOrWhiteSpace(password))
                    {
                        sql = "UPDATE Accounts SET Website=$w, Username=$u WHERE Id=$id";
                        using (var cmd = new SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("$w", website);
                            cmd.Parameters.AddWithValue("$u", username);
                            cmd.Parameters.AddWithValue("$id", id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    else
                    {
                        sql = "UPDATE Accounts SET Website=$w, Username=$u, Password=$p WHERE Id=$id";
                        using (var cmd = new SqliteCommand(sql, conn))
                        {
                            cmd.Parameters.AddWithValue("$w", website);
                            cmd.Parameters.AddWithValue("$u", username);
                            cmd.Parameters.AddWithValue("$p", Encrypt(password));
                            cmd.Parameters.AddWithValue("$id", id);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                LoadData();
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Update failed:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------- NEW: grid → inputs ----------
        private void dgvAccounts_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && dgvAccounts.CurrentRow != null)
            {
                var w = dgvAccounts.CurrentRow.Cells["colWebsite"].Value;
                var u = dgvAccounts.CurrentRow.Cells["colUsername"].Value;
                txtWebsite.Text = w == null ? "" : w.ToString();
                txtUsername.Text = u == null ? "" : u.ToString();
                txtPassword.Clear(); // only set if user wants to change
            }
        }

        // ---------- NEW: Search ----------
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            string term = (txtSearch.Text ?? "").Trim();

            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                string sql = "SELECT Id, Website, Username FROM Accounts WHERE Website LIKE $s OR Username LIKE $s";
                using (var cmd = new SqliteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("$s", "%" + term + "%");
                    using (var reader = cmd.ExecuteReader())
                    {
                        var dt = new DataTable();
                        dt.Load(reader);
                        dgvAccounts.AutoGenerateColumns = false;
                        dgvAccounts.DataSource = dt;
                    }
                }
            }
        }

        // ---------- Data binding ----------
        private void LoadData()
        {
            using (var conn = new SqliteConnection(_connectionString))
            {
                conn.Open();
                const string sql = "SELECT Id, Website, Username FROM Accounts";
                using (var cmd = new SqliteCommand(sql, conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var dt = new DataTable();
                    dt.Load(reader);
                    dgvAccounts.AutoGenerateColumns = false;
                    dgvAccounts.DataSource = dt;
                }
            }
        }

        // ---------- Helpers ----------
        private void ClearInputs()
        {
            txtWebsite.Clear();
            txtUsername.Clear();
            txtPassword.Clear();
            txtWebsite.Focus();
        }

        // AES (demo only)
        private string Encrypt(string plainText)
        {
            if (plainText == null) plainText = "";
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
                aes.IV = new byte[16]; // demo only (use random IV per record in real apps)

                using (var enc = aes.CreateEncryptor(aes.Key, aes.IV))
                {
                    byte[] input = Encoding.UTF8.GetBytes(plainText);
                    byte[] cipher = enc.TransformFinalBlock(input, 0, input.Length);
                    return Convert.ToBase64String(cipher);
                }
            }
        }

        private string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(Key.PadRight(32).Substring(0, 32));
                aes.IV = new byte[16];

                using (var dec = aes.CreateDecryptor(aes.Key, aes.IV))
                {
                    byte[] input = Convert.FromBase64String(cipherText);
                    byte[] plain = dec.TransformFinalBlock(input, 0, input.Length);
                    return Encoding.UTF8.GetString(plain);
                }
            }
        }
    }
}
