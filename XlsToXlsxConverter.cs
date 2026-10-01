// ============================================================================
//  Tekla Structures macro : XLS to XLSX Converter   v2.1
//  Author                 : Rajeev Pulari
//  Place in               : ..\Environments\common\macros\modeling\  (or drawings)
//  Run from               : Tekla > Applications & components > Macros
//
//  Converts Excel 97-2003 workbooks (.xls, e.g. Tekla reports / Organizer
//  exports) to .xlsx, next to the originals. Opens in the current model folder.
//  Uses Excel through PowerShell (Excel must be installed).
//  Optional: delete each original .xls after a VERIFIED conversion.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using TSM = Tekla.Structures.Model;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            using (ConvertForm form = new ConvertForm())
                form.ShowDialog();
        }
    }

    // ── Windows Form ────────────────────────────────────────────────────────────
    public class ConvertForm : Form
    {
        private const int TimeoutMs = 300000;   // 5 minutes for the whole batch

        private ListBox  listFiles;
        private Button   btnBrowse;
        private Button   btnRemove;
        private Button   btnClear;
        private Button   btnConvert;
        private CheckBox chkDelete;
        private TextBox  txtLog;
        private Label    lblStatus;

        public ConvertForm()
        {
            this.Text            = "Tekla — XLS to XLSX Converter v2.1";
            this.Size            = new Size(660, 590);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.BackColor       = Color.FromArgb(245, 245, 245);

            // ── File list label ──────────────────────────────────────────────
            Label lblFiles    = new Label();
            lblFiles.Text     = "Selected XLS files:";
            lblFiles.Font     = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblFiles.Location = new Point(12, 12);
            lblFiles.Size     = new Size(300, 18);
            this.Controls.Add(lblFiles);

            // ── File list box ────────────────────────────────────────────────
            listFiles                     = new ListBox();
            listFiles.Location            = new Point(12, 34);
            listFiles.Size                = new Size(620, 200);
            listFiles.Font                = new Font("Consolas", 9f);
            listFiles.SelectionMode       = SelectionMode.MultiExtended;
            listFiles.HorizontalScrollbar = true;
            this.Controls.Add(listFiles);

            // ── Buttons row ──────────────────────────────────────────────────
            btnBrowse  = MakeButton("Browse...",        12, 246, 110, Color.SteelBlue);
            btnRemove  = MakeButton("Remove Selected", 130, 246, 130, Color.Gray);
            btnClear   = MakeButton("Clear All",       268, 246,  90, Color.Gray);
            btnConvert = MakeButton("Convert to XLSX", 522, 246, 110, Color.SeaGreen);

            btnBrowse.Click  += BrowseClick;
            btnRemove.Click  += RemoveClick;
            btnClear.Click   += (s, e) => { listFiles.Items.Clear(); txtLog.Clear(); lblStatus.Text = ""; };
            btnConvert.Click += ConvertClick;

            this.Controls.AddRange(new Control[] { btnBrowse, btnRemove, btnClear, btnConvert });

            // ── Delete-original option (on = previous behaviour) ─────────────
            chkDelete          = new CheckBox();
            chkDelete.Text     = "Delete original .xls after a successful conversion";
            chkDelete.Checked  = true;
            chkDelete.Font     = new Font("Segoe UI", 9f);
            chkDelete.Location = new Point(12, 284);
            chkDelete.Size     = new Size(620, 22);
            this.Controls.Add(chkDelete);

            // ── Status label ─────────────────────────────────────────────────
            lblStatus           = new Label();
            lblStatus.Text      = "";
            lblStatus.Font      = new Font("Segoe UI", 9f, FontStyle.Italic);
            lblStatus.ForeColor = Color.DimGray;
            lblStatus.Location  = new Point(12, 312);
            lblStatus.Size      = new Size(620, 18);
            this.Controls.Add(lblStatus);

            // ── Log label ────────────────────────────────────────────────────
            Label lblLog    = new Label();
            lblLog.Text     = "Conversion log:";
            lblLog.Font     = new Font("Segoe UI", 9f, FontStyle.Bold);
            lblLog.Location = new Point(12, 336);
            lblLog.Size     = new Size(200, 18);
            this.Controls.Add(lblLog);

            // ── Log text box ─────────────────────────────────────────────────
            txtLog            = new TextBox();
            txtLog.Location   = new Point(12, 356);
            txtLog.Size       = new Size(620, 180);
            txtLog.Multiline  = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly   = true;
            txtLog.BackColor  = Color.FromArgb(25, 25, 25);
            txtLog.ForeColor  = Color.LightGreen;
            txtLog.Font       = new Font("Consolas", 9f);
            this.Controls.Add(txtLog);
        }

        // ── Helper: create a uniform button ─────────────────────────────────────
        private Button MakeButton(string text, int x, int y, int width, Color color)
        {
            Button b    = new Button();
            b.Text      = text;
            b.Location  = new Point(x, y);
            b.Size      = new Size(width, 30);
            b.BackColor = color;
            b.ForeColor = Color.White;
            b.FlatStyle = FlatStyle.Flat;
            b.Font      = new Font("Segoe UI", 9f);
            b.FlatAppearance.BorderSize = 0;
            return b;
        }

        private void UpdateCount()
        {
            lblStatus.Text = listFiles.Items.Count + " file(s) ready to convert.";
        }

        // ── Browse for XLS files ─────────────────────────────────────────────────
        private void BrowseClick(object sender, EventArgs e)
        {
            // Open the dialog in the current Tekla model folder when connected
            string modelFolder = "";
            try
            {
                TSM.Model model = new TSM.Model();
                if (model.GetConnectionStatus()) modelFolder = model.GetInfo().ModelPath;
            }
            catch { }

            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title       = "Select one or more XLS files to convert";
                ofd.Filter      = "Excel 97-2003 Workbook (*.xls)|*.xls";
                ofd.Multiselect = true;

                if (!string.IsNullOrEmpty(modelFolder) && Directory.Exists(modelFolder))
                    ofd.InitialDirectory = modelFolder;

                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    foreach (string f in ofd.FileNames)
                    {
                        // The *.xls filter can still let .xlsx/.xlsm through when typed by hand
                        if (!string.Equals(Path.GetExtension(f), ".xls", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!ContainsPath(f)) listFiles.Items.Add(f);
                    }
                    UpdateCount();
                }
            }
        }

        private bool ContainsPath(string path)
        {
            foreach (object item in listFiles.Items)
                if (string.Equals(item.ToString(), path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // ── Remove selected items from list ──────────────────────────────────────
        private void RemoveClick(object sender, EventArgs e)
        {
            int[] selected = new int[listFiles.SelectedIndices.Count];
            listFiles.SelectedIndices.CopyTo(selected, 0);
            for (int i = selected.Length - 1; i >= 0; i--)
                listFiles.Items.RemoveAt(selected[i]);
            UpdateCount();
        }

        // ── Run conversion ───────────────────────────────────────────────────────
        private void ConvertClick(object sender, EventArgs e)
        {
            if (listFiles.Items.Count == 0)
            {
                MessageBox.Show(this, "Please add at least one .xls file using Browse.", "No Files Selected",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true);
            txtLog.Clear();
            lblStatus.Text = "Converting — please wait...";
            Application.DoEvents();

            // Unique temp names, so two Tekla sessions can't overwrite each other's files
            string tag      = Guid.NewGuid().ToString("N").Substring(0, 8);
            string listPath = Path.Combine(Path.GetTempPath(), "TeklaConvertList_" + tag + ".txt");
            string psPath   = Path.Combine(Path.GetTempPath(), "TeklaXlsConvert_"  + tag + ".ps1");
            string logPath  = Path.Combine(Path.GetTempPath(), "TeklaConvertLog_"  + tag + ".txt");

            var filePaths = new List<string>();
            foreach (object item in listFiles.Items) filePaths.Add(item.ToString());

            try
            {
                File.WriteAllLines(listPath, filePaths.ToArray(), Encoding.UTF8);
                File.WriteAllText(psPath, BuildScript(listPath, logPath, chkDelete.Checked), Encoding.UTF8);

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName        = "powershell.exe";
                psi.Arguments       = "-NoProfile -ExecutionPolicy Bypass -NonInteractive -WindowStyle Hidden -File \"" + psPath + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow  = true;

                bool finished;
                using (Process proc = Process.Start(psi))
                {
                    // Wait in short steps so the window keeps repainting
                    var sw = Stopwatch.StartNew();
                    finished = false;
                    while (sw.ElapsedMilliseconds < TimeoutMs)
                    {
                        if (proc.WaitForExit(200)) { finished = true; break; }
                        Application.DoEvents();
                    }
                    if (!finished)
                    {
                        try { proc.Kill(); } catch { }
                    }
                }

                if (File.Exists(logPath))
                    txtLog.Text = File.ReadAllText(logPath, Encoding.UTF8).Replace("\n", Environment.NewLine).Replace("\r\r", "\r");
                else
                    txtLog.Text = "No log was produced. PowerShell may have been blocked.";

                if (!finished)
                    txtLog.AppendText(Environment.NewLine + "TIMEOUT: stopped after " + (TimeoutMs / 60000) +
                        " minutes. Check Task Manager for a leftover EXCEL.EXE process.");

                // Keep only the files that did NOT convert, so they can be retried
                int done = 0;
                for (int i = listFiles.Items.Count - 1; i >= 0; i--)
                {
                    string xls  = listFiles.Items[i].ToString();
                    string xlsx = Path.ChangeExtension(xls, ".xlsx");
                    if (File.Exists(xlsx) && File.GetLastWriteTime(xlsx) >= DateTime.Now.AddMinutes(-(TimeoutMs / 60000) - 1))
                    {
                        listFiles.Items.RemoveAt(i);
                        done++;
                    }
                }
                lblStatus.Text = done + " converted" +
                    (listFiles.Items.Count > 0 ? ", " + listFiles.Items.Count + " left in the list (see log)." : ". Done.");
            }
            catch (Exception ex)
            {
                txtLog.Text    = "ERROR: " + ex.Message;
                lblStatus.Text = "Conversion failed.";
            }
            finally
            {
                try { File.Delete(psPath);   } catch { }
                try { File.Delete(listPath); } catch { }
                try { File.Delete(logPath);  } catch { }
                SetBusy(false);
            }
        }

        private void SetBusy(bool busy)
        {
            this.Cursor        = busy ? Cursors.WaitCursor : Cursors.Default;
            btnConvert.Enabled = !busy;
            btnBrowse.Enabled  = !busy;
            btnRemove.Enabled  = !busy;
            btnClear.Enabled   = !busy;
            chkDelete.Enabled  = !busy;
        }

        // ── PowerShell script builder ─────────────────────────────────────────────
        // Paths are embedded as PowerShell literal strings (single quotes doubled)
        private static string BuildScript(string listPath, string logPath, bool deleteOriginal)
        {
            string safeList = listPath.Replace("'", "''");
            string safeLog  = logPath.Replace("'", "''");

            return
@"$listPath = '" + safeList + @"'
$logPath  = '" + safeLog  + @"'
$deleteOriginal = $" + (deleteOriginal ? "true" : "false") + @"
$log      = New-Object System.Collections.Generic.List[string]
$log.Add(('[{0}] XLS to XLSX Conversion' -f [DateTime]::Now.ToString('yyyy-MM-dd HH:mm:ss')))
$log.Add(('-' * 50))

$files = Get-Content -LiteralPath $listPath -Encoding UTF8
$excel = $null
$converted = 0
$skipped   = 0
try {
    $excel                  = New-Object -ComObject Excel.Application
    $excel.Visible          = $false
    $excel.DisplayAlerts    = $false
    $excel.AskToUpdateLinks = $false

    foreach ($filePath in $files) {
        $filePath = $filePath.Trim()
        if ([string]::IsNullOrEmpty($filePath)) { continue }
        $name = [IO.Path]::GetFileName($filePath)
        if (-not (Test-Path -LiteralPath $filePath)) {
            $log.Add(('FAIL : {0}  --  file not found' -f $name)); $skipped++; continue
        }
        $dir  = [IO.Path]::GetDirectoryName($filePath)
        $xlsx = [IO.Path]::Combine($dir, [IO.Path]::GetFileNameWithoutExtension($filePath) + '.xlsx')
        $wb   = $null
        try {
            # UpdateLinks = 0, ReadOnly = true: the original is never modified
            $wb = $excel.Workbooks.Open($filePath, 0, $true)
            $wb.SaveAs($xlsx, 51)          # 51 = xlOpenXMLWorkbook (.xlsx)
            $wb.Close($false)
            [Runtime.InteropServices.Marshal]::ReleaseComObject($wb) | Out-Null
            $wb = $null

            # Only delete the .xls once the .xlsx really exists and is not empty
            $ok = (Test-Path -LiteralPath $xlsx) -and ((Get-Item -LiteralPath $xlsx).Length -gt 0)
            if (-not $ok) { throw 'the .xlsx was not written' }
            if ($deleteOriginal) {
                Remove-Item -LiteralPath $filePath -Force
                $log.Add(('OK   : {0}  ->  {1}  (original deleted)' -f $name, [IO.Path]::GetFileName($xlsx)))
            } else {
                $log.Add(('OK   : {0}  ->  {1}' -f $name, [IO.Path]::GetFileName($xlsx)))
            }
            $converted++
        } catch {
            $log.Add(('FAIL : {0}  --  {1}' -f $name, $_.Exception.Message))
            $skipped++
        } finally {
            if ($null -ne $wb) {
                try { $wb.Close($false) } catch {}
                [Runtime.InteropServices.Marshal]::ReleaseComObject($wb) | Out-Null
            }
        }
    }
} catch {
    $log.Add('CRITICAL: ' + $_.Exception.Message + '  (is Microsoft Excel installed?)')
} finally {
    if ($null -ne $excel) {
        try { $excel.Quit() } catch {}
        [Runtime.InteropServices.Marshal]::ReleaseComObject($excel) | Out-Null
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}
$log.Add(('-' * 50))
$log.Add(('Done  --  Converted: {0}   Failed: {1}' -f $converted, $skipped))
$log | Out-File -LiteralPath $logPath -Encoding UTF8
";
        }
    }
}
