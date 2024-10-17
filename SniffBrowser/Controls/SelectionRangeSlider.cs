using System;
using System.Drawing;
using System.ComponentModel;
using System.Windows.Forms;

namespace SniffBrowser.Controls
{
    public partial class SelectionRangeSlider : UserControl
    {
        /// <summary>
		/// Required designer variable.
		/// </summary>
		private Container components = null;

        public SelectionRangeSlider()
        {
            InitializeComponent();

        }

        enum ActiveMarkType { none, left, right };

        private Color colorInner = Color.LightGreen;
        private Color colorRange = Color.FromKnownColor(KnownColor.Control);
        private Color colorShadowLight = Color.FromKnownColor(KnownColor.ControlLightLight);
        private Color colorShadowDark = Color.FromKnownColor(KnownColor.ControlDarkDark);
        private int sizeShadow = 1;
        private double Minimum = 0;
        private double Maximum = 10;
        private double rangeMin = 3;
        private double rangeMax = 5;
        private ActiveMarkType ActiveMark = ActiveMarkType.none;

        private int BarHeight = 6;      // Height of Bar
        private int MarkWidth = 8;      // Width of mark knobs
        private int MarkHeight = 24;    // total height of mark knobs
        private int TickHeight = 4; // Height of axis tick
        private int numAxisDivision = 10;

        private int PosL = 0, PosR = 0; // Pixel-Position of mark buttons
        private int XPosMin, XPosMax;

        private Point[] LMarkPnt = new Point[5];
        private Point[] RMarkPnt = new Point[5];

        private bool MoveLMark = false;
        private bool MoveRMark = false;

        // Properties
        public double RangeMaximum
        {
            set { rangeMax = (double)value; Range2Pos(); Invalidate(true); }
            get { return rangeMax; }
        }
        public double RangeMinimum
        {
            set { rangeMin = (double)value; Range2Pos(); Invalidate(true); }
            get { return rangeMin; }
        }
        public double BarMaximum
        {
            set { Maximum = (double)value; Range2Pos(); Invalidate(true); }
            get { return Maximum; }
        }
        public double BarMinimum
        {
            set { Minimum = (double)value; Range2Pos(); Invalidate(true); }
            get { return Minimum; }
        }
        public int AxisDivisionNum      // axis division
        {
            set { numAxisDivision = value; }
            get { return numAxisDivision; }
        }
        public Color InnerColor         // color of inner
        {
            set { colorInner = value; }
            get { return colorInner; }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.Name = "SelectionRangeSlider";
            this.Size = new Size(232, 32);
            this.KeyPress += new KeyPressEventHandler(this.OnKeyPress);
            this.Resize += new EventHandler(this.OnResize);
            this.Load += new EventHandler(this.OnLoad);
            this.SizeChanged += new EventHandler(this.OnSizeChanged);
            this.MouseUp += new MouseEventHandler(this.OnMouseUp);
            this.Paint += new PaintEventHandler(this.OnPaint);
            this.Leave += new EventHandler(this.OnLeave);
            this.MouseMove += new MouseEventHandler(this.OnMouseMove);
            this.MouseDown += new MouseEventHandler(this.OnMouseDown);

        }
        #endregion

