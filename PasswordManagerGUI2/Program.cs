using System;
using System.Windows.Forms;
using SQLitePCL;  // NuGet: SQLitePCLRaw.bundle_e_sqlite3

namespace PasswordManagerGUI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Initialize native SQLite engine for Microsoft.Data.Sqlite
            try { Batteries_V2.Init(); } catch { Batteries.Init(); }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}
