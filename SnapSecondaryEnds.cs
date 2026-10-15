using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            Model model = new Model();
            if (!model.GetConnectionStatus())
            {
                MessageBox.Show("Tekla model is not connected.");
                return;
            }
            using (SnapForm f = new SnapForm(model)) f.ShowDialog();
        }
    }

    public class SnapForm : Form
    {
        private readonly Model _model;
        private readonly Picker _picker = new Picker();

        private Beam _main;
        private readonly List<Beam> _secs = new List<Beam>();

        private Label lblMain, lblSecs;
        private TextBox txtTol;
        private Button btnMain, btnSec, btnClear, btnFinish, btnCancel;

        public SnapForm(Model model)
        {
            _model = model;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Snap Secondary Ends to Main";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 490;
            Height = 400;
            TopMost = true;

            btnMain = new Button { Text = "1. Select MAIN", Left = 20, Top = 20, Width = 230, Height = 40 };
            lblMain = new Label { Text = "Main: none", Left = 265, Top = 30, Width = 195, Height = 24, AutoEllipsis = true };

            btnSec = new Button { Text = "2. Select SECONDARY (multi)", Left = 20, Top = 85, Width = 230, Height = 40 };
            lblSecs = new Label { Text = "Secondary: 0", Left = 265, Top = 95, Width = 195, Height = 24 };

            btnClear = new Button { Text = "Clear secondary list", Left = 20, Top = 145, Width = 230, Height = 34 };

            Label lt = new Label { Text = "Tolerance (mm):", Left = 20, Top = 208, Width = 140, Height = 24 };
            txtTol = new TextBox { Text = "3", Left = 165, Top = 205, Width = 85 };

            btnFinish = new Button { Text = "Finish (Apply)", Left = 20, Top = 285, Width = 230, Height = 50 };
            btnCancel = new Button { Text = "Cancel", Left = 265, Top = 285, Width = 185, Height = 50 };

            btnMain.Click += (s, e) => PickMain();
            btnSec.Click += (s, e) => PickSecs();
            btnClear.Click += (s, e) => { _secs.Clear(); UpdateLabels(); };
            btnFinish.Click += (s, e) => Apply();
            btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[] { btnMain, lblMain, btnSec, lblSecs, btnClear, lt, txtTol, btnFinish, btnCancel });
        }

        private void UpdateLabels()
        {
            lblMain.Text = _main == null ? "Main: none" : "Main: " + _main.Profile.ProfileString;
            lblSecs.Text = "Secondary: " + _secs.Count;
        }

        private void PickMain()
        {
            Hide();
            try
            {
                Beam b = _picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Select MAIN profile") as Beam;
                if (b != null) { _main = b; _secs.RemoveAll(x => x.Identifier.ID == b.Identifier.ID); }
                else MessageBox.Show("Main must be a straight beam.");
            }
            catch (Exception) { /* Esc */ }
            Show();
            UpdateLabels();
        }

        private void PickSecs()
        {
            Hide();
            try
            {
                ModelObjectEnumerator en = _picker.PickObjects(Picker.PickObjectsEnum.PICK_N_PARTS,
                    "Select SECONDARY profiles (middle click / Esc when done)");
                while (en.MoveNext())
                {
                    Beam b = en.Current as Beam;
                    if (b == null) continue;
                    if (_main != null && b.Identifier.ID == _main.Identifier.ID) continue;
                    if (_secs.Exists(x => x.Identifier.ID == b.Identifier.ID)) continue;
                    _secs.Add(b);
                }
            }
            catch (Exception) { /* Esc */ }
            Show();
            UpdateLabels();
        }

        private void Apply()
        {
            double tol;
            if (!double.TryParse(txtTol.Text, out tol) || tol < 0)
            {
                MessageBox.Show("Invalid tolerance.");
                return;
            }
            if (_main == null || _secs.Count == 0)
            {
                MessageBox.Show("Select the main and at least one secondary profile.");
                return;
            }

            Point mainStart = _main.StartPoint;
            Point mainEnd = _main.EndPoint;
            int modified = 0, ends = 0;

            foreach (Beam sec in _secs)
            {
                bool changed = false;

                Point t = ClosestWithinTol(sec.StartPoint, mainStart, mainEnd, tol);
                if (t != null) { sec.StartPoint = new Point(t); changed = true; ends++; }

                t = ClosestWithinTol(sec.EndPoint, mainStart, mainEnd, tol);
                if (t != null) { sec.EndPoint = new Point(t); changed = true; ends++; }

                if (changed && sec.Modify()) modified++;
            }

            _model.CommitChanges();
            MessageBox.Show(modified + " profile(s) modified, " + ends + " end(s) moved.");
            Close();
        }

        private static Point ClosestWithinTol(Point p, Point a, Point b, double tol)
        {
            double dA = Distance.PointToPoint(p, a);
            double dB = Distance.PointToPoint(p, b);
            double d = Math.Min(dA, dB);
            if (d <= tol && d > 1e-6) return dA <= dB ? a : b;
            return null;
        }
    }
}