        private void OnPaint(object sender, PaintEventArgs e)
        {
            int h = this.Height;
            int w = this.Width;
            int baryoff, markyoff, tickyoff;
            double dtick;
            int tickpos;
            Pen penRange = new Pen(colorRange);
            Pen penShadowLight = new Pen(colorShadowLight);
            Pen penShadowDark = new Pen(colorShadowDark);
            SolidBrush brushShadowLight = new SolidBrush(colorShadowLight);
            SolidBrush brushShadowDark = new SolidBrush(colorShadowDark);
            SolidBrush brushInner = new SolidBrush(colorInner);
            SolidBrush brushRange = new SolidBrush(colorRange);

            // range
            XPosMin = MarkWidth + 1;
            XPosMax = w - MarkWidth - 1;

            // range check
            if (PosL < XPosMin) PosL = XPosMin;
            if (PosL > XPosMax) PosL = XPosMax;
            if (PosR > XPosMax) PosR = XPosMax;
            if (PosR < XPosMin) PosR = XPosMin;

            Range2Pos();

            baryoff = (h - BarHeight) / 2;
            markyoff = (h - MarkHeight) / 2;
            tickyoff = markyoff + MarkHeight - TickHeight / 2;

            // total range bar frame
            e.Graphics.FillRectangle(brushShadowDark, 0, baryoff, w - 1, sizeShadow);   // top
            e.Graphics.FillRectangle(brushShadowDark, 0, baryoff, sizeShadow, BarHeight - 1);   // left
            e.Graphics.FillRectangle(brushShadowLight, 0, baryoff + BarHeight - 1 - sizeShadow, w - 1, sizeShadow); // bottom
            e.Graphics.FillRectangle(brushShadowLight, w - 1 - sizeShadow, baryoff, sizeShadow, BarHeight - 1); // right

            // inner region
            e.Graphics.FillRectangle(brushInner, PosL, baryoff + sizeShadow, PosR - PosL, BarHeight - 1 - 2 * sizeShadow);

            // Skala
            if (numAxisDivision > 1)
            {
                dtick = (double)(XPosMax - XPosMin) / (double)numAxisDivision;
                for (int i = 0; i < numAxisDivision + 1; i++)
                {
                    tickpos = (int)Math.Round((double)i * dtick);
                    e.Graphics.DrawLine(penShadowDark, MarkWidth + 1 + tickpos,
                        tickyoff,
                        MarkWidth + 1 + tickpos,
                        tickyoff + TickHeight);
                }
            }

            // left mark knob
            LMarkPnt[0].X = PosL - MarkWidth; LMarkPnt[0].Y = markyoff;
            LMarkPnt[1].X = PosL; LMarkPnt[1].Y = markyoff;
            LMarkPnt[2].X = PosL; LMarkPnt[2].Y = markyoff + MarkHeight;
            LMarkPnt[3].X = PosL - MarkWidth; LMarkPnt[3].Y = markyoff + 2 * MarkHeight / 3;
            LMarkPnt[4].X = PosL - MarkWidth; LMarkPnt[4].Y = markyoff;
            e.Graphics.FillPolygon(brushRange, LMarkPnt);
            e.Graphics.DrawLine(penShadowLight, LMarkPnt[0].X - 1, LMarkPnt[0].Y, LMarkPnt[0].X - 1, LMarkPnt[3].Y); // left shadow
            e.Graphics.DrawLine(penShadowLight, LMarkPnt[3].X - 1, LMarkPnt[3].Y, LMarkPnt[1].X - 1, LMarkPnt[2].Y); // lower left shadow
            e.Graphics.DrawLine(penShadowLight, LMarkPnt[0].X - 1, LMarkPnt[0].Y, LMarkPnt[1].X, LMarkPnt[0].Y); // upper shadow
            if (PosL < PosR)
                e.Graphics.DrawLine(penShadowDark, LMarkPnt[1].X, LMarkPnt[1].Y + 1, LMarkPnt[1].X, LMarkPnt[2].Y); // right shadow
            if (ActiveMark == ActiveMarkType.left)
            {
                e.Graphics.DrawLine(penShadowLight, PosL - MarkWidth / 2 - 1, markyoff + 2, PosL - MarkWidth / 2 - 1, markyoff + 2 * MarkHeight / 3 - 2); // active mark
                e.Graphics.DrawLine(penShadowDark, PosL - MarkWidth / 2, markyoff + 2, PosL - MarkWidth / 2, markyoff + 2 * MarkHeight / 3 - 2); // active mark			
            }

            // right mark knob
            RMarkPnt[0].X = PosR + MarkWidth; RMarkPnt[0].Y = markyoff;
            RMarkPnt[1].X = PosR; RMarkPnt[1].Y = markyoff;
            RMarkPnt[2].X = PosR; RMarkPnt[2].Y = markyoff + MarkHeight;
            RMarkPnt[3].X = PosR + MarkWidth; RMarkPnt[3].Y = markyoff + 2 * MarkHeight / 3;
            RMarkPnt[4].X = PosR + MarkWidth; RMarkPnt[4].Y = markyoff;
            e.Graphics.FillPolygon(brushRange, RMarkPnt);
            if (PosL < PosR)
                e.Graphics.DrawLine(penShadowLight, RMarkPnt[1].X - 1, RMarkPnt[1].Y + 1, RMarkPnt[2].X - 1, RMarkPnt[2].Y); // left shadow
            e.Graphics.DrawLine(penShadowDark, RMarkPnt[2].X, RMarkPnt[2].Y, RMarkPnt[3].X, RMarkPnt[3].Y); // lower right shadow
            e.Graphics.DrawLine(penShadowLight, RMarkPnt[0].X, RMarkPnt[0].Y, RMarkPnt[1].X - 1, RMarkPnt[0].Y); // upper shadow
            e.Graphics.DrawLine(penShadowDark, RMarkPnt[0].X, RMarkPnt[0].Y + 1, RMarkPnt[3].X, RMarkPnt[3].Y); // right shadow
            if (ActiveMark == ActiveMarkType.right)
            {
                e.Graphics.DrawLine(penShadowLight, PosR + MarkWidth / 2 - 1, markyoff + 2, PosR + MarkWidth / 2 - 1, markyoff + 2 * MarkHeight / 3 - 2); // active mark
                e.Graphics.DrawLine(penShadowDark, PosR + MarkWidth / 2, markyoff + 2, PosR + MarkWidth / 2, markyoff + 2 * MarkHeight / 3 - 2); // active mark				
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            Rectangle LMarkRect = new Rectangle(LMarkPnt[0].X, LMarkPnt[0].Y, LMarkPnt[2].X - LMarkPnt[0].X, LMarkPnt[2].Y - LMarkPnt[0].Y);
            Rectangle RMarkRect = new Rectangle(RMarkPnt[2].X, RMarkPnt[0].Y, RMarkPnt[0].X - RMarkPnt[2].X, RMarkPnt[2].Y - RMarkPnt[0].Y);

            if (LMarkRect.Contains(e.X, e.Y))
            {
                this.Capture = true;
                MoveLMark = true;
                ActiveMark = ActiveMarkType.left;
                Invalidate(true);
            }

            if (RMarkRect.Contains(e.X, e.Y))
            {
                this.Capture = true;
                MoveRMark = true;
                ActiveMark = ActiveMarkType.right;
                Invalidate(true);
            }
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            this.Capture = false;

            MoveLMark = false;
            MoveRMark = false;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!MoveLMark && !MoveRMark)
            {
                this.Cursor = Cursors.Arrow;
                return;
            }

            Rectangle LMarkRect = new Rectangle(LMarkPnt[0].X, LMarkPnt[0].Y, LMarkPnt[2].X - LMarkPnt[0].X, LMarkPnt[2].Y - LMarkPnt[0].Y);
            Rectangle RMarkRect = new Rectangle(RMarkPnt[2].X, RMarkPnt[0].Y, RMarkPnt[0].X - RMarkPnt[2].X, RMarkPnt[2].Y - RMarkPnt[0].Y);

            if (LMarkRect.Contains(e.X, e.Y) || RMarkRect.Contains(e.X, e.Y))
                this.Cursor = Cursors.SizeWE;
            else 
                this.Cursor = Cursors.Arrow;

            if (MoveLMark)
            {
                this.Cursor = Cursors.SizeWE;
                PosL = e.X;
                if (PosL < XPosMin)
                    PosL = XPosMin;
                if (PosL > XPosMax)
                    PosL = XPosMax;
                if (PosR < PosL)
                    PosR = PosL;
                Pos2Range();
                ActiveMark = ActiveMarkType.left;
                Invalidate(true);
            }
            else if (MoveRMark)
            {
                this.Cursor = Cursors.SizeWE;
                PosR = e.X;
                if (PosR > XPosMax)
                    PosR = XPosMax;
                if (PosR < XPosMin)
                    PosR = XPosMin;
                if (PosL > PosR)
                    PosL = PosR;
                Pos2Range();
                ActiveMark = ActiveMarkType.right;
                Invalidate(true);
            }
        }

