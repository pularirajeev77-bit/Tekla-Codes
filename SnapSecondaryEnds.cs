// =============================================================================
//  Snap Secondary Ends to Main  -  Tekla Structures C# macro (modeling)
//  Author  : Rajeev Pulari
//  Version : 2.1  (2026-10-15)
//
//  Pick one MAIN beam and any number of SECONDARY beams. Every secondary end that
//  lies within the tolerance of the main beam is moved exactly onto it:
//    - onto the main beam's start or end point (always), or
//    - onto the main beam's reference line anywhere along it
//      ("Also snap onto the main line", off by default).
//  Ends further away than the tolerance, or already exactly on, are left alone.
//
//  v2.1 changes
//    - The dialog closed on the first pick: it was hidden with Hide(), which ends
//      a modal dialog (ShowDialog). It now turns invisible and disabled while you
//      pick, and comes back afterwards.
//    - Parts are re-read from the model just before they are changed, so edits
//      made in Tekla after picking are not overwritten with stale data.
//    - A secondary whose both ends would land on the same point (zero length) is
//      skipped instead of failing.
//    - Tolerance accepts "3.5" and "3,5".
//    - CommitChanges only when something changed; failed modifies are counted.
//    - New option: snap onto the main beam's line, not only its end points.
// =============================================================================
using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const string VERSION = "2.1";

        private readonly Model _model;
        private readonly Picker _picker = new Picker();

        private Beam _main;
        private readonly List<Beam> _secs = new List<Beam>();

        private Label lblMain, lblSecs;
        private TextBox txtTol;
        private CheckBox chkLine;
        private Button btnMain, btnSec, btnClear, btnFinish, btnCancel;

        public SnapForm(Model model)
        {
            _model = model;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Snap Secondary Ends to Main  v" + VERSION;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Width = 490;
            Height = 420;
            TopMost = true;

            btnMain = new Button { Text = "1. Select MAIN", Left = 20, Top = 20, Width = 230, Height = 40 };
            lblMain = new Label { Text = "Main: none", Left = 265, Top = 30, Width = 195, Height = 24, AutoEllipsis = true };

            btnSec = new Button { Text = "2. Select SECONDARY (multi)", Left = 20, Top = 85, Width = 230, Height = 40 };
            lblSecs = new Label { Text = "Secondary: 0", Left = 265, Top = 95, Width = 195, Height = 24 };

            btnClear = new Button { Text = "Clear secondary list", Left = 20, Top = 145, Width = 230, Height = 34 };

            Label lt = new Label { Text = "Tolerance (mm):", Left = 20, Top = 208, Width = 140, Height = 24 };
            txtTol = new TextBox { Text = "3", Left = 165, Top = 205, Width = 85 };

            chkLine = new CheckBox
            {
                Text = "Also snap onto the main line (not only its ends)",
                Left = 20, Top = 240, Width = 430, Height = 24, Checked = false
            };

            btnFinish = new Button { Text = "Finish (Apply)", Left = 20, Top = 300, Width = 230, Height = 50 };
            btnCancel = new Button { Text = "Cancel", Left = 265, Top = 300, Width = 185, Height = 50 };

            ToolTip tips = new ToolTip();
            tips.SetToolTip(btnMain, "Pick the beam the secondaries should land on.");
            tips.SetToolTip(btnSec, "Pick the beams whose ends should snap to the main. Middle-click when done.");
            tips.SetToolTip(txtTol, "Only ends within this distance of the main are moved.");
            tips.SetToolTip(chkLine, "Off: ends snap to the main's start/end point. On: also to the nearest point on its reference line.");

            btnMain.Click += (s, e) => PickMain();
            btnSec.Click += (s, e) => PickSecs();
            btnClear.Click += (s, e) => { _secs.Clear(); UpdateLabels(); };
            btnFinish.Click += (s, e) => Apply();
            btnCancel.Click += (s, e) => Close();

            Controls.AddRange(new Control[] { btnMain, lblMain, btnSec, lblSecs, btnClear, lt, txtTol, chkLine, btnFinish, btnCancel });
        }

        // Hide() on a modal dialog ends ShowDialog - make it invisible instead.
        private void BeginPick() { Opacity = 0; Enabled = false; }
        private void EndPick() { Enabled = true; Opacity = 1; Activate(); }

        private void UpdateLabels()
        {
            lblMain.Text = _main == null ? "Main: none" : "Main: " + _main.Profile.ProfileString + "  (ID " + _main.Identifier.ID + ")";
            lblSecs.Text = "Secondary: " + _secs.Count;
        }

        private void PickMain()
        {
            BeginPick();
            try
            {
                Beam b = _picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Select MAIN profile") as Beam;
                if (b != null)
                {
                    _main = b;
                    _secs.RemoveAll(x => x.Identifier.ID == b.Identifier.ID);
                }
                else MessageBox.Show("Main must be a straight beam.");
            }
            catch (Exception) { /* Esc */ }
            EndPick();
            UpdateLabels();
        }

        private void PickSecs()
        {
            BeginPick();
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
            EndPick();
            UpdateLabels();
        }

        private void Apply()
        {
            double tol;
            string t = (txtTol.Text ?? "").Trim().Replace(',', '.');
            if (!double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out tol) || tol < 0)
            {
                MessageBox.Show("Invalid tolerance.");
                return;
            }
            if (_main == null || _secs.Count == 0)
            {
                MessageBox.Show("Select the main and at least one secondary profile.");
                return;
            }

            // Fresh copy of the main (it may have been edited since it was picked)
            if (!_main.Select())
            {
                MessageBox.Show("The main beam no longer exists in the model.");
                return;
            }
            Point mainStart = _main.StartPoint;
            Point mainEnd = _main.EndPoint;
            bool toLine = chkLine.Checked;

            int modified = 0, ends = 0, failed = 0, skipped = 0;

            foreach (Beam sec in _secs)
            {
                if (!sec.Select()) { failed++; continue; }   // deleted or not readable

                Point newStart = Snap(sec.StartPoint, mainStart, mainEnd, tol, toLine);
                Point newEnd = Snap(sec.EndPoint, mainStart, mainEnd, tol, toLine);
                if (newStart == null && newEnd == null) continue;

                Point s = newStart ?? sec.StartPoint;
                Point e = newEnd ?? sec.EndPoint;
                if (Distance.PointToPoint(s, e) < 1.0) { skipped++; continue; }   // would be zero length

                int moved = (newStart != null ? 1 : 0) + (newEnd != null ? 1 : 0);
                sec.StartPoint = new Point(s);
                sec.EndPoint = new Point(e);

                if (sec.Modify()) { modified++; ends += moved; }
                else failed++;
            }

            if (modified > 0) _model.CommitChanges();

            string msg = modified + " profile(s) modified, " + ends + " end(s) moved.";
            if (skipped > 0) msg += "\n" + skipped + " skipped (both ends would meet at one point).";
            if (failed > 0) msg += "\n" + failed + " could not be modified.";
            MessageBox.Show(msg);
            Close();
        }

        // New position for p, or null when it should not move.
        private static Point Snap(Point p, Point a, Point b, double tol, bool toLine)
        {
            double dA = Distance.PointToPoint(p, a);
            double dB = Distance.PointToPoint(p, b);
            Point target = dA <= dB ? a : b;
            double d = Math.Min(dA, dB);

            if (toLine && d > tol)
            {
                Point onLine = ClosestOnSegment(p, a, b);
                double dl = Distance.PointToPoint(p, onLine);
                if (dl < d) { target = onLine; d = dl; }
            }

            if (d <= tol && d > 1e-6) return new Point(target);
            return null;
        }

        private static Point ClosestOnSegment(Point p, Point a, Point b)
        {
            Vector ab = new Vector(b.X - a.X, b.Y - a.Y, b.Z - a.Z);
            double len2 = ab.X * ab.X + ab.Y * ab.Y + ab.Z * ab.Z;
            if (len2 < 1e-12) return new Point(a);
            double t = ((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y + (p.Z - a.Z) * ab.Z) / len2;
            if (t < 0) t = 0; else if (t > 1) t = 1;
            return new Point(a.X + ab.X * t, a.Y + ab.Y * t, a.Z + ab.Z * t);
        }
    }
}
