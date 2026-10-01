// ============================================================================
//  Tekla Structures macro : Interactive UI Polybeam Cut   v2.1
//  Description: Dialog with position settings and pick buttons that cuts a
//               main part with a polybeam that follows the secondary parts'
//               path (e.g. a pipe/tube running into another member). Live
//               preview: the cut body is shown as a yellow part (class 6) that
//               can be repositioned before the final cut.
//  Settings   : PROFILE_CLEARANCE (added to the secondary profile size),
//               CUT_LENGTH (length of path either side of the meeting point),
//               CUTTING_MATERIAL (must exist in the material catalog).
// ============================================================================

#pragma warning disable 1633
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Runtime"
#pragma warning restore 1633

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures.Geometry3d;
using Point = Tekla.Structures.Geometry3d.Point;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        // --- SETTINGS ---
        private const double PROFILE_CLEARANCE = 150.0;
        private const double CUT_LENGTH        = 200.0;
        private const string CUTTING_MATERIAL  = "Zero_Density";
        private const string PREVIEW_CLASS     = "6";        // yellow, easy to see
        private const double JOIN_TOLERANCE    = 2.0;        // mm, for stitching secondary paths

        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            Model model = new Model();
            if (!model.GetConnectionStatus())
            {
                MessageBox.Show("Tekla Structures is not connected.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Part mainPart = null;
            List<Part> secondaryParts = new List<Part>();
            PolyBeam activePreviewPart = null;

            using (Form dialog = new Form())
            {
                dialog.Width = 340;
                dialog.Height = 325;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.Text = "Cut Profile Settings v2.1";
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.TopMost = true;
                dialog.MaximizeBox = false;
                dialog.MinimizeBox = false;

                // On plane row
                Label lblPlane = new Label() { Left = 15, Top = 20, Text = "On plane:", Width = 70 };
                ComboBox cmbPlane = new ComboBox() { Left = 85, Top = 18, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbPlane.Items.AddRange(new string[] { "MIDDLE", "LEFT", "RIGHT" });
                cmbPlane.SelectedIndex = 2; // RIGHT
                TextBox txtPlaneOffset = new TextBox() { Left = 195, Top = 18, Width = 100, Text = "0.0" };

                // On depth row
                Label lblDepth = new Label() { Left = 15, Top = 60, Text = "On depth:", Width = 70 };
                ComboBox cmbDepth = new ComboBox() { Left = 85, Top = 58, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
                cmbDepth.Items.AddRange(new string[] { "MIDDLE", "FRONT", "BEHIND" });
                cmbDepth.SelectedIndex = 2; // BEHIND
                TextBox txtDepthOffset = new TextBox() { Left = 195, Top = 58, Width = 100, Text = "0.0" };

                Button btnPickMain = new Button() { Text = "1. Select Part to Cut (Main)", Left = 15, Top = 100, Width = 280, Height = 30 };
                Button btnPickSec  = new Button() { Text = "2. Select Parts for Cut (Sec)", Left = 15, Top = 140, Width = 280, Height = 30 };
                Button btnPreview  = new Button() { Text = "3. Preview Profile", Left = 15, Top = 190, Width = 135, Height = 30, BackColor = System.Drawing.Color.LightYellow };
                Button btnModify   = new Button() { Text = "Modify Preview", Left = 160, Top = 190, Width = 135, Height = 30, BackColor = System.Drawing.Color.LightYellow };
                Button btnExecute  = new Button() { Text = "4. Execute Final Cut", Left = 15, Top = 235, Width = 280, Height = 35, BackColor = System.Drawing.Color.LightGreen };

                // Make the dialog invisible while picking WITHOUT hiding it:
                // Hide() on a modal dialog ends ShowDialog, which closed the tool
                // (and deleted the preview) on the first pick.
                Action beginPick = () => { dialog.Opacity = 0; dialog.Enabled = false; };
                Action endPick   = () => { dialog.Enabled = true; dialog.Opacity = 1; dialog.Activate(); };

                // Apply the dialog's position values to the preview part
                Func<bool> applyPosition = () =>
                {
                    if (activePreviewPart == null) return false;
                    double pOffset, dOffset;
                    if (!TryNum(txtPlaneOffset.Text, out pOffset) || !TryNum(txtDepthOffset.Text, out dOffset))
                    {
                        MessageBox.Show(dialog, "Offsets must be numbers.", "Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }
                    activePreviewPart.Position.Plane = (Position.PlaneEnum)Enum.Parse(typeof(Position.PlaneEnum), cmbPlane.SelectedItem.ToString());
                    activePreviewPart.Position.Depth = (Position.DepthEnum)Enum.Parse(typeof(Position.DepthEnum), cmbDepth.SelectedItem.ToString());
                    activePreviewPart.Position.PlaneOffset = pOffset;
                    activePreviewPart.Position.DepthOffset = dOffset;
                    return true;
                };

                Action deletePreview = () =>
                {
                    if (activePreviewPart != null)
                    {
                        try { activePreviewPart.Delete(); } catch { }
                        activePreviewPart = null;
                        model.CommitChanges();
                    }
                };

                // 1. Pick main part
                btnPickMain.Click += (s, e) =>
                {
                    beginPick();
                    try
                    {
                        Part picked = new Picker().PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Select the main part to be cut") as Part;
                        if (picked != null)
                        {
                            mainPart = picked;
                            btnPickMain.Text = "1. Main Part: Selected";
                        }
                    }
                    catch { } // ESC
                    finally { endPick(); }
                };

                // 2. Pick secondary parts
                btnPickSec.Click += (s, e) =>
                {
                    beginPick();
                    try
                    {
                        ModelObjectEnumerator en = new Picker().PickObjects(Picker.PickObjectsEnum.PICK_N_PARTS,
                            "Select the secondary reference parts, then middle-click");
                        var picked = new List<Part>();
                        while (en.MoveNext())
                        {
                            Part pt = en.Current as Part;
                            // the main part can't also be a path part
                            if (pt != null && (mainPart == null || pt.Identifier.ID != mainPart.Identifier.ID)) picked.Add(pt);
                        }
                        if (picked.Count > 0)
                        {
                            secondaryParts = picked;
                            btnPickSec.Text = "2. Sec Parts: " + picked.Count + " Selected";
                        }
                    }
                    catch { } // ESC keeps the previous selection
                    finally { endPick(); }
                };

                // 3. Preview
                btnPreview.Click += (s, e) =>
                {
                    if (mainPart == null || secondaryParts.Count == 0)
                    {
                        MessageBox.Show(dialog, "Please select both Main and Secondary parts first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    deletePreview(); // regenerate from scratch

                    List<Point> path;
                    string boxProfile;
                    if (!GenerateProfilePath(mainPart, secondaryParts, PROFILE_CLEARANCE, CUT_LENGTH, out path, out boxProfile))
                        return;

                    PolyBeam preview = new PolyBeam();
                    preview.Name = "CUT";
                    preview.Profile.ProfileString = boxProfile;
                    preview.Material.MaterialString = CUTTING_MATERIAL;
                    preview.Class = PREVIEW_CLASS;

                    // Copy the secondary part's position (not share its object), then apply the dialog values
                    Position src = secondaryParts[0].Position;
                    preview.Position.Rotation = src.Rotation;
                    preview.Position.RotationOffset = src.RotationOffset;
                    activePreviewPart = preview;
                    if (!applyPosition()) { activePreviewPart = null; return; }

                    foreach (Point pt in path)
                        preview.Contour.AddContourPoint(new ContourPoint(pt, null));

                    if (!preview.Insert())
                    {
                        activePreviewPart = null;
                        MessageBox.Show(dialog, "Could not create the preview. Check that the material '" + CUTTING_MATERIAL +
                            "' and profile '" + boxProfile + "' exist in the catalogs.", "Preview", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    model.CommitChanges();
                };

                // Modify preview position
                btnModify.Click += (s, e) =>
                {
                    if (activePreviewPart == null)
                    {
                        MessageBox.Show(dialog, "No active preview to modify. Please click 'Preview Profile' first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    if (applyPosition())
                    {
                        activePreviewPart.Modify();
                        model.CommitChanges();
                    }
                };

                // 4. Execute
                btnExecute.Click += (s, e) =>
                {
                    if (mainPart == null || secondaryParts.Count == 0)
                    {
                        MessageBox.Show(dialog, "Please select parts first.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }

                    if (activePreviewPart == null)
                    {
                        btnPreview.PerformClick();
                        if (activePreviewPart == null) return;   // path generation / insert failed
                    }

                    // Operative part must carry the boolean class
                    activePreviewPart.Class = BooleanPart.BooleanOperativeClassName;
                    activePreviewPart.Modify();

                    BooleanPart partCut = new BooleanPart();
                    partCut.Father = mainPart;
                    partCut.OperativePart = activePreviewPart;

                    if (partCut.Insert())
                    {
                        activePreviewPart.Delete();   // the cut stays, the body goes
                        activePreviewPart = null;
                        model.CommitChanges();
                        dialog.DialogResult = DialogResult.OK;
                        dialog.Close();
                    }
                    else
                    {
                        // put the preview back to visible class so the user can adjust and retry
                        activePreviewPart.Class = PREVIEW_CLASS;
                        activePreviewPart.Modify();
                        model.CommitChanges();
                        MessageBox.Show(dialog, "Cut failed. Make sure the preview intersects the main part.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                };

                // No ghost preview parts left when the dialog is closed or cancelled
                dialog.FormClosed += (s, e) =>
                {
                    if (dialog.DialogResult != DialogResult.OK) deletePreview();
                };

                dialog.Controls.AddRange(new Control[] { lblPlane, cmbPlane, txtPlaneOffset, lblDepth, cmbDepth, txtDepthOffset,
                                                         btnPickMain, btnPickSec, btnPreview, btnModify, btnExecute });
                dialog.ShowDialog();
            }
        }

        // ------------------------------------------------------------------
        //  Cut path: the secondary parts' centre-lines stitched into one path,
        //  then CUT_LENGTH either side of the point nearest to the main part.
        // ------------------------------------------------------------------
        private static bool GenerateProfilePath(Part mainPart, List<Part> secondaryParts, double clearance, double cutLength,
                                                out List<Point> shortPath, out string boxProfileString)
        {
            shortPath = new List<Point>();
            boxProfileString = string.Empty;
            if (secondaryParts == null || secondaryParts.Count == 0) return false;

            var paths = new List<List<Point>>();
            foreach (Part secPart in secondaryParts)
            {
                var path = new List<Point>();
                Beam beam = secPart as Beam;
                PolyBeam poly = secPart as PolyBeam;
                if (beam != null)
                {
                    path.Add(beam.StartPoint);
                    path.Add(beam.EndPoint);
                }
                else if (poly != null)
                {
                    foreach (ContourPoint cp in poly.Contour.ContourPoints)
                        path.Add(new Point(cp.X, cp.Y, cp.Z));
                }
                if (path.Count >= 2) paths.Add(path);
            }
            if (paths.Count == 0)
            {
                MessageBox.Show("The secondary parts must be beams or polybeams.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // Cut body size = secondary profile + clearance
            double secHeight = GetProfileSize(secondaryParts[0], "HEIGHT");
            double secWidth  = GetProfileSize(secondaryParts[0], "WIDTH");
            if (secHeight <= 0.1) secHeight = 100.0;
            if (secWidth <= 0.1) secWidth = 100.0;
            secHeight += clearance;
            secWidth += clearance;
            // Invariant format: "PL250*250.5", never "PL250*250,5"
            boxProfileString = "PL" + secHeight.ToString("0.##", CultureInfo.InvariantCulture) + "*" + secWidth.ToString("0.##", CultureInfo.InvariantCulture);

            // Stitch the paths end-to-end
            var unified = new List<Point>(paths[0]);
            paths.RemoveAt(0);
            bool gap = false;
            while (paths.Count > 0)
            {
                bool joined = false;
                for (int i = 0; i < paths.Count; i++)
                {
                    List<Point> cur = paths[i];
                    Point lastU = unified[unified.Count - 1], firstU = unified[0];
                    Point firstC = cur[0], lastC = cur[cur.Count - 1];

                    if (Dist(lastU, firstC) < JOIN_TOLERANCE)       { cur.RemoveAt(0); unified.AddRange(cur); }
                    else if (Dist(lastU, lastC) < JOIN_TOLERANCE)   { cur.Reverse(); cur.RemoveAt(0); unified.AddRange(cur); }
                    else if (Dist(firstU, lastC) < JOIN_TOLERANCE)  { cur.RemoveAt(cur.Count - 1); unified.InsertRange(0, cur); }
                    else if (Dist(firstU, firstC) < JOIN_TOLERANCE) { cur.Reverse(); cur.RemoveAt(cur.Count - 1); unified.InsertRange(0, cur); }
                    else continue;

                    paths.RemoveAt(i);
                    joined = true;
                    break;
                }
                if (!joined)
                {
                    gap = true;
                    unified.AddRange(paths[0]);
                    paths.RemoveAt(0);
                }
            }

            // Drop consecutive duplicate points (zero-length segments break the polybeam)
            for (int i = unified.Count - 1; i > 0; i--)
                if (Dist(unified[i], unified[i - 1]) < 0.01) unified.RemoveAt(i);

            if (unified.Count < 2)
            {
                MessageBox.Show("Could not form a valid continuous path from the selected parts.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            if (gap)
                MessageBox.Show("The secondary parts do not all connect end-to-end (within " + JOIN_TOLERANCE +
                    " mm); the path may jump between them. Check the preview.", "Notice", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // Path point closest to the main part
            Solid mainSolid = mainPart.GetSolid();
            Point aabbMin = mainSolid.MinimumPoint, aabbMax = mainSolid.MaximumPoint;
            int focus = 0;
            double minD = double.MaxValue;
            for (int i = 0; i < unified.Count; i++)
            {
                double d = DistToAABB(unified[i], aabbMin, aabbMax);
                if (d < minD) { minD = d; focus = i; }
            }

            // Backward
            if (focus == 0)
            {
                // Path ends here: EXTEND beyond the end, into the main part.
                // (Before, the point was placed back along the first segment, so the path folded back on itself.)
                shortPath.Add(Extend(unified[1], unified[0], cutLength));
            }
            else
            {
                double back = cutLength;
                Point cur = unified[focus];
                var tmp = new List<Point>();
                for (int i = focus - 1; i >= 0; i--)
                {
                    double d = Dist(cur, unified[i]);
                    if (back <= d) { tmp.Add(Along(cur, unified[i], back)); back = 0; break; }
                    tmp.Add(unified[i]);
                    back -= d;
                    cur = unified[i];
                }
                tmp.Reverse();
                shortPath.AddRange(tmp);
            }

            shortPath.Add(unified[focus]);

            // Forward
            int last = unified.Count - 1;
            if (focus == last)
            {
                shortPath.Add(Extend(unified[last - 1], unified[last], cutLength));
            }
            else
            {
                double fwd = cutLength;
                Point cur = unified[focus];
                for (int i = focus + 1; i <= last; i++)
                {
                    double d = Dist(cur, unified[i]);
                    if (fwd <= d) { shortPath.Add(Along(cur, unified[i], fwd)); fwd = 0; break; }
                    shortPath.Add(unified[i]);
                    fwd -= d;
                    cur = unified[i];
                }
            }

            return shortPath.Count >= 2;
        }

        // --- helpers ---
        private static double GetProfileSize(Part part, string what)
        {
            double v = 0.0;
            if (!part.GetReportProperty("PROFILE." + what, ref v) || v <= 0.1)
                part.GetReportProperty(what, ref v);
            return v;
        }

        private static bool TryNum(string text, out double value)
        {
            string s = (text ?? "").Trim();
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static double Dist(Point a, Point b)
        {
            double dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double DistToAABB(Point p, Point min, Point max)
        {
            double dx = Math.Max(0, Math.Max(min.X - p.X, p.X - max.X));
            double dy = Math.Max(0, Math.Max(min.Y - p.Y, p.Y - max.Y));
            double dz = Math.Max(0, Math.Max(min.Z - p.Z, p.Z - max.Z));
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // Point at 'distance' from start, towards target
        private static Point Along(Point start, Point target, double distance)
        {
            double len = Dist(start, target);
            if (len < 0.01) return new Point(start);
            return new Point(start.X + (target.X - start.X) / len * distance,
                             start.Y + (target.Y - start.Y) / len * distance,
                             start.Z + (target.Z - start.Z) / len * distance);
        }

        // Point 'distance' BEYOND 'end', continuing the direction from 'from' to 'end'
        private static Point Extend(Point from, Point end, double distance)
        {
            double len = Dist(from, end);
            if (len < 0.01) return new Point(end);
            return new Point(end.X + (end.X - from.X) / len * distance,
                             end.Y + (end.Y - from.Y) / len * distance,
                             end.Z + (end.Z - from.Z) / len * distance);
        }
    }
}
