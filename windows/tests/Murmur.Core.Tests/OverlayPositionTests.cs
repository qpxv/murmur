
namespace Murmur.Core.Tests;

public class OverlayPositionTests
{
    private static readonly ScreenRect Screen = new(0, 0, 1000, 600);

    [Theory]
    [InlineData(OverlayPosition.TopLeft, 20, 20)]
    [InlineData(OverlayPosition.TopCenter, 450, 20)]
    [InlineData(OverlayPosition.TopRight, 880, 20)]
    [InlineData(OverlayPosition.MiddleLeft, 20, 280)]
    [InlineData(OverlayPosition.MiddleCenter, 450, 280)]
    [InlineData(OverlayPosition.MiddleRight, 880, 280)]
    [InlineData(OverlayPosition.BottomLeft, 20, 540)]
    [InlineData(OverlayPosition.BottomCenter, 450, 540)]
    [InlineData(OverlayPosition.BottomRight, 880, 540)]
    public void Places_pill_in_its_cell(OverlayPosition position, double x, double y) =>
        Assert.Equal(new ScreenPoint(x, y), position.Origin(width: 100, height: 40, Screen, margin: 20));

    [Fact]
    public void Respects_offset_work_area()
    {
        var secondScreen = new ScreenRect(1000, 50, 800, 500);
        Assert.Equal(new ScreenPoint(1680, 490), OverlayPosition.BottomRight.Origin(width: 100, height: 40, secondScreen, margin: 20));
    }
}
