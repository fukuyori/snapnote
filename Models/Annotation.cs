using System.Windows;
using System.Windows.Media;

namespace SnapNoteStudio.Models;

public enum AnnotationType
{
    Arrow,
    Line,
    Rectangle,
    Ellipse,
    Text,
    Step,           // 番号ステップ
    FilledRect,     // 塗りつぶし四角形
    Mosaic,         // モザイク
    Blur,           // ぼかし
    Highlighter,    // 蛍光ペン
    Spotlight,      // スポットライト
    Magnifier       // 拡大鏡
}

public abstract class Annotation
{
    public Guid Id { get; } = Guid.NewGuid();
    public abstract AnnotationType Type { get; }
    public Color StrokeColor { get; set; } = Colors.Red;
    public double StrokeThickness { get; set; } = 3;
    public double Opacity { get; set; } = 1.0;
    public bool IsSelected { get; set; }

    public abstract Rect GetBounds();
    public abstract void Move(Vector delta);
    public abstract Annotation Clone();
}

public class ArrowAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Arrow;
    
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public double ArrowHeadLength { get; set; } = 15;
    public double ArrowHeadAngle { get; set; } = 30;

    public override Rect GetBounds()
    {
        return new Rect(
            Math.Min(StartPoint.X, EndPoint.X) - StrokeThickness,
            Math.Min(StartPoint.Y, EndPoint.Y) - StrokeThickness,
            Math.Abs(EndPoint.X - StartPoint.X) + StrokeThickness * 2,
            Math.Abs(EndPoint.Y - StartPoint.Y) + StrokeThickness * 2);
    }

    public override void Move(Vector delta)
    {
        StartPoint = new Point(StartPoint.X + delta.X, StartPoint.Y + delta.Y);
        EndPoint = new Point(EndPoint.X + delta.X, EndPoint.Y + delta.Y);
    }

    public override Annotation Clone()
    {
        return new ArrowAnnotation
        {
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            StrokeColor = StrokeColor,
            StrokeThickness = StrokeThickness,
            ArrowHeadLength = ArrowHeadLength,
            ArrowHeadAngle = ArrowHeadAngle
        };
    }

    public (Point, Point) GetArrowHeadPoints()
    {
        var direction = EndPoint - StartPoint;
        var length = direction.Length;
        if (length < 0.001) return (EndPoint, EndPoint);

        direction.Normalize();

        var angle1 = Math.Atan2(direction.Y, direction.X) + Math.PI + ArrowHeadAngle * Math.PI / 180;
        var angle2 = Math.Atan2(direction.Y, direction.X) + Math.PI - ArrowHeadAngle * Math.PI / 180;

        var point1 = new Point(
            EndPoint.X + ArrowHeadLength * Math.Cos(angle1),
            EndPoint.Y + ArrowHeadLength * Math.Sin(angle1));
        var point2 = new Point(
            EndPoint.X + ArrowHeadLength * Math.Cos(angle2),
            EndPoint.Y + ArrowHeadLength * Math.Sin(angle2));

        return (point1, point2);
    }
}

public class LineAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Line;
    
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }

    public override Rect GetBounds()
    {
        return new Rect(
            Math.Min(StartPoint.X, EndPoint.X) - StrokeThickness,
            Math.Min(StartPoint.Y, EndPoint.Y) - StrokeThickness,
            Math.Abs(EndPoint.X - StartPoint.X) + StrokeThickness * 2,
            Math.Abs(EndPoint.Y - StartPoint.Y) + StrokeThickness * 2);
    }

    public override void Move(Vector delta)
    {
        StartPoint = new Point(StartPoint.X + delta.X, StartPoint.Y + delta.Y);
        EndPoint = new Point(EndPoint.X + delta.X, EndPoint.Y + delta.Y);
    }

    public override Annotation Clone()
    {
        return new LineAnnotation
        {
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            StrokeColor = StrokeColor,
            StrokeThickness = StrokeThickness
        };
    }
}

public class RectangleAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Rectangle;
    
    public Rect Bounds { get; set; }
    public bool IsFilled { get; set; }
    public Color FillColor { get; set; } = Color.FromArgb(64, 255, 0, 0);

    public override Rect GetBounds() => Bounds;

    public override void Move(Vector delta)
    {
        Bounds = new Rect(
            Bounds.X + delta.X,
            Bounds.Y + delta.Y,
            Bounds.Width,
            Bounds.Height);
    }

    public override Annotation Clone()
    {
        return new RectangleAnnotation
        {
            Bounds = Bounds,
            StrokeColor = StrokeColor,
            StrokeThickness = StrokeThickness,
            IsFilled = IsFilled,
            FillColor = FillColor
        };
    }
}

public class EllipseAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Ellipse;
    
    public Rect Bounds { get; set; }
    public bool IsFilled { get; set; }
    public Color FillColor { get; set; } = Color.FromArgb(64, 255, 0, 0);

    public override Rect GetBounds() => Bounds;

    public override void Move(Vector delta)
    {
        Bounds = new Rect(
            Bounds.X + delta.X,
            Bounds.Y + delta.Y,
            Bounds.Width,
            Bounds.Height);
    }

    public override Annotation Clone()
    {
        return new EllipseAnnotation
        {
            Bounds = Bounds,
            StrokeColor = StrokeColor,
            StrokeThickness = StrokeThickness,
            IsFilled = IsFilled,
            FillColor = FillColor
        };
    }
}

