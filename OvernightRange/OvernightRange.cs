using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using ATAS.Indicators;
using Atas_Indicators.Modules;
using OFT.Rendering.Context;
using OFT.Rendering.Tools;

namespace Atas_Indicators
{
    [DisplayName("Overnight Range")]
    [Category("My Indicators")]
    public class OvernightRange : Indicator
    {
        private static readonly TimeSpan SessionOpen  = new(18, 0,  0);
        private static readonly TimeSpan SessionClose = new( 9, 30, 0);
        // drawEnd defaults to 16:15 EST (RTH close)

        private readonly SessionTracker _tracker = new(SessionOpen, SessionClose);
        private RenderFont? _font;

        // ═══════════════════════════════════════════════════════════════════════
        //  EXTENSION
        // ═══════════════════════════════════════════════════════════════════════

        [Display(Name = "Mode",             GroupName = "Extension", Order = 0)]
        public ExtendMode Extension { get; set; } = ExtendMode.ToTime;

        [Display(Name = "Draw Until (EST)", GroupName = "Extension", Order = 1)]
        public TimeSpan DrawUntil { get; set; } = new(16, 15, 0);  // RTH close

        // ═══════════════════════════════════════════════════════════════════════
        //  SETTINGS
        // ═══════════════════════════════════════════════════════════════════════

        // ── High / Low ────────────────────────────────────────────────────────
        [Display(Name = "Show",  GroupName = "High / Low",    Order = 10)]
        public bool ShowHighLow { get; set; } = true;

        [Display(Name = "Style", GroupName = "High / Low",    Order = 11)]
        public LineSettings HighLow { get; set; } = new(Color.DarkOrange, 2);

        // ═══════════════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ═══════════════════════════════════════════════════════════════════════

        public OvernightRange() : base(true)
        {
            DenyToChangePanel   = true;
            EnableCustomDrawing = true;
            SubscribeToDrawingEvents(DrawingLayouts.Final);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  CONTROLLER
        // ═══════════════════════════════════════════════════════════════════════

        protected override void OnCalculate(int bar, decimal value)
        {
            if (bar == 0) { _tracker.Reset(); return; }

            _tracker.DrawEnd = Extension == ExtendMode.ToTime
                ? DrawUntil
                : new TimeSpan(23, 59, 59);

            var c = GetCandle(bar);
            _tracker.Process(bar, c.Time, c.Open, c.High, c.Low);
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  VIEW
        // ═══════════════════════════════════════════════════════════════════════

        protected override void OnRender(RenderContext ctx, DrawingLayouts layout)
        {
            if (layout != DrawingLayouts.Final) return;

            var s = _tracker.Last;
            if (s == null || !s.IsReady) return;

            _font ??= new RenderFont("Arial", 8);

            int x1 = ChartInfo.GetXByBar(s.EndBar);
            int x2 = ComputeX2(ctx, s);

            if (x1 > ctx.ClipBounds.Right || x2 < ctx.ClipBounds.Left) return;

            PaintBoundary(ctx, s, x1);

            if (ShowHighLow)
            {
                var pen = HighLow.MakePen();
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.High, pen, HighLow.Color,
                    ChartInfo.GetXByBar(s.HighBar), x2, "ONH");
                DrawHelper.HLine(ctx, ChartInfo, _font!, s.Low,  pen, HighLow.Color,
                    ChartInfo.GetXByBar(s.LowBar),  x2, "ONL");
            }

        }

        private int ComputeX2(RenderContext ctx, SessionSnapshot s)
        {
            int xRight = Extension switch
            {
                ExtendMode.ToAxis  => ctx.ClipBounds.Right,
                ExtendMode.ToSweep => s.SweepBar >= 0
                    ? ChartInfo.GetXByBar(s.SweepBar)
                    : ChartInfo.GetXByBar(CurrentBar),
                _ => s.DayEndBar >= 0
                    ? ChartInfo.GetXByBar(s.DayEndBar)
                    : ChartInfo.GetXByBar(CurrentBar),
            };
            return Math.Min(xRight, ctx.ClipBounds.Right);
        }

        private void PaintBoundary(RenderContext ctx, SessionSnapshot s, int x1)
            => DrawHelper.VLine(ctx, ChartInfo,
                DrawHelper.MakePen(HighLow.Color, 1, LineStyle.Dotted), x1, s.High, s.Low);

        protected override void OnDispose() => _font = null;
    }
}
