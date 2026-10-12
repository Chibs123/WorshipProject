using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace WorshipProjectionApp;

public class DragAdorner : Adorner
{
    private readonly UIElement _child;
    private System.Windows.Point _offset;

    public DragAdorner(UIElement adornedElement, UIElement child)
        : base(adornedElement)
    {
        _child = child;
        AddVisualChild(_child);
        IsHitTestVisible = false;
    }

    public void UpdatePosition(System.Windows.Point point)
    {
        _offset = point;
        InvalidateArrange();
    }

    protected override int VisualChildrenCount => 1;

    protected override Visual GetVisualChild(int index) => _child;

    protected override System.Windows.Size MeasureOverride(System.Windows.Size constraint)
    {
        _child.Measure(constraint);
        return _child.DesiredSize;
    }

    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        _child.Arrange(new System.Windows.Rect(_offset, _child.DesiredSize));
        return finalSize;
    }
}