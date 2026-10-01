// ============================================================================
//  Tekla Structures macro : Dynamic Pin Creator   v2.1
//  Author                 : Rajeev Pulari
//  NickName               : UIPin
//  Creates a round pin between two picked points with a round cap plate at
//  each end, shop-welded to the pin (one assembly: PIN-).
//    Pin  : D<pin dia>,  class 5, name  "Ø<dia>_PIN", part prefix r-
//    Caps : D<cap dia>,  class 6, name  "Ø<dia>_CAP", thickness = cap thickness,
//           placed OUTSIDE the picked points (pin length = picked length)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using T3D = Tekla.Structures.Geometry3d;

namespace Tekla.Technology.Akit.UserScript
{
    public class Script
    {
        public struct Component
        {
            public static string Name     = "DynamicPinCreator";
            public static string NickName = "UIPin";
            public static string Message  = "UI-driven Pin with dynamic naming and prefixes";
        }

        public static void Run(Tekla.Technology.Akit.IScript akit)
        {
            Model model = new Model();
            if (!model.GetConnectionStatus())
            {
                MessageBox.Show("Could not connect to the Tekla model.", "Connection Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (PinForm form = new PinForm(model))
                form.ShowDialog();
        }
    }

    public class PinForm : Form
    {
        private TextBox txtPinDia;
        private TextBox txtCapDia;
        private TextBox txtCapThick;
        private TextBox txtMaterial;
        private readonly Model teklaModel;

        public PinForm(Model model)
        {
            teklaModel = model;
            SetupUI();
        }

        private void SetupUI()
        {
            this.Text = "Dynamic Pin Generator v2.1";
            this.Size = new Size(320, 260);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.TopMost = true;

            int yPos = 20;
            int spacing = 35;

            this.Controls.Add(new Label() { Text = "Pin Diameter (mm):", Location = new System.Drawing.Point(20, yPos), AutoSize = true });
            txtPinDia = new TextBox() { Text = "30", Location = new System.Drawing.Point(160, yPos), Width = 100 };
            this.Controls.Add(txtPinDia);

            yPos += spacing;
            this.Controls.Add(new Label() { Text = "Cap Diameter (mm):", Location = new System.Drawing.Point(20, yPos), AutoSize = true });
            txtCapDia = new TextBox() { Text = "60", Location = new System.Drawing.Point(160, yPos), Width = 100 };
            this.Controls.Add(txtCapDia);

            yPos += spacing;
            this.Controls.Add(new Label() { Text = "Cap Thickness (mm):", Location = new System.Drawing.Point(20, yPos), AutoSize = true });
            txtCapThick = new TextBox() { Text = "12", Location = new System.Drawing.Point(160, yPos), Width = 100 };
            this.Controls.Add(txtCapThick);

            yPos += spacing;
            this.Controls.Add(new Label() { Text = "Material:", Location = new System.Drawing.Point(20, yPos), AutoSize = true });
            txtMaterial = new TextBox() { Text = "S235JR", Location = new System.Drawing.Point(160, yPos), Width = 100 };
            this.Controls.Add(txtMaterial);

            yPos += 45;
            Button btnCreate = new Button();
            btnCreate.Text = "Pick Points && Create Pin";
            btnCreate.Location = new System.Drawing.Point(20, yPos);
            btnCreate.Size = new System.Drawing.Size(240, 35);
            btnCreate.BackColor = System.Drawing.Color.SteelBlue;
            btnCreate.ForeColor = System.Drawing.Color.White;
            btnCreate.FlatStyle = FlatStyle.Flat;
            btnCreate.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
            btnCreate.Click += BtnCreate_Click;
            this.Controls.Add(btnCreate);
            this.AcceptButton = btnCreate;
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            double pinDia, capDia, capThick;
            if (!TryNum(txtPinDia.Text, out pinDia) || !TryNum(txtCapDia.Text, out capDia) || !TryNum(txtCapThick.Text, out capThick))
            {
                MessageBox.Show(this, "Please enter valid numbers for the dimensions.", "Input Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (pinDia <= 0 || capDia <= 0 || capThick <= 0)
            {
                MessageBox.Show(this, "Diameters and cap thickness must be greater than 0.", "Input Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (capDia <= pinDia)
            {
                var r = MessageBox.Show(this, "Cap diameter (" + Fmt(capDia) + ") is not larger than the pin (" + Fmt(pinDia) +
                    "). Continue anyway?", "Check dimensions", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
            }

            string material = txtMaterial.Text.Trim();
            if (material.Length == 0)
            {
                MessageBox.Show(this, "Please enter a material.", "Input Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            this.Hide();
            ExecuteGeometryMacro(pinDia, capDia, capThick, material);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        // Accepts 12.5 and 12,5 whatever the Windows number format
        private static bool TryNum(string text, out double value)
        {
            string s = (text ?? "").Trim();
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        // Invariant formatting: "D12.5", never "D12,5" (which Tekla would not recognise)
        private static string Fmt(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private void ExecuteGeometryMacro(double pinDia, double capDia, double capThick, string material)
        {
            Picker picker = new Picker();
            T3D.Point startPoint, endPoint;
            try
            {
                startPoint = picker.PickPoint("Pick start point of main pin");
                endPoint   = picker.PickPoint("Pick end point of main pin");
            }
            catch (Exception)
            {
                return;   // picking cancelled: nothing was created
            }
            if (startPoint == null || endPoint == null) return;

            T3D.Vector dir = new T3D.Vector(endPoint.X - startPoint.X, endPoint.Y - startPoint.Y, endPoint.Z - startPoint.Z);
            if (dir.GetLength() < 1.0)
            {
                MessageBox.Show("Start and end point are the same - pick two different points.", "Pin",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            dir.Normalize();

            var created = new List<ModelObject>();
            try
            {
                // Pin
                Beam pin = MakeRound(startPoint, endPoint, pinDia, material, "5", "Ø" + Fmt(pinDia) + "_PIN", "PIN-");
                Insert(pin, "pin", created);

                // Caps, outside the picked points
                T3D.Point cap1Start = new T3D.Point(startPoint.X - dir.X * capThick, startPoint.Y - dir.Y * capThick, startPoint.Z - dir.Z * capThick);
                Beam startCap = MakeRound(cap1Start, new T3D.Point(startPoint), capDia, material, "6", "Ø" + Fmt(pinDia) + "_CAP", "CAP-");
                Insert(startCap, "start cap", created);

                T3D.Point cap2End = new T3D.Point(endPoint.X + dir.X * capThick, endPoint.Y + dir.Y * capThick, endPoint.Z + dir.Z * capThick);
                Beam endCap = MakeRound(new T3D.Point(endPoint), cap2End, capDia, material, "6", "Ø" + Fmt(pinDia) + "_CAP", "CAP-");
                Insert(endCap, "end cap", created);

                // Shop welds: caps join the pin's assembly (pin is the main part -> PIN-)
                Insert(MakeWeld(pin, startCap), "start weld", created);
                Insert(MakeWeld(pin, endCap), "end weld", created);

                teklaModel.CommitChanges();
            }
            catch (Exception ex)
            {
                // Roll back whatever was created, so no half pin is left in the model
                for (int i = created.Count - 1; i >= 0; i--)
                {
                    try { created[i].Delete(); } catch { }
                }
                teklaModel.CommitChanges();
                MessageBox.Show("Pin was not created: " + ex.Message, "Pin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static Beam MakeRound(T3D.Point a, T3D.Point b, double dia, string material, string cls, string name, string asmPrefix)
        {
            Beam beam = new Beam(a, b);
            beam.Profile.ProfileString   = "D" + Fmt(dia);
            beam.Material.MaterialString = material;
            beam.Class                   = cls;
            beam.Name                    = name;
            beam.PartNumber.Prefix       = "r-";
            beam.AssemblyNumber.Prefix   = asmPrefix;
            beam.Position.Depth          = Position.DepthEnum.MIDDLE;
            beam.Position.Plane          = Position.PlaneEnum.MIDDLE;
            return beam;
        }

        private static Weld MakeWeld(Part main, Part secondary)
        {
            Weld w = new Weld();
            w.MainObject = main;
            w.SecondaryObject = secondary;
            w.ShopWeld = true;
            return w;
        }

        private static void Insert(ModelObject obj, string what, List<ModelObject> created)
        {
            if (!obj.Insert())
                throw new Exception("Tekla could not insert the " + what + " (check the profile and material exist in the catalogs).");
            created.Add(obj);
        }
    }
}