public class TextAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Text;
    
    public Point Position { get; set; }
    public string Text { get; set; } = string.Empty;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 16;
    public FontWeight FontWeight { get; set; } = FontWeights.Normal;

    public override Rect GetBounds()
    {
        // Approximate bounds - actual size depends on text rendering
        var width = Text.Length * FontSize * 0.6;
        var height = FontSize * 1.5;
        return new Rect(Position.X, Position.Y, Math.Max(width, 50), height);
    }

    public override void Move(Vector delta)
    {
        Position = new Point(Position.X + delta.X, Position.Y + delta.Y);
    }

    public override Annotation Clone()
    {
        return new TextAnnotation
        {
            Position = Position,
            Text = Text,
            StrokeColor = StrokeColor,
            FontFamily = FontFamily,
            FontSize = FontSize,
            FontWeight = FontWeight
        };
    }
}

// 番号ステップ
public class StepAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Step;
    
    public Point Position { get; set; }
    public int StepNumber { get; set; } = 1;
    public double Size { get; set; } = 32;

    public override Rect GetBounds()
    {
        return new Rect(Position.X - Size / 2, Position.Y - Size / 2, Size, Size);
    }

    public override void Move(Vector delta)
    {
        Position = new Point(Position.X + delta.X, Position.Y + delta.Y);
    }

    public override Annotation Clone()
    {
        return new StepAnnotation
        {
            Position = Position,
            StepNumber = StepNumber,
            Size = Size,
            StrokeColor = StrokeColor
        };
    }
}

// 塗りつぶし四角形
public class FilledRectAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.FilledRect;
    
    public Rect Bounds { get; set; }
    public Color FillColor { get; set; } = Colors.Black;

    public override Rect GetBounds() => Bounds;

    public override void Move(Vector delta)
    {
        Bounds = new Rect(Bounds.X + delta.X, Bounds.Y + delta.Y, Bounds.Width, Bounds.Height);
    }

    public override Annotation Clone()
    {
        return new FilledRectAnnotation
        {
            Bounds = Bounds,
            FillColor = FillColor,
            StrokeColor = StrokeColor
        };
    }
}

// モザイク
public class MosaicAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Mosaic;
    
    public Rect Bounds { get; set; }
    public int BlockSize { get; set; } = 10;

    public override Rect GetBounds() => Bounds;

    public override void Move(Vector delta)
    {
        Bounds = new Rect(Bounds.X + delta.X, Bounds.Y + delta.Y, Bounds.Width, Bounds.Height);
    }

    public override Annotation Clone()
    {
        return new MosaicAnnotation
        {
            Bounds = Bounds,
            BlockSize = BlockSize
        };
    }
}

// ぼかし
public class BlurAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Blur;
    
    public Rect Bounds { get; set; }
    public double BlurRadius { get; set; } = 10;

    public override Rect GetBounds() => Bounds;

    public override void Move(Vector delta)
    {
        Bounds = new Rect(Bounds.X + delta.X, Bounds.Y + delta.Y, Bounds.Width, Bounds.Height);
    }

    public override Annotation Clone()
    {
        return new BlurAnnotation
        {
            Bounds = Bounds,
            BlurRadius = BlurRadius
        };
    }
}

// 蛍光ペン（ハイライト）
public class HighlighterAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Highlighter;
    
    public List<Point> Points { get; set; } = new();
    public double HighlightWidth { get; set; } = 20;
    public Color HighlightColor { get; set; } = Color.FromArgb(128, 255, 255, 0); // 半透明黄色

    public override Rect GetBounds()
    {
        if (Points.Count == 0) return Rect.Empty;
        
        double minX = Points.Min(p => p.X) - HighlightWidth / 2;
        double minY = Points.Min(p => p.Y) - HighlightWidth / 2;
        double maxX = Points.Max(p => p.X) + HighlightWidth / 2;
        double maxY = Points.Max(p => p.Y) + HighlightWidth / 2;
        
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    public override void Move(Vector delta)
    {
        for (int i = 0; i < Points.Count; i++)
        {
            Points[i] = new Point(Points[i].X + delta.X, Points[i].Y + delta.Y);
        }
    }

    public override Annotation Clone()
    {
        return new HighlighterAnnotation
        {
            Points = new List<Point>(Points),
            HighlightWidth = HighlightWidth,
            HighlightColor = HighlightColor
        };
    }
}

// スポットライト
public class SpotlightAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Spotlight;
    
    public Rect Bounds { get; set; }
    public double DarknessOpacity { get; set; } = 0.6;

    public override Rect GetBounds() => Bounds;

    public override void Move(Vector delta)
    {
        Bounds = new Rect(Bounds.X + delta.X, Bounds.Y + delta.Y, Bounds.Width, Bounds.Height);
    }

    public override Annotation Clone()
    {
        return new SpotlightAnnotation
        {
            Bounds = Bounds,
            DarknessOpacity = DarknessOpacity
        };
    }
}

// 拡大鏡
public class MagnifierAnnotation : Annotation
{
    public override AnnotationType Type => AnnotationType.Magnifier;
    
    public Point SourceCenter { get; set; }  // 拡大する元の中心位置
    public Point DisplayPosition { get; set; }  // 表示位置
    public double SourceRadius { get; set; } = 50;  // 元の範囲の半径
    public double ZoomFactor { get; set; } = 2.0;  // 拡大率

    public override Rect GetBounds()
    {
        double displaySize = SourceRadius * ZoomFactor * 2;
        return new Rect(DisplayPosition.X - displaySize / 2, DisplayPosition.Y - displaySize / 2, displaySize, displaySize);
    }

    public override void Move(Vector delta)
    {
        DisplayPosition = new Point(DisplayPosition.X + delta.X, DisplayPosition.Y + delta.Y);
    }

    public override Annotation Clone()
    {
        return new MagnifierAnnotation
        {
            SourceCenter = SourceCenter,
            DisplayPosition = DisplayPosition,
            SourceRadius = SourceRadius,
            ZoomFactor = ZoomFactor,
            StrokeColor = StrokeColor
        };
    }
}