        // transform pixel position to range position
        private void Pos2Range()
        {
            int w = this.Width;
            int posw = w - 2 * MarkWidth - 2;

            rangeMin = Minimum + (int)Math.Round((double)(Maximum - Minimum) * (double)(PosL - XPosMin) / (double)posw);
            rangeMax = Minimum + (int)Math.Round((double)(Maximum - Minimum) * (double)(PosR - XPosMin) / (double)posw);
        }

        // transform range position to pixel position
        private void Range2Pos()
        {
            int w = this.Width;
            int posw = w - 2 * MarkWidth - 2;

            PosL = XPosMin + (int)Math.Round((double)posw * (double)(rangeMin - Minimum) / (double)(Maximum - Minimum));
            PosR = XPosMin + (int)Math.Round((double)posw * (double)(rangeMax - Minimum) / (double)(Maximum - Minimum));
        }

        private void OnResize(object sender, EventArgs e)
        {
            //Range2Pos();
            Invalidate(true);
        }

        private void OnKeyPress(object sender, KeyPressEventArgs e)
        {
            if (ActiveMark == ActiveMarkType.left)
            {
                if (e.KeyChar == '+')
                {
                    rangeMin++;
                    if (rangeMin > Maximum)
                        rangeMin = Maximum;
                    if (rangeMax < rangeMin)
                        rangeMax = rangeMin;
                }
                else if (e.KeyChar == '-')
                {
                    rangeMin--;
                    if (rangeMin < Minimum)
                        rangeMin = Minimum;
                }
            }
            else if (ActiveMark == ActiveMarkType.right)
            {
                if (e.KeyChar == '+')
                {
                    rangeMax++;
                    if (rangeMax > Maximum)
                        rangeMax = Maximum;
                }
                else if (e.KeyChar == '-')
                {
                    rangeMax--;
                    if (rangeMax < Minimum)
                        rangeMax = Minimum;
                    if (rangeMax < rangeMin)
                        rangeMin = rangeMax;
                }
            }
            Invalidate(true);
        }

        private void OnLoad(object sender, EventArgs e)
        {
            // use double buffering
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.UserPaint, true);
        }

        private void OnSizeChanged(object sender, EventArgs e)
        {
            Invalidate(true);
            Update();
        }

        private void OnLeave(object sender, EventArgs e)
        {
            ActiveMark = ActiveMarkType.none;
        }
    }
}
