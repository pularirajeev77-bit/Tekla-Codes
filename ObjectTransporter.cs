#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Runtime"
#pragma reference "Tekla.Structures"
#pragma reference "Tekla.Structures.Model"
#pragma reference "Tekla.Structures.Datatype"
#pragma reference "System.Windows.Forms"
#pragma reference "System.Drawing"
#pragma warning restore 1633 // Unrecognized #pragma directive

// ============================================================================
//  Tekla Structures macro : Object Transporter   v2.1
//  Author                 : Rajeev Pulari
//  Copies beams/columns and contour plates from one model and pastes them into
//  another (or the same) model, relative to a picked base point.
//  COPY  : pick parts + base point  -> writes %TEMP%\Tekla_Macro_Clipboard.csv
//  PASTE : pick target point        -> recreates the parts, moved base -> target
//  Coordinates are stored in GLOBAL coordinates, whatever work plane is active.
// ============================================================================

namespace UserMacros
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Drawing;
    using System.Windows.Forms;
    using Tekla.Structures.Model;
    using Tekla.Structures.Model.UI;
    using Tekla.Structures.Geometry3d;
    using Point = Tekla.Structures.Geometry3d.Point;

    public sealed class Macro
    {
        private static readonly string ClipboardPath = Path.Combine(Path.GetTempPath(), "Tekla_Macro_Clipboard.csv");
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private enum ActionType { None, Copy, Paste, Exit }

        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime)
        {
            Model model = new Model();
            if (!model.GetConnectionStatus())
            {
                MessageBox.Show("Tekla Structures model is not connected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            ActionType choice = ShowActionDialog();
            if (choice != ActionType.Copy && choice != ActionType.Paste) return;

            // Work in GLOBAL coordinates so copy and paste agree even when the
            // two models (or two moments) use different work planes.
            WorkPlaneHandler wph = model.GetWorkPlaneHandler();
            TransformationPlane original = wph.GetCurrentTransformationPlane();
            try
            {
                wph.SetCurrentTransformationPlane(new TransformationPlane());
                if (choice == ActionType.Copy) ExecuteCopy();
                else ExecutePaste(model);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unexpected error: " + ex.Message, "Object Transporter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                wph.SetCurrentTransformationPlane(original);
            }
        }

        // ========================================================
        // DIALOG: COPY / PASTE / EXIT
        // ========================================================
        private static ActionType ShowActionDialog()
        {
            ActionType selectedAction = ActionType.Exit;

            using (Form form = new Form())
            {
                form.Text = "Tekla Object Transporter v2.1";
                form.Size = new Size(380, 175);
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.StartPosition = FormStartPosition.CenterScreen;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowIcon = false;
                form.TopMost = true;

                bool hasClipboard = File.Exists(ClipboardPath);

                Label lblPrompt = new Label
                {
                    Text = hasClipboard
                        ? "Choose action.  Clipboard from " + File.GetLastWriteTime(ClipboardPath).ToString("dd MMM HH:mm") + "."
                        : "Choose action.  Clipboard is empty - run Copy first.",
                    Location = new System.Drawing.Point(20, 18),
                    Size = new Size(330, 20),
                    Font = new Font(FontFamily.GenericSansSerif, 9f, FontStyle.Regular)
                };

                Button btnCopy  = MakeButton("Copy",  20, FontStyle.Bold);
                Button btnPaste = MakeButton("Paste", 130, FontStyle.Bold);
                Button btnExit  = MakeButton("Exit",  240, FontStyle.Regular);
                btnPaste.Enabled = hasClipboard;

                btnCopy.Click  += (s, e) => { selectedAction = ActionType.Copy;  form.Close(); };
                btnPaste.Click += (s, e) => { selectedAction = ActionType.Paste; form.Close(); };
                btnExit.Click  += (s, e) => { selectedAction = ActionType.Exit;  form.Close(); };

                form.Controls.Add(lblPrompt);
                form.Controls.Add(btnCopy);
                form.Controls.Add(btnPaste);
                form.Controls.Add(btnExit);
                form.AcceptButton = btnCopy;
                form.CancelButton = btnExit;

                form.ShowDialog();
            }
            return selectedAction;
        }

        private static Button MakeButton(string text, int x, FontStyle style)
        {
            return new Button
            {
                Text = text,
                Location = new System.Drawing.Point(x, 60),
                Size = new Size(95, 36),
                Font = new Font(FontFamily.GenericSansSerif, 9f, style)
            };
        }

        // ========================================================
        // 1. COPY
        // Row formats (';'-separated, invariant culture):
        //   BASE_POINT;x;y;z
        //   BEAM;x1;y1;z1;x2;y2;z2;profile;material;class;depth;plane;rotation;
        //        name;finish;depthOffset;planeOffset;rotationOffset
        //   PLATE;x,y,z,chamferType,cx,cy|...;profile;material;class;depth;
        //        name;finish;depthOffset
        // ========================================================
        private static void ExecuteCopy()
        {
            Picker picker = new Picker();
            ModelObjectEnumerator selectedObjects;
            try
            {
                selectedObjects = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_PARTS, "Pick parts to copy (middle-click when done)");
            }
            catch (Exception) { return; }   // user interrupted

            Point basePoint;
            try
            {
                basePoint = picker.PickPoint("Pick base point for the copy");
            }
            catch (Exception) { return; }   // cancelling now aborts instead of silently using 0,0,0

            var dataRows = new List<string>
            {
                string.Format(Inv, "BASE_POINT;{0};{1};{2}", basePoint.X, basePoint.Y, basePoint.Z)
            };

            int copied = 0, skipped = 0;
            while (selectedObjects.MoveNext())
            {
                ModelObject modelObj = selectedObjects.Current;

                if (modelObj is Beam beam)
                {
                    dataRows.Add(string.Join(";", new string[]
                    {
                        "BEAM",
                        D(beam.StartPoint.X), D(beam.StartPoint.Y), D(beam.StartPoint.Z),
                        D(beam.EndPoint.X),   D(beam.EndPoint.Y),   D(beam.EndPoint.Z),
                        Clean(beam.Profile.ProfileString),
                        Clean(beam.Material.MaterialString),
                        Clean(beam.Class),
                        ((int)beam.Position.Depth).ToString(Inv),
                        ((int)beam.Position.Plane).ToString(Inv),
                        ((int)beam.Position.Rotation).ToString(Inv),
                        Clean(beam.Name),
                        Clean(beam.Finish),
                        D(beam.Position.DepthOffset),
                        D(beam.Position.PlaneOffset),
                        D(beam.Position.RotationOffset)
                    }));
                    copied++;
                }
                else if (modelObj is ContourPlate plate)
                {
                    var ptStrings = new List<string>();
                    foreach (ContourPoint cp in plate.Contour.ContourPoints)
                    {
                        int chType = 0; double cx = 0, cy = 0;
                        if (cp.Chamfer != null)
                        {
                            chType = (int)cp.Chamfer.Type;
                            cx = cp.Chamfer.X;
                            cy = cp.Chamfer.Y;
                        }
                        ptStrings.Add(string.Join(",", new string[] { D(cp.X), D(cp.Y), D(cp.Z), chType.ToString(Inv), D(cx), D(cy) }));
                    }

                    dataRows.Add(string.Join(";", new string[]
                    {
                        "PLATE",
                        string.Join("|", ptStrings),
                        Clean(plate.Profile.ProfileString),
                        Clean(plate.Material.MaterialString),
                        Clean(plate.Class),
                        ((int)plate.Position.Depth).ToString(Inv),
                        Clean(plate.Name),
                        Clean(plate.Finish),
                        D(plate.Position.DepthOffset)
                    }));
                    copied++;
                }
                else
                {
                    skipped++;   // polybeams, bent plates, etc.
                }
            }

            if (copied > 0)
            {
                File.WriteAllLines(ClipboardPath, dataRows);
                MessageBox.Show("Copied " + copied + " part(s) to the clipboard." +
                    (skipped > 0 ? "\n" + skipped + " unsupported part(s) skipped (only beams/columns and contour plates)." : ""),
                    "Copy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("No beams, columns or contour plates were selected.", "Copy",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ========================================================
        // 2. PASTE
        // ========================================================
        private static void ExecutePaste(Model model)
        {
            if (!File.Exists(ClipboardPath))
            {
                MessageBox.Show("Clipboard file not found. Run COPY in the source model first.", "Paste",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string[] lines = File.ReadAllLines(ClipboardPath);
            if (lines.Length == 0)
            {
                MessageBox.Show("Clipboard is empty.", "Paste", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            Point basePoint = new Point(0, 0, 0);
            int startIndex = 0;
            if (lines[0].StartsWith("BASE_POINT", StringComparison.Ordinal))
            {
                string[] bp = lines[0].Split(';');
                if (bp.Length >= 4) basePoint = new Point(P(bp[1]), P(bp[2]), P(bp[3]));
                startIndex = 1;
            }

            Picker picker = new Picker();
            Point targetPoint;
            try
            {
                targetPoint = picker.PickPoint("Pick target point in this model");
            }
            catch (Exception) { return; }

            Vector t = new Vector(targetPoint.X - basePoint.X, targetPoint.Y - basePoint.Y, targetPoint.Z - basePoint.Z);
            int pasted = 0, failed = 0;

            for (int i = startIndex; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(';');

                try
                {
                    // BEAM needs indices 0..12 -> 13 fields (the old check of 12 could crash on parts[12])
                    if (parts[0] == "BEAM" && parts.Length >= 13)
                    {
                        Beam b = new Beam
                        {
                            StartPoint = new Point(P(parts[1]) + t.X, P(parts[2]) + t.Y, P(parts[3]) + t.Z),
                            EndPoint   = new Point(P(parts[4]) + t.X, P(parts[5]) + t.Y, P(parts[6]) + t.Z)
                        };
                        b.Profile.ProfileString   = parts[7];
                        b.Material.MaterialString = parts[8];
                        b.Class                   = parts[9];
                        b.Position.Depth    = (Position.DepthEnum)int.Parse(parts[10], Inv);
                        b.Position.Plane    = (Position.PlaneEnum)int.Parse(parts[11], Inv);
                        b.Position.Rotation = (Position.RotationEnum)int.Parse(parts[12], Inv);
                        // Extra fields from v2.1 copies (older clipboards simply don't have them)
                        if (parts.Length >= 18)
                        {
                            b.Name   = parts[13];
                            b.Finish = parts[14];
                            b.Position.DepthOffset    = P(parts[15]);
                            b.Position.PlaneOffset    = P(parts[16]);
                            b.Position.RotationOffset = P(parts[17]);
                        }
                        if (b.Insert()) pasted++; else failed++;
                    }
                    // PLATE needs indices 0..5 -> 6 fields (the old check of 5 could crash on parts[5])
                    else if (parts[0] == "PLATE" && parts.Length >= 6)
                    {
                        ContourPlate pl = new ContourPlate();
                        foreach (string token in parts[1].Split('|'))
                        {
                            string[] v = token.Split(',');
                            if (v.Length < 3) continue;
                            Chamfer ch = null;
                            if (v.Length >= 6)
                            {
                                int type = int.Parse(v[3], Inv);
                                if (type != 0) ch = new Chamfer(P(v[4]), P(v[5]), (Chamfer.ChamferTypeEnum)type);
                            }
                            pl.AddContourPoint(new ContourPoint(new Point(P(v[0]) + t.X, P(v[1]) + t.Y, P(v[2]) + t.Z), ch));
                        }
                        pl.Profile.ProfileString   = parts[2];
                        pl.Material.MaterialString = parts[3];
                        pl.Class                   = parts[4];
                        pl.Position.Depth = (Position.DepthEnum)int.Parse(parts[5], Inv);
                        if (parts.Length >= 9)
                        {
                            pl.Name   = parts[6];
                            pl.Finish = parts[7];
                            pl.Position.DepthOffset = P(parts[8]);
                        }
                        if (pl.Insert()) pasted++; else failed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch (Exception)
                {
                    failed++;   // one bad row no longer stops the rest
                }
            }

            model.CommitChanges();
            MessageBox.Show("Pasted " + pasted + " part(s)." + (failed > 0 ? "\n" + failed + " row(s) could not be created." : ""),
                "Paste", MessageBoxButtons.OK, failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }

        // ── helpers ─────────────────────────────────────────────
        private static string D(double v) { return v.ToString("R", Inv); }
        private static double P(string s) { return double.Parse(s, NumberStyles.Float, Inv); }

        // Separators inside text would break the row format
        private static string Clean(string s)
        {
            return (s ?? "").Replace(";", " ").Replace("|", " ").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
