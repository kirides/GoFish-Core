using GoFishCore.WpfUI.ViewModels;
using ICSharpCode.AvalonEdit.Rendering;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace GoFishCore.WpfUI
{
    public class HighlightingBackgroundRenderer : IBackgroundRenderer
    {
        static Pen pen;
        MainViewModel vm;

        static HighlightingBackgroundRenderer()
        {
            pen = new Pen(Brushes.Black, 0.0);
        }

        public HighlightingBackgroundRenderer(MainViewModel host)
        {
            this.vm = host;
        }

        public KnownLayer Layer
        {
            get { return KnownLayer.Background; }
        }

        public void Draw(TextView textView, DrawingContext drawingContext)
        {
            foreach (var v in textView.VisualLines)
            {
                var rc = BackgroundGeometryBuilder.GetRectsFromVisualSegment(textView, v, 0, 1000).First();
                // NB: This lookup to fetch the doc line number isn't great, we could
                // probably do it once then just increment.
                var linenum = v.FirstDocumentLine.LineNumber - 1;
                if (linenum != vm.HighlightedLine) continue;
                var brush = Brushes.Yellow;
                drawingContext.DrawRectangle(brush, pen, new Rect(0, rc.Top, textView.ActualWidth, rc.Height));
            }
        }
    }
}
